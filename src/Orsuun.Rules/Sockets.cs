#nullable enable
using System;

namespace Orsuun.Rules
{
    /// <summary>
    /// What a set Korshard lends its bearer: one dead soldier's trait. Weapon shards sharpen the attack,
    /// armor shards harden the body. Class-slayer shards (PvP) arrive with the Pits.
    /// </summary>
    public enum ShardType
    {
        /// <summary>Weapon: +damage against everything that is not a Commander.</summary>
        BeastSlayer = 0,
        /// <summary>Weapon: flat attack.</summary>
        Piercer = 1,
        /// <summary>Weapon: heavier critical hits.</summary>
        Deathdealer = 2,
        /// <summary>Armor: defense.</summary>
        Bulwark = 10,
        /// <summary>Armor: max HP.</summary>
        Vigor = 11,
        /// <summary>Armor: chance to take no damage from a hit.</summary>
        Evasion = 12,
        /// <summary>Armor: attack speed.</summary>
        Haste = 13,
        /// <summary>Armor: less damage from Commanders and their mechanics.</summary>
        Warding = 14,
    }

    /// <summary>One socket on an item: empty, holding a shard of a rank, or blocked by a Dead Shard.</summary>
    public readonly struct Socket
    {
        public static readonly Socket Empty = default;
        public static readonly Socket DeadShard = new Socket(null, 0, true);

        public Socket(ShardType? type, int rank, bool dead = false)
        {
            Type = type;
            Rank = rank;
            Dead = dead;
        }

        public ShardType? Type { get; }
        /// <summary>0 = Trooper .. 4 = Guard of the Khan.</summary>
        public int Rank { get; }
        public bool Dead { get; }
        public bool IsEmpty => Type == null && !Dead;
    }

    public static class SocketRules
    {
        public const int InsertSuccessBp = 7000;
        public const int RankCount = 5;

        /// <summary>Sockets by rarity, GDD section 5: Common 1, Uncommon and Rare 2, Epic and Legendary 3.</summary>
        public static int SocketCount(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Common: return 1;
                case Rarity.Uncommon:
                case Rarity.Rare: return 2;
                default: return 3;
            }
        }

        public static bool IsWeaponShard(ShardType type) => (int)type < 10;

        public static bool Fits(ShardType type, EquipSlot slot) => IsWeaponShard(type) == (slot == EquipSlot.Weapon);

        /// <summary>Sorn to knock a Dead Shard out of a socket.</summary>
        public static long ClearCost(ItemState item) => 5_000L * item.ItemLevel;

        public static readonly ShardType[] WeaponShards = { ShardType.BeastSlayer, ShardType.Piercer, ShardType.Deathdealer };
        public static readonly ShardType[] ArmorShards = { ShardType.Bulwark, ShardType.Vigor, ShardType.Evasion, ShardType.Haste, ShardType.Warding };

        public static ShardType[] ShardsFor(EquipSlot slot) => slot == EquipSlot.Weapon ? WeaponShards : ArmorShards;

        /// <summary>Bonus of a shard by rank index 0..4, in the unit named on the type.</summary>
        public static int Value(ShardType type, int rank)
        {
            switch (type)
            {
                case ShardType.BeastSlayer: return 4 * (rank + 1);       // percent damage vs non-bosses
                case ShardType.Piercer: return 5 * (rank + 1);           // flat attack
                case ShardType.Deathdealer: return 10 * (rank + 1);      // crit multiplier points
                case ShardType.Bulwark: return 3 * (rank + 1);           // defense
                case ShardType.Vigor: return 150 * (rank + 1);           // max HP
                case ShardType.Evasion: return 200 * (rank + 1);         // basis points
                case ShardType.Haste: return 4 * (rank + 1);             // percent attack speed
                case ShardType.Warding: return 5 * (rank + 1);           // percent less damage from Commanders
                default: return 0;
            }
        }

        public static string Name(ShardType type)
        {
            switch (type)
            {
                case ShardType.BeastSlayer: return "Beast-Slayer";
                default: return type.ToString();
            }
        }

        public static string Describe(ShardType type, int rank)
        {
            int v = Value(type, rank);
            switch (type)
            {
                case ShardType.BeastSlayer: return "+" + v + "% vs Hollowed";
                case ShardType.Piercer: return "+" + v + " attack";
                case ShardType.Deathdealer: return "+" + v + "% crit damage";
                case ShardType.Bulwark: return "+" + v + " defense";
                case ShardType.Vigor: return "+" + v + " HP";
                case ShardType.Evasion: return "+" + v / 100 + "% evasion";
                case ShardType.Haste: return "+" + v + "% attack speed";
                case ShardType.Warding: return "-" + v + "% Commander damage";
                default: return "";
            }
        }
    }

    public sealed class SocketService
    {
        /// <summary>Null when the insert may run, otherwise the reason.</summary>
        public string? InsertBlocker(ItemState item, int socketIndex, ShardType type, int rank, Inventory inventory)
        {
            if (item.Destroyed) return "Item was destroyed";
            if (socketIndex < 0 || socketIndex >= item.Sockets.Length) return "No such socket";
            if (!item.Sockets[socketIndex].IsEmpty) return item.Sockets[socketIndex].Dead ? "Socket holds a Dead Shard" : "Socket is taken";
            if (!SocketRules.Fits(type, item.Slot)) return SocketRules.Name(type) + " does not fit this slot";
            if (rank < 0 || rank >= SocketRules.RankCount) return "No such rank";
            if (inventory.Korshards[rank] <= 0) return "No " + Content.KorshardRanks[rank] + " shard";
            return null;
        }

        /// <summary>
        /// Sets a shard: 70% it takes, 30% it shatters and a Dead Shard blocks the socket. The shard is spent either way.
        /// </summary>
        public bool TryInsert(ItemState item, int socketIndex, ShardType type, int rank, Inventory inventory, IRandom rng)
        {
            string? blocker = InsertBlocker(item, socketIndex, type, rank, inventory);
            if (blocker != null) throw new InvalidOperationException(blocker);

            inventory.Korshards[rank]--;
            bool ok = rng.RollBp(SocketRules.InsertSuccessBp);
            item.Sockets[socketIndex] = ok ? new Socket(type, rank) : Socket.DeadShard;
            return ok;
        }

        public string? ClearBlocker(ItemState item, int socketIndex, Inventory inventory)
        {
            if (socketIndex < 0 || socketIndex >= item.Sockets.Length) return "No such socket";
            if (!item.Sockets[socketIndex].Dead) return "Nothing dead in that socket";
            if (inventory.Sorn < SocketRules.ClearCost(item)) return "Not enough sorn";
            return null;
        }

        /// <summary>Knocks a Dead Shard out for sorn; the socket is empty again.</summary>
        public void Clear(ItemState item, int socketIndex, Inventory inventory)
        {
            string? blocker = ClearBlocker(item, socketIndex, inventory);
            if (blocker != null) throw new InvalidOperationException(blocker);
            inventory.Sorn -= SocketRules.ClearCost(item);
            item.Sockets[socketIndex] = Socket.Empty;
        }
    }
}
