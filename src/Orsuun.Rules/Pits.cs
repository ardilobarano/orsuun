#nullable enable
using System;
using System.Collections.Generic;
using Orsuun.Rules.Combat;

namespace Orsuun.Rules
{
    /// <summary>What a Pit shop line hands over.</summary>
    public enum PitGood { Korshard = 0, Turnstones = 1, EtchingNeedle = 2, PinningWax = 3, Oathstone = 4, TechniqueScroll = 5, MastersNeedle = 6, GrandmasterNeedle = 7 }

    public sealed class PitShopItem
    {
        public PitShopItem(int id, string name, int laurels, int korshardRank)
            : this(id, name, laurels, PitGood.Korshard, 1, korshardRank) { }

        public PitShopItem(int id, string name, int laurels, PitGood good, int amount, int korshardRank = 0)
        {
            Id = id;
            Name = name;
            Laurels = laurels;
            Good = good;
            Amount = amount;
            KorshardRank = korshardRank;
        }

        public int Id { get; }
        public string Name { get; }
        public int Laurels { get; }
        public PitGood Good { get; }
        public int Amount { get; }
        public int KorshardRank { get; }

        /// <summary>Hands the goods to the inventory (Korshards by rank; a Technique Scroll for one of the buyer's class skills).</summary>
        public void GrantTo(Inventory inventory, HeroClass cls = HeroClass.Vanguard, IRandom? rng = null)
        {
            switch (Good)
            {
                case PitGood.Korshard: inventory.Korshards[KorshardRank] += Amount; break;
                case PitGood.Turnstones: inventory.Turnstones += Amount; break;
                case PitGood.EtchingNeedle: inventory.EtchingNeedles += Amount; break;
                case PitGood.PinningWax: inventory.PinningWax += Amount; break;
                case PitGood.Oathstone: inventory.Oathstones += Amount; break;
                case PitGood.MastersNeedle: inventory.MastersNeedles += Amount; break;
                case PitGood.GrandmasterNeedle: inventory.GrandmasterNeedles += Amount; break;
                case PitGood.TechniqueScroll:
                    for (int i = 0; i < Amount; i++) inventory.Books[Books.Id(cls, (rng ?? new XorShiftRandom(1)).NextInt(SkillGrades.Slots))]++;
                    break;
            }
        }
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
                gear.Add(new ItemState(item.ItemLevel, item.Rarity, item.Slot)
                {
                    UpgradeLevel = Math.Max(0, Math.Min(ItemState.MaxUpgradeLevel, item.UpgradeLevel + step)),
                    AverageDamagePercent = item.AverageDamagePercent,
                    SkillDamagePercent = item.SkillDamagePercent,
                });
            return gear;
        }

        /// <summary>
        /// The Pit shop (GDD: Technique Scrolls, Korshards and frames; Pit rewards are currency and cosmetics, never
        /// upgrade protection). Korshards, and since the seasons (25 Sep 2026) Turnstones, Etching Needles, Pinning Wax
        /// and Oathstones, since skill grades (26 Sep 2026) Technique Scrolls, and a Master's Needle (26 Sep 2026: its second
        /// source beside the Carvers' Archive, about two days of Pit fights); frames wait for name frames.
        /// </summary>
        public static readonly PitShopItem[] Shop =
        {
            new PitShopItem(1, "Trooper Korshard", 10, 0),
            new PitShopItem(2, "Rider Korshard", 25, 1),
            new PitShopItem(3, "Captain Korshard", 60, 2),
            new PitShopItem(4, "5 Turnstones", 15, PitGood.Turnstones, 5),
            new PitShopItem(5, "Etching Needle", 20, PitGood.EtchingNeedle, 1),
            new PitShopItem(6, "Pinning Wax", 25, PitGood.PinningWax, 1),
            new PitShopItem(7, "Oathstone", 45, PitGood.Oathstone, 1),
            new PitShopItem(8, "Technique Scroll", 30, PitGood.TechniqueScroll, 1),
            new PitShopItem(9, "Master's Needle", 90, PitGood.MastersNeedle, 1),
            new PitShopItem(10, "Grandmaster's Needle", 250, PitGood.GrandmasterNeedle, 1),
        };

        // ---- Pit seasons (owner, 25 Sep 2026; GDD: the Pit ladder resets weekly and pays titles and season currency) ----

        /// <summary>A Pit season is the War season: the bounty week.</summary>
        public static string SeasonKey(DateTime local) => Bounties.WeekKey(local);

        /// <summary>Fights a season needs before its end pays.</summary>
        public const int SeasonMinFights = 3;

        /// <summary>Laurels at a season's end by the league it ended in (Bronze .. Khagan).</summary>
        public static readonly int[] SeasonLaurels = { 20, 40, 70, 110, 160, 220 };

        public static int LeagueIndex(int rating)
        {
            int index = 0;
            for (int i = 0; i < LeagueFloors.Length; i++)
                if (rating >= LeagueFloors[i]) index = i;
            return index;
        }

        /// <summary>A season's end: the league's Laurels, and 100 more for the first, 50 for the second and third.</summary>
        public static int SeasonReward(int rating, int rank) =>
            SeasonLaurels[LeagueIndex(rating)] + (rank == 1 ? 100 : rank >= 2 && rank <= 3 ? 50 : 0);

        /// <summary>The title a season's end gives, held through the next season (shown on the board).</summary>
        public static string? Title(int rank) => rank == 1 ? "Champion of the Pits" : rank >= 2 && rank <= 3 ? "Pit Veteran" : null;

        /// <summary>Ratings drift halfway back to the start at a season's end, so the ladder climbs again each week.</summary>
        public static int SoftReset(int rating) => StartRating + (rating - StartRating) / 2;

        public static PitShopItem? ShopItem(int id)
        {
            foreach (PitShopItem item in Shop)
                if (item.Id == id) return item;
            return null;
        }
    }
}
