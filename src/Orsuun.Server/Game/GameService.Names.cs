using Microsoft.EntityFrameworkCore;
using Orsuun.Rules;
using Orsuun.Server.Data;

namespace Orsuun.Server.Game;

// Name reports and their review queue (owner, 27 Sep 2026: "Name reports & review queue"): players report a hero's or a
// guild's name from chat; the moderation page lists the reported names, most reported first, to keep, rename or ban.
public sealed partial class GameService
{
    /// <summary>Name reports one player may send in a day.</summary>
    public const int NameReportsPerDay = 20;

    public async Task<MessageDto> ReportNameAsync(Account account, NameReportRequest request, CancellationToken ct)
    {
        Account target = await _db.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == request.AccountId, ct)
                         ?? throw new GameException("no_hero", "That hero is gone.");
        string kind = request.Kind == "guild" ? "guild" : "hero";
        Guid targetId;
        string name;
        if (kind == "hero")
        {
            if (target.Id == account.Id) throw new GameException("bad_report", "You cannot report your own name.");
            targetId = target.Id;
            name = NameOf(target);
        }
        else
        {
            if (target.GuildId is not Guid guildId) throw new GameException("no_guild", "That hero is not in a guild.");
            if (guildId == account.GuildId) throw new GameException("bad_report", "Take it up with your guild's leader.");
            Guild guild = await _db.Guilds.AsNoTracking().FirstOrDefaultAsync(g => g.Id == guildId, ct) ?? throw new GameException("no_guild", "That guild is gone.");
            targetId = guild.Id;
            name = $"{guild.Name} [{guild.Tag}]";
        }
        DateTime day = DateTime.UtcNow.AddDays(-1);
        if (await _db.NameReports.CountAsync(r => r.ReporterId == account.Id && r.Utc > day, ct) >= NameReportsPerDay)
            throw new GameException("report_wait", "You have sent many reports today. Thank you: try again tomorrow.");
        string shown = name.Length > 40 ? name[..40] : name;
        if (!await _db.NameReports.AnyAsync(r => r.Kind == kind && r.TargetId == targetId && r.Name == shown && r.ReporterId == account.Id, ct))
        {
            _db.NameReports.Add(new NameReport { Kind = kind, TargetId = targetId, Name = shown, ReporterId = account.Id, Utc = DateTime.UtcNow });
            await AlertModeratorsAsync("name", kind == "guild" ? "A guild's name was reported" : "A hero's name was reported", shown + ".", ct);
            await SaveAsync(ct);
        }
        return new MessageDto("Reported. A moderator will look at the name.");
    }

    /// <summary>The names waiting for a moderator: unreviewed reports grouped by name, most reported first.</summary>
    public async Task<AdminNameDto[]> AdminNamesAsync(CancellationToken ct)
    {
        var open = await _db.NameReports.AsNoTracking().Where(r => !r.Reviewed).ToListAsync(ct);
        var heroIds = open.Where(r => r.Kind == "hero").Select(r => r.TargetId).Distinct().ToList();
        var guildIds = open.Where(r => r.Kind == "guild").Select(r => r.TargetId).Distinct().ToList();
        var heroes = await _db.Accounts.AsNoTracking().Where(a => heroIds.Contains(a.Id)).Select(a => new { a.Id, a.Name, a.BannedUtc }).ToListAsync(ct);
        var guilds = await _db.Guilds.AsNoTracking().Where(g => guildIds.Contains(g.Id)).Select(g => new { g.Id, g.Name, g.Tag }).ToListAsync(ct);
        var list = new List<AdminNameDto>();
        foreach (var group in open.GroupBy(r => (r.Kind, r.TargetId)))
        {
            string name, tag = "";
            bool banned = false;
            if (group.Key.Kind == "hero")
            {
                var h = heroes.FirstOrDefault(x => x.Id == group.Key.TargetId);
                if (h == null) continue;   // deleted since
                name = ShownName(h.Id, h.Name);
                banned = h.BannedUtc != null;
            }
            else
            {
                var g = guilds.FirstOrDefault(x => x.Id == group.Key.TargetId);
                if (g == null) continue;
                name = g.Name;
                tag = g.Tag;
            }
            // Reports on an older name (renamed since) do not count against the new one.
            int reports = group.Count(r => group.Key.Kind == "hero" ? r.Name == name : r.Name == $"{name} [{tag}]");
            if (reports == 0) continue;
            list.Add(new AdminNameDto(group.Key.Kind, group.Key.TargetId, name, tag, reports, group.Min(r => r.Utc), group.Max(r => r.Utc), banned));
        }
        return list.OrderByDescending(n => n.Reports).ThenByDescending(n => n.LastUtc).Take(100).ToArray();
    }

    /// <summary>Marks a name's reports reviewed (kept as it is, renamed, or banned).</summary>
    private Task ReviewNameAsync(string kind, Guid targetId, CancellationToken ct) =>
        _db.NameReports.Where(r => r.Kind == kind && r.TargetId == targetId && !r.Reviewed).ExecuteUpdateAsync(s => s.SetProperty(r => r.Reviewed, true), ct);

    public async Task AdminKeepNameAsync(string admin, AdminNameKeepRequest request, CancellationToken ct)
    {
        string kind = request.Kind == "guild" ? "guild" : "hero";
        await ReviewNameAsync(kind, request.TargetId, ct);
        Log(admin, "name-keep", kind + ":" + request.TargetId, "");
        await SaveAsync(ct);
    }

    /// <summary>A moderator renames a hero: the name must pass the usual rules and be free; the hero gets a letter.</summary>
    public async Task AdminRenameHeroAsync(string admin, Guid id, AdminHeroRenameRequest request, CancellationToken ct)
    {
        Account hero = await _db.Accounts.FirstOrDefaultAsync(a => a.Id == id, ct) ?? throw new GameException("no_hero", "That hero is gone.");
        string name = (request.Name ?? "").Trim();
        if (Characters.NameProblem(name) is string problem) throw new GameException("bad_name", problem);
        string key = Characters.NameKey(name);
        if (await _db.Accounts.AnyAsync(a => a.Id != id && a.NameKey == key, ct)) throw new GameException("name_taken", "That name is taken.");
        string old = NameOf(hero);
        hero.Name = name;
        hero.NameKey = key;
        Log(admin, "hero-rename", id.ToString(), $"{old} -> {name}");
        SendLetter(hero.Id, "system", "The moderators", "Your name was changed",
            $"Your hero's name, {old}, was reported and did not fit the game's rules, so a moderator changed it to {name}.");
        await SaveAsync(ct);
        await ReviewNameAsync("hero", id, ct);
    }
}
