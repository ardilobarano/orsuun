using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Orsuun.Rules;
using Orsuun.Rules.Combat;
using Orsuun.Server.Data;

namespace Orsuun.Server.Game;

/// <summary>
/// The Endless Tower (owner, 7 Oct 2026; Rules.Tower): a climb is scored here floor by floor from floor 1 until the hero
/// falls, each floor with its own seed; the client replays the last few. The week's best floor is the ladder, settled by
/// the world clock when the week turns (SettleTowerSeasonAsync, claimed by its TowerSeasons row).
/// </summary>
public sealed partial class GameService
{
    private string TowerSeasonNow() => Rules.Bounties.WeekKey(_bells.LocalNow);
    private string TowerSeasonLast() => Rules.Bounties.WeekKey(_bells.LocalNow.AddDays(-7));

    private int TowerBestOf(Account account) => account.TowerSeason == TowerSeasonNow() ? account.TowerBest : 0;

    private int TowerClimbsLeft(Account account) =>
        Math.Max(0, Tower.ClimbsPerDay - (account.TowerClimbDay == Rules.Bounties.DayKey(_bells.LocalNow) ? account.TowerClimbs : 0));

    /// <summary>The hero's week in the tower and the week's ladder (the ten best, and the hero's place).</summary>
    public async Task<TowerDto> TowerAsync(Account account, CancellationToken ct, string? message = null)
    {
        string season = TowerSeasonNow();
        int best = TowerBestOf(account);
        var top = await _db.Accounts.AsNoTracking()
            .Where(a => a.TowerSeason == season && a.TowerBest > 0 && a.BannedUtc == null)
            .OrderByDescending(a => a.TowerBest).ThenBy(a => a.TowerBestUtc).ThenBy(a => a.Id)
            .Take(Tower.PaidRanks)
            .Select(a => new { a.Id, a.Name, a.Class, a.Xp, a.TowerBest, a.TitleId, a.PitTitle, a.TowerTitle })
            .ToListAsync(ct);
        TowerRowDto[] ladder = top.Select((t, i) => new TowerRowDto(i + 1, t.Id, ShownName(t.Id, t.Name), t.Class, Content.LevelFor(t.Xp), t.TowerBest,
            Achievements.TitleOf(t.TitleId) ?? t.PitTitle ?? t.TowerTitle ?? "")).ToArray();
        int rank = 0;
        if (best > 0)
        {
            DateTime when = account.TowerBestUtc;
            rank = 1 + await _db.Accounts.CountAsync(a => a.TowerSeason == season && a.BannedUtc == null && a.Id != account.Id
                && (a.TowerBest > best || (a.TowerBest == best && a.TowerBestUtc < when)), ct);
        }
        int paid = account.TowerSeason == season ? account.TowerPaid : 0;
        int next = Tower.NextMilestone(paid);
        string holds = Tower.ChestPreview(next);
        long bestEver = FeatCounters.Parse(account.Feats)[FeatMetric.TowerFloor];
        return new TowerDto(best, TowerClimbsLeft(account), Tower.ClimbsPerDay, next, holds, ladder, rank,
            Rules.Bounties.SecondsToWeeklyReset(_bells.LocalNow), account.TowerTitle, (int)bestEver, message);
    }

    /// <summary>
    /// Climbs the tower: floor after floor from the first until the hero falls (or tops it). The floors pay their monsters'
    /// sorn and XP but no pieces; each milestone floor passed for the first time this week opens its chest. Returns the last
    /// floors to replay.
    /// </summary>
    public async Task<TowerClimbDto> ClimbTowerAsync(Account account, TowerClimbRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        if (!Unlocks.Open(Feature.Tower, Content.LevelFor(account.Xp))) throw new GameException("locked", Unlocks.Locked(Feature.Tower));
        if (account.AtRiver) throw new GameException("at_river", "Leave the river first.");
        if (TowerClimbsLeft(account) <= 0) throw new GameException("no_climbs", "Today's climbs are used. New ones come at 20:00.");
        DateTime now = DateTime.UtcNow;
        string season = TowerSeasonNow(), day = Rules.Bounties.DayKey(_bells.LocalNow);
        if (account.TowerSeason != season)
        {
            account.TowerSeason = season;
            account.TowerBest = 0;
            account.TowerPaid = 0;
        }
        account.TowerClimbs = (account.TowerClimbDay == day ? account.TowerClimbs : 0) + 1;
        account.TowerClimbDay = day;

        HeroStats hero = Hero(account);
        Inventory inventory = Snapshot(account);
        var last = new Queue<DungeonFloorDto>();
        int fellOn = 0;
        for (int floor = 1; floor <= Tower.MaxFloors; floor++)
        {
            ulong seed = BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(8));
            int potions = inventory.Potions;
            StageRunResult run = StageRun.Simulate(Tower.Floor(floor), hero, inventory, seed);
            last.Enqueue(new DungeonFloorDto(floor, seed, potions, run.Cleared));
            if (last.Count > Tower.ReplayFloors) last.Dequeue();
            if (!run.Cleared)
            {
                fellOn = floor;
                break;
            }
        }
        int reached = fellOn > 0 ? fellOn - 1 : Tower.MaxFloors;
        // No pieces from the tower's floors (a climb of a hundred floors would fill the bag); its chests pay instead.
        inventory.Loot.Clear();
        var chests = new List<string>();
        for (int milestone = Tower.NextMilestone(account.TowerPaid); milestone <= reached; milestone += Tower.MilestoneEvery)
        {
            chests.Add($"floor {milestone}: {Tower.Chest(milestone, inventory, account.Class, _rng)}");
            account.TowerPaid = milestone;
        }
        Apply(account, inventory, hunt: true);
        bool newBest = reached > account.TowerBest;
        if (newBest)
        {
            account.TowerBest = reached;
            account.TowerBestUtc = now;
        }
        FeatBest(account, FeatMetric.TowerFloor, reached);
        string text = fellOn > 0 ? $"You reached floor {reached} of the Endless Tower and fell on floor {fellOn}."
            : $"You topped the Endless Tower: all {Tower.MaxFloors} floors!";
        if (newBest && reached > 0) text += " A new best this week!";
        _db.Ledger.Add(Entry(account.Id, null, "tower", $"season={season} reached={reached} fell={fellOn} best={account.TowerBest} chests={chests.Count}", 0, request.RequestId));
        await SaveAsync(ct);
        TowerDto tower = await TowerAsync(account, ct);
        return new TowerClimbDto(ToState(account), last.ToArray(), reached, fellOn, newBest, string.Join("; ", chests), text, tower);
    }

    /// <summary>
    /// When the week turns (from the world clock, every tick): the week that ended is claimed once by its TowerSeasons row;
    /// its ten best climbers are paid sorn by letter, the three best take a title for the new week (last week's end), and
    /// world chat names the best.
    /// </summary>
    private async Task SettleTowerSeasonAsync(CancellationToken ct)
    {
        string last = TowerSeasonLast();
        if (await _db.TowerSeasons.AnyAsync(t => t.Season == last, ct)) return;
        await SettleTowerSeasonCoreAsync(last, ct);
    }

    private async Task SettleTowerSeasonCoreAsync(string ended, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var record = new TowerSeasonRecord { Season = ended, SettledUtc = DateTime.UtcNow };
        _db.TowerSeasons.Add(record);
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { _db.ChangeTracker.Clear(); return; }
        var climbers = await _db.Accounts.AsNoTracking()
            .Where(a => a.TowerSeason == ended && a.TowerBest > 0 && a.BannedUtc == null)
            .OrderByDescending(a => a.TowerBest).ThenBy(a => a.TowerBestUtc).ThenBy(a => a.Id)
            .Take(Tower.PaidRanks)
            .Select(a => new { a.Id, a.Name, a.TowerBest })
            .ToListAsync(ct);
        // Last week's titles end; this week's three best take theirs.
        await _db.Accounts.Where(a => a.TowerTitle != null).ExecuteUpdateAsync(s => s.SetProperty(a => a.TowerTitle, (string?)null), ct);
        var champions = new List<string>();
        for (int i = 0; i < climbers.Count; i++)
        {
            var c = climbers[i];
            int rank = i + 1;
            string? title = Tower.Title(rank);
            long sorn = Tower.SeasonSorn(c.TowerBest, rank);
            if (title != null) await _db.Accounts.Where(a => a.Id == c.Id).ExecuteUpdateAsync(s => s.SetProperty(a => a.TowerTitle, title), ct);
            if (rank <= 3) champions.Add(ShownName(c.Id, c.Name));
            SendLetter(c.Id, "tower", Tower.Name, $"The tower's week: rank {rank}",
                $"The week is over. You climbed to floor {c.TowerBest} and finished {rank} of every climber"
                + (title != null ? $", and you wear the title {title} through the new week." : "."), sorn: sorn);
        }
        record.Climbers = climbers.Count;
        record.Champions = string.Join(", ", champions);
        if (champions.Count > 0)
            SystemLine(Chat.World, $"The Endless Tower's week is over: {champions[0]} climbed highest (floor {climbers[0].TowerBest}) and is Lord of the Endless Tower.");
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        SendPushes();
    }

    /// <summary>Development: settles the running week's ladder now, as if the week had turned (the smoke test).</summary>
    public async Task<TowerDto> DevTowerWeekEndAsync(Account account, CancellationToken ct)
    {
        await SettleTowerSeasonCoreAsync(TowerSeasonNow(), ct);
        await _db.Entry(account).ReloadAsync(ct);
        return await TowerAsync(account, ct, "The week was settled.");
    }
}
