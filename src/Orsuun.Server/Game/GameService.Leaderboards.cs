using Microsoft.EntityFrameworkCore;
using Orsuun.Rules;
using Orsuun.Server.Data;

namespace Orsuun.Server.Game;

// Leaderboards (owner, 28 Sep 2026: "Leaderboards"): heroes by level, furthest stage and Pit rating, guilds by level and by
// this week's raid; the hero boards this week (gains since the week's first heartbeat) or of all time.
public sealed partial class GameService
{
    public const int BoardRows = 50;

    /// <summary>Starts the hero's leaderboard week at its first heartbeat or push of the week: weekly boards count from here.</summary>
    private void RollBoardWeek(Account account)
    {
        string week = Rules.Bounties.WeekKey(_bells.LocalNow);
        if (account.BoardWeek == week) return;
        account.BoardWeek = week;
        account.BoardWeekXp = account.Xp;
        account.BoardWeekStage = account.HighestStageCleared;
    }

    public async Task<LeaderboardDto> LeaderboardAsync(Account account, string? board, string? period, CancellationToken ct)
    {
        board = board is "stage" or "pits" or "guilds" ? board : "level";
        bool week = period == "week" && board != "pits";
        string weekKey = Rules.Bounties.WeekKey(_bells.LocalNow);
        if (board == "guilds") return await GuildBoardAsync(account, week, weekKey, ct);

        IQueryable<Account> heroes = _db.Accounts.AsNoTracking().Where(a => a.BannedUtc == null);
        if (week) heroes = heroes.Where(a => a.BoardWeek == weekKey);
        if (board == "pits") heroes = heroes.Where(a => a.PitWins + a.PitLosses > 0);
        IOrderedQueryable<Account> ordered = (board, week) switch
        {
            ("level", false) => heroes.OrderByDescending(a => a.Xp),
            ("level", true) => heroes.Where(a => a.Xp > a.BoardWeekXp).OrderByDescending(a => a.Xp - a.BoardWeekXp),
            ("stage", false) => heroes.OrderByDescending(a => a.HighestStageCleared).ThenByDescending(a => a.Xp),
            ("stage", true) => heroes.Where(a => a.HighestStageCleared > a.BoardWeekStage)
                .OrderByDescending(a => a.HighestStageCleared - a.BoardWeekStage).ThenByDescending(a => a.HighestStageCleared),
            _ => heroes.OrderByDescending(a => a.PitRating).ThenByDescending(a => a.PitWins),
        };
        var top = await ordered.Take(BoardRows)
            .Select(a => new { a.Id, a.Name, a.Class, a.Xp, a.Banner, a.TitleId, a.PitTitle, a.TowerTitle, a.HighestStageCleared, a.PitRating, a.BoardWeekXp, a.BoardWeekStage, a.GuildId })
            .ToListAsync(ct);
        var guildIds = top.Where(t => t.GuildId != null).Select(t => t.GuildId).Distinct().ToList();
        var tags = await _db.Guilds.AsNoTracking().Where(g => guildIds.Contains(g.Id)).ToDictionaryAsync(g => g.Id, g => g.Tag, ct);
        long ValueOf(long xp, int stage, int rating, long weekXp, int weekStage) => (board, week) switch
        {
            ("level", false) => Content.LevelFor(xp),
            ("level", true) => xp - weekXp,
            ("stage", false) => stage,
            ("stage", true) => stage - weekStage,
            _ => rating,
        };
        var rows = top.Select((t, i) => new LeaderRowDto(i + 1, t.Id, ShownName(t.Id, t.Name), Achievements.TitleOf(t.TitleId) ?? t.PitTitle ?? t.TowerTitle ?? "",
            t.Class.ToString(), Content.LevelFor(t.Xp), t.Banner, ValueOf(t.Xp, t.HighestStageCleared, t.PitRating, t.BoardWeekXp, t.BoardWeekStage),
            t.GuildId is Guid g && tags.TryGetValue(g, out string? tag) ? tag : "")).ToArray();

        // The hero's own place, when it is on this board at all.
        LeaderRowDto? mine = rows.FirstOrDefault(r => r.Id == account.Id);
        bool onBoard = account.BannedUtc == null && (!week || account.BoardWeek == weekKey) && (board != "pits" || account.PitWins + account.PitLosses > 0);
        if (mine == null && onBoard)
        {
            long value = ValueOf(account.Xp, account.HighestStageCleared, account.PitRating, account.BoardWeekXp, account.BoardWeekStage);
            int ahead = (board, week) switch
            {
                ("level", false) => await heroes.CountAsync(a => a.Xp > account.Xp, ct),
                ("level", true) => await heroes.CountAsync(a => a.Xp - a.BoardWeekXp > value, ct),
                ("stage", false) => await heroes.CountAsync(a => a.HighestStageCleared > account.HighestStageCleared
                                                              || (a.HighestStageCleared == account.HighestStageCleared && a.Xp > account.Xp), ct),
                ("stage", true) => await heroes.CountAsync(a => a.HighestStageCleared - a.BoardWeekStage > value, ct),
                _ => await heroes.CountAsync(a => a.PitRating > account.PitRating, ct),
            };
            if (value > 0 || !week)
                mine = new LeaderRowDto(ahead + 1, account.Id, NameOf(account), TitleOf(account) ?? "", account.Class.ToString(), Content.LevelFor(account.Xp),
                    account.Banner, value, _guild?.Tag ?? "");
        }
        string note = board == "pits" ? "The Pits' season rating." : week ? "Gained since this week began (Monday 20:00)." : "";
        return new LeaderboardDto(board, week ? "week" : "all", rows, mine, note);
    }

    /// <summary>Guilds by level (all time) or by this week's raid (felled first, then the most damage).</summary>
    private async Task<LeaderboardDto> GuildBoardAsync(Account account, bool week, string weekKey, CancellationToken ct)
    {
        var guilds = await _db.Guilds.AsNoTracking().Select(g => new { g.Id, g.Name, g.Tag, g.Xp }).ToListAsync(ct);
        var raids = week
            ? await _db.GuildRaids.AsNoTracking().Where(r => r.Week == weekKey).Select(r => new { r.GuildId, Damage = r.HpMax - r.HpLeft, r.SlainUtc }).ToListAsync(ct)
            : null;
        var ranked = week
            ? guilds.Select(g => new { g, raid = raids!.FirstOrDefault(r => r.GuildId == g.Id) }).Where(x => x.raid != null && x.raid.Damage > 0)
                .OrderByDescending(x => x.raid!.SlainUtc != null).ThenBy(x => x.raid!.SlainUtc ?? DateTime.MaxValue).ThenByDescending(x => x.raid!.Damage)
                .Select(x => (x.g.Id, x.g.Name, x.g.Tag, x.g.Xp, Value: x.raid!.Damage)).ToList()
            : guilds.OrderByDescending(g => g.Xp).Select(g => (g.Id, g.Name, g.Tag, g.Xp, Value: (long)Guilds.Level(g.Xp))).ToList();
        var rows = ranked.Take(BoardRows).Select((g, i) => new LeaderRowDto(i + 1, g.Id, g.Name, "", "", Guilds.Level(g.Xp), Banner.None, g.Value, g.Tag)).ToArray();
        LeaderRowDto? mine = null;
        if (account.GuildId is Guid mineId)
        {
            int at = ranked.FindIndex(g => g.Id == mineId);
            if (at >= 0) mine = new LeaderRowDto(at + 1, mineId, ranked[at].Name, "", "", Guilds.Level(ranked[at].Xp), Banner.None, ranked[at].Value, ranked[at].Tag);
        }
        return new LeaderboardDto("guilds", week ? "week" : "all", rows, mine, week ? "This week's guild raid: felled first, then the most damage." : "");
    }
}

// Inspect a hero (owner, 28 Sep 2026: "Inspect a hero"): anyone may see another hero's class, title, Banner, guild, how far
// they have come and the gear they wear (with its upgrade levels and etchings).
public sealed partial class GameService
{
    public async Task<InspectDto> InspectAsync(Account viewer, Guid id, CancellationToken ct)
    {
        Account hero = await _db.Accounts.AsNoTracking().Include(a => a.Items).FirstOrDefaultAsync(a => a.Id == id, ct)
                       ?? throw new GameException("no_hero", "That hero is gone.");
        Guild? guild = hero.GuildId is Guid g ? await _db.Guilds.AsNoTracking().FirstOrDefaultAsync(x => x.Id == g, ct) : null;
        ItemDto[] worn = hero.Items.Where(i => i.Equipped && !i.Destroyed).OrderBy(i => i.Slot).Select(ToDto).ToArray();
        string skin = WornPieces(hero).FirstOrDefault(p => p.Kind == WardrobeKind.Skin)?.Look ?? "";
        return new InspectDto(hero.Id, NameOf(hero), TitleOf(hero) ?? "", hero.Class, hero.Figure, Content.LevelFor(hero.Xp), hero.Banner,
            guild?.Name ?? "", guild?.Tag ?? "", hero.HighestStageCleared, hero.PitRating, hero.PitWins, skin, worn, hero.BannedUtc != null);
    }
}
