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

        // Steppe-sounding halves for the hero screen's RANDOM name (27 Sep 2026: typing a name was the first thing a new
        // player had to do before playing). Made up, not taken from any game or history book.
        private static readonly string[] ManStarts = { "Ar", "Bat", "Tem", "Kara", "Ogu", "Sar", "Tul", "Yes", "Bor", "Kaz", "Tog", "Er", "Alp", "Bek", "Dar", "Ur", "Tar", "Sen" };
        private static readonly string[] ManEnds = { "dakan", "mur", "tay", "gan", "bek", "tar", "uk", "ren", "kin", "dar", "an", "gai", "sun", "doq" };
        private static readonly string[] WomanStarts = { "Ay", "Sa", "Ne", "Tu", "Gul", "Ak", "Ye", "Bo", "Zar", "Il", "Ce", "Kyr", "Al", "Yu", "Ser", "Mer" };
        private static readonly string[] WomanEnds = { "lin", "ra", "na", "sun", "ya", "ke", "mira", "vel", "sha", "rin", "tal", "ane", "ise" };

        /// <summary>A name to start from: a start and an ending for the figure, always one NameProblem accepts (the server
        /// may still find it taken; another tap gives another).</summary>
        public static string SuggestName(Figure figure, IRandom rng)
        {
            string[] starts = figure == Figure.Woman ? WomanStarts : ManStarts;
            string[] ends = figure == Figure.Woman ? WomanEnds : ManEnds;
            for (int i = 0; i < 32; i++)
            {
                string name = starts[rng.NextInt(starts.Length)] + ends[rng.NextInt(ends.Length)];
                if (NameProblem(name) == null) return name;
            }
            return figure == Figure.Woman ? "Aylin" : "Temur";
        }

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
