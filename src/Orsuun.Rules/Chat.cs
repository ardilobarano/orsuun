#nullable enable
using System;
using System.Linq;

namespace Orsuun.Rules
{
    /// <summary>
    /// Chat (owner, 24 Sep 2026: "all chat"): one world channel for every player and one channel per guild. Guild
    /// events (joins, levels, skills) are posted in the guild channel as system lines, which makes it the guild log;
    /// Commander kills and fortress captures go to the world channel. Bad words are starred out; three reports hide a
    /// line; a player can block another.
    /// </summary>
    public static class Chat
    {
        public const string World = "world";
        /// <summary>
        /// The Bazaar Call (owner, 28 Sep 2026: picked "Bazaar Call trade chat"; GDD: "a trade channel where players link
        /// items; tapping a linked item shows its full card and offers TRADE or WHISPER. 30-second shout cooldown, level
        /// 20+"). Everyone may read it; heroes of TradeLevel call in it, once every TradeCooldownSeconds, a line that may
        /// link one of their own pieces.
        /// </summary>
        public const string Trade = "trade";
        public const int TradeLevel = 20;
        public const int TradeCooldownSeconds = 30;
        public const int MaxLength = 200;
        public const int CooldownSeconds = 3;
        /// <summary>Lines a channel read returns at most.</summary>
        public const int PageSize = 50;
        public const int HideAfterReports = 3;
        public const int MaxBlocked = 50;
        /// <summary>Lines older than this are deleted.</summary>
        public const int KeepDays = 7;

        public static string GuildChannel(Guid guildId) => "g:" + guildId.ToString("N");

        /// <summary>Control characters out, runs of spaces collapsed, trimmed.</summary>
        public static string Normalise(string? text)
        {
            string plain = new string((text ?? "").Select(c => char.IsControl(c) ? ' ' : c).ToArray());
            return string.Join(" ", plain.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
        }

        public static string? TextProblem(string text)
        {
            if (text.Length == 0) return "Say something first.";
            if (text.Length > MaxLength) return $"At most {MaxLength} characters.";
            return null;
        }

        /// <summary>What is stored and shown: normalised, bad words starred out.</summary>
        public static string Clean(string? text) => WordFilter.Mask(Normalise(text));
    }

    /// <summary>
    /// Private messages (owner, 26 Sep 2026: "go for private messages", "they need to stay after days and days", "make it
    /// a screen"): hero to hero, on their own MESSAGES screen, kept with no expiry (only a conversation past
    /// KeepPerConversation loses its oldest). The chat's text rules, flood limit, mutes and blocks apply; a reported
    /// message goes to the moderators' queue.
    /// </summary>
    public static class Whispers
    {
        public const int PageSize = 50;
        public const int KeepPerConversation = 1000;
        /// <summary>Conversations listed, newest first.</summary>
        public const int MaxConversations = 60;
        /// <summary>Stored as a moderation line in this channel prefix plus the recipient's id (never shown in chat).</summary>
        public const string ReportChannel = "w:";
    }

    /// <summary>
    /// Friends (owner, 25 Sep 2026: "adding friends and friend list"): each hero keeps its own list. A friend request
    /// waits until the other hero takes it (both then see each other), is turned down, or taken back. Neither side may
    /// have blocked the other.
    /// </summary>
    public static class Friends
    {
        public const int MaxFriends = 50;
        /// <summary>Requests one hero may have waiting at once.</summary>
        public const int MaxAsked = 20;
        /// <summary>A hero counts as online while its heartbeat (every 30 s) is this fresh.</summary>
        public const int OnlineMinutes = 2;

        public static bool Online(int minutesAway) => minutesAway < OnlineMinutes;

        /// <summary>"online", "5 min ago", "3 h ago", "2 days ago".</summary>
        public static string Seen(int minutesAway)
        {
            if (Online(minutesAway)) return "online";
            if (minutesAway < 60) return minutesAway + " min ago";
            if (minutesAway < 48 * 60) return minutesAway / 60 + " h ago";
            return minutesAway / (24 * 60) + " days ago";
        }
    }
}
