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
    /// <summary>The party board (owner, 29 Sep 2026: "Party finder"): a hero listed as looking for a party stays listed this
    /// long (or until it joins one), and the board shows this many heroes of the place.</summary>
    public const int LookMinutes = 30, BoardSize = 12;

    public static bool Looking(DateTime? listedUtc, DateTime now) => listedUtc is DateTime at && at > now.AddMinutes(-LookMinutes) && at <= now.AddMinutes(1);

    /// <summary>The park ids of a place (Place): a campaign map's ten stages, or a zone alone; none for a dungeon.</summary>
    public static (int First, int Last) Stages(int place) =>
        place < 0 ? (1, 0) : place >= 1000 ? ((place - 1001) * MapDef.StagesPerMap + 1, (place - 1000) * MapDef.StagesPerMap) : (place, place);

        /// <summary>The bonus (basis points of the hunt's XP and sorn) for this many partymates hunting alongside.</summary>
        public static int BonusBp(int matesTogether) => Math.Min(MaxMembers - 1, Math.Max(0, matesTogether)) * BonusBpPerMate;

        /// <summary>Where a hero hunts, for "together": a campaign map, a zone, or nowhere shared (a dungeon floor, -1).</summary>
        public static int Place(int parkedStage) =>
            Dungeons.IsFloor(parkedStage) ? -1 : Content.IsZone(parkedStage) ? parkedStage : 1000 + Content.MapOfStage(parkedStage).Id;

        public static bool Together(int stageA, int stageB) => Place(stageA) >= 0 && Place(stageA) == Place(stageB);
    }
}
