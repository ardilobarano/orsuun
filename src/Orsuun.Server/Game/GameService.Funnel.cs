using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Orsuun.Rules;
using Orsuun.Server.Data;

namespace Orsuun.Server.Game;

// Tester analytics and crash reports (owner, 27 Sep 2026: "Tester analytics & crash reports"): each hero's firsts are
// Milestones rows (one per hero and name), the phones add their guide steps, and the moderation page shows how far new
// heroes get (Funnel) and what errors the phones sent (Errors). Nothing here changes play.
public sealed partial class GameService
{
    /// <summary>The funnel's rows in the order a new hero meets them, with the moderation page's words.</summary>
    public static readonly (string Name, string Label)[] FunnelSteps =
    {
        ("hunted", "Hunted (first settlement)"),
        ("tutorial-1", "Saw the guide"),
        ("first-Korstones", "Broke a Korstone"),
        ("first-ForgeAttempts", "Forged once"),
        ("tutorial-done", "Finished the guide"),
        ("tutorial-skipped", "Skipped the guide"),
        ("first-Pushes", "Pushed once"),
        ("stage-1", "Cleared stage 1"),
        ("level-5", "Level 5"),
        ("stage-5", "Cleared stage 5"),
        ("first-BountiesClaimed", "Claimed a bounty"),
        ("level-10", "Level 10"),
        ("stage-10", "Cleared stage 10"),
        ("first-CommanderFights", "Fought a Commander"),
        ("guild", "Joined a guild"),
        ("stage-20", "Cleared stage 20"),
        ("level-20", "Level 20"),
        ("first-SiegeFights", "Fought in a siege"),
        ("first-DungeonClears", "Cleared a dungeon"),
        ("pit-win", "Won a duel in the Pits"),
        ("stage-40", "Cleared stage 40"),
        ("level-30", "Level 30"),
        ("stage-60", "Cleared stage 60"),
        ("purchase", "Bought Amber"),
        ("day-1", "Came back after a day"),
        ("day-7", "Came back after a week"),
    };

    private static readonly int[] MarkedLevels = { 5, 10, 20, 30, 40, 60 };
    private static readonly int[] MarkedStages = { 1, 5, 10, 20, 40, 60, 80, 100, 120 };
    private static readonly Regex PhoneMilestone = new("^tutorial-([0-9]{1,2}|done|skipped)$", RegexOptions.CultureInvariant);

    /// <summary>Marks already written, so a heartbeat does not ask the database again (per server process).</summary>
    private static readonly ConcurrentDictionary<string, byte> KnownMarks = new();

    private readonly List<(Guid Account, string Name)> _marks = new();

    /// <summary>Queues a first for the hero; written after the request's save (ON CONFLICT: once per hero and name).</summary>
    private void Mark(Account account, string name)
    {
        if (!KnownMarks.ContainsKey(account.Id.ToString("N") + name)) _marks.Add((account.Id, name));
    }

    private void MarkLevels(Account account, int before, int after)
    {
        foreach (int level in MarkedLevels)
            if (before < level && after >= level) Mark(account, "level-" + level);
    }

    private void MarkStage(Account account, int stage)
    {
        if (Array.IndexOf(MarkedStages, stage) >= 0) Mark(account, "stage-" + stage);
    }

    /// <summary>A heartbeat: hunting at all, and coming back a day and a week after the hero was made.</summary>
    private void MarkVisit(Account account, long countedSeconds, DateTime now)
    {
        if (countedSeconds > 0) Mark(account, "hunted");
        if (now - account.CreatedUtc >= TimeSpan.FromDays(1)) Mark(account, "day-1");
        if (now - account.CreatedUtc >= TimeSpan.FromDays(7)) Mark(account, "day-7");
    }

    private async Task FlushMarksAsync(CancellationToken ct)
    {
        if (_marks.Count == 0) return;
        DateTime now = DateTime.UtcNow;
        foreach ((Guid id, string name) in _marks.Distinct().ToList())
        {
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $@"INSERT INTO ""Milestones"" (""AccountId"", ""Name"", ""Utc"") VALUES ({id}, {name}, {now}) ON CONFLICT (""AccountId"", ""Name"") DO NOTHING", ct);
            KnownMarks.TryAdd(id.ToString("N") + name, 0);
        }
        _marks.Clear();
    }

    /// <summary>A first only the phone sees (a guide step, the guide done or skipped).</summary>
    public async Task<object> PhoneMilestoneAsync(Account account, MilestoneRequest request, CancellationToken ct)
    {
        string name = (request.Name ?? "").Trim();
        if (!PhoneMilestone.IsMatch(name)) throw new GameException("bad_milestone", "Unknown milestone.");
        Mark(account, name);
        await FlushMarksAsync(ct);
        return new { ok = true };
    }

    /// <summary>
    /// How far the heroes made in the last <paramref name="days"/> days got: the way in (logins made, sworn, with a hero,
    /// hunted) and each first with how many reached it and the median minutes it took from the hero's making.
    /// </summary>
    public async Task<AdminFunnelDto> AdminFunnelAsync(int days, CancellationToken ct)
    {
        days = Math.Clamp(days, 1, 90);
        DateTime since = DateTime.UtcNow.AddDays(-days);
        var heroes = await _db.Accounts.AsNoTracking().Where(a => a.CreatedUtc >= since)
            .Select(a => new { a.Id, a.LoginId, a.CreatedUtc }).ToListAsync(ct);
        var ids = heroes.Select(h => h.Id).ToList();
        var made = heroes.ToDictionary(h => h.Id, h => h.CreatedUtc);
        var marks = await _db.Milestones.AsNoTracking().Where(m => ids.Contains(m.AccountId)).ToListAsync(ct);
        var steps = new List<AdminFunnelStepDto>();
        foreach ((string name, string label) in FunnelSteps)
        {
            var times = marks.Where(m => m.Name == name).Select(m => (m.Utc - made[m.AccountId]).TotalMinutes).OrderBy(x => x).ToList();
            steps.Add(new AdminFunnelStepDto(name, label, times.Count, times.Count == 0 ? 0 : Math.Round(times[times.Count / 2], 1)));
        }
        // The guide's steps, one row each, after the list.
        for (int step = 1; step <= 12; step++)
        {
            string name = "tutorial-" + step;
            int count = marks.Count(m => m.Name == name);
            if (count > 0 && step > 1) steps.Add(new AdminFunnelStepDto(name, "Guide step " + step, count, 0));
        }

        var logins = await _db.Logins.AsNoTracking().Where(l => l.CreatedUtc >= since).Select(l => new { l.Id, l.SwornUtc }).ToListAsync(ct);
        var loginIds = logins.Select(l => l.Id).ToList();
        var withHero = await _db.Accounts.AsNoTracking().Where(a => loginIds.Contains(a.LoginId)).Select(a => new { a.LoginId, a.Id }).ToListAsync(ct);
        var huntedHeroes = await _db.Milestones.AsNoTracking().Where(m => m.Name == "hunted").Select(m => m.AccountId).ToListAsync(ct);
        var hunted = new HashSet<Guid>(huntedHeroes);
        var wayIn = new[]
        {
            new AdminFunnelStepDto("logins", "Installs (new logins)", logins.Count, 0),
            new AdminFunnelStepDto("sworn", "Swore a Banner", logins.Count(l => l.SwornUtc != null), 0),
            new AdminFunnelStepDto("hero", "Made a hero", withHero.Select(h => h.LoginId).Distinct().Count(), 0),
            new AdminFunnelStepDto("played", "Hunted with it", withHero.Where(h => hunted.Contains(h.Id)).Select(h => h.LoginId).Distinct().Count(), 0),
        };
        return new AdminFunnelDto(days, heroes.Count, wayIn, steps.ToArray());
    }

    /// <summary>The phones' error reports of the last week, one row per message: how often, on how many heroes, where.</summary>
    public async Task<AdminErrorDto[]> AdminErrorsAsync(CancellationToken ct)
    {
        DateTime since = DateTime.UtcNow.AddDays(-7);
        var logs = await _db.ClientLogs.AsNoTracking().Where(l => l.Utc >= since).OrderByDescending(l => l.Utc).Take(2000).ToListAsync(ct);
        return logs.GroupBy(l => l.Message)
            .Select(g => new AdminErrorDto(g.Key, g.Count(), g.Select(l => l.AccountId).Distinct().Count(),
                string.Join(", ", g.Select(l => l.Platform).Distinct()), string.Join(", ", g.Select(l => l.Version).Distinct().Take(4)),
                g.Min(l => l.Utc), g.Max(l => l.Utc), g.First().Stack))
            .OrderByDescending(e => e.LastUtc).Take(60).ToArray();
    }
}
