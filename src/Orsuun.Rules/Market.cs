#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Orsuun.Rules
{
    public enum ListingStatus
    {
        Active = 0,
        Sold = 1,
        Cancelled = 2,
        Expired = 3,
    }

    /// <summary>
    /// The Salt Exchange (owner, 24 Sep 2026: "a global trading screen that all players can list their items for gold
    /// or buying from them"; GDD: one exchange, 5% tax, the Salt Peace keeps trade neutral between Banners). Any piece
    /// in the bag can be listed for sorn; the buyer pays the price, the seller receives it less the tax (a sorn sink).
    /// A listing lasts 48 hours, then the piece goes back to its seller.
    /// </summary>
    public static class Market
    {
        public const int TaxPercent = 5;
        public const long MinPrice = 1_000;
        public const long MaxPrice = 1_000_000_000;
        public const int ListingHours = 48;
        public const int MaxListings = 10;
        public const int PageSize = 8;

        public static long Tax(long price) => price * TaxPercent / 100;
        public static long Payout(long price) => price - Tax(price);

        public static string? PriceProblem(long price)
        {
            if (price < MinPrice) return $"The lowest price is {MinPrice:N0} sorn.";
            if (price > MaxPrice) return $"The highest price is {MaxPrice:N0} sorn.";
            return null;
        }

        /// <summary>Price history (owner, 26 Sep 2026: "Exchange: materials + prices"): sales of the last two weeks.</summary>
        public const int HistoryDays = 14;
        /// <summary>At most this many recent sales are read for a price history.</summary>
        public const int HistorySales = 50;

        /// <summary>A stack's price for one of its count (a piece's, and a stack of one's, is its price).</summary>
        public static long UnitPrice(long price, int count) => count > 1 ? price / count : price;

        /// <summary>
        /// The pieces a price history compares: the same slot, rarity and forge level, and the same look band (ten item
        /// levels: ItemLooks.Tier), so a +7 Epic level 60 sabre is priced against its own kind.
        /// </summary>
        public static (int Low, int High) BandLevels(int band)
        {
            int low = band * ItemLooks.LevelsPerLook;
            return (low, band >= ItemLooks.MaxTier ? int.MaxValue : low + ItemLooks.LevelsPerLook - 1);
        }
    }

    /// <summary>
    /// Stackable goods on the Exchange (owner, 26 Sep 2026: "Exchange: materials + prices"): draughts, hunt materials, the
    /// Forge's scrolls and wards, Turnstones, needles, Pinning Wax, Oathstones, Summoning Markers and Korshards by rank,
    /// each listed as a count for one price, like Technique Scrolls. Ids are stored on listings: append, never renumber.
    /// Hunt Marks, Laurels, Guild Tallies and Honor stay with whoever earned them.
    /// </summary>
    public static class TradeGoods
    {
        public const int ScrollOfMercy = 2;
        public const int FirstKorshard = 11;
        /// <summary>Moon, Tide and Heart Pearls, then the fish (Fishing.Fish), after the five Korshard ranks.</summary>
        public const int FirstPearl = 16;
        public const int FirstFish = 19;
        /// <summary>The Grandmaster's Needle, after the fish.</summary>
        public const int GrandmasterNeedle = 24;

        private static readonly string[] Names =
        {
            "Draught", "Hunt material", "Scroll of Mercy", "Khan's Alloy", "Anvil Ward", "Turnstone", "Etching Needle",
            "Master's Needle", "Pinning Wax", "Oathstone", "Summoning Marker",
        };

        public static int Count => GrandmasterNeedle + 1;

        public static bool Valid(int id) => id >= 0 && id < Count;

        public static string Name(int id) =>
            !Valid(id) ? "?" : id < FirstKorshard ? Names[id] : id < FirstPearl ? Content.KorshardRanks[id - FirstKorshard] + " Korshard"
            : id < FirstFish ? Fishing.PearlNames[id - FirstPearl] : id == GrandmasterNeedle ? "Grandmaster's Needle" : Fishing.Fish[id - FirstFish].Name;

        public static int Held(Inventory inventory, int id) => id switch
        {
            0 => inventory.Potions,
            1 => inventory.Materials,
            2 => inventory.ScrollsOfMercy,
            3 => inventory.KhansAlloys,
            4 => inventory.AnvilWards,
            5 => inventory.Turnstones,
            6 => inventory.EtchingNeedles,
            7 => inventory.MastersNeedles,
            8 => inventory.PinningWax,
            9 => inventory.Oathstones,
            10 => inventory.SummoningMarkers,
            GrandmasterNeedle => inventory.GrandmasterNeedles,
            _ when id >= FirstFish && Valid(id) => inventory.Fish[id - FirstFish],
            _ when id >= FirstPearl && Valid(id) => inventory.Pearls[id - FirstPearl],
            _ => Valid(id) ? inventory.Korshards[id - FirstKorshard] : 0,
        };

        /// <summary>Adds (or with a negative count takes) goods of one kind.</summary>
        public static void Add(Inventory inventory, int id, int count)
        {
            switch (id)
            {
                case 0: inventory.Potions += count; break;
                case 1: inventory.Materials += count; break;
                case 2: inventory.ScrollsOfMercy += count; break;
                case 3: inventory.KhansAlloys += count; break;
                case 4: inventory.AnvilWards += count; break;
                case 5: inventory.Turnstones += count; break;
                case 6: inventory.EtchingNeedles += count; break;
                case 7: inventory.MastersNeedles += count; break;
                case 8: inventory.PinningWax += count; break;
                case 9: inventory.Oathstones += count; break;
                case 10: inventory.SummoningMarkers += count; break;
                default:
                    if (!Valid(id)) throw new ArgumentOutOfRangeException(nameof(id));
                    if (id == GrandmasterNeedle) inventory.GrandmasterNeedles += count;
                    else if (id >= FirstFish) inventory.Fish[id - FirstFish] += count;
                    else if (id >= FirstPearl) inventory.Pearls[id - FirstPearl] += count;
                    else inventory.Korshards[id - FirstKorshard] += count;
                    break;
            }
        }
    }

    /// <summary>
    /// The bag (owner, 26 Sep 2026: "Bigger bag, Leave new drops behind, and add selling mechanic for them for sorns but not
    /// automatically"): 120 loose pieces (worn gear and the depot apart). When it is full, new drops are left behind and
    /// nothing already in it is touched; a piece can be sold to the merchant for sorn, one at a time, by hand.
    /// </summary>
    public static class Bag
    {
        public const int Size = 120;

        /// <summary>Mobs' worth of sorn a piece fetches from the merchant, by rarity (Common .. Legendary).</summary>
        private static readonly int[] RarityMobs = { 2, 4, 8, 20, 50 };

        /// <summary>
        /// What the merchant pays: the hunt's sorn for a few mobs of the piece's level (150 + 30 a level, near the lane's
        /// pay), more by rarity, and a quarter more for each forge level. A sink for junk, far below the Forge's cost.
        /// </summary>
        public static long SellPrice(ItemState item) =>
            (150L + 30L * Math.Max(1, item.ItemLevel)) * RarityMobs[Math.Max(0, Math.Min(RarityMobs.Length - 1, (int)item.Rarity))] * (4 + item.UpgradeLevel) / 4;

        /// <summary>
        /// How long an away hunt takes to fill a bag holding <paramref name="held"/> pieces, at the offline rate on the parked
        /// stage (the phone's "your bag is full" notice, 27 Sep 2026), or null when it stays short of full within the
        /// offline cap. Hour by hour with the server's own settlement, so the drops are the rules' drops.
        /// </summary>
        public static long? SecondsUntilFull(Combat.StageConfig stage, Combat.HeroStats hero, int held, IRandom rng)
        {
            if (held >= Size) return 0;
            var inventory = new Inventory();
            var carry = new Combat.HuntCarry();
            int before = 0;
            for (long hour = 0; hour * 3600 < OfflineRewards.FreeCapSeconds; hour++)
            {
                Combat.HuntYield.Settle(stage, hero, 3600, 3600, OfflineRewards.OfflineEfficiencyBp, inventory, rng, carry);
                int dropped = inventory.Loot.Count;
                if (held + dropped >= Size)
                {
                    int need = Size - held - before, gained = Math.Max(1, dropped - before);
                    return hour * 3600 + 3600L * need / gained;
                }
                before = dropped;
            }
            return null;
        }

        /// <summary>The new drops that fit a bag holding <paramref name="held"/> pieces: the best rarity first, then as they fell.</summary>
        public static List<ItemState> Fitting(int held, IEnumerable<ItemState> drops)
        {
            int room = Math.Max(0, Size - held);
            return drops.Select((d, i) => (Drop: d, Order: i)).OrderByDescending(x => (int)x.Drop.Rarity).ThenBy(x => x.Order)
                .Take(room).OrderBy(x => x.Order).Select(x => x.Drop).ToList();
        }
    }
}
