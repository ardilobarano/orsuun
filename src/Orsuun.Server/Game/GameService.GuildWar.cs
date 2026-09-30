using System.Globalization;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Orsuun.Rules;
using Orsuun.Rules.Combat;
using Orsuun.Server.Data;

namespace Orsuun.Server.Game;

/// <summary>
/// Guild war (owner, 24 Sep 2026: guild war and fortress bids; GDD section 7), asynchronous like the sieges: the leader
/// signs up for a war night, the world clock pairs the signed guilds at 21:00 and settles each war when its hour is up,
/// and in between members fight duels on the three lanes (Rules.GuildWars, Rules.Duels). The war row is locked for every
/// change; the guild rows only when a war is settled.
/// </summary>
public sealed partial class GameService
{
    /// <summary>The war night running now, or the next one: key and local start.</summary>
    private (string Night, DateTime Start) WarNight()
    {
        DateTime start = GuildWars.StartOf(_bells.LocalNow);
        return (GuildWars.NightKey(start), start);
    }

    /// <summary>The next night that has not started yet (sign-ups go there).</summary>
    private (string Night, DateTime Start) SignupNight()
    {
        DateTime local = _bells.LocalNow;
        DateTime start = GuildWars.StartOf(local);
        if (local >= start) start = GuildWars.StartOf(start.AddMinutes(GuildWars.WindowMinutes));
        return (GuildWars.NightKey(start), start);
    }

    private Task<GuildWar?> RunningWarAsync(Guid guildId, DateTime now, CancellationToken ct) =>
        _db.GuildWars.AsNoTracking()
            .Where(w => (w.GuildA == guildId || w.GuildB == guildId) && w.Result == 0 && w.StartsUtc <= now && w.EndsUtc > now)
            .OrderByDescending(w => w.Id).FirstOrDefaultAsync(ct);

    private async Task<GuildWar> LockWarAsync(long id, CancellationToken ct)
    {
        GuildWar war = (await _db.GuildWars.FromSql($@"SELECT * FROM ""GuildWars"" WHERE ""Id"" = {id} FOR UPDATE").ToListAsync(ct)).FirstOrDefault()
            ?? throw new GameException("no_war", "That war is over.");
        await _db.Entry(war).ReloadAsync(ct);
        return war;
    }

    public async Task<GuildWarDto> GuildWarAsync(Account account, string message, CancellationToken ct)
    {
        Guild guild = GuildOf(InGuild(account)) ?? throw new GameException("no_guild", "You are not in a guild.");
        DateTime now = DateTime.UtcNow;
        (string signupNight, DateTime signupStart) = SignupNight();
        bool signed = await _db.GuildWarSignups.AsNoTracking().AnyAsync(s => s.Night == signupNight && s.GuildId == guild.Id, ct);
        int signedGuilds = await _db.GuildWarSignups.AsNoTracking().CountAsync(s => s.Night == signupNight, ct);
        int toNext = Math.Max(0, (int)(_bells.ToUtc(signupStart) - now).TotalSeconds);

        GuildWar? war = await RunningWarAsync(guild.Id, now, ct);
        GuildWarFoeDto? foe = null;
        var lanes = new GuildWarLaneDto[GuildWars.Lanes];
        int myKills = 0, theirKills = 0, myScore = 0, theirScore = 0, secondsLeft = 0, fightsLeft = 0, cooldown = 0;
        string lastEvent = "";
        if (war != null)
        {
            bool sideA = war.GuildA == guild.Id;
            Guild? other = await _db.Guilds.AsNoTracking().FirstOrDefaultAsync(g => g.Id == (sideA ? war.GuildB : war.GuildA), ct);
            if (other != null) foe = new GuildWarFoeDto(other.Tag, other.Name, other.Color, Guilds.Level(other.Xp), other.WarRating);
            int[] fronts = war.Fronts;
            for (int i = 0; i < GuildWars.Lanes; i++)
            {
                int mine = sideA ? fronts[i] : -fronts[i];
                lanes[i] = new GuildWarLaneDto(GuildWars.LaneNames[i], mine, (sideA ? war.FlagA : war.FlagB) == i, (sideA ? war.FlagB : war.FlagA) == i,
                    GuildWars.Broken(mine));
            }
            myKills = sideA ? war.KillsA : war.KillsB;
            theirKills = sideA ? war.KillsB : war.KillsA;
            myScore = GuildWars.Score(myKills, fronts, sideA);
            theirScore = GuildWars.Score(theirKills, fronts, !sideA);
            secondsLeft = Math.Max(0, (int)(war.EndsUtc - now).TotalSeconds);
            GuildWarEntry? entry = await _db.GuildWarEntries.AsNoTracking().FirstOrDefaultAsync(e => e.WarId == war.Id && e.AccountId == account.Id, ct);
            fightsLeft = GuildWars.FightsPerWar - (entry?.Fights ?? 0);
            cooldown = entry == null ? 0 : Math.Max(0, (int)(entry.LastUtc.AddSeconds(GuildWars.CooldownSeconds) - now).TotalSeconds);
            lastEvent = war.LastEvent;
        }
        else
        {
            for (int i = 0; i < GuildWars.Lanes; i++) lanes[i] = new GuildWarLaneDto(GuildWars.LaneNames[i], 0, false, false, false);
        }

        GuildWar? last = await _db.GuildWars.AsNoTracking().Where(w => (w.GuildA == guild.Id || w.GuildB == guild.Id) && w.Result != 0)
            .OrderByDescending(w => w.Id).FirstOrDefaultAsync(ct);
        string lastResult = last?.LastEvent ?? "";

        var ladder = await _db.Guilds.AsNoTracking().Where(g => g.WarWins + g.WarLosses + g.WarDraws > 0 || g.Id == guild.Id)
            .OrderByDescending(g => g.WarRating).ThenByDescending(g => g.WarWins).Take(10).ToListAsync(ct);
        GuildWarLadderDto[] rows = ladder.Select(g => new GuildWarLadderDto(g.Tag, g.Name, g.Color, g.WarRating, g.WarWins, g.WarLosses, g.WarDraws, g.Id == guild.Id)).ToArray();

        return new GuildWarDto(guild.WarRating, guild.WarWins, guild.WarLosses, guild.WarDraws,
            signupStart.ToString("dddd HH:mm", CultureInfo.InvariantCulture), toNext, signed, signedGuilds,
            account.GuildRank == GuildRank.Leader, Guilds.CanManage(account.GuildRank), war != null, foe, myKills, theirKills, myScore, theirScore, lanes,
            secondsLeft, fightsLeft, cooldown, lastEvent, lastResult, rows, message);
    }

    /// <summary>GDD: the guild leader signs up. For the next night that has not begun; withdrawing is allowed until then.</summary>
    public async Task<GuildWarDto> GuildWarSignupAsync(Account account, GuildWarSignupRequest request, CancellationToken ct)
    {
        Guild guild = GuildOf(InGuild(account)) ?? throw new GameException("no_guild", "You are not in a guild.");
        if (account.GuildRank != GuildRank.Leader) throw new GameException("guild_rank", "Only the leader signs the guild up for war.");
        (string night, DateTime start) = SignupNight();
        string when = start.ToString("dddd HH:mm", CultureInfo.InvariantCulture);
        GuildWarSignup? existing = await _db.GuildWarSignups.FirstOrDefaultAsync(s => s.Night == night && s.GuildId == guild.Id, ct);
        string message;
        if (request.Join)
        {
            int members = await MemberCountAsync(guild.Id, ct);
            if (members < GuildWars.MinMembers) throw new GameException("war_members", $"A guild needs {GuildWars.MinMembers} members to go to war.");
            if (existing == null)
            {
                _db.GuildWarSignups.Add(new GuildWarSignup { Night = night, GuildId = guild.Id, Utc = DateTime.UtcNow });
                GuildLine(guild, $"{DisplayName(account)} signed the guild up for war on {when}.");
            }
            message = $"Signed up for {when}. Foes are drawn when the horns sound.";
        }
        else
        {
            if (existing != null)
            {
                _db.GuildWarSignups.Remove(existing);
                GuildLine(guild, $"{DisplayName(account)} withdrew the guild from the war on {when}.");
            }
            message = "Withdrawn from the war.";
        }
        await SaveAsync(ct);
        return await GuildWarAsync(account, message, ct);
    }

    /// <summary>The leader or an officer plants the guild's war flag on a lane: wins there push two steps.</summary>
    public async Task<GuildWarDto> GuildWarFlagAsync(Account account, GuildWarFlagRequest request, CancellationToken ct)
    {
        RequireManager(account);
        if (request.Lane < 0 || request.Lane >= GuildWars.Lanes) throw new GameException("bad_lane", "No such lane.");
        Guild guild = GuildOf(account)!;
        GuildWar running = await RunningWarAsync(guild.Id, DateTime.UtcNow, ct) ?? throw new GameException("no_war", "Your guild is not at war now.");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        GuildWar war = await LockWarAsync(running.Id, ct);
        if (war.GuildA == guild.Id) war.FlagA = request.Lane;
        else war.FlagB = request.Lane;
        GuildLine(guild, $"{DisplayName(account)} planted the war flag on the {GuildWars.LaneNames[request.Lane]}.");
        await SaveAsync(ct);
        await tx.CommitAsync(ct);
        return await GuildWarAsync(account, $"The war flag flies over the {GuildWars.LaneNames[request.Lane]}.", ct);
    }

    /// <summary>
    /// One duel on a lane: a member of the other guild is drawn as the defender; gear decides with a little luck
    /// (Rules.Duels). A win is a kill and pushes the lane; a loss is a kill for the other side. The client replays it.
    /// </summary>
    public async Task<GuildWarFightDto> GuildWarFightAsync(Account account, GuildWarFightRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        if (request.Lane < 0 || request.Lane >= GuildWars.Lanes) throw new GameException("bad_lane", "No such lane.");
        Guild guild = GuildOf(InGuild(account)) ?? throw new GameException("no_guild", "You are not in a guild.");
        DateTime now = DateTime.UtcNow;
        GuildWar running = await RunningWarAsync(guild.Id, now, ct) ?? throw new GameException("no_war", "Your guild is not at war now.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        GuildWar war = await LockWarAsync(running.Id, ct);
        if (war.Result != 0 || now >= war.EndsUtc) throw new GameException("no_war", "The war is over.");
        GuildWarEntry? entry = await _db.GuildWarEntries.FirstOrDefaultAsync(e => e.WarId == war.Id && e.AccountId == account.Id, ct);
        if (entry == null)
        {
            entry = new GuildWarEntry { WarId = war.Id, AccountId = account.Id, GuildId = guild.Id };
            _db.GuildWarEntries.Add(entry);
        }
        if (entry.Fights >= GuildWars.FightsPerWar) throw new GameException("war_spent", "You have fought all your duels in this war.");
        if (entry.Fights > 0 && now < entry.LastUtc.AddSeconds(GuildWars.CooldownSeconds))
        {
            int seconds = (int)Math.Ceiling((entry.LastUtc.AddSeconds(GuildWars.CooldownSeconds) - now).TotalSeconds);
            throw new GameException("siege_cooldown", $"You catch your breath: {seconds} more seconds.");
        }

        bool sideA = war.GuildA == guild.Id;
        Guid foeId = sideA ? war.GuildB : war.GuildA;
        Guild? foe = await _db.Guilds.AsNoTracking().FirstOrDefaultAsync(g => g.Id == foeId, ct);
        List<Account> defenders = await _db.Accounts.AsNoTracking().Where(a => a.GuildId == foeId).OrderBy(a => a.Id).ToListAsync(ct);
        if (foe == null || defenders.Count == 0) throw new GameException("no_foe", "The other guild has left the field.");

        ulong seed = BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(8));
        var rng = new XorShiftRandom(seed);
        Account defender = defenders[rng.NextInt(defenders.Count)];
        HeroStats a = Duels.Neutral(account.Items.Where(i => i.Equipped && !i.Destroyed).Select(i => i.ToState()), Content.LevelFor(account.Xp));
        HeroStats d = Duels.Neutral(defender.Items.Where(i => i.Equipped && !i.Destroyed).Select(i => i.ToState()), Content.LevelFor(defender.Xp));
        Duels.Compress(a, d);
        double edge = Duels.Edge(a, d);
        bool won = Duels.Roll(edge, rng);
        string foeName = $"[{foe.Tag}] {NameOf(defender)}";
        BossDef champion = Duels.Stage(foeName, Hero(account), won, seed);

        int lane = request.Lane;
        string laneName = GuildWars.LaneNames[lane];
        string me = DisplayName(account);
        string text;
        int before = war.Fronts[lane];
        if (won)
        {
            if (sideA) war.KillsA++; else war.KillsB++;
            bool flag = (sideA ? war.FlagA : war.FlagB) == lane;
            war.SetFront(lane, GuildWars.Push(before, sideA, flag));
            text = $"You felled {foeName} on the {laneName}.";
            war.LastEvent = $"{me} felled {foeName} on the {laneName}.";
        }
        else
        {
            if (sideA) war.KillsB++; else war.KillsA++;
            text = $"{foeName} held the {laneName} against you.";
            war.LastEvent = $"{foeName} held the {laneName} against {me}.";
        }
        int after = war.Fronts[lane];
        if (!GuildWars.Broken(before) && GuildWars.Broken(after))
        {
            text += $" The {laneName} is broken!";
            GuildLine(guild, $"{me} broke the {laneName} of [{foe.Tag}]!");
            GuildLine(foe, $"[{guild.Tag}] broke our {laneName}.");
        }

        entry.Fights++;
        if (won) entry.Wins++;
        entry.LastUtc = now;
        var inventory = Snapshot(account);
        inventory.Sorn += GuildWars.FightSorn;
        inventory.HuntMarks += GuildWars.FightMarks;
        Apply(account, inventory);
        _db.Ledger.Add(Entry(account.Id, null, "guildwar",
            $"war={war.Id} lane={lane} defender={defender.Id} seed={seed} edge={edge.ToString("0.000", CultureInfo.InvariantCulture)} won={won} front={after} kills={war.KillsA}-{war.KillsB}",
            GuildWars.FightSorn, request.RequestId));
        await SaveAsync(ct);
        await tx.CommitAsync(ct);

        // The replay dresses the champion as the defender: their class and the look band of their armour.
        Item? armor = defender.Items.FirstOrDefault(i => i.Equipped && !i.Destroyed && i.Slot == EquipSlot.Armor);
        int band = armor != null ? ItemLooks.Tier(armor.ToState().ItemLevel) : 0;
        var duel = new DuelResultDto(lane, seed, champion.Name, champion.Hp, champion.Attack, won, (int)Math.Round(Duels.WinChance(edge) * 100), text,
            defender.Class, band, defender.Figure);
        return new GuildWarFightDto(ToState(account), duel, await GuildWarAsync(account, "", ct));
    }

    /// <summary>A line in the guild's chat log (its system channel).</summary>
    private void GuildLine(Guild guild, string text) => SystemLine(Chat.GuildChannel(guild.Id), text);

    // ---- The world clock: pairing at 21:00, settling when the hour is up ----

    /// <summary>Pairs tonight's signed guilds once the night has begun (the night row is the lock).</summary>
    private async Task PairTonightAsync(CancellationToken ct)
    {
        DateTime local = _bells.LocalNow;
        (string night, DateTime start) = WarNight();
        if (!GuildWars.Running(local, start)) return;
        await PairAsync(night, night, _bells.ToUtc(start), _bells.ToUtc(start.AddMinutes(GuildWars.WindowMinutes)), ct);
    }

    /// <summary>
    /// Pairs the guilds signed up for <paramref name="signupNight"/> into wars on the night row <paramref name="rowKey"/>
    /// (the same key on a real night; a dev night takes the next night's sign-ups early).
    /// </summary>
    private async Task PairAsync(string signupNight, string rowKey, DateTime startsUtc, DateTime endsUtc, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $@"INSERT INTO ""GuildWarNights"" (""Night"", ""StartsUtc"", ""EndsUtc"", ""Paired"") VALUES ({rowKey}, {startsUtc}, {endsUtc}, FALSE)
               ON CONFLICT (""Night"") DO NOTHING", ct);
        GuildWarNight row = (await _db.GuildWarNights.FromSql($@"SELECT * FROM ""GuildWarNights"" WHERE ""Night"" = {rowKey} FOR UPDATE").ToListAsync(ct)).First();
        if (row.Paired) return;

        var signups = await _db.GuildWarSignups.Where(s => s.Night == signupNight).OrderBy(s => s.Utc).ToListAsync(ct);
        var ids = signups.Select(s => s.GuildId).ToList();
        var guilds = await _db.Guilds.Where(g => ids.Contains(g.Id)).ToListAsync(ct);
        var counts = await _db.Accounts.AsNoTracking().Where(a => a.GuildId != null && ids.Contains(a.GuildId.Value)).GroupBy(a => a.GuildId)
            .Select(x => new { x.Key, Count = x.Count() }).ToListAsync(ct);
        var ready = new List<Guild>();
        foreach (GuildWarSignup s in signups)
        {
            Guild? g = guilds.FirstOrDefault(x => x.Id == s.GuildId);
            if (g == null) continue;
            int members = counts.Where(c => c.Key == g.Id).Select(c => c.Count).FirstOrDefault();
            if (members >= GuildWars.MinMembers) ready.Add(g);
            else GuildLine(g, $"Too few members for tonight's war: a guild needs {GuildWars.MinMembers}.");
        }

        List<(int A, int B)> pairs = GuildWars.Pair(ready.Select(g => g.WarRating).ToList(), out int bye);
        foreach ((int ia, int ib) in pairs)
        {
            Guild ga = ready[ia], gb = ready[ib];
            _db.GuildWars.Add(new GuildWar
            {
                Night = rowKey, GuildA = ga.Id, GuildB = gb.Id, StartsUtc = startsUtc, EndsUtc = endsUtc,
                LastEvent = $"The horns sound: [{ga.Tag}] against [{gb.Tag}].",
            });
            GuildLine(ga, $"War! [{gb.Tag}] {gb.Name} is our foe tonight. Fight on the lanes until the horns fall silent.");
            GuildLine(gb, $"War! [{ga.Tag}] {ga.Name} is our foe tonight. Fight on the lanes until the horns fall silent.");
        }
        if (bye >= 0) GuildLine(ready[bye], "No foe answered the horn tonight.");
        if (pairs.Count > 0)
            SystemLine(Chat.World, pairs.Count == 1 ? $"War night: [{ready[pairs[0].A].Tag}] and [{ready[pairs[0].B].Tag}] take the field."
                : $"War night: {pairs.Count} guild wars take the field.");
        row.Paired = true;
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>Settles every war whose hour is up: score, rating, treasury and XP, and the news.</summary>
    private async Task SettleWarsAsync(CancellationToken ct)
    {
        DateTime now = DateTime.UtcNow;
        List<long> due = await _db.GuildWars.AsNoTracking().Where(w => w.Result == 0 && w.EndsUtc <= now).Select(w => w.Id).ToListAsync(ct);
        foreach (long id in due) await SettleWarAsync(id, ct);
    }

    private async Task<Guild?> TryLockGuildAsync(Guid id, CancellationToken ct)
    {
        Guild? guild = (await _db.Guilds.FromSql($@"SELECT * FROM ""Guilds"" WHERE ""Id"" = {id} FOR UPDATE").ToListAsync(ct)).FirstOrDefault();
        if (guild != null) await _db.Entry(guild).ReloadAsync(ct);
        return guild;
    }

    private async Task SettleWarAsync(long id, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        GuildWar war = await LockWarAsync(id, ct);
        if (war.Result != 0) return;
        // Both guild rows, in id order so two settlements never wait on each other.
        Guid first = war.GuildA.CompareTo(war.GuildB) <= 0 ? war.GuildA : war.GuildB;
        Guid second = first == war.GuildA ? war.GuildB : war.GuildA;
        Guild? g1 = await TryLockGuildAsync(first, ct);
        Guild? g2 = await TryLockGuildAsync(second, ct);
        Guild? ga = g1?.Id == war.GuildA ? g1 : g2?.Id == war.GuildA ? g2 : null;
        Guild? gb = g1?.Id == war.GuildB ? g1 : g2?.Id == war.GuildB ? g2 : null;

        int[] fronts = war.Fronts;
        int scoreA = GuildWars.Score(war.KillsA, fronts, true), scoreB = GuildWars.Score(war.KillsB, fronts, false);
        // A guild that disbanded mid-war forfeits.
        int result = ga == null && gb == null ? 3 : ga == null ? 2 : gb == null ? 1 : GuildWars.Result(scoreA, scoreB);
        war.Result = result;
        if (ga != null && gb != null)
        {
            (ga.WarRating, gb.WarRating) = GuildWars.Rate(ga.WarRating, gb.WarRating, result);
            if (result == 1) { ga.WarWins++; gb.WarLosses++; }
            else if (result == 2) { gb.WarWins++; ga.WarLosses++; }
            else { ga.WarDraws++; gb.WarDraws++; }
        }
        Pay(ga, result == 1, result == 3);
        Pay(gb, result == 2, result == 3);
        // Every member who fought gets Guild Tallies by their side's result (one UPDATE a side).
        foreach ((Guid side, bool won) in new[] { (war.GuildA, result == 1), (war.GuildB, result == 2) })
        {
            int tallies = won ? GuildWars.WinTallies : result == 3 ? GuildWars.DrawTallies : GuildWars.LossTallies;
            List<Guid> fought = await _db.GuildWarEntries.Where(e => e.WarId == war.Id && e.GuildId == side).Select(e => e.AccountId).ToListAsync(ct);
            if (fought.Count > 0)
                await _db.Accounts.Where(a => fought.Contains(a.Id)).ExecuteUpdateAsync(s => s.SetProperty(a => a.Tallies, a => a.Tallies + tallies), ct);
        }
        string tagA = ga != null ? "[" + ga.Tag + "]" : "a scattered guild", tagB = gb != null ? "[" + gb.Tag + "]" : "a scattered guild";
        war.LastEvent = result == 3 ? $"{tagA} and {tagB} fought to a draw, {scoreA} to {scoreB}."
            : result == 1 ? $"{tagA} beat {tagB}, {scoreA} to {scoreB}." : $"{tagB} beat {tagA}, {scoreB} to {scoreA}.";
        if (ga != null) GuildLine(ga, "The war is over: " + war.LastEvent);
        if (gb != null) GuildLine(gb, "The war is over: " + war.LastEvent);
        SystemLine(Chat.World, "Guild war: " + war.LastEvent);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        static void Pay(Guild? g, bool won, bool draw)
        {
            if (g == null) return;
            g.Treasury += won ? GuildWars.WinTreasury : draw ? GuildWars.DrawTreasury : 0;
            g.Xp += won ? GuildWars.WinXp : draw ? GuildWars.DrawXp : GuildWars.LossXp;
        }
    }

    /// <summary>Everything the world clock does on a tick (WorldClock, every 30 seconds). Each step has its own row locks.</summary>
    public async Task TickWorldAsync(CancellationToken ct)
    {
        await TickEventsAsync(ct);
        await PairTonightAsync(ct);
        await SettleWarsAsync(ct);
        foreach (FortressDef def in Fortresses.All) await AdvanceKeepAsync(def.Id, ct);
        await SettlePitSeasonAsync(ct);
        await AnnounceCommandersAsync(ct);
        await SettlePartyDungeonsAsync(ct);
    }

    // ---- Development: war nights and keep sieges on demand (the smoke test and screenshots) ----

    /// <summary>Starts a war night now for the guilds signed up for the next night, lasting <paramref name="minutes"/>.</summary>
    public async Task<GuildWarDto> DevWarNightAsync(Account account, int minutes, CancellationToken ct)
    {
        (string night, _) = SignupNight();
        DateTime now = DateTime.UtcNow;
        await PairAsync(night, "D" + now.Ticks.ToString(CultureInfo.InvariantCulture), now, now.AddMinutes(Math.Clamp(minutes, 1, 120)), ct);
        return await GuildWarAsync(account, "A war night began now.", ct);
    }

    /// <summary>Ends every running war now and settles them.</summary>
    public async Task<GuildWarDto> DevWarEndAsync(Account account, CancellationToken ct)
    {
        DateTime now = DateTime.UtcNow;
        await _db.GuildWars.Where(w => w.Result == 0 && w.EndsUtc > now).ExecuteUpdateAsync(s => s.SetProperty(w => w.EndsUtc, now.AddSeconds(-1)), ct);
        await SettleWarsAsync(ct);
        return await GuildWarAsync(account, "Every war ended now.", ct);
    }
}
