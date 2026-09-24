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
}
