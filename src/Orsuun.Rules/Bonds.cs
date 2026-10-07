#nullable enable
using System;

namespace Orsuun.Rules
{
    /// <summary>
    /// Sworn bonds (owner, 7 Oct 2026: picked "Sworn bonds": "Two heroes swear a bond (Metin2's marriage, reimagined): extra
    /// XP while they hunt in the same party, a bond ring that grows stronger with time together, and a bond line in chat").
    /// A hero of MinLevel asks a friend of MinLevel (not one of their own heroes); the friend answers within AskMinutes.
    /// Sworn, the two have one bond each: while both hunt the same map in the same party, each earns XpBonusBp more XP from
    /// the live hunt (on top of the party's bonus; the lane's combat is untouched) and the time counts toward their Bond Ring,
    /// whose level (RingHours) raises that bonus. Their own chat line is theirs alone. Either may break the bond; a hero who
    /// broke or lost one waits RebondDays before swearing again. Assumptions (not stated by the owner): every number, that
    /// the ring's strength is the XP bonus (not combat stats), and the friends-only ask.
    /// </summary>
    public static class Bonds
    {
        public const int MinLevel = 15, AskMinutes = 30, RebondDays = 3, MaxRing = 10;

        /// <summary>Hours hunted together for each ring level (level 0 when sworn).</summary>
        public static readonly int[] RingHours = { 0, 1, 3, 6, 10, 15, 25, 40, 60, 90, 130 };

        /// <summary>The ring's level after a time hunted together.</summary>
        public static int RingLevel(long secondsTogether)
        {
            int level = 0;
            for (int i = 1; i < RingHours.Length; i++)
                if (secondsTogether >= RingHours[i] * 3600L) level = i;
            return level;
        }

        /// <summary>When the next ring level comes (seconds together), or -1 at the top.</summary>
        public static long NextRingSeconds(int level) => level >= MaxRing ? -1 : RingHours[level + 1] * 3600L;

        /// <summary>The XP bonus while hunting together, in basis points of the hunt's XP: 3% sworn, up to 10% at ring 10.</summary>
        public static int XpBonusBp(int ring) => 300 + 70 * Math.Min(MaxRing, Math.Max(0, ring));

        public static string RingName(int level) => level <= 0 ? "Bond Ring" : "Bond Ring +" + level;

        /// <summary>Both hunt the same map in the same party, the partner lately seen.</summary>
        public static bool Together(Guid? myLeader, int myStage, Guid? theirLeader, int theirStage, bool theirOnline) =>
            theirOnline && myLeader != null && myLeader == theirLeader && Parties.Together(myStage, theirStage);
    }
}
