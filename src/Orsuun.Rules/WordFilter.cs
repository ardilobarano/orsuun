#nullable enable
using System;
using System.Linq;
using System.Text;

namespace Orsuun.Rules
{
    /// <summary>
    /// Crude but cheap: text shown to other players (guild names, chat) is checked against the obvious slurs and
    /// obscenities. Strong words count anywhere, even spaced out; short ones only as a whole word, so Canal Wardens
    /// and Essex Riders pass. Reports and blocking back it up in chat; a review queue comes before a public launch.
    /// </summary>
    public static class WordFilter
    {
        private static readonly string[] Strong =
        {
            "fuck", "shit", "cunt", "nigg", "hitler", "nazi", "porn", "pussy", "whore", "slut", "bitch", "penis", "vagina",
        };
        private static readonly string[] Short = { "sex", "anal", "anus", "dick", "cock", "fag", "rape", "kys", "tits", "cum" };

        private static bool BadWord(string word)
        {
            string letters = new string(word.ToLowerInvariant().Where(char.IsLetter).ToArray());
            if (letters.Length == 0) return false;
            return Strong.Any(letters.Contains) || Short.Any(b => letters == b || letters == b + "s" || letters == b + "y");
        }

        public static bool IsClean(string text)
        {
            string letters = new string(text.ToLowerInvariant().Where(char.IsLetter).ToArray());
            if (Strong.Any(letters.Contains)) return false;
            return !text.Split(new[] { ' ', '-', '\'' }, StringSplitOptions.RemoveEmptyEntries).Any(BadWord);
        }

        /// <summary>The text with every bad word starred out (chat keeps the rest of the line).</summary>
        public static string Mask(string text)
        {
            var sb = new StringBuilder(text.Length);
            int i = 0;
            while (i < text.Length)
            {
                if (char.IsWhiteSpace(text[i])) { sb.Append(text[i++]); continue; }
                int start = i;
                while (i < text.Length && !char.IsWhiteSpace(text[i])) i++;
                string word = text.Substring(start, i - start);
                sb.Append(BadWord(word) ? new string('*', word.Length) : word);
            }
            return sb.ToString();
        }
    }
}
