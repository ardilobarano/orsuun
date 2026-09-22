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

        public ItemState(int itemLevel, Rarity rarity)
        {
            ItemLevel = itemLevel;
            Rarity = rarity;
        }

        public int ItemLevel { get; }
        public Rarity Rarity { get; }
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
