#nullable enable
using System.Linq;

namespace Orsuun.Rules
{
    /// <summary>
    /// Characters (owner, 25 Sep 2026: "character creation with name selection after signing up or logging in like
    /// metin2, total 4 char slots with a common depot of items to trade between each other"). An account holds up to
    /// four named characters; Amber and the Banner belong to the account, everything else to the character; the depot
    /// is shared by all four.
    /// </summary>
    public static class Characters
    {
        public const int MaxSlots = 4;
        /// <summary>Pieces the shared depot holds.</summary>
        public const int DepotSlots = 40;
        public const int NameMin = 3;
        public const int NameMax = 16;

        /// <summary>Null when the name may be taken: 3-16 letters or digits, starting with a letter, and clean.</summary>
        public static string? NameProblem(string? name)
        {
            string n = (name ?? "").Trim();
            if (n.Length < NameMin || n.Length > NameMax) return $"A name has {NameMin} to {NameMax} letters or digits.";
            if (!char.IsLetter(n[0])) return "A name starts with a letter.";
            if (!n.All(char.IsLetterOrDigit)) return "Letters and digits only, no spaces.";
            if (!WordFilter.IsClean(n)) return "Choose another name.";
            return null;
        }

        /// <summary>Names are unique regardless of case.</summary>
        public static string NameKey(string name) => name.Trim().ToLowerInvariant();
    }
}
