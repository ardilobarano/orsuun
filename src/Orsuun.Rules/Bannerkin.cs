#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Orsuun.Rules.Combat;

namespace Orsuun.Rules
{
    /// <summary>What the Bannerkin brings to a fight, from the six pieces it wears (Bannerkin.Stats).</summary>
    public sealed class KinStats
    {
        /// <summary>The kin's worth: the average of its six pieces' item level by rarity and forge level (empty slots 0).</summary>
        public int Score { get; set; }
        /// <summary>Hunter's Blessing: crit chance added for its ticks, on its cooldown.</summary>
        public int FocusBp { get; set; }
        public int FocusTicks { get; set; }
        public int FocusCooldownTicks { get; set; }
        /// <summary>Mending Song: this share of the hero's HP back, on its cooldown, whenever he is hurt.</summary>
        public int HealPercent { get; set; }
        public int HealCooldownTicks { get; set; }

        /// <summary>The hunt's pace estimate (HuntYield.Settle): the blessing's crits over its share of the time, in percent.</summary>
        public int HuntPercent => FocusCooldownTicks <= 0 ? 0 : FocusBp / 100 * FocusTicks / FocusCooldownTicks;
    }

    /// <summary>
    /// The Bannerkin (owner, 28 Sep 2026: picked "Bannerkin companion"; GDD section 12: "A Drumcaller companion who walks
    /// the lane behind the hero, casts Hunter's Blessing and Mending Song on cooldown, and has 6 gear slots with its own
    /// Forge risk ... Bannerkin gear is a separate market"). It joins at level 25 with a plain set; its pieces (drum,
    /// robe, hat, charm, bracer, boots) are items of their own that only it wears, forge like any piece (Oathbreak and
    /// all), trade on the Exchange and drop from map bosses and Commander chests. Its six pieces make its score; the score
    /// sets how much crit its Hunter's Blessing lends and how much HP its Mending Song gives back. Both casts are the
    /// lane's (LaneSim), on their own clocks, with no roll of their own, so replays stay exact. Duels and the Pits leave
    /// it out, like the wardrobe. Assumptions (not stated by the owner): the level it joins, its six slots, the numbers,
    /// where its gear drops and that its gear carries no etchings or sockets.
    /// </summary>
    public static class Bannerkin
    {
        public const int JoinLevel = 25;

        /// <summary>Its six slots, as item slots: drum, robe, hat, charm, bracer, boots.</summary>
        public static readonly EquipSlot[] Slots =
        {
            EquipSlot.Weapon, EquipSlot.Armor, EquipSlot.Helmet, EquipSlot.Necklace, EquipSlot.Bracelet, EquipSlot.Shoes,
        };

        public static bool Wears(EquipSlot slot) => Array.IndexOf(Slots, slot) >= 0;

        public static string SlotName(EquipSlot slot) => slot switch
        {
            EquipSlot.Weapon => "Drum",
            EquipSlot.Armor => "Robe",
            EquipSlot.Helmet => "Hat",
            EquipSlot.Necklace => "Charm",
            EquipSlot.Bracelet => "Bracer",
            _ => "Boots",
        };

        public const int FocusSeconds = 8, FocusCooldownSeconds = 18, HealCooldownSeconds = 12;
        public const int MaxFocusBp = 4000, MaxHealPercent = 25;

        /// <summary>A piece's worth: item level, by rarity, by forge level (a +9 is 168% of its +0).</summary>
        public static int PieceScore(ItemState piece)
        {
            int rarity = piece.Rarity switch
            {
                Rarity.Uncommon => 100, Rarity.Rare => 110, Rarity.Epic => 125, Rarity.Legendary => 145, _ => 90,
            };
            return piece.ItemLevel * rarity / 100 * ForgeRules.StatPercent(piece) / 100;
        }

        /// <summary>The kin's stats from what it wears, or null for no Bannerkin.</summary>
        public static KinStats? Stats(IEnumerable<ItemState>? worn)
        {
            if (worn == null) return null;
            var pieces = worn.Where(p => p != null && p.Kin && !p.Destroyed && Wears(p.Slot)).ToList();
            if (pieces.Count == 0) return null;
            int score = pieces.Sum(PieceScore) / Slots.Length;
            return new KinStats
            {
                Score = score,
                FocusBp = Math.Min(MaxFocusBp, 800 + score * 20),
                FocusTicks = FocusSeconds * LaneSim.TicksPerSecond,
                FocusCooldownTicks = FocusCooldownSeconds * LaneSim.TicksPerSecond,
                HealPercent = Math.Min(MaxHealPercent, 3 + score / 10),
                HealCooldownTicks = HealCooldownSeconds * LaneSim.TicksPerSecond,
            };
        }

        /// <summary>A Bannerkin piece: no etchings, no sockets, no weapon rolls.</summary>
        public static ItemState NewPiece(int itemLevel, Rarity rarity, EquipSlot slot) => new ItemState(Math.Max(1, itemLevel), rarity, slot, kin: true);

        /// <summary>The plain set it joins with: six Rare pieces of the hero's level band.</summary>
        public static List<ItemState> StarterSet(int heroLevel)
        {
            int level = Math.Max(1, heroLevel / 10 * 10);
            return Slots.Select(s => NewPiece(level, Rarity.Rare, s)).ToList();
        }

        /// <summary>A map boss's Bannerkin piece: a quarter of the time, Rare or better.</summary>
        public const int BossDropBp = 2500;

        /// <summary>A Commander chest's piece by rank: half the time from the Commander's own, a quarter from an Officer's.</summary>
        public static int CommanderDropBp(int rank) => rank == 1 ? 5000 : rank <= 5 ? 2500 : 1000;

        /// <summary>Rolls a Bannerkin piece into the loot: the chance first, then its rarity and slot.</summary>
        public static ItemState? RollDrop(int itemLevel, int chanceBp, Rarity minimum, Rarity cap, Inventory inventory, IRandom rng)
        {
            if (!rng.RollBp(chanceBp)) return null;
            Rarity rarity = Content.RollRarity(rng, cap);
            if (rarity < minimum) rarity = minimum;
            ItemState piece = NewPiece(itemLevel, rarity, Slots[rng.NextInt(Slots.Length)]);
            inventory.Loot.Add(piece);
            return piece;
        }
    }
}
