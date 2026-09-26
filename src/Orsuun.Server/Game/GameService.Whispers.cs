using Microsoft.EntityFrameworkCore;
using Orsuun.Rules;
using Orsuun.Server.Data;

namespace Orsuun.Server.Game;

/// <summary>
/// Private messages (owner, 26 Sep 2026: "go for private messages", "they need to stay after days and days", "make it a
/// screen"; Rules.Whispers): hero to hero, kept with no expiry. Sending follows the chat's rules (text filter, flood
/// limit, mutes); neither side may have blocked the other, and a hero cannot write to its own login's heroes. Opening a
/// conversation marks what it received as read; the HUD counts the unread. A reported message is copied into the chat
/// moderation queue (channel "w:" + the recipient's id), so moderators mute or ban from the same page.
/// </summary>
public sealed partial class GameService
{
    public async Task<WhispersDto> WhispersAsync(Account account, string message, CancellationToken ct)
    {
        Guid me = account.Id;
        var groups = await _db.PrivateMessages.AsNoTracking()
            .Where(m => m.FromId == me || m.ToId == me)
            .GroupBy(m => m.FromId == me ? m.ToId : m.FromId)
            .Select(g => new { Other = g.Key, LastId = g.Max(m => m.Id), Unread = g.Count(m => m.ToId == me && !m.Read) })
            .OrderByDescending(x => x.LastId)
            .Take(Whispers.MaxConversations)
            .ToListAsync(ct);
        var lastIds = groups.Select(g => g.LastId).ToList();
        var lasts = await _db.PrivateMessages.AsNoTracking().Where(m => lastIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, ct);
        var others = groups.Select(g => g.Other).ToList();
        var heroes = await _db.Accounts.AsNoTracking().Where(a => others.Contains(a.Id))
            .Select(a => new { a.Id, a.Name, a.Class, a.Xp, a.LastHeartbeatUtc }).ToDictionaryAsync(a => a.Id, ct);
        DateTime now = DateTime.UtcNow;
        var list = new List<WhisperConversationDto>();
        foreach (var g in groups)
        {
            if (!heroes.TryGetValue(g.Other, out var hero) || !lasts.TryGetValue(g.LastId, out PrivateMessage? last)) continue;
            list.Add(new WhisperConversationDto(hero.Id, ShownName(hero.Id, hero.Name), hero.Class.ToString(), Content.LevelFor(hero.Xp),
                (int)Math.Max(0, (now - hero.LastHeartbeatUtc).TotalMinutes), last.Text, last.Utc, last.FromId == me, g.Unread));
        }
        return new WhispersDto(list.ToArray(), groups.Sum(g => g.Unread), message);
    }

    /// <summary>A conversation by the other hero's id, or by name (a conversation begun from the name box).</summary>
    public async Task<WhisperThreadDto> WhisperThreadByAsync(Account account, Guid otherId, string? name, long after, long before, CancellationToken ct)
    {
        if (otherId == Guid.Empty) otherId = (await FindHeroAsync(Guid.Empty, name, ct)).Id;
        if (otherId == account.Id) throw new GameException("self", "That is you.");
        return await WhisperThreadAsync(account, otherId, after, before, "", ct);
    }

    /// <summary>
    /// A conversation: the newest page, the lines after <paramref name="after"/> (polling), or the page before
    /// <paramref name="before"/> (scrolling back). What the other hero sent is marked read.
    /// </summary>
    public async Task<WhisperThreadDto> WhisperThreadAsync(Account account, Guid otherId, long after, long before, string message, CancellationToken ct)
    {
        Guid me = account.Id;
        var other = await _db.AccountsAsNoTrackingById(otherId)
            .Select(a => new { a.Id, a.Name, a.Class, a.Xp, a.LastHeartbeatUtc }).FirstOrDefaultAsync(ct)
            ?? throw new GameException("no_hero", "That hero is gone.");
        IQueryable<PrivateMessage> between = _db.PrivateMessages.AsNoTracking()
            .Where(m => (m.FromId == me && m.ToId == otherId) || (m.FromId == otherId && m.ToId == me));
        List<PrivateMessage> rows;
        if (after > 0) rows = await between.Where(m => m.Id > after).OrderBy(m => m.Id).Take(Whispers.PageSize).ToListAsync(ct);
        else
        {
            IQueryable<PrivateMessage> page = before > 0 ? between.Where(m => m.Id < before) : between;
            rows = await page.OrderByDescending(m => m.Id).Take(Whispers.PageSize).ToListAsync(ct);
            rows.Reverse();
        }
        bool hasOlder = after <= 0 && rows.Count > 0 && await between.AnyAsync(m => m.Id < rows[0].Id, ct);
        await _db.PrivateMessages.Where(m => m.FromId == otherId && m.ToId == me && !m.Read).ExecuteUpdateAsync(s => s.SetProperty(m => m.Read, true), ct);
        long latest = after > 0 && rows.Count == 0 ? after : rows.Count > 0 ? rows.Max(m => m.Id) : 0;
        if (after <= 0 && before > 0)
            latest = await between.OrderByDescending(m => m.Id).Select(m => m.Id).FirstOrDefaultAsync(ct);
        return new WhisperThreadDto(other.Id, ShownName(other.Id, other.Name), other.Class.ToString(), Content.LevelFor(other.Xp),
            (int)Math.Max(0, (DateTime.UtcNow - other.LastHeartbeatUtc).TotalMinutes),
            rows.Select(m => new WhisperLineDto(m.Id, m.FromId == me, m.Text, m.Utc)).ToArray(), latest, hasOlder,
            Blocks(account.Blocked ?? "", otherId), message);
    }

    public async Task<WhisperThreadDto> SendWhisperAsync(Account account, WhisperSendRequest request, CancellationToken ct)
    {
        HeroRef other = await FindHeroAsync(request.AccountId, request.Name, ct);
        if (other.Id == account.Id) throw new GameException("self", "That is you.");
        if (other.LoginId == account.LoginId) throw new GameException("own_hero", "That is one of your own heroes.");
        if (Blocks(account.Blocked ?? "", other.Id)) throw new GameException("blocked", "You blocked them. Unblock them in CHAT first.");
        if (Blocks(other.Blocked, account.Id)) throw new GameException("not_taking", "They are not taking your messages.");
        string text = Chat.Clean(request.Text);
        if (Chat.TextProblem(text) is string problem) throw new GameException("bad_text", problem);
        DateTime now = DateTime.UtcNow;
        if (account.MutedUntilUtc is DateTime muted && muted > now)
        {
            int minutes = (int)Math.Ceiling((muted - now).TotalMinutes);
            throw new GameException("muted", minutes >= 120 ? $"A moderator muted you for {minutes / 60} more hours." : $"A moderator muted you for {minutes} more minutes.");
        }
        if (account.LastChatUtc is DateTime last && (now - last).TotalSeconds < Chat.CooldownSeconds)
            throw new GameException("chat_cooldown", "Slow down a little.");
        account.LastChatUtc = now;
        _db.PrivateMessages.Add(new PrivateMessage { FromId = account.Id, ToId = other.Id, Text = text, Utc = now });
        await SaveAsync(ct);

        // Kept with no expiry; only a very long conversation loses its oldest lines, now and then.
        if (Random.Shared.Next(20) == 0)
        {
            Guid me = account.Id, them = other.Id;
            IQueryable<PrivateMessage> between = _db.PrivateMessages
                .Where(m => (m.FromId == me && m.ToId == them) || (m.FromId == them && m.ToId == me));
            long cut = await between.OrderByDescending(m => m.Id).Skip(Whispers.KeepPerConversation).Select(m => m.Id).FirstOrDefaultAsync(ct);
            if (cut > 0) await between.Where(m => m.Id <= cut).ExecuteDeleteAsync(ct);
        }
        return await WhisperThreadAsync(account, other.Id, request.After, 0, "", ct);
    }

    public async Task<WhisperThreadDto> ReportWhisperAsync(Account account, WhisperReportRequest request, CancellationToken ct)
    {
        PrivateMessage line = await _db.PrivateMessages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == request.MessageId && m.ToId == account.Id, ct)
            ?? throw new GameException("no_line", "You can report only a message sent to you.");
        string channel = Whispers.ReportChannel + account.Id.ToString("N");
        bool known = await _db.ChatMessages.AnyAsync(m => m.Channel == channel && m.AccountId == line.FromId && m.Utc == line.Utc, ct);
        if (!known)
        {
            var sender = await _db.AccountsAsNoTrackingById(line.FromId).Select(a => new { a.Name, a.Banner }).FirstOrDefaultAsync(ct);
            // A copy in the moderators' queue: never shown in chat (no chat reads a "w:" channel).
            var copy = new ChatMessage
            {
                Channel = channel, AccountId = line.FromId, Name = sender != null ? ShownName(line.FromId, sender.Name) : "", Banner = sender?.Banner ?? default,
                Text = line.Text, Utc = line.Utc, Reports = 1, Hidden = true,
            };
            _db.ChatMessages.Add(copy);
            await SaveAsync(ct);
            _db.ChatReports.Add(new ChatReport { MessageId = copy.Id, ReporterId = account.Id, Utc = DateTime.UtcNow });
            await SaveAsync(ct);
        }
        return await WhisperThreadAsync(account, line.FromId, 0, 0, "Reported. A moderator will look at it.", ct);
    }
}

internal static class WhisperQueries
{
    public static IQueryable<Account> AccountsAsNoTrackingById(this GameDb db, Guid id) => db.Accounts.AsNoTracking().Where(a => a.Id == id);
}
