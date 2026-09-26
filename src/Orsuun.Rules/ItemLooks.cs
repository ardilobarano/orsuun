using System;

namespace Orsuun.Rules
{
    /// <summary>
    /// Item catalog for the visible slots (owner, 23 Sep 2026): only the weapon and the body armour show on the
    /// character, and their look changes with the item's level band, a new look every <see cref="LevelsPerLook"/>
    /// levels (1-9, 10-19, ... 100-105). Stat-only slots keep one base name each and have no look.
    /// These are the Vanguard's tables; other classes get their own when class selection exists. Bands 8 and 9 were named
    /// after their art (25 Sep 2026): the Colossus Graves' bone-and-iron Gravewrought, the Khan's guard in black and gold.
    /// Since the Vanguard's redesign (owner, 26 Sep 2026: "glaive sword one handed two handed", mixed by level) his weapons
    /// take turns by band: a one-handed sword, a glaive, a two-handed greatsword (<see cref="WeaponKinds"/>).
    /// </summary>
    /// <summary>The Vanguard's weapon kinds: a glaive stands from the ground past his head, swords rise from his fist.</summary>
    public enum WeaponKind
    {
        Glaive = 0,
        Sword = 1,
        Greatsword = 2,
    }

    public static class ItemLooks
    {
        public const int LevelsPerLook = 10;

        public static readonly string[] WeaponNames =
        {
            "Herder's Sword", "Rider's Glaive", "Horsebreaker Greatsword", "Tamga Sword", "Crescent Glaive", "Banner Greatsword",
            "Emberwake Sword", "Oathkeeper Glaive", "Colossus Greatsword", "Khan's Crescent", "Glaive of the Nine Oaths",
        };

        /// <summary>What each band's weapon is, as the Vanguard holds it (the client fits the model to the hand by it).</summary>
        public static readonly WeaponKind[] WeaponKinds =
        {
            WeaponKind.Sword, WeaponKind.Glaive, WeaponKind.Greatsword, WeaponKind.Sword, WeaponKind.Glaive, WeaponKind.Greatsword,
            WeaponKind.Sword, WeaponKind.Glaive, WeaponKind.Greatsword, WeaponKind.Sword, WeaponKind.Glaive,
        };

        public static WeaponKind KindOf(int itemLevel) => WeaponKinds[Tier(itemLevel)];

        public static readonly string[] ArmorNames =
        {
            "Quilted Coat", "Lamellar Coat", "Bronzescale Lamellar", "Wolfhide Lamellar", "Riveted Cuirass", "Banner Lamellar",
            "Emberplate", "Oathsworn Harness", "Gravewrought Lamellar", "Khan's Lamellar", "Harness of the Nine Oaths",
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
