using System.Globalization;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Orsuun.Rules;
using Orsuun.Rules.Combat;
using Orsuun.Server.Data;

namespace Orsuun.Server.Game;

/// <summary>
/// Fortress keeps for the guilds (Rules.FortressKeeps): bids from the treasury all week, the four highest storm the keep
/// on Sunday 20:00 for an hour while the holding guild holds it, and the keep's flag goes to the winner for a week.
/// Every change to a fortress happens under its row lock; the world clock moves each keep through its week.
/// </summary>
public sealed partial class GameService
{
    private (string Week, DateTime StartsUtc, DateTime EndsUtc) KeepSchedule()
    {
        DateTime local = _bells.LocalNow;
        DateTime start = FortressKeeps.SiegeStart(local);
        return (Rules.Bounties.WeekKey(local), _bells.ToUtc(start), _bells.ToUtc(start.AddMinutes(FortressKeeps.WindowMinutes)));
    }

    private async Task<Fortress> LockFortressAsync(int id, CancellationToken ct)
    {
        Fortress fort = (await _db.Fortresses.FromSql($@"SELECT * FROM ""Fortresses"" WHERE ""Id"" = {id} FOR UPDATE").ToListAsync(ct)).FirstOrDefault()
            ?? throw new GameException("no_fortress", "That fortress is not built yet.");
        await _db.Entry(fort).ReloadAsync(ct);
        return fort;
    }

    /// <summary>Moves one keep through its week: a new week opens bids, Sunday 20:00 closes them, the hour's end settles it.</summary>
    private async Task AdvanceKeepAsync(int fortressId, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        Fortress fort = await LockFortressAsync(fortressId, ct);
        if (Step(fort, DateTime.UtcNow) == 0) return;
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>Applies whatever is due to a locked keep; returns how many steps it took (0: nothing to save).</summary>
    private int Step(Fortress fort, DateTime now)
    {
        int steps = 0;
        (string week, DateTime starts, DateTime ends) = KeepSchedule();
        if (fort.KeepWeek != week)
        {
            // A week left unfinished (the server was down): bids that never met a siege go back; a siege underway settles.
            if (fort.KeepWeek != "" && fort.KeepState == 0) RefundBids(fort, all: true);
            else if (fort.KeepWeek != "" && fort.KeepState == 1) Resolve(fort);
            fort.KeepWeek = week;
            fort.KeepState = 0;
            fort.KeepStartsUtc = starts;
            fort.KeepEndsUtc = ends;
            fort.KeepMended = 0;
            steps++;
        }
        if (fort.KeepState == 0 && now >= fort.KeepStartsUtc)
        {
            CloseBids(fort);
            steps++;
        }
        if (fort.KeepState == 1 && now >= fort.KeepEndsUtc)
        {
            Resolve(fort);
            steps++;
        }
        return steps;
    }

    private List<FortressBid> BidsOn(Fortress fort) =>
        _db.FortressBids.Where(b => b.Week == fort.KeepWeek && b.FortressId == fort.Id).ToList();

    private void Refund(FortressBid bid)
    {
        bid.Refunded = true;
        long amount = bid.Amount;
        // One UPDATE: the treasury is credited without loading the guild row (as guild XP from sieges).
        _db.Guilds.Where(g => g.Id == bid.GuildId).ExecuteUpdate(s => s.SetProperty(g => g.Treasury, g => g.Treasury + amount));
    }

    private void RefundBids(Fortress fort, bool all)
    {
        foreach (FortressBid bid in BidsOn(fort))
            if (all || !bid.Contender) if (!bid.Refunded) Refund(bid);
    }

    /// <summary>Sunday 20:00: the four highest bids contend (spent); the rest go back to their treasuries.</summary>
    private void CloseBids(Fortress fort)
    {
        FortressDef def = Fortresses.Find(fort.Id)!;
        List<FortressBid> bids = BidsOn(fort);
        List<int> picked = FortressKeeps.PickContenders(bids.Select(b => (b.Amount, b.Utc)).ToList());
        foreach (int i in picked) bids[i].Contender = true;
        foreach (FortressBid bid in bids) if (!bid.Contender && !bid.Refunded) Refund(bid);
        fort.KeepState = 1;

        var ids = bids.Where(b => b.Contender).Select(b => b.GuildId).ToList();
        var tags = _db.Guilds.AsNoTracking().Where(g => ids.Contains(g.Id)).Select(g => new { g.Id, g.Tag }).ToList();
        string holder = HolderTag(fort);
        if (ids.Count == 0)
        {
            fort.LastEvent = holder == "" ? $"No guild bid for the keep of {def.Name}." : $"No guild bid for the keep of {def.Name}: [{holder}] holds it.";
            fort.KeepState = 2;
        }
        else
        {
            string names = string.Join(", ", ids.Select(id => "[" + tags.Where(t => t.Id == id).Select(t => t.Tag).FirstOrDefault() + "]"));
            fort.LastEvent = $"The keep of {def.Name} is stormed tonight by {names}.";
            SystemLine(Chat.World, fort.LastEvent + (holder == "" ? "" : $" [{holder}] holds it."));
        }
    }

    private string HolderTag(Fortress fort) =>
        fort.FlagGuildId is Guid id ? _db.Guilds.AsNoTracking().Where(g => g.Id == id).Select(g => g.Tag).FirstOrDefault() ?? "" : "";

    /// <summary>The hour is up: the best contender takes the keep if it beat the wall and the holders' mending.</summary>
    private void Resolve(Fortress fort)
    {
        FortressDef def = Fortresses.Find(fort.Id)!;
        // A contender that disbanded since has no one to raise its flag.
        List<FortressBid> contenders = BidsOn(fort).Where(b => b.Contender && _db.Guilds.Any(g => g.Id == b.GuildId)).ToList();
        int best = FortressKeeps.Winner(contenders.Select(b => b.Damage).ToList(), fort.KeepMended);
        string holder = HolderTag(fort);
        if (best >= 0)
        {
            Guid winner = contenders[best].GuildId;
            string tag = _db.Guilds.AsNoTracking().Where(g => g.Id == winner).Select(g => g.Tag).FirstOrDefault() ?? "?";
            fort.FlagGuildId = winner;
            fort.LastEvent = $"[{tag}] took the keep of {def.Name} with {contenders[best].Damage.ToString("N0", CultureInfo.InvariantCulture)} damage.";
            SystemLine(Chat.GuildChannel(winner), $"We hold the keep of {def.Name} for a week: our flag flies over it.");
        }
        else
        {
            fort.LastEvent = holder == "" ? $"The keep of {def.Name} stood: no guild broke it." : $"[{holder}] held the keep of {def.Name}.";
        }
        SystemLine(Chat.World, fort.LastEvent);
        fort.KeepState = 2;
    }

    /// <summary>The keeps as the WAR screen shows them.</summary>
    private async Task<KeepDto[]> KeepsAsync(Account account, List<Fortress> forts, CancellationToken ct)
    {
        DateTime now = DateTime.UtcNow;
        string week = KeepSchedule().Week;
        var bids = await _db.FortressBids.AsNoTracking().Where(b => b.Week == week).ToListAsync(ct);
        var guildIds = bids.Select(b => b.GuildId).Concat(forts.Where(f => f.FlagGuildId != null).Select(f => f.FlagGuildId!.Value)).Distinct().ToList();
        var guilds = await _db.Guilds.AsNoTracking().Where(g => guildIds.Contains(g.Id)).ToListAsync(ct);
        Guid? mine = account.GuildId;
        FortressBid? myBid = mine == null ? null : bids.FirstOrDefault(b => b.GuildId == mine);
        bool manager = mine != null && Guilds.CanManage(account.GuildRank);
        var list = new List<KeepDto>();
        foreach (Fortress f in forts)
        {
            FortressDef def = Fortresses.Find(f.Id)!;
            Guild? holder = guilds.FirstOrDefault(g => g.Id == f.FlagGuildId);
            bool thisWeek = f.KeepWeek == week;
            int state = thisWeek ? f.KeepState : 0;
            DateTime starts = thisWeek ? f.KeepStartsUtc : KeepSchedule().StartsUtc;
            DateTime ends = thisWeek ? f.KeepEndsUtc : KeepSchedule().EndsUtc;
            KeepBidDto[] rows = bids.Where(b => b.FortressId == f.Id && !(state >= 1 && !b.Contender))
                .OrderByDescending(b => b.Amount).ThenBy(b => b.Utc)
                .Select(b =>
                {
                    Guild? g = guilds.FirstOrDefault(x => x.Id == b.GuildId);
                    return new KeepBidDto(g?.Tag ?? "?", g?.Name ?? "", g?.Color ?? "#B0B0B0", b.Amount, b.Contender, b.Damage, b.GuildId == mine);
                }).ToArray();
            bool holding = mine != null && f.FlagGuildId == mine;
            bool contending = myBid != null && myBid.FortressId == f.Id && myBid.Contender;
            bool open = state == 1 && now >= starts && now < ends;
            list.Add(new KeepDto(f.Id, def.Name, holder?.Tag ?? "", holder?.Name ?? "", holder?.Color ?? "#B0B0B0", state,
                Math.Max(0, (int)(starts - now).TotalSeconds), Math.Max(0, (int)(ends - now).TotalSeconds), rows,
                myBid != null && myBid.FortressId == f.Id ? myBid.Amount : 0, contending, holding,
                manager && state == 0 && !holding && (myBid == null || myBid.FortressId == f.Id), open && (holding || contending),
                FortressKeeps.KeepWall, f.KeepMended, f.LastEvent));
        }
        return list.ToArray();
    }

    /// <summary>
    /// The leader or an officer adds treasury sorn to the guild's bid on a keep (one keep a week; the holders defend
    /// theirs). The bid is held until Sunday 20:00: it is spent if the guild contends, else it goes back.
    /// </summary>
    public async Task<WarDto> KeepBidAsync(Account account, KeepBidRequest request, CancellationToken ct)
    {
        RequireManager(account);
        FortressDef def = Fortresses.Find(request.FortressId) ?? throw new GameException("no_fortress", "Unknown fortress.");
        if (request.Amount < 1_000 || request.Amount > 1_000_000_000) throw new GameException("bad_amount", "Bid between 1,000 and 1,000,000,000 sorn.");
        Guid guildId = account.GuildId!.Value;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        Fortress fort = await LockFortressAsync(def.Id, ct);
        Step(fort, DateTime.UtcNow);
        if (fort.KeepState != 0) throw new GameException("bids_closed", "Bids for this week's keeps are closed; they open again on Monday at 20:00.");
        if (fort.FlagGuildId == guildId) throw new GameException("own_keep", "Your guild holds this keep: it defends it on Sunday.");
        FortressBid? bid = await _db.FortressBids.FirstOrDefaultAsync(b => b.Week == fort.KeepWeek && b.GuildId == guildId, ct);
        if (bid != null && bid.FortressId != def.Id)
            throw new GameException("one_keep", $"Your guild already bids on {Fortresses.Find(bid.FortressId)!.Name} this week.");
        long total = (bid?.Amount ?? 0) + request.Amount;
        if (total < FortressKeeps.MinBid) throw new GameException("bid_low", $"A bid starts at {FortressKeeps.MinBid.ToString("N0", CultureInfo.InvariantCulture)} sorn.");

        Guild guild = await LockGuildAsync(guildId, ct);
        if (guild.Treasury < request.Amount) throw new GameException("treasury", "Not enough sorn in the treasury.");
        guild.Treasury -= request.Amount;
        if (bid == null)
        {
            bid = new FortressBid { Week = fort.KeepWeek, GuildId = guildId, FortressId = def.Id };
            _db.FortressBids.Add(bid);
        }
        bid.Amount = total;
        bid.Utc = DateTime.UtcNow;
        GuildLine(guild, $"{DisplayName(account)} raised our bid on the keep of {def.Name} to {total.ToString("N0", CultureInfo.InvariantCulture)} sorn.");
        await SaveAsync(ct);
        await tx.CommitAsync(ct);
        return await WarAsync(account, ct) with { Message = $"Your guild bids {total.ToString("N0", CultureInfo.InvariantCulture)} sorn on the keep of {def.Name}." };
    }

    /// <summary>
    /// One fight at the keep during the Sunday siege: a contender's member storms it (damage counts for the guild), a
    /// holder's member holds it (mends half the damage). The champion is the lord of the Hall; the client replays it as
    /// a siege fight. Shares the siege cooldown.
    /// </summary>
    public async Task<StateDto> KeepFightAsync(Account account, KeepFightRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        Guid guildId = InGuild(account).GuildId!.Value;
        FortressDef def = Fortresses.Find(request.FortressId) ?? throw new GameException("no_fortress", "Unknown fortress.");
        DateTime now = DateTime.UtcNow;
        if (account.LastSiegeUtc is DateTime last && now < last.AddMinutes(Fortresses.CooldownMinutes))
        {
            int minutes = (int)Math.Ceiling((last.AddMinutes(Fortresses.CooldownMinutes) - now).TotalMinutes);
            throw new GameException("siege_cooldown", $"Your warband regroups: {minutes} more minute{(minutes == 1 ? "" : "s")}.");
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        Fortress fort = await LockFortressAsync(def.Id, ct);
        Step(fort, now);
        if (fort.KeepState == 2 && now < fort.KeepEndsUtc) throw new GameException("keep_closed", "No guild bid for this keep this week.");
        if (fort.KeepState != 1 || now < fort.KeepStartsUtc || now >= fort.KeepEndsUtc)
            throw new GameException("keep_closed", "The keep is stormed on Sunday from 20:00 for an hour.");
        bool holding = fort.FlagGuildId == guildId;
        FortressBid? bid = holding ? null : await _db.FortressBids.FirstOrDefaultAsync(b => b.Week == fort.KeepWeek && b.GuildId == guildId && b.FortressId == def.Id && b.Contender, ct);
        if (!holding && bid == null) throw new GameException("not_contender", "Only the four highest bidders storm the keep, and its holders hold it.");

        BossDef champion = Fortresses.Champion(def, SiegePhase.Hall);
        ulong seed = BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(8));
        int potionsAtStart = account.Potions;
        Bell bell = _bells.Active;
        var inventory = Snapshot(account);
        BossRunResult run = BossRun.Simulate(champion, Hero(account), inventory, seed, bell);
        inventory.Sorn += Fortresses.FightSorn;
        inventory.HuntMarks += Fortresses.FightMarks;
        string text;
        if (holding)
        {
            long mend = run.Damage * FortressKeeps.MendPercent / 100;
            fort.KeepMended += mend;
            text = $"You held the keep of {def.Name}: it mended by {mend.ToString("N0", CultureInfo.InvariantCulture)}.";
        }
        else
        {
            bid!.Damage += run.Damage;
            text = $"You stormed the keep of {def.Name} for {run.Damage.ToString("N0", CultureInfo.InvariantCulture)}; your guild has {bid.Damage.ToString("N0", CultureInfo.InvariantCulture)}.";
        }
        Apply(account, inventory);
        account.LastSiegeUtc = now;
        Count(account, BountyMetric.SiegeFights, 1);
        _db.Ledger.Add(Entry(account.Id, null, "keep", $"fortress={def.Id} holding={holding} seed={seed} damage={run.Damage} mended={fort.KeepMended}",
            Fortresses.FightSorn, request.RequestId));
        await SaveAsync(ct);
        await AddGuildXpAsync(guildId, run.Damage / Guilds.SiegeDamagePerXp, ct);
        await tx.CommitAsync(ct);
        return ToState(account, siege: new SiegeResultDto(def.Id, champion.Id, holding, seed, run.Damage, potionsAtStart, bell, SiegePhase.Hall, 0, false, false,
            fort.Holder, text));
    }

    /// <summary>GDD: each guild holding a keep earns 2% of the Salt Exchange tax (one exchange serves every region for now).</summary>
    private async Task PayKeepHoldersAsync(long tax, CancellationToken ct)
    {
        long share = tax * FortressKeeps.TaxSharePercent / 100;
        if (share <= 0) return;
        List<Guid> holders = await _db.Fortresses.AsNoTracking().Where(f => f.FlagGuildId != null).Select(f => f.FlagGuildId!.Value).ToListAsync(ct);
        foreach (Guid id in holders)
            await _db.Guilds.Where(g => g.Id == id).ExecuteUpdateAsync(s => s.SetProperty(g => g.Treasury, g => g.Treasury + share), ct);
    }

    /// <summary>Development: opens this week's keep sieges now for <paramref name="minutes"/> (bids close at once).</summary>
    public async Task<WarDto> DevKeepSiegeAsync(Account account, int minutes, CancellationToken ct)
    {
        DateTime now = DateTime.UtcNow;
        foreach (FortressDef def in Fortresses.All)
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            Fortress fort = await LockFortressAsync(def.Id, ct);
            Step(fort, now);
            if (fort.KeepState == 0)
            {
                fort.KeepStartsUtc = now;
                fort.KeepEndsUtc = now.AddMinutes(Math.Clamp(minutes, 1, 120));
                Step(fort, now);
            }
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        return await WarAsync(account, ct) with { Message = "The keep sieges began now." };
    }

    /// <summary>Development: ends the running keep sieges now and settles them.</summary>
    public async Task<WarDto> DevKeepEndAsync(Account account, CancellationToken ct)
    {
        DateTime now = DateTime.UtcNow;
        foreach (FortressDef def in Fortresses.All)
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            Fortress fort = await LockFortressAsync(def.Id, ct);
            if (fort.KeepState == 1) fort.KeepEndsUtc = now.AddSeconds(-1);
            Step(fort, now);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        return await WarAsync(account, ct) with { Message = "The keep sieges ended now." };
    }
}
