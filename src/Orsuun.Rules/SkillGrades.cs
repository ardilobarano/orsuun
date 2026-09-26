#nullable enable
using System;
using System.Collections.Generic;
using Orsuun.Rules.Combat;

namespace Orsuun.Rules
{
    public enum SkillTier { Normal = 0, Mastered = 1, Grand = 2, Peerless = 3 }

    /// <summary>
    /// Skill books (owner, 26 Sep 2026: "each classes each skill need a seperate book"): one Technique Scroll per skill of
    /// each class, twenty in all since every class has five skills (world bible: "Technique Scroll: Iron Whirl"). Book id =
    /// class * 5 + skill slot. Books drop for any class and trade on the Salt Exchange and in direct trade, stacked.
    /// </summary>
    public static class Books
    {
        public const int Classes = 4;
        public const int Count = Classes * SkillGrades.Slots;
        public const int MaxStack = 999;

        public static int Id(HeroClass cls, int slot) => (int)cls * SkillGrades.Slots + slot;
        public static HeroClass ClassOf(int bookId) => (HeroClass)(bookId / SkillGrades.Slots);
        public static int SlotOf(int bookId) => bookId % SkillGrades.Slots;
        public static bool Valid(int bookId) => bookId >= 0 && bookId < Count;
        public static string SkillName(int bookId) => SkillDef.For(ClassOf(bookId))[SlotOf(bookId)].Name;
        public static string Name(int bookId) => "Technique Scroll: " + SkillName(bookId);
    }

    /// <summary>
    /// Skill grades (GDD section 4; owner, 26 Sep 2026). A skill climbs from Normal through Mastered M1-M10, Grand G1-G10
    /// and Peerless P. Mastered steps, Metin2 style: a step needs successful reads of that skill's book, one for Normal to
    /// M1 and M1 to M2, then two for M2 to M3, three for M3 to M4 and so on up to nine for M9 to M10 (46 in all); a read
    /// spends one book, succeeds 70% of the time, and the skill rests 8 hours after each read. Grand steps and Peerless
    /// burn one Oathstone each at 60%, and every try costs Honor (owner: "a good hardening thing"), more each step:
    /// Honor comes from Korstones, Pit wins and dungeon Wardens. Each step adds skill power: +2% a Mastered step, +3% a
    /// Grand step, +10% for Peerless (+60% in all); a haste skill's duration grows by half as much. Grades belong to each
    /// class's skills (by book id). The GDD's Normal 1-17 is folded into the hero's level; the Scroll of Clear Mind and
    /// Scholar's Incense are not built.
    /// </summary>
    public static class SkillGrades
    {
        /// <summary>Skills a class has (owner, 26 Sep 2026: five; the fourth and fifth unlock at levels 30 and 60).</summary>
        public const int Slots = 5;
        public const int MasteredSteps = 10;
        public const int GrandSteps = 10;
        /// <summary>0 Normal, 1-10 M1-M10, 11-20 G1-G10, 21 Peerless.</summary>
        public const int Max = MasteredSteps + GrandSteps + 1;
        public const int ReadChanceBp = 7000;
        public const int OathstoneChanceBp = 6000;
        public const int ReadCooldownHours = 8;
        public const int MasteredPercentPerStep = 2;
        public const int GrandPercentPerStep = 3;
        public const int PeerlessPercent = 10;

        /// <summary>Honor paid by every Oathstone try: this much times the Grand step tried (G1 30 .. G10 300, Peerless 330).</summary>
        public const int HonorPerGrandStep = 30;
        public const int HonorPerKorstone = 1;
        public const int HonorPerPitWin = 5;
        public const int HonorPerWarden = 10;

        public static SkillTier Tier(int grade) =>
            grade <= 0 ? SkillTier.Normal : grade <= MasteredSteps ? SkillTier.Mastered : grade < Max ? SkillTier.Grand : SkillTier.Peerless;

        /// <summary>"Normal", "M1".."M10", "G1".."G10", "P".</summary>
        public static string Name(int grade) => Tier(grade) switch
        {
            SkillTier.Normal => "Normal",
            SkillTier.Mastered => "M" + grade,
            SkillTier.Grand => "G" + (grade - MasteredSteps),
            _ => "P",
        };

        /// <summary>Extra skill power at a grade, percent.</summary>
        public static int BonusPercent(int grade)
        {
            int g = Math.Max(0, Math.Min(Max, grade));
            int mastered = Math.Min(g, MasteredSteps), grand = Math.Max(0, Math.Min(g - MasteredSteps, GrandSteps));
            return mastered * MasteredPercentPerStep + grand * GrandPercentPerStep + (g >= Max ? PeerlessPercent : 0);
        }

        /// <summary>The next step from <paramref name="grade"/> reads books (Normal to M10); later steps burn an Oathstone.</summary>
        public static bool NeedsBooks(int grade) => grade < MasteredSteps;

        /// <summary>Successful reads the step from <paramref name="grade"/> needs: 1, 1, 2, 3 .. 9.</summary>
        public static int ReadsNeeded(int grade) => NeedsBooks(grade) ? Math.Max(1, grade) : 0;

        /// <summary>Honor an Oathstone try from <paramref name="grade"/> costs (0 for book steps).</summary>
        public static int HonorCost(int grade) => NeedsBooks(grade) || grade >= Max ? 0 : HonorPerGrandStep * (grade - MasteredSteps + 1);

        public static int ChanceBp(int grade) => NeedsBooks(grade) ? ReadChanceBp : OathstoneChanceBp;

        /// <summary>Seconds until a skill may read again, from the seconds since its last read.</summary>
        public static long CooldownLeft(long secondsSinceRead) => Math.Max(0, ReadCooldownHours * 3600L - secondsSinceRead);

        /// <summary>Why the skill cannot train now, or null.</summary>
        public static string? Problem(int grade, int books, int oathstones, long honor, long secondsSinceRead)
        {
            if (grade >= Max) return "Peerless: the skill cannot climb higher.";
            if (NeedsBooks(grade))
            {
                if (books < 1) return "The next read needs this skill's Technique Scroll (Warden chests, the shops, the Exchange).";
                long left = CooldownLeft(secondsSinceRead);
                if (left > 0) return $"The mind needs rest: this skill can read again in {left / 3600}h {left % 3600 / 60}m.";
                return null;
            }
            if (oathstones < 1) return "The next step burns an Oathstone (the Carvers' Archive, the Pit shop).";
            int cost = HonorCost(grade);
            if (honor < cost) return $"The oath asks {cost} Honor for this step; you hold {honor}. Honor comes from Korstones, Pit wins and dungeon Wardens.";
            return null;
        }

        /// <summary>
        /// One try: a read (on success the step's reads count one up, and a full count raises the grade) or an Oathstone
        /// (on success the grade rises). Returns the new grade and reads toward the next step.
        /// </summary>
        public static (int Grade, int Reads) Train(int grade, int reads, IRandom rng, out bool success)
        {
            success = rng.RollBp(ChanceBp(grade));
            if (!success) return (grade, reads);
            if (!NeedsBooks(grade)) return (Math.Min(Max, grade + 1), 0);
            return reads + 1 >= ReadsNeeded(grade) ? (grade + 1, 0) : (grade, reads + 1);
        }

        /// <summary>The grades of a class's five skills out of all twenty (by book id).</summary>
        public static int[] ForClass(IReadOnlyList<int> all, HeroClass cls)
        {
            var grades = new int[Slots];
            for (int i = 0; i < Slots; i++)
            {
                int id = Books.Id(cls, i);
                grades[i] = id < all.Count ? all[id] : 0;
            }
            return grades;
        }

        /// <summary>Twenty numbers (one per book id) stored as "n;n;..".</summary>
        public static int[] Parse(string? text, int max = Max)
        {
            var values = new int[Books.Count];
            string[] parts = (text ?? "").Split(';');
            for (int i = 0; i < values.Length && i < parts.Length; i++)
                if (int.TryParse(parts[i], out int v)) values[i] = Math.Max(0, Math.Min(max, v));
            return values;
        }

        public static string Format(IEnumerable<int> values) => string.Join(";", values);
    }
}
