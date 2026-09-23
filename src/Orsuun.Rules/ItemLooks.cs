using System;

namespace Orsuun.Rules
{
    /// <summary>
    /// Item catalog for the visible slots (owner, 23 Sep 2026): only the weapon and the body armour show on the
    /// character, and their look changes with the item's level band, a new look every <see cref="LevelsPerLook"/>
    /// levels (1-9, 10-19, ... 100-105). Stat-only slots keep one base name each and have no look.
    /// These are the Vanguard's tables; other classes get their own when class selection exists.
    /// </summary>
    public static class ItemLooks
    {
        public const int LevelsPerLook = 10;

        public static readonly string[] WeaponNames =
        {
            "Herder's Glaive", "Rider's Glaive", "Horsebreaker Glaive", "Tamga Glaive", "Crescent Glaive", "Banner Glaive",
            "Emberwake Glaive", "Oathkeeper Glaive", "Khan's Crescent", "Korstone Glaive", "Glaive of the Nine Oaths",
        };

        public static readonly string[] ArmorNames =
        {
            "Quilted Coat", "Lamellar Coat", "Bronzescale Lamellar", "Wolfhide Lamellar", "Riveted Cuirass", "Banner Lamellar",
            "Emberplate", "Oathsworn Harness", "Khan's Lamellar", "Korstone Plate", "Harness of the Nine Oaths",
        };

        public static int MaxTier => WeaponNames.Length - 1;

        /// <summary>True for the slots whose look changes and shows on the character.</summary>
        public static bool HasLooks(EquipSlot slot) => slot == EquipSlot.Weapon || slot == EquipSlot.Armor;

        /// <summary>Level band of an item level: 1-9 is 0, 10-19 is 1, ... capped at the last band.</summary>
        public static int Tier(int itemLevel) => Math.Max(0, Math.Min(MaxTier, itemLevel / LevelsPerLook));

        public static string BaseName(EquipSlot slot, int itemLevel) =>
            slot == EquipSlot.Weapon ? WeaponNames[Tier(itemLevel)]
            : slot == EquipSlot.Armor ? ArmorNames[Tier(itemLevel)]
            : Content.SlotBaseNames[(int)slot];

        /// <summary>The model the client shows for an item, e.g. "Weapon_T2"; null for stat-only slots.</summary>
        public static string? LookId(ItemState item) => HasLooks(item.Slot) ? item.Slot + "_T" + Tier(item.ItemLevel) : null;
    }
}
