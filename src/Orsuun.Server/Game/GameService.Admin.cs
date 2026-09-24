using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Orsuun.Rules;
using Orsuun.Server.Data;

namespace Orsuun.Server.Game;

/// <summary>
/// Moderation (owner, 24 Sep 2026: "go" on the moderation tool before testers join). Moderators are game accounts
/// whose email is listed in the server's Admin:Emails setting; they sign in to the admin page (/admin) with that
/// email and password and get a 12-hour session. They work the report queue, hide lines, mute and ban players, and
/// rename or disband guilds; every action is logged.
/// </summary>
public sealed partial class GameService
{
    public static readonly TimeSpan AdminSessionLength = TimeSpan.FromHours(12);
    private static readonly ConcurrentDictionary<string, (string Email, DateTime Expires)> AdminSessions = new();

    private static string Mask(string? email)
    {
        if (string.IsNullOrEmpty(email)) return "";
        int at = email.IndexOf('@');
        return at <= 1 ? "***" + email.Substring(Math.Max(0, at)) : email[0] + "***" + email.Substring(at);
    }

    /// <summary>The moderator behind an admin session token, or null.</summary>
    public static string? AdminFor(string? token)
    {
        if (string.IsNullOrEmpty(token) || !AdminSessions.TryGetValue(token, out var session)) return null;
        if (session.Expires < DateTime.UtcNow)
        {
            AdminSessions.TryRemove(token, out _);
            return null;
        }
        return session.Email;
    }

    public async Task<AdminLoginDto> AdminLoginAsync(AdminLoginRequest request, IReadOnlyCollection<string> admins, string? clientIp, CancellationToken ct)
    {
        string email = AccountRules.NormaliseEmail(request.Email);
        string ipKey = "admin-ip:" + (clientIp ?? "?");
        if (RecentFailures("admin:" + email) >= MaxFailedLogins || RecentFailures(ipKey) >= MaxFailedLogins)
            throw new GameException("login_wait", "Too many tries. Wait a few minutes and try again.");
        Account? account = admins.Contains(email) ? await _db.Accounts.AsNoTracking().SingleOrDefaultAsync(a => a.Email == email, ct) : null;
        if (account?.PasswordHash == null || !Passwords.Verify(request.Password ?? "", account.PasswordHash))
        {
            RecordFailure("admin:" + email);
            RecordFailure(ipKey);
            throw new GameException("admin_unauthorized", "Wrong email or password, or not a moderator.");
        }
        string token = NewToken();
        AdminSessions[token] = (email, DateTime.UtcNow + AdminSessionLength);
        _db.AdminActions.Add(new AdminAction { Utc = DateTime.UtcNow, Admin = email, Action = "login", Target = "", Detail = clientIp ?? "" });
        await _db.SaveChangesAsync(ct);
        return new AdminLoginDto(token, email, (int)AdminSessionLength.TotalMinutes);
    }

    private void Log(string admin, string action, string target, string detail) =>
        _db.AdminActions.Add(new AdminAction
        {
            Utc = DateTime.UtcNow, Admin = admin, Action = action, Target = target,
            Detail = detail.Length <= 300 ? detail : detail.Substring(0, 300),
        });

    public async Task<AdminOverviewDto> AdminOverviewAsync(CancellationToken ct)
    {
        DateTime day = DateTime.UtcNow.AddDays(-1);
        DateTime now = DateTime.UtcNow;
        return new AdminOverviewDto(
            await _db.Accounts.CountAsync(ct),
            await _db.Accounts.CountAsync(a => a.LastHeartbeatUtc > day, ct),
            await _db.Accounts.CountAsync(a => a.CreatedUtc > day, ct),
            await _db.Accounts.CountAsync(a => a.Email != null, ct),
            await _db.ChatMessages.CountAsync(m => m.Utc > day && m.AccountId != Guid.Empty, ct),
            await _db.ChatMessages.CountAsync(m => m.Reports > 0 && !m.Reviewed, ct),
            await _db.Guilds.CountAsync(ct),
            await _db.MarketListings.CountAsync(l => l.Status == ListingStatus.Active, ct),
            await _db.Accounts.CountAsync(a => a.MutedUntilUtc > now, ct),
            await _db.Accounts.CountAsync(a => a.BannedUtc != null, ct));
    }

    private static AdminLineDto LineDto(ChatMessage m) =>
        new(m.Id, m.Channel == Chat.World ? "world" : "guild", m.AccountId, m.AccountId == Guid.Empty ? "(system)" : m.Name, m.Text, m.Utc, m.Reports, m.Hidden, m.Reviewed);

    /// <summary>Reported lines no moderator has looked at yet, most reported first.</summary>
    public async Task<AdminLineDto[]> AdminReportsAsync(CancellationToken ct) =>
        (await _db.ChatMessages.AsNoTracking().Where(m => m.Reports > 0 && !m.Reviewed)
            .OrderByDescending(m => m.Reports).ThenByDescending(m => m.Id).Take(100).ToListAsync(ct)).Select(LineDto).ToArray();

    /// <summary>The newest lines of world chat (or all channels of one player), hidden ones included.</summary>
    public async Task<AdminLineDto[]> AdminChatAsync(Guid? accountId, string? q, CancellationToken ct)
    {
        IQueryable<ChatMessage> query = _db.ChatMessages.AsNoTracking();
        query = accountId is Guid id ? query.Where(m => m.AccountId == id) : query.Where(m => m.Channel == Chat.World);
        if (!string.IsNullOrWhiteSpace(q))
        {
            string text = q.Trim().ToLowerInvariant();
            query = query.Where(m => m.Text.ToLower().Contains(text) || m.Name.ToLower().Contains(text));
        }
        return (await query.OrderByDescending(m => m.Id).Take(150).ToListAsync(ct)).Select(LineDto).ToArray();
    }

    public async Task AdminLineAsync(string admin, long id, AdminLineRequest request, CancellationToken ct)
    {
        ChatMessage line = await _db.ChatMessages.FirstOrDefaultAsync(m => m.Id == id, ct) ?? throw new GameException("no_line", "That line is gone.");
        switch (request.Action)
        {
            case "hide": line.Hidden = true; line.Reviewed = true; break;
            case "show": line.Hidden = false; line.Reviewed = true; break;
            case "dismiss": line.Reviewed = true; break;
            default: throw new GameException("bad_action", "hide, show or dismiss.");
        }
        Log(admin, "line-" + request.Action, line.AccountId.ToString(), $"#{line.Id} {line.Name}: {line.Text}");
        await _db.SaveChangesAsync(ct);
    }

    private static AdminPlayerDto PlayerDto(Account a, string? guildTag, int reported) =>
        new(a.Id, Banners.GeneratedName(a.Id), Mask(a.Email), a.Banner, guildTag ?? "", Content.LevelFor(a.Xp), a.CreatedUtc, a.LastHeartbeatUtc,
            a.MutedUntilUtc > DateTime.UtcNow ? a.MutedUntilUtc : null, a.BannedUtc, a.BanReason, reported);

    /// <summary>Players by generated name, email, account id or guild tag (playtest scale: names are computed).</summary>
    public async Task<AdminPlayerDto[]> AdminPlayersAsync(string? q, CancellationToken ct)
    {
        var rows = await _db.Accounts.AsNoTracking().IgnoreAutoIncludes()
            .Select(a => new { a.Id, a.Email, a.GuildId, a.LastHeartbeatUtc, a.BannedUtc, a.MutedUntilUtc }).ToListAsync(ct);
        var tags = await _db.Guilds.AsNoTracking().Select(g => new { g.Id, g.Tag }).ToDictionaryAsync(g => g.Id, g => g.Tag, ct);
        string text = (q ?? "").Trim().ToLowerInvariant();
        var hits = rows.Where(r => text.Length == 0
                || Banners.GeneratedName(r.Id).ToLowerInvariant().Contains(text)
                || r.Id.ToString().StartsWith(text)
                || (r.Email ?? "").Contains(text)
                || (r.GuildId is Guid g && tags.TryGetValue(g, out string? tag) && tag.ToLowerInvariant() == text))
            .OrderByDescending(r => r.BannedUtc != null).ThenByDescending(r => r.MutedUntilUtc > DateTime.UtcNow).ThenByDescending(r => r.LastHeartbeatUtc)
            .Take(50).Select(r => r.Id).ToList();
        var accounts = await _db.Accounts.AsNoTracking().IgnoreAutoIncludes().Where(a => hits.Contains(a.Id)).ToListAsync(ct);
        var reported = await _db.ChatMessages.AsNoTracking().Where(m => hits.Contains(m.AccountId) && m.Reports > 0)
            .GroupBy(m => m.AccountId).Select(x => new { x.Key, Count = x.Count() }).ToListAsync(ct);
        return hits.Select(id => accounts.First(a => a.Id == id))
            .Select(a => PlayerDto(a, a.GuildId is Guid g && tags.TryGetValue(g, out string? tag) ? tag : null,
                reported.Where(r => r.Key == a.Id).Select(r => r.Count).FirstOrDefault()))
            .ToArray();
    }

    private async Task<Account> AccountForAdminAsync(Guid id, CancellationToken ct) =>
        await _db.Accounts.SingleOrDefaultAsync(a => a.Id == id, ct) ?? throw new GameException("no_player", "No such player.");

    public async Task AdminMuteAsync(string admin, Guid id, AdminMuteRequest request, CancellationToken ct)
    {
        Account account = await AccountForAdminAsync(id, ct);
        int minutes = Math.Clamp(request.Minutes, 0, 60 * 24 * 365);
        account.MutedUntilUtc = minutes == 0 ? null : DateTime.UtcNow.AddMinutes(minutes);
        Log(admin, minutes == 0 ? "unmute" : "mute", id.ToString(), minutes == 0 ? Banners.GeneratedName(id) : $"{Banners.GeneratedName(id)} for {minutes} min: {request.Reason}");
        await SaveAsync(ct);
    }

    /// <summary>Bans a player: no sign-in, their Exchange listings come down, they leave their guild, lines hidden on request.</summary>
    public async Task AdminBanAsync(string admin, Guid id, AdminBanRequest request, CancellationToken ct)
    {
        Account account = await AccountForAdminAsync(id, ct);
        if (account.BannedUtc != null) throw new GameException("banned", "Already banned.");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        account.BannedUtc = DateTime.UtcNow;
        string reason = (request.Reason ?? "").Trim();
        account.BanReason = reason.Length <= 200 ? reason : reason.Substring(0, 200);
        var listed = await _db.MarketListings.Where(l => l.SellerId == id && l.Status == ListingStatus.Active).ToListAsync(ct);
        foreach (MarketListing l in listed)
        {
            l.Status = ListingStatus.Cancelled;
            l.ClosedUtc = DateTime.UtcNow;
            Item? item = account.Items.SingleOrDefault(i => i.Id == l.ItemId);
            if (item != null) item.Listed = false;
        }
        if (account.GuildId != null)
        {
            _guild = null;
            await LeaveCoreAsync(account, ct);
        }
        if (request.HideLines) await _db.ChatMessages.Where(m => m.AccountId == id).ExecuteUpdateAsync(s => s.SetProperty(m => m.Hidden, true).SetProperty(m => m.Reviewed, true), ct);
        await _db.Devices.Where(d => d.AccountId == id).ExecuteUpdateAsync(s => s.SetProperty(d => d.SessionToken, (string?)null), ct);
        Log(admin, "ban", id.ToString(), $"{Banners.GeneratedName(id)}: {account.BanReason} (lines hidden: {request.HideLines}, listings closed: {listed.Count})");
        await SaveAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task AdminUnbanAsync(string admin, Guid id, CancellationToken ct)
    {
        Account account = await AccountForAdminAsync(id, ct);
        account.BannedUtc = null;
        account.BanReason = null;
        Log(admin, "unban", id.ToString(), Banners.GeneratedName(id));
        await SaveAsync(ct);
    }

    public async Task<AdminGuildDto[]> AdminGuildsAsync(string? q, CancellationToken ct)
    {
        string text = (q ?? "").Trim().ToLowerInvariant();
        IQueryable<Guild> query = _db.Guilds.AsNoTracking();
        if (text.Length > 0) query = query.Where(g => g.NameKey.Contains(text) || g.Tag.ToLower() == text);
        var guilds = await query.OrderByDescending(g => g.CreatedUtc).Take(100).ToListAsync(ct);
        var ids = guilds.Select(g => (Guid?)g.Id).ToList();
        var members = await _db.Accounts.AsNoTracking().Where(a => ids.Contains(a.GuildId))
            .Select(a => new { a.Id, a.GuildId, a.GuildRank }).ToListAsync(ct);
        return guilds.Select(g =>
        {
            var mine = members.Where(m => m.GuildId == g.Id).ToList();
            var leader = mine.FirstOrDefault(m => m.GuildRank == GuildRank.Leader);
            return new AdminGuildDto(g.Id, g.Name, g.Tag, Guilds.Level(g.Xp), mine.Count, leader == null ? "" : Banners.GeneratedName(leader.Id), g.CreatedUtc, g.Open);
        }).ToArray();
    }

    public async Task AdminRenameGuildAsync(string admin, Guid id, AdminRenameRequest request, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        Guild guild = await LockGuildAsync(id, ct);
        if (Guilds.NameProblem(request.Name) is string nameProblem) throw new GameException("bad_name", nameProblem);
        string tag = Guilds.NormaliseTag(request.Tag ?? "");
        if (Guilds.TagProblem(tag) is string tagProblem) throw new GameException("bad_tag", tagProblem);
        string name = Guilds.NormaliseName(request.Name);
        string key = name.ToLowerInvariant();
        if (await _db.Guilds.AnyAsync(g => g.Id != id && g.NameKey == key, ct)) throw new GameException("name_taken", "That name is taken.");
        if (await _db.Guilds.AnyAsync(g => g.Id != id && g.Tag == tag, ct)) throw new GameException("tag_taken", "That tag is taken.");
        Log(admin, "guild-rename", id.ToString(), $"[{guild.Tag}] {guild.Name} -> [{tag}] {name}");
        guild.Name = name;
        guild.NameKey = key;
        guild.Tag = tag;
        GuildEvent(guild, $"A moderator renamed the guild to {name} [{tag}].");
        await SaveAsync(ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>Disbands a guild: every member leaves, its requests, chat and fortress flags go.</summary>
    public async Task AdminDisbandGuildAsync(string admin, Guid id, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        Guild guild = await LockGuildAsync(id, ct);
        await _db.Accounts.Where(a => a.GuildId == id).ExecuteUpdateAsync(s => s
            .SetProperty(a => a.GuildId, (Guid?)null).SetProperty(a => a.GuildRank, GuildRank.Member)
            .SetProperty(a => a.GuildJoinedUtc, (DateTime?)null).SetProperty(a => a.GuildDonated, 0L), ct);
        await _db.GuildRequests.Where(r => r.GuildId == id).ExecuteDeleteAsync(ct);
        string channel = Chat.GuildChannel(id);
        await _db.ChatMessages.Where(m => m.Channel == channel).ExecuteDeleteAsync(ct);
        await _db.Fortresses.Where(f => f.FlagGuildId == id).ExecuteUpdateAsync(s => s.SetProperty(f => f.FlagGuildId, (Guid?)null), ct);
        Log(admin, "guild-disband", id.ToString(), $"[{guild.Tag}] {guild.Name}");
        _db.Guilds.Remove(guild);
        await SaveAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<AdminActionDto[]> AdminLogAsync(CancellationToken ct) =>
        (await _db.AdminActions.AsNoTracking().OrderByDescending(a => a.Id).Take(150).ToListAsync(ct))
            .Select(a => new AdminActionDto(a.Utc, a.Admin, a.Action, a.Target, a.Detail)).ToArray();
}
