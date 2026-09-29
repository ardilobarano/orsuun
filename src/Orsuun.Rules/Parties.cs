using System;

namespace Orsuun.Rules
{
    /// <summary>
    /// Hunting parties (owner, 29 Sep 2026: "Hunting parties": "Form a party of 2-4 friends or guildmates: their heroes walk
    /// and fight beside yours on the same map (not just at camps), and the party shares a small XP/sorn bonus while
    /// together."). A party is named by its leader; invites go to friends and guildmates and lapse after a while. Each
    /// partymate hunting the same place online adds <see cref="BonusBpPerMate"/> to the hunt's XP and sorn (paid like a
    /// fish meal, never in the lane's combat, so replays are untouched). Assumptions (not stated by the owner): the size,
    /// the 5% a mate, the ten-minute invite and what "together" means.
    /// </summary>
    public static class Parties
    {
        public const int MaxMembers = 4, InviteMinutes = 10, BonusBpPerMate = 500, PresentSeconds = 180;

        /// <summary>The bonus (basis points of the hunt's XP and sorn) for this many partymates hunting alongside.</summary>
        public static int BonusBp(int matesTogether) => Math.Min(MaxMembers - 1, Math.Max(0, matesTogether)) * BonusBpPerMate;

        /// <summary>Where a hero hunts, for "together": a campaign map, a zone, or nowhere shared (a dungeon floor, -1).</summary>
        public static int Place(int parkedStage) =>
            Dungeons.IsFloor(parkedStage) ? -1 : Content.IsZone(parkedStage) ? parkedStage : 1000 + Content.MapOfStage(parkedStage).Id;

        public static bool Together(int stageA, int stageB) => Place(stageA) >= 0 && Place(stageA) == Place(stageB);
    }
}
