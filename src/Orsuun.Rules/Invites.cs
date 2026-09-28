using System;

namespace Orsuun.Rules
{
    /// <summary>
    /// Invite a friend (owner, 28 Sep 2026: "a code; when the friend's hero reaches level 10 both get sorn and a Scroll of
    /// Mercy", not Amber, which stays real money only). Every hero has a code; a new hero of another login enters it before
    /// level 10, and when that hero reaches level 10 both heroes are paid by letter, each by how far they have come.
    /// </summary>
    public static class Invites
    {
        /// <summary>The level the invited hero must reach, and the level after which a code can no longer be entered.</summary>
        public const int RewardLevel = 10;

        /// <summary>Heroes one code can bring in (keeps a code from farming Scrolls of Mercy with throwaway heroes).</summary>
        public const int MaxInvited = 20;

        public const int CodeLength = 6;

        /// <summary>The code's letters: no 0/O, 1/I/L, so a code read aloud or copied by hand stays right.</summary>
        public const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

        public const int ScrollsOfMercy = 1;

        /// <summary>Each hero's sorn: a hundred and fifty of the mobs where that hero stands.</summary>
        public static long Sorn(int highestStageCleared) =>
            Content.Stage(Math.Max(1, Math.Min(Content.TotalStages, highestStageCleared))).SornPerMob * 150;

        public static string NewCode(IRandom random)
        {
            var chars = new char[CodeLength];
            for (int i = 0; i < chars.Length; i++) chars[i] = Alphabet[random.NextInt(Alphabet.Length)];
            return new string(chars);
        }

        /// <summary>A code as typed: capitals, without spaces or dashes.</summary>
        public static string Clean(string typed) =>
            typed == null ? "" : typed.Trim().ToUpperInvariant().Replace("-", "").Replace(" ", "");

        public static bool Valid(string code)
        {
            if (code == null || code.Length != CodeLength) return false;
            foreach (char c in code)
                if (Alphabet.IndexOf(c) < 0) return false;
            return true;
        }
    }
}
