using Microsoft.EntityFrameworkCore;
using Orsuun.Rules;
using Orsuun.Server.Data;

namespace Orsuun.Server.Game;

/// <summary>
/// Chat (owner, 24 Sep 2026: "all chat"): the world channel and the guild channel, system lines for world and guild
/// events, reports (three hide a line) and blocking. The client polls a channel with the last line id it holds.
/// </summary>
public sealed partial class GameService
{
    private static List<Guid> BlockedList(Account account) =>
        account.Blocked.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(s => Guid.TryParse(s, out Guid g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty).ToList();

    /// <summary>The stored channel for the client's "world" or "guild".</summary>
    private string ChannelFor(Account account, string? channel) => channel switch
    {
        null or "" or Chat.World => Chat.World,
        "guild" => account.GuildId is Guid g ? Chat.GuildChannel(g) : throw new GameException("no_guild", "You are not in a guild."),
        _ => throw new GameException("bad_channel", "Unknown chat channel."),
    };

    /// <summary>A system line (no speaker), saved with the caller's changes.</summary>
    private void SystemLine(string channel, string text) => _db.ChatMessages.Add(new ChatMessage
    {
        Channel = channel, AccountId = Guid.Empty, Text = text.Length <= Chat.MaxLength ? text : text.Substring(0, Chat.MaxLength), Utc = DateTime.UtcNow,
    });

    /// <summary>A guild event: the guild screen's latest line, and a system line in guild chat (the guild log).</summary>
    private void GuildEvent(Guild guild, string text)
    {
        guild.LastEvent = text.Length <= 160 ? text : text.Substring(0, 160);
        SystemLine(Chat.GuildChannel(guild.Id), text);
    }

    public async Task<ChatDto> ChatAsync(Account account, string? channel, long after, CancellationToken ct)
    {
        string stored = ChannelFor(account, channel);
        List<Guid> blocked = BlockedList(account);
        IQueryable<ChatMessage> query = _db.ChatMessages.AsNoTracking().Where(m => m.Channel == stored && !m.Hidden);
        if (after > 0) query = query.Where(m => m.Id > after);
        List<ChatMessage> rows = await query.OrderByDescending(m => m.Id).Take(Chat.PageSize).ToListAsync(ct);
        rows.Reverse();
        ChatLineDto[] lines = rows.Where(m => !blocked.Contains(m.AccountId))
            .Select(m => new ChatLineDto(m.Id, m.AccountId, m.Name, m.Banner, m.Text, m.Utc, m.AccountId == Guid.Empty, m.AccountId == account.Id))
            .ToArray();
        long latest = rows.Count > 0 ? rows[^1].Id : after;
        return new ChatDto(stored == Chat.World ? Chat.World : "guild", lines, latest, blocked.Count);
    }

    public async Task<ChatDto> SayAsync(Account account, ChatSayRequest request, CancellationToken ct)
    {
        string stored = ChannelFor(account, request.Channel);
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
        _db.ChatMessages.Add(new ChatMessage { Channel = stored, AccountId = account.Id, Name = DisplayName(account), Banner = account.Banner, Text = text, Utc = now });
        await SaveAsync(ct);
        // Old lines go now and then; a week of chat is plenty for a playtest.
        if (Random.Shared.Next(100) == 0)
        {
            DateTime cutoff = now.AddDays(-Chat.KeepDays);
            await _db.ChatMessages.Where(m => m.Utc < cutoff).ExecuteDeleteAsync(ct);
        }
        return await ChatAsync(account, request.Channel, request.After, ct);
    }

    public async Task<ChatDto> ReportAsync(Account account, ChatReportRequest request, CancellationToken ct)
    {
        ChatMessage line = await _db.ChatMessages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == request.MessageId, ct)
            ?? throw new GameException("no_line", "That line is gone.");
        if (line.AccountId == Guid.Empty || line.AccountId == account.Id) throw new GameException("bad_report", "You cannot report that line.");
        if (!await _db.ChatReports.AnyAsync(r => r.MessageId == line.Id && r.ReporterId == account.Id, ct))
        {
            _db.ChatReports.Add(new ChatReport { MessageId = line.Id, ReporterId = account.Id, Utc = DateTime.UtcNow });
            await SaveAsync(ct);
            // A new report puts the line back in the moderators' queue; it hides itself at three reports unless a
            // moderator had already looked at it and kept it.
            await _db.ChatMessages.Where(m => m.Id == line.Id).ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Reports, m => m.Reports + 1)
                .SetProperty(m => m.Hidden, m => m.Hidden || (!m.Reviewed && m.Reports + 1 >= Chat.HideAfterReports))
                .SetProperty(m => m.Reviewed, false), ct);
        }
        return await ChatAsync(account, request.Channel, 0, ct);
    }

    /// <summary>Blocks or unblocks a player; unblocking Guid.Empty clears the whole list.</summary>
    public async Task<ChatDto> BlockAsync(Account account, ChatBlockRequest request, CancellationToken ct)
    {
        List<Guid> blocked = BlockedList(account);
        if (request.Block)
        {
            if (request.AccountId == Guid.Empty || request.AccountId == account.Id) throw new GameException("bad_block", "You cannot block that player.");
            if (!blocked.Contains(request.AccountId))
            {
                if (blocked.Count >= Chat.MaxBlocked) throw new GameException("block_full", $"You can block at most {Chat.MaxBlocked} players.");
                blocked.Add(request.AccountId);
            }
        }
        else if (request.AccountId == Guid.Empty) blocked.Clear();
        else blocked.Remove(request.AccountId);
        account.Blocked = string.Join(';', blocked);
        await SaveAsync(ct);
        return await ChatAsync(account, request.Channel, 0, ct);
    }
}
