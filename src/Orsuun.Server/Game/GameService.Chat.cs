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
        Chat.Trade => Chat.Trade,
        "guild" => account.GuildId is Guid g ? Chat.GuildChannel(g) : throw new GameException("no_guild", "You are not in a guild."),
        // The hunting party's own channel (Rules.Parties), keyed by its leader.
        "party" => account.PartyLeaderId is Guid p ? PartyChannel(p) : throw new GameException("no_party", "You are not in a party."),
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
        // A partymate reads the party's lines from when they joined (a party formed again by the same leader starts clean).
        if (IsPartyChannel(stored))
        {
            DateTime joined = account.PartyJoinedUtc ?? DateTime.UtcNow;
            query = query.Where(m => m.Utc >= joined);
        }
        List<ChatMessage> rows = await query.OrderByDescending(m => m.Id).Take(Chat.PageSize).ToListAsync(ct);
        rows.Reverse();
        // The Bazaar Call's links, as the pieces are now: a piece its sender no longer has shows as gone.
        List<Guid> linked = rows.Where(m => m.ItemId != null).Select(m => m.ItemId!.Value).Distinct().ToList();
        Dictionary<Guid, Item> pieces = linked.Count == 0 ? new Dictionary<Guid, Item>()
            : await _db.Items.AsNoTracking()
                .Where(i => linked.Contains(i.Id) && !i.Destroyed).ToDictionaryAsync(i => i.Id, ct);
        ChatLineDto[] lines = rows.Where(m => !blocked.Contains(m.AccountId))
            .Select(m =>
            {
                Item? piece = m.ItemId is Guid id && pieces.TryGetValue(id, out Item? found) && found.OwnerId == m.AccountId ? found : null;
                return new ChatLineDto(m.Id, m.AccountId, m.Name, m.Banner, m.Text, m.Utc, m.AccountId == Guid.Empty, m.AccountId == account.Id, m.Title,
                    piece == null ? null : ToDto(piece), m.ItemId != null && piece == null);
            })
            .ToArray();
        long latest = rows.Count > 0 ? rows[^1].Id : after;
        return new ChatDto(stored == Chat.World ? Chat.World : stored == Chat.Trade ? Chat.Trade : IsPartyChannel(stored) ? "party" : "guild", lines, latest, blocked.Count);
    }

    public async Task<ChatDto> SayAsync(Account account, ChatSayRequest request, CancellationToken ct)
    {
        string stored = ChannelFor(account, request.Channel);
        string text = Chat.Clean(request.Text);
        bool trade = stored == Chat.Trade;
        // A Bazaar Call may be a linked piece alone.
        if (!(trade && request.ItemId != null && text.Length == 0) && Chat.TextProblem(text) is string problem) throw new GameException("bad_text", problem);
        DateTime now = DateTime.UtcNow;
        if (account.MutedUntilUtc is DateTime muted && muted > now)
        {
            int minutes = (int)Math.Ceiling((muted - now).TotalMinutes);
            throw new GameException("muted", minutes >= 120 ? $"A moderator muted you for {minutes / 60} more hours." : $"A moderator muted you for {minutes} more minutes.");
        }
        if (account.LastChatUtc is DateTime last && (now - last).TotalSeconds < Chat.CooldownSeconds)
            throw new GameException("chat_cooldown", "Slow down a little.");
        Guid? link = null;
        if (trade)
        {
            if (Content.LevelFor(account.Xp) < Chat.TradeLevel)
                throw new GameException("trade_level", $"The Bazaar Call opens at level {Chat.TradeLevel}.");
            DateTime since = now.AddSeconds(-Chat.TradeCooldownSeconds);
            DateTime? lastCall = await _db.ChatMessages.AsNoTracking()
                .Where(m => m.Channel == Chat.Trade && m.AccountId == account.Id && m.Utc > since)
                .OrderByDescending(m => m.Utc).Select(m => (DateTime?)m.Utc).FirstOrDefaultAsync(ct);
            if (lastCall is DateTime called)
            {
                int wait = Math.Max(1, (int)Math.Ceiling(Chat.TradeCooldownSeconds - (now - called).TotalSeconds));
                throw new GameException("trade_cooldown", $"You can call the bazaar again in {wait} seconds.");
            }
            if (request.ItemId is Guid itemId)
            {
                if (!account.Items.Any(i => i.Id == itemId && !i.Destroyed)) throw new GameException("no_item", "That piece is not yours.");
                link = itemId;
            }
        }
        account.LastChatUtc = now;
        if (trade) Feat(account, FeatMetric.BazaarCalls, 1);
        _db.ChatMessages.Add(new ChatMessage { Channel = stored, AccountId = account.Id, Name = DisplayName(account), Title = TitleOf(account), Banner = account.Banner, Text = text, ItemId = link, Utc = now });
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
            await AlertModeratorsAsync("chat", "A chat line was reported", $"{line.Name}: \u201c{Clip(line.Text, 80)}\u201d.", ct);
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
                // Blocking a friend (or one asking) ends it.
                await _db.Friendships.Where(f => (f.FromId == account.Id && f.ToId == request.AccountId) || (f.FromId == request.AccountId && f.ToId == account.Id))
                    .ExecuteDeleteAsync(ct);
            }
        }
        else if (request.AccountId == Guid.Empty) blocked.Clear();
        else blocked.Remove(request.AccountId);
        account.Blocked = string.Join(';', blocked);
        await SaveAsync(ct);
        return await ChatAsync(account, request.Channel, 0, ct);
    }
}
