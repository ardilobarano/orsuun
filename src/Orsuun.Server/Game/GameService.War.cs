using System.Globalization;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Orsuun.Rules;
using Orsuun.Rules.Combat;
using Orsuun.Server.Data;

namespace Orsuun.Server.Game;

/// <summary>
/// The multiplayer layer (owner, 24 Sep 2026): the Banner oath, the War of Banners point race, Commander HP pools
/// shared by the whole server, and fortress sieges between the Banners.
/// </summary>
public sealed partial class GameService
{
    /// <summary>The oath: once per account, to one of the three Banners.</summary>
    public async Task<StateDto> SwearAsync(Account account, BannerRequest request, CancellationToken ct)
    {
        if (request.Banner == Banner.None || !Enum.IsDefined(request.Banner)) throw new GameException("bad_banner", "Choose one of the three Banners.");
        if (account.Banner != Banner.None) throw new GameException("sworn", "You are already sworn to the " + Banners.Def(account.Banner).Name + ".");
        account.Banner = request.Banner;
        account.SwornUtc = DateTime.UtcNow;
        _db.Ledger.Add(Entry(account.Id, null, "oath", request.Banner.ToString(), 0, Guid.NewGuid().ToString("N")));
        await SaveAsync(ct);
        return ToState(account);
    }

    private string Season(int weeksBack = 0) => Banners.SeasonKey(_bells.LocalNow.AddDays(-7 * weeksBack));

    /// <summary>Adds War of Banners points to a Banner's season total (an upsert, safe under concurrent fights).</summary>
    private async Task AddPointsAsync(Banner banner, long points, CancellationToken ct)
    {
        if (banner == Banner.None || points <= 0) return;
        string season = Season();
        int id = (int)banner;
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $@"INSERT INTO ""BannerScores"" (""Season"", ""Banner"", ""Points"") VALUES ({season}, {id}, {points})
               ON CONFLICT (""Season"", ""Banner"") DO UPDATE SET ""Points"" = ""BannerScores"".""Points"" + EXCLUDED.""Points""", ct);
    }

    private async Task<Banner> LastWinnerAsync(CancellationToken ct)
    {
        string last = Season(1);
        var rows = await _db.BannerScores.AsNoTracking().Where(s => s.Season == last).ToListAsync(ct);
        long Of(Banner b) => rows.Where(r => r.Banner == b).Select(r => r.Points).FirstOrDefault();
        return Banners.Leader(Of(Banner.Ember), Of(Banner.Sky), Of(Banner.Gold));
    }

    /// <summary>Sorn bonus on hunting for a Banner: last season's winner, plus each fortress it holds.</summary>
    private async Task<int> SornBonusPercentAsync(Banner banner, CancellationToken ct)
    {
        if (banner == Banner.None) return 0;
        int held = await _db.Fortresses.AsNoTracking().CountAsync(f => f.Holder == banner, ct);
        Banner winner = await LastWinnerAsync(ct);
        return (winner == banner ? Banners.WinnerBonusPercent : 0) + held * Banners.FortressBonusPercent;
    }

    public async Task<WarDto> WarAsync(Account account, CancellationToken ct)
    {
        string season = Season();
        var rows = await _db.BannerScores.AsNoTracking().Where(s => s.Season == season).ToListAsync(ct);
        var forts = await _db.Fortresses.AsNoTracking().OrderBy(f => f.Id).ToListAsync(ct);
        var flagIds = forts.Where(f => f.FlagGuildId != null).Select(f => f.FlagGuildId!.Value).Distinct().ToList();
        var tags = await _db.Guilds.AsNoTracking().Where(g => flagIds.Contains(g.Id)).Select(g => new { g.Id, g.Tag }).ToListAsync(ct);
        var standings = Banners.All.Select(d => new BannerStandingDto(d.Id, d.Name,
            rows.Where(r => r.Banner == d.Id).Select(r => r.Points).FirstOrDefault(), forts.Count(f => f.Holder == d.Id))).ToArray();
        FortressDto[] fortresses = forts.Select(f =>
        {
            FortressDef def = Fortresses.Find(f.Id)!;
            return new FortressDto(f.Id, def.Name, def.Region, f.Holder, (SiegePhase)f.Phase, f.Wall, f.WallMax, f.SiegeEmber, f.SiegeSky, f.SiegeGold, f.LastEvent,
                tags.Where(t => t.Id == f.FlagGuildId).Select(t => t.Tag).FirstOrDefault() ?? "");
        }).ToArray();
        int cooldown = account.LastSiegeUtc is DateTime last
            ? Math.Max(0, (int)(last.AddMinutes(Fortresses.CooldownMinutes) - DateTime.UtcNow).TotalSeconds) : 0;
        return new WarDto(season, standings, await LastWinnerAsync(ct), await SornBonusPercentAsync(account.Banner, ct), fortresses, cooldown,
            await KeepsAsync(account, forts, ct));
    }

    /// <summary>
    /// How many fighters a fresh Commander pool is sized for: the accounts that fought a Commander in the last week
    /// (at least one), so one player can still slay it on a quiet server and a busy server needs the crowd.
    /// </summary>
    private async Task<long> PoolFightersAsync(DateTime now, CancellationToken ct)
    {
        DateTime since = now.AddDays(-7);
        int fighters = await _db.BossHits.AsNoTracking().Where(h => h.Utc > since).Select(h => h.AccountId).Distinct().CountAsync(ct);
        return Math.Clamp(fighters, 1, 50);
    }

    /// <summary>
    /// The Commander's clock row, locked for the rest of the transaction, rolled to the current spawn, with the spawn's
    /// pool opened on its first fight.
    /// </summary>
    private async Task<BossClock> LockClockAsync(BossDef boss, DateTime now, CancellationToken ct)
    {
        BossClock? clock = (await _db.BossClocks.FromSql($@"SELECT * FROM ""BossClocks"" WHERE ""BossId"" = {boss.Id} FOR UPDATE").ToListAsync(ct)).FirstOrDefault();
        if (clock == null) clock = await ClockAsync(boss, now, ct);
        while (now >= clock.SpawnUtc.AddSeconds(boss.RespawnSeconds))
            clock.SpawnUtc = clock.SpawnUtc.AddSeconds(boss.RespawnSeconds);
        if (clock.PoolSpawnUtc != clock.SpawnUtc)
        {
            long pool = boss.Hp * await PoolFightersAsync(now, ct);
            clock.PoolSpawnUtc = clock.SpawnUtc;
            clock.HpMax = pool;
            clock.HpLeft = pool;
            clock.SlainUtc = null;
            clock.SlainBanner = Banner.None;
            clock.SlainBy = null;
        }
        return clock;
    }

    /// <summary>How many sworn players were about lately: fortress walls are sized by it.</summary>
    private async Task<int> SiegePlayersAsync(DateTime now, CancellationToken ct)
    {
        DateTime since = now.AddDays(-3);
        return await _db.Accounts.AsNoTracking().CountAsync(a => a.Banner != Banner.None && a.LastHeartbeatUtc > since, ct);
    }

    /// <summary>Creates the three fortresses on a fresh database, each held by its first Banner. Called at startup.</summary>
    public static async Task SeedFortressesAsync(GameDb db, CancellationToken ct)
    {
        foreach (FortressDef def in Fortresses.All)
        {
            if (await db.Fortresses.AnyAsync(f => f.Id == def.Id, ct)) continue;
            long wall = Fortresses.PhaseHp(SiegePhase.Gate, 1);
            db.Fortresses.Add(new Fortress
            {
                Id = def.Id, Holder = def.FirstHolder, HeldSinceUtc = DateTime.UtcNow, Phase = (int)SiegePhase.Gate, WallMax = wall, Wall = wall,
                LastEvent = def.Name + " stands under the " + Banners.Def(def.FirstHolder).Name + ".",
            });
        }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// One siege fight. A player of another Banner attacks the phase's champion and the damage comes off the wall;
    /// a player of the holding Banner defends and mends the wall by half the damage. Breaking the Gate or the Yard
    /// opens the next phase; breaking the Hall hands the fortress to the attacking Banner with the most siege damage.
    /// The fortress row is locked for the fight. The client replays the fight from the seed.
    /// </summary>
    public async Task<StateDto> SiegeAsync(Account account, SiegeRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        if (account.Banner == Banner.None) throw new GameException("no_banner", "Swear to a Banner first.");
        FortressDef def = Fortresses.Find(request.FortressId) ?? throw new GameException("no_fortress", "Unknown fortress.");
        DateTime now = DateTime.UtcNow;
        if (account.LastSiegeUtc is DateTime last && now < last.AddMinutes(Fortresses.CooldownMinutes))
        {
            int minutes = (int)Math.Ceiling((last.AddMinutes(Fortresses.CooldownMinutes) - now).TotalMinutes);
            throw new GameException("siege_cooldown", $"Your warband regroups: {minutes} more minute{(minutes == 1 ? "" : "s")}.");
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        Fortress fort = (await _db.Fortresses.FromSql($@"SELECT * FROM ""Fortresses"" WHERE ""Id"" = {def.Id} FOR UPDATE").ToListAsync(ct)).FirstOrDefault()
            ?? throw new GameException("no_fortress", "That fortress is not built yet.");
        bool defending = account.Banner == fort.Holder;
        var phase = (SiegePhase)fort.Phase;
        BossDef champion = Fortresses.Champion(def, phase);

        ulong seed = BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(8));
        int potionsAtStart = account.Potions;
        Bell bell = _bells.Active;
        var inventory = Snapshot(account);
        BossRunResult run = BossRun.Simulate(champion, Hero(account), inventory, seed, bell);
        inventory.Sorn += Fortresses.FightSorn;
        inventory.HuntMarks += Fortresses.FightMarks;

        string name = DisplayName(account);
        string bannerName = Banners.Def(account.Banner).Name;
        bool broke = false, captured = false;
        long points = run.Damage / Banners.SiegeDamagePerPoint;
        Banner pointsTo = account.Banner;
        Banner conqueror = Banner.None;
        string text;
        if (defending)
        {
            long mend = run.Damage * Fortresses.DefenceMendPercent / 100;
            fort.Wall = Math.Min(fort.WallMax, fort.Wall + mend);
            text = $"You held the {Fortresses.PhaseName(phase)} of {def.Name}: the wall mended by {mend.ToString("N0", CultureInfo.InvariantCulture)}.";
        }
        else
        {
            fort.Wall = Math.Max(0, fort.Wall - run.Damage);
            if (account.Banner == Banner.Ember) fort.SiegeEmber += run.Damage;
            else if (account.Banner == Banner.Sky) fort.SiegeSky += run.Damage;
            else fort.SiegeGold += run.Damage;
            text = $"You struck the {Fortresses.PhaseName(phase)} of {def.Name} for {run.Damage.ToString("N0", CultureInfo.InvariantCulture)}.";
            if (fort.Wall == 0)
            {
                broke = true;
                inventory.Sorn += Fortresses.BreakSorn;
                inventory.HuntMarks += Fortresses.BreakMarks;
                points += Banners.PointsSiegePhase;
                int players = await SiegePlayersAsync(now, ct);
                if (phase == SiegePhase.Hall)
                {
                    // The fortress goes to the attacking Banner that did the most in this siege.
                    var attackers = new[] { (Banner.Ember, fort.SiegeEmber), (Banner.Sky, fort.SiegeSky), (Banner.Gold, fort.SiegeGold) }
                        .Where(x => x.Item1 != fort.Holder).OrderByDescending(x => x.Item2).ToArray();
                    conqueror = attackers[0].Item1;
                    captured = true;
                    if (conqueror == account.Banner) inventory.HuntMarks += Fortresses.CaptureMarks;
                    fort.Holder = conqueror;
                    fort.HeldSinceUtc = now;
                    fort.Phase = (int)SiegePhase.Gate;
                    fort.WallMax = fort.Wall = Fortresses.PhaseHp(SiegePhase.Gate, players);
                    fort.SiegeEmber = fort.SiegeSky = fort.SiegeGold = 0;
                    // The guild flag stays: guilds win the keep at the Sunday keep siege (GameService.Keeps).
                    fort.LastEvent = $"The {Banners.Def(conqueror).Name} took {def.Name}; {name} broke the Hall.";
                    text = $"The Hall of {def.Name} falls! {def.Name} now flies the {Banners.Def(conqueror).Name}.";
                    SystemLine(Chat.World, fort.LastEvent);
                }
                else
                {
                    var next = (SiegePhase)(fort.Phase + 1);
                    fort.Phase = (int)next;
                    fort.WallMax = fort.Wall = Fortresses.PhaseHp(next, players);
                    fort.LastEvent = $"{name} of the {bannerName} broke the {Fortresses.PhaseName(phase)} of {def.Name}.";
                    text = $"You broke the {Fortresses.PhaseName(phase)} of {def.Name}! The {Fortresses.PhaseName(next)} lies open.";
                }
            }
        }

        Apply(account, inventory);
        account.LastSiegeUtc = now;
        Count(account, BountyMetric.SiegeFights, 1);
        _db.Ledger.Add(Entry(account.Id, null, "siege", $"fortress={def.Id} phase={phase} defending={defending} seed={seed} damage={run.Damage} wall={fort.Wall}/{fort.WallMax} broke={broke} captured={captured} holder={fort.Holder}",
            Fortresses.FightSorn, request.RequestId));
        await SaveAsync(ct);
        await AddPointsAsync(pointsTo, points, ct);
        await AddGuildXpAsync(account.GuildId, run.Damage / Guilds.SiegeDamagePerXp, ct);
        if (captured) await AddPointsAsync(conqueror, Banners.PointsFortressTaken, ct);
        await tx.CommitAsync(ct);
        return ToState(account, siege: new SiegeResultDto(def.Id, champion.Id, defending, seed, run.Damage, potionsAtStart, bell, phase, fort.Wall, broke, captured, fort.Holder, text));
    }
}
