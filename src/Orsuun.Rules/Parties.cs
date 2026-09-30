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

    /// <summary>
    /// Party dungeons (owner, 30 Sep 2026: "Enter a dungeon together: partymates fight each floor beside you (each hero's own
    /// run on the server) and the party shares the end chest."). A partymate opens a dungeon for the party; the others join
    /// for JoinSeconds, each with a key and a run of their own. When it closes, the party's chest is shared: one Warden's chest
    /// for each member who cleared, pooled and dealt out evenly among them (its goods and Technique Scrolls, which letters
    /// carry). It needs at least MinJoined heroes in the run.
    /// </summary>
    public static class PartyDungeons
    {
        public const int JoinSeconds = 180, SettleAfterMinutes = 30, MinJoined = 2;

        /// <summary>A member's part of the party's chest: goods (TradeGoods id to count) and Technique Scrolls (book id to count).</summary>
        public sealed class Share
        {
            public readonly System.Collections.Generic.SortedDictionary<int, int> Goods = new System.Collections.Generic.SortedDictionary<int, int>();
            public readonly System.Collections.Generic.SortedDictionary<int, int> Books = new System.Collections.Generic.SortedDictionary<int, int>();
            public bool Empty => Goods.Count == 0 && Books.Count == 0;
        }

        /// <summary>The party's chest for <paramref name="clearers"/> heroes: that many Warden's chests pooled, each good and
        /// scroll dealt one at a time round the members (the leftovers of one kind start where the last kind's stopped).</summary>
        public static Share[] Pool(DungeonDef dungeon, int level, int clearers, IRandom rng)
        {
            var shares = new Share[Math.Max(0, clearers)];
            for (int i = 0; i < shares.Length; i++) shares[i] = new Share();
            if (shares.Length == 0) return shares;
            var pool = new Inventory();
            for (int i = 0; i < shares.Length; i++) Dungeons.WardenChest(pool, level, rng, dungeon);
            int next = 0;
            for (int good = 0; good < TradeGoods.Count; good++)
                for (int n = TradeGoods.Held(pool, good); n > 0; n--)
                {
                    Share s = shares[next++ % shares.Length];
                    s.Goods[good] = (s.Goods.TryGetValue(good, out int had) ? had : 0) + 1;
                }
            for (int book = 0; book < pool.Books.Length; book++)
                for (int n = pool.Books[book]; n > 0; n--)
                {
                    Share s = shares[next++ % shares.Length];
                    s.Books[book] = (s.Books.TryGetValue(book, out int had) ? had : 0) + 1;
                }
            return shares;
        }
    }
}
