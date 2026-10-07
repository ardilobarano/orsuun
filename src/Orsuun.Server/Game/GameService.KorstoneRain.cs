using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Orsuun.Rules;
using Orsuun.Rules.Combat;
using Orsuun.Server.Data;

namespace Orsuun.Server.Game;

/// <summary>
/// Korstone Rain (owner, 7 Oct 2026; Rules.KorstoneRain): the world clock drops a Giant Korstone on a map every few hours
/// (TickRainAsync); heroes hunting that map strike it (StrikeRainAsync, under the fall's row lock), and the strike that
/// breaks it showers its strikers and the map's other hunters by letter. The fall as last known lives on EventCalendar.Rain,
/// so a state is built without asking the database.
/// </summary>
public sealed partial class GameService
{
    /// <summary>The Turnstone's id among the trade goods (a letter carries one good).</summary>
    private const int TurnstoneGood = 5;

    private static EventCalendar.RainEntry RainEntryOf(KorstoneFall f) =>
        new(f.Id, f.Map, f.Camp, f.FellUtc, f.EndsUtc, f.HpMax, f.HpLeft, f.BrokenUtc, f.BrokenBy);

    /// <summary>The Giant Korstone as the hero's state carries it: while it stands, and for a minute after it breaks (its
    /// shower on the field); null otherwise.</summary>
    private RainDto? RainOf(Account account)
    {
        EventCalendar.RainEntry? r = _events.Rain;
        if (r == null) return null;
        DateTime now = DateTime.UtcNow;
        bool broken = r.BrokenUtc != null;
        if (broken ? now - r.BrokenUtc!.Value > TimeSpan.FromMinutes(1) : now >= r.EndsUtc) return null;
        bool mine = account.RainFallId == r.Id;
        return new RainDto(r.Id, r.Map, Content.Maps[r.Map - 1].Name, r.Camp, r.HpLeft, r.HpMax, (long)Math.Max(0, (r.EndsUtc - now).TotalSeconds),
            (long)Math.Max(0, (now - r.FellUtc).TotalSeconds), Math.Max(0, KorstoneRain.StrikesPerHero - (mine ? account.RainStrikes : 0)),
            mine ? account.RainDamage : 0, broken, r.BrokenBy);
    }

    /// <summary>From the world clock: says an unbroken stone's end once, drops the next stone when its time comes, and keeps
    /// the calendar's copy of the latest fall.</summary>
    private async Task TickRainAsync(CancellationToken ct)
    {
        DateTime now = DateTime.UtcNow;
        KorstoneFall? last = await _db.KorstoneFalls.AsNoTracking().OrderByDescending(f => f.Id).FirstOrDefaultAsync(ct);
        if (last != null && last.BrokenUtc == null && last.EndsUtc <= now && !last.Settled)
        {
            int claimed = await _db.KorstoneFalls.Where(f => f.Id == last.Id && !f.Settled).ExecuteUpdateAsync(s => s.SetProperty(f => f.Settled, true), ct);
            if (claimed == 1)
            {
                SystemLine(Chat.World, $"The Giant Korstone on {Content.Maps[last.Map - 1].Name} sank back into the earth unbroken.");
                await SaveAsync(ct);
            }
        }
        if (last == null || now - last.FellUtc >= TimeSpan.FromMinutes(KorstoneRain.IntervalMinutes)) last = await DropKorstoneAsync(null, now, ct);
        _events.SetRain(last == null ? null : RainEntryOf(last));
    }

    /// <summary>Drops a Giant Korstone: on <paramref name="map"/>, else on the map of a hero hunting now (so busier maps are
    /// likelier), else on any map; its health is for the heroes hunting there.</summary>
    private async Task<KorstoneFall> DropKorstoneAsync(int? map, DateTime now, CancellationToken ct)
    {
        DateTime recent = now - OnlineGrace;
        List<int> hunted = await _db.Accounts.AsNoTracking()
            .Where(a => a.LastHeartbeatUtc > recent && !a.AtRiver && a.BannedUtc == null && a.ParkedStage >= 1 && a.ParkedStage <= Content.TotalStages)
            .Select(a => a.ParkedStage).ToListAsync(ct);
        List<int> maps = hunted.Select(MapQuests.MapOfPlace).ToList();
        int chosen = map ?? (maps.Count > 0 ? maps[_rng.NextInt(maps.Count)] : 1 + _rng.NextInt(Content.Maps.Length));
        int heroes = maps.Count(m => m == chosen);
        var fall = new KorstoneFall
        {
            Map = chosen, Camp = _rng.NextInt(EliteCamps.Camps), FellUtc = now, EndsUtc = now.AddMinutes(KorstoneRain.UpMinutes), Heroes = heroes,
            HpMax = KorstoneRain.HpFor(chosen, heroes),
        };
        fall.HpLeft = fall.HpMax;
        _db.KorstoneFalls.Add(fall);
        SystemLine(Chat.World, $"A Giant Korstone falls from the sky on {Content.Maps[chosen - 1].Name}! Every hero hunting there may strike it {KorstoneRain.StrikesPerHero} times in the next {KorstoneRain.UpMinutes} minutes.");
        await SaveAsync(ct);
        return fall;
    }

    /// <summary>
    /// A strike on the standing Giant Korstone: a StrikeSeconds lane against it and the monsters it calls, scored here with
    /// its own seed (the client replays it). Its damage comes off the shared health under the fall's row lock; the strike
    /// that breaks it showers everyone.
    /// </summary>
    public async Task<RainStrikeDto> StrikeRainAsync(Account account, RainStrikeRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        DateTime now = DateTime.UtcNow;
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        KorstoneFall fall = (await _db.KorstoneFalls.FromSql(
                $@"SELECT * FROM ""KorstoneFalls"" WHERE ""BrokenUtc"" IS NULL AND ""EndsUtc"" > {now} ORDER BY ""Id"" DESC LIMIT 1 FOR UPDATE").ToListAsync(ct))
            .FirstOrDefault() ?? throw new GameException("no_stone", "No Giant Korstone stands now.");
        string mapName = Content.Maps[fall.Map - 1].Name;
        if (account.AtRiver || MapQuests.MapOfPlace(account.ParkedStage) != fall.Map)
            throw new GameException("not_here", $"The Giant Korstone fell on {mapName}: hunt there to strike it.");
        if (account.RainFallId != fall.Id)
        {
            account.RainFallId = fall.Id;
            account.RainStrikes = 0;
            account.RainDamage = 0;
        }
        if (account.RainStrikes >= KorstoneRain.StrikesPerHero) throw new GameException("no_strikes", "You have struck this stone three times.");

        ulong seed = BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(8));
        Inventory inventory = Snapshot(account);
        int potions = inventory.Potions;
        LaneSim lane = BossRun.Create(KorstoneRain.Stage(fall.Map), Hero(account), inventory, seed);
        while (lane.CurrentTick < KorstoneRain.StrikeTicks && lane.Deaths == 0)
        {
            lane.Tick();
            lane.DrainEvents();
        }
        long damage = Math.Min(lane.BossDamageDealt, fall.HpLeft);
        Apply(account, inventory, hunt: true);
        fall.HpLeft -= damage;
        account.RainStrikes++;
        account.RainDamage += damage;
        KorstoneStrike? row = await _db.KorstoneStrikes.FindAsync(new object[] { fall.Id, account.Id }, ct);
        if (row == null) _db.KorstoneStrikes.Add(row = new KorstoneStrike { FallId = fall.Id, AccountId = account.Id });
        row.Name = NameOf(account);
        row.Damage += damage;
        row.Strikes++;
        row.Utc = now;
        bool broke = fall.HpLeft <= 0;
        string text = broke ? $"You broke the Giant Korstone with {damage:N0} damage! Its shower comes to everyone by letter."
            : $"You struck the Giant Korstone for {damage:N0} damage: {fall.HpLeft * 100 / Math.Max(1, fall.HpMax)}% of it stands.";
        if (broke)
        {
            fall.BrokenUtc = now;
            fall.BrokenBy = NameOf(account);
            await _db.SaveChangesAsync(ct);   // this strike's damage, before the shares are read
            int showered = await ShowerAsync(fall, now, ct);
            SystemLine(Chat.World, $"{NameOf(account)} broke the Giant Korstone on {mapName}! Its shower falls on {showered} heroes.");
        }
        _db.Ledger.Add(Entry(account.Id, null, "rain-strike", $"fall={fall.Id} map={fall.Map} damage={damage} left={fall.HpLeft} broke={broke}", 0, request.RequestId));
        await SaveAsync(ct);
        await tx.CommitAsync(ct);
        _events.SetRain(RainEntryOf(fall));
        return new RainStrikeDto(ToState(account), fall.Map, seed, potions, damage, fall.HpLeft, fall.HpMax, broke, text);
    }

    /// <summary>The broken stone's shower, by letter: every striker by the share of the damage they dealt, every other hero
    /// hunting the map a little. Returns how many heroes it fell on.</summary>
    private async Task<int> ShowerAsync(KorstoneFall fall, DateTime now, CancellationToken ct)
    {
        string mapName = Content.Maps[fall.Map - 1].Name;
        var strikers = await _db.KorstoneStrikes.AsNoTracking().Where(s => s.FallId == fall.Id).ToListAsync(ct);
        long total = Math.Max(1, strikers.Sum(s => s.Damage));
        foreach (KorstoneStrike s in strikers)
        {
            int share = (int)(s.Damage * 1000 / total);
            var shower = KorstoneRain.Shower(fall.Map, true, share, _rng);
            SendLetter(s.AccountId, "rain", "Korstone Rain", "The Giant Korstone broke!",
                $"The stone on {mapName} broke under {strikers.Count} heroes' blows, and you dealt {share / 10}% of them. Its shower falls on you.",
                sorn: shower.Sorn, goodId: TradeGoods.FirstKorshard + shower.ShardRank, goodCount: shower.Shards);
            if (shower.Turnstones > 0)
                SendLetter(s.AccountId, "rain", "Korstone Rain", "Turnstones from the stone", "Shards of the Giant Korstone, good for turning etchings.",
                    goodId: TurnstoneGood, goodCount: shower.Turnstones);
        }
        DateTime recent = now - OnlineGrace;
        var struck = strikers.Select(s => s.AccountId).ToHashSet();
        int firstStage = (fall.Map - 1) * MapDef.StagesPerMap + 1, lastStage = fall.Map * MapDef.StagesPerMap;
        List<Guid> bystanders = await _db.Accounts.AsNoTracking()
            .Where(a => a.LastHeartbeatUtc > recent && !a.AtRiver && a.BannedUtc == null && a.ParkedStage >= firstStage && a.ParkedStage <= lastStage)
            .OrderByDescending(a => a.LastHeartbeatUtc).Select(a => a.Id).Take(200).ToListAsync(ct);
        int showered = strikers.Count;
        foreach (Guid id in bystanders.Where(id => !struck.Contains(id)))
        {
            var shower = KorstoneRain.Shower(fall.Map, false, 0, _rng);
            SendLetter(id, "rain", "Korstone Rain", "The Giant Korstone broke!",
                $"The stone on {mapName} broke while you hunted there, and its shower reached you too.",
                sorn: shower.Sorn, goodId: TradeGoods.FirstKorshard + shower.ShardRank, goodCount: shower.Shards);
            showered++;
        }
        return showered;
    }

    /// <summary>Development: drops a Giant Korstone now on a map (the hero's when 0), whatever the clock says.</summary>
    public async Task<StateDto> DevRainAsync(Account account, int map, CancellationToken ct)
    {
        int on = map >= 1 && map <= Content.Maps.Length ? map : Math.Max(1, MapQuests.MapOfPlace(account.ParkedStage));
        DateTime now = DateTime.UtcNow;
        // Only one stone stands at a time: one still standing ends quietly first.
        await _db.KorstoneFalls.Where(f => f.BrokenUtc == null && f.EndsUtc > now)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.EndsUtc, now).SetProperty(f => f.Settled, true), ct);
        KorstoneFall fall = await DropKorstoneAsync(on, now, ct);
        _events.SetRain(RainEntryOf(fall));
        return ToState(account);
    }
}
