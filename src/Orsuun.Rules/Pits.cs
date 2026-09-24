#nullable enable
using System;
using System.Collections.Generic;

namespace Orsuun.Rules
{
    public sealed class PitShopItem
    {
        public PitShopItem(int id, string name, int laurels, int korshardRank)
        {
            Id = id;
            Name = name;
            Laurels = laurels;
            KorshardRank = korshardRank;
        }

        public int Id { get; }
        public string Name { get; }
        public int Laurels { get; }
        public int KorshardRank { get; }
    }

    /// <summary>
    /// The Pits (GDD section 7: 1v1 against another player's defense snapshot, always open, 5 free tickets a day, rank
    /// points and Laurels; an Elo ladder with leagues Bronze to Khagan and a public board with gear inspection; the
    /// attacker's manual casting is an edge). A fight is a duel (Rules.Duels) on the neutral frame, with no compression
    /// ("whales still win duels"), and the attacker's edge added. Three challengers are offered at a time: players near
    /// the attacker's rating, and where the server has too few, Pit shades cut from the attacker's own gear one forge level
    /// weaker, even, or one stronger.
    /// </summary>
    public static class Pits
    {
        public const int TicketsPerDay = 5;
        public const int StartRating = 1000;
        public const int RatingK = 32;
        /// <summary>The attacker's edge (GDD: attackers may cast by hand), on the duel's log-time scale.</summary>
        public const double AttackerEdge = 0.1;
        public const int WinLaurels = 10;
        public const int LossLaurels = 2;
        public const int Challengers = 3;
        /// <summary>Players within this many rating points are offered as challengers.</summary>
        public const int MatchWindow = 250;

        public static readonly string[] Leagues = { "Bronze", "Iron", "Silver", "Gold", "Jade", "Khagan" };
        private static readonly int[] LeagueFloors = { 0, 1100, 1250, 1400, 1600, 1800 };

        public static string League(int rating)
        {
            string league = Leagues[0];
            for (int i = 0; i < LeagueFloors.Length; i++)
                if (rating >= LeagueFloors[i]) league = Leagues[i];
            return league;
        }

        /// <summary>
        /// Ratings after a fight: the attacker moves by Elo (K 32); a real defender by half as much the other way, since
        /// their snapshot fought without them. A shade's rating does not move.
        /// </summary>
        public static (int Attacker, int Defender) Rate(int attacker, int defender, bool attackerWon, bool realDefender)
        {
            double expect = 1.0 / (1.0 + Math.Pow(10.0, (defender - attacker) / 400.0));
            int delta = (int)Math.Round(RatingK * ((attackerWon ? 1.0 : 0.0) - expect), MidpointRounding.AwayFromZero);
            return (attacker + delta, realDefender ? defender - delta / 2 : defender);
        }

        /// <summary>A Pit shade's gear: copies of the given pieces, <paramref name="step"/> forge levels up or down (0..+9).</summary>
        public static List<ItemState> ShadeGear(IEnumerable<ItemState> equipped, int step)
        {
            var gear = new List<ItemState>();
            foreach (ItemState item in equipped)
                gear.Add(new ItemState(item.ItemLevel, item.Rarity, item.Slot) { UpgradeLevel = Math.Max(0, Math.Min(ItemState.MaxUpgradeLevel, item.UpgradeLevel + step)) });
            return gear;
        }

        /// <summary>The Pit shop (GDD: Technique Scrolls, Korshards and frames; Korshards for now).</summary>
        public static readonly PitShopItem[] Shop =
        {
            new PitShopItem(1, "Trooper Korshard", 10, 0),
            new PitShopItem(2, "Rider Korshard", 25, 1),
            new PitShopItem(3, "Captain Korshard", 60, 2),
        };

        public static PitShopItem? ShopItem(int id)
        {
            foreach (PitShopItem item in Shop)
                if (item.Id == id) return item;
            return null;
        }
    }
}
