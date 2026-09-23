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

        public ItemState(int itemLevel, Rarity rarity, EquipSlot slot = EquipSlot.Weapon)
        {
            ItemLevel = itemLevel;
            Rarity = rarity;
            Slot = slot;
            Sockets = new Socket[SocketRules.SocketCount(rarity)];
        }

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
    }
}
