using System;
using System.Collections.Generic;
using Orsuun.Rules.Combat;

namespace Orsuun.Rules
{
    /// <summary>
    /// A character's figure (owner, 26 Sep 2026: "Second look per class"): every class has a man and a woman. The
    /// Vanguard and the Wraithsworn were drawn first as men, the Kestrel and the Drumcaller as women; the other figure
    /// wears the class's second look ("Alt" models). Chosen when a character is made; it stays through class changes.
    /// </summary>
    public enum Figure
    {
        Man = 0,
        Woman = 1,
    }

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

        /// <summary>
        /// The other classes' names (owner, 26 Sep 2026: "Names per class"), matching the looks they wear since their
        /// redesign: the Kestrel's knives and outfits, the Wraithsworn's sabres and plate, the Drumcaller's staves and robes.
        /// A piece has no class: its name follows whoever holds it (ShownClass on the client, the hero on the server).
        /// </summary>
        private static readonly Dictionary<HeroClass, string[]> ClassWeaponNames = new Dictionary<HeroClass, string[]>
        {
            [HeroClass.Kestrel] = new[]
            {
                "Twin Skinning Knives", "Rider's Talons", "Bronze Fangs", "Wolf-Fang Knives", "Silver Talons", "Hawkwing Knives",
                "Emberwake Talons", "Oathkeeper Knives", "Bone Talons", "Khan's Twin Crescents", "Talons of the Nine Oaths",
            },
            [HeroClass.Wraithsworn] = new[]
            {
                "Herder's Sabre", "Rider's Sabre", "Bronze Sabre", "Rune Sabre", "Voidedge Sabre", "Duskreaver",
                "Emberwake Sabre", "Oathkeeper Sabre", "Gravebone Sabre", "Khan's Nightblade", "Sabre of the Nine Oaths",
            },
            [HeroClass.Drumcaller] = new[]
            {
                "Feathered Staff", "Bone Charm Staff", "Bronze Bell Staff", "Wolf-Fang Staff", "Silver Crescent Staff", "Skycrystal Staff",
                "Emberorb Staff", "Eagle Staff", "Carved Bone Staff", "Dragon Crystal Staff", "Staff of the Nine Oaths",
            },
        };

        private static readonly Dictionary<HeroClass, string[]> ClassArmorNames = new Dictionary<HeroClass, string[]>
        {
            [HeroClass.Kestrel] = new[]
            {
                "Leather Jerkin", "Rider's Leathers", "Bronzescale Bodice", "Wolfhide Jerkin", "Silverplate Bodice", "Hawkwing Harness",
                "Emberweave Harness", "Oathsworn Leathers", "Gravewrought Bodice", "Khan's Silks", "Raiment of the Nine Oaths",
            },
            [HeroClass.Wraithsworn] = new[]
            {
                "Void Robe", "Padded Coat", "Bronzebound Leathers", "Wolfcollar Lamellar", "Voidsteel Plate", "Horned Plate",
                "Emberplate", "Oathsworn Plate", "Gravewrought Plate", "Khan's Nightplate", "Plate of the Nine Oaths",
            },
            [HeroClass.Drumcaller] = new[]
            {
                "Beaded Hides", "Dyed Leathers", "Bronze-Studded Hides", "Wolfskin Mantle", "Silverthread Robes", "Skysilk Regalia",
                "Emberweave Robes", "Oathsworn Mantle", "Gravebone Regalia", "Khan's Stormsilk", "Regalia of the Nine Oaths",
            },
        };

        /// <summary>The class whose names a piece shows when no class is given (the client keeps it on the playing hero).</summary>
        public static HeroClass ShownClass { get; set; } = HeroClass.Vanguard;

        public static readonly string[] ArmorNames =
        {
            "Quilted Coat", "Lamellar Coat", "Bronzescale Lamellar", "Wolfhide Lamellar", "Riveted Cuirass", "Banner Lamellar",
            "Emberplate", "Oathsworn Harness", "Gravewrought Lamellar", "Khan's Lamellar", "Harness of the Nine Oaths",
        };

        public static int MaxTier => WeaponNames.Length - 1;

        /// <summary>The figure a class was first drawn as.</summary>
        public static Figure NativeFigure(HeroClass cls) => cls == HeroClass.Kestrel || cls == HeroClass.Drumcaller ? Figure.Woman : Figure.Man;

        /// <summary>True when a character of this figure wears the class's second look.</summary>
        public static bool SecondLook(HeroClass cls, Figure figure) => figure != NativeFigure(cls);

        /// <summary>True for the slots whose look changes and shows on the character.</summary>
        public static bool HasLooks(EquipSlot slot) => slot == EquipSlot.Weapon || slot == EquipSlot.Armor;

        /// <summary>Level band of an item level: 1-9 is 0, 10-19 is 1, ... capped at the last band.</summary>
        public static int Tier(int itemLevel) => Math.Max(0, Math.Min(MaxTier, itemLevel / LevelsPerLook));

        public static string BaseName(EquipSlot slot, int itemLevel) => BaseName(slot, itemLevel, ShownClass);

        public static string BaseName(EquipSlot slot, int itemLevel, HeroClass cls) =>
            slot == EquipSlot.Weapon ? (ClassWeaponNames.TryGetValue(cls, out string[]? w) ? w : WeaponNames)[Tier(itemLevel)]
            : slot == EquipSlot.Armor ? (ClassArmorNames.TryGetValue(cls, out string[]? a) ? a : ArmorNames)[Tier(itemLevel)]
            : Content.SlotBaseNames[(int)slot];

        /// <summary>The model the client shows for an item, e.g. "Weapon_T2"; null for stat-only slots.</summary>
        public static string? LookId(ItemState item) => HasLooks(item.Slot) ? item.Slot + "_T" + Tier(item.ItemLevel) : null;
    }
}
