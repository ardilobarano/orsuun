#nullable enable
using System.Collections.Generic;

namespace Orsuun.Rules
{
    public enum Rarity
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
        Epic = 3,
        Legendary = 4,
    }

    public enum EquipSlot
    {
        Weapon = 0,
        Armor = 1,
        Helmet = 2,
        Shield = 3,
        Bracelet = 4,
        Necklace = 5,
        Earrings = 6,
        Shoes = 7,
    }

    /// <summary>One rolled etching on an item. Tier is 1 to 5.</summary>
    public readonly struct Etching
    {
        public Etching(int entryId, int tier, int value)
        {
            EntryId = entryId;
            Tier = tier;
            Value = value;
        }

        public int EntryId { get; }
        public int Tier { get; }
        public int Value { get; }
    }

    /// <summary>The mutable state of one piece of gear that the Forge and etching rules act on.</summary>
    public sealed class ItemState
    {
        public const int MaxUpgradeLevel = 9;
        public const int MaxEtchings = 5;
        /// <summary>With a Grandmaster's Needle an Epic or Legendary piece may hold a sixth (Rules.EtchingActions).</summary>
        public const int SixthEtching = 6;

        public ItemState(int itemLevel, Rarity rarity, EquipSlot slot = EquipSlot.Weapon, bool kin = false)
        {
            ItemLevel = itemLevel;
            Rarity = rarity;
            Slot = slot;
            Kin = kin;
            Sockets = kin ? new Socket[0] : new Socket[SocketRules.SocketCount(rarity)];
        }

        /// <summary>A Bannerkin piece (Rules.Bannerkin): only the Bannerkin wears it; no etchings, sockets or rolls.</summary>
        public bool Kin { get; }

        /// <summary>Korshard sockets, count fixed by rarity.</summary>
        public Socket[] Sockets { get; }

        public int ItemLevel { get; }
        public Rarity Rarity { get; }
        public EquipSlot Slot { get; }

        /// <summary>Rarity name plus the slot's base name for the item's level band, e.g. "Rare Rider's Glaive".</summary>
        public string DisplayName => Content.ItemName(this);

        /// <summary>Model the client shows for this item (weapon and armour only), e.g. "Armor_T1"; null otherwise.</summary>
        public string? LookId => ItemLooks.LookId(this);
        public int UpgradeLevel { get; set; }

        /// <summary>Forgemaster's Patience: bonus chance earned by failures at +7 and above, in basis points.</summary>
        public int PatienceBp { get; set; }

        public List<Etching> Etchings { get; } = new List<Etching>(MaxEtchings);

        /// <summary>Index into Etchings held by Pinning Wax, or -1.</summary>
        public int LockedEtchingIndex { get; set; } = -1;

        /// <summary>True after an Oathbreak. A destroyed item accepts no further operations.</summary>
        public bool Destroyed { get; set; }

        /// <summary>Temper steps past +9 (Rules.Tempering): each adds 1% to the base stats.</summary>
        public int Temper { get; set; }

        /// <summary>A weapon's average damage roll in percent (WeaponRolls): plain attacks hit this much harder or softer.</summary>
        public int AverageDamagePercent { get; set; }

        /// <summary>A weapon's skill damage roll in percent (WeaponRolls): skills hit this much harder or softer.</summary>
        public int SkillDamagePercent { get; set; }
    }

    /// <summary>
    /// Average damage and skill damage (owner, 26 Sep 2026, Metin2's "ortalama zarar" and skill damage): every weapon of
    /// item level 30 and up rolls both when it drops and keeps them. Average damage runs from -30% to +60% and skill damage
    /// from -15% to +30%, weighted so that the top is nearly impossible: average damage centres on +10% (a quarter of
    /// weapons roll below zero, 6% reach +30%, one in a thousand +50%, about one in 60,000 +60%); skill damage the same
    /// at half the scale (one in 30,000 reaches +30%). The weights are a split bell curve, fixed here as integers.
    /// </summary>
    public static class WeaponRolls
    {
        public const int FromItemLevel = 30;
        public const int AverageMin = -30, AverageMax = 60;
        public const int SkillMin = -15, SkillMax = 30;

        /// <summary>Weights of -30% .. +60% average damage (mode +10%, spread 16 below and 13 above).</summary>
        private static readonly int[] AverageWeights =
        {
            4394, 5127, 5959, 6899, 7956, 9139, 10458, 11920, 13534, 15306, 17242, 19348, 21627, 24079, 26705, 29502, 32465,
            35587, 38856, 42260, 45783, 49407, 53110, 56867, 60653, 64439, 68194, 71887, 75484, 78952, 82258, 85368, 88250,
            90873, 93210, 95234, 96923, 98258, 99222, 99805, 100000, 99705, 98824, 97372, 95377, 92870, 89897, 86505, 82750,
            78691, 74389, 69908, 65309, 60653, 55996, 51392, 46889, 42527, 38344, 34368, 30623, 27124, 23884, 20907, 18193,
            15738, 13534, 11569, 9832, 8306, 6976, 5824, 4834, 3988, 3271, 2667, 2162, 1742, 1395, 1111, 879, 692, 541, 421,
            325, 250, 191, 145, 110, 82, 61,
        };

        /// <summary>Weights of -15% .. +30% skill damage (mode +5%, spread 8 below and 6.5 above).</summary>
        private static readonly int[] SkillWeights =
        {
            4394, 5959, 7956, 10458, 13534, 17242, 21627, 26705, 32465, 38856, 45783, 53110, 60653, 68194, 75484, 82258, 88250,
            93210, 96923, 99222, 100000, 98824, 95377, 89897, 82750, 74389, 65309, 55996, 46889, 38344, 30623, 23884, 18193,
            13534, 9832, 6976, 4834, 3271, 2162, 1395, 879, 541, 325, 191, 110, 61,
        };

        public static bool Applies(ItemState item) => item.Slot == EquipSlot.Weapon && item.ItemLevel >= FromItemLevel;

        /// <summary>Rolls both on a weapon of item level 30 or more (nothing else changes, nothing is drawn otherwise).</summary>
        public static void Roll(ItemState item, IRandom rng)
        {
            if (!Applies(item)) return;
            item.AverageDamagePercent = AverageMin + Pick(AverageWeights, rng);
            item.SkillDamagePercent = SkillMin + Pick(SkillWeights, rng);
        }

        /// <summary>The chance of a roll of exactly <paramref name="value"/>, in parts per million (for tests and odds).</summary>
        public static int AveragePpm(int value) => Ppm(AverageWeights, value - AverageMin);

        public static int SkillPpm(int value) => Ppm(SkillWeights, value - SkillMin);

        private static int Pick(int[] weights, IRandom rng)
        {
            int total = 0;
            foreach (int w in weights) total += w;
            int roll = rng.NextInt(total);
            for (int i = 0; i < weights.Length; i++)
            {
                roll -= weights[i];
                if (roll < 0) return i;
            }
            return weights.Length - 1;
        }

        private static int Ppm(int[] weights, int index)
        {
            if (index < 0 || index >= weights.Length) return 0;
            long total = 0;
            foreach (int w in weights) total += w;
            return (int)(weights[index] * 1_000_000L / total);
        }
    }
}
