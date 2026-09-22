#nullable enable
using System;
using System.Collections.Generic;

namespace Orsuun.Rules.Combat
{
    /// <summary>
    /// What a kill drops. The live lane and the server's time settlement both call this, so a player
    /// earns the same loot per pack whether the client was watching or not.
    /// </summary>
    public static class HuntYield
    {
        public const int PotionDropBp = 800;
        public const int MaterialDropBp = 300;
        public const int KorstoneSornPacks = 20;
        public const int KorstoneScrollBp = 5000;
        public const int KorstoneAlloyBp = 1000;
        public const int AutoCastMultiplierPercent = 150;

        public const int GearDropBp = 400;

        public static void LootMob(StageConfig stage, Inventory inventory, IRandom rng, out string? drop)
        {
            inventory.Sorn += stage.SornPerMob * (80 + rng.NextInt(41)) / 100;
            drop = null;
            if (rng.RollBp(PotionDropBp))
            {
                inventory.Potions++;
                drop = "+1 Bloodroot Draught";
            }
            if (rng.RollBp(MaterialDropBp))
            {
                inventory.Materials++;
                drop = "+1 " + stage.MaterialName;
            }
            if (rng.RollBp(GearDropBp))
                drop = DropGear(stage, inventory, rng, stage.GearRarityCap).DisplayName;
        }

        public static string LootBoss(StageConfig stage, Inventory inventory, IRandom rng)
        {
            long sorn = stage.SornPerMob * KorstoneSornPacks * 2;
            inventory.Sorn += sorn;
            inventory.Materials += 3;
            ItemState gear = DropGear(stage, inventory, rng, Rarity.Legendary, minimum: Rarity.Rare);
            string text = stage.BossName + " falls: +" + sorn + " sorn, +3 " + stage.MaterialName + ", " + gear.DisplayName;
            if (rng.RollBp(KorstoneAlloyBp * 3))
            {
                inventory.KhansAlloys++;
                text += ", +1 Khan's Alloy";
            }
            return text;
        }

        /// <summary>Rolls one piece of gear for the stage's level band and adds it to the loot list.</summary>
        public static ItemState DropGear(StageConfig stage, Inventory inventory, IRandom rng, Rarity cap, Rarity minimum = Rarity.Common)
        {
            Rarity rarity = Content.RollRarity(rng, cap);
            if (rarity < minimum) rarity = minimum;
            var slot = (EquipSlot)rng.NextInt(8);
            var item = new ItemState(Math.Max(1, stage.GearItemLevel), rarity, slot);

            EtchingPool pool = EtchingPool.For(slot);
            int count = Content.EtchingsAtDrop(rarity, rng);
            var taken = new HashSet<int>();
            for (int i = 0; i < count; i++)
            {
                int entry;
                do entry = rng.NextInt(pool.Entries.Count); while (!taken.Add(entry));
                int tier = EtchingRules.RollTier(rarity, rng);
                item.Etchings.Add(new Etching(entry, tier, pool.Entries[entry].TierValues[tier - 1]));
            }

            inventory.Loot.Add(item);
            return item;
        }

        public static string LootKorstone(StageConfig stage, Inventory inventory, IRandom rng)
        {
            long sorn = stage.SornPerMob * KorstoneSornPacks;
            int turnstones = 1 + rng.NextInt(3);
            int materials = 1 + rng.NextInt(2);
            inventory.Sorn += sorn;
            inventory.Turnstones += turnstones;
            inventory.Materials += materials;

            string text = "Korstone chest: +" + sorn + " sorn, +" + turnstones + " Turnstone, +" + materials + " Wolf Sinew";
            if (rng.RollBp(KorstoneScrollBp))
            {
                inventory.ScrollsOfMercy++;
                text += ", +1 Scroll of Mercy";
            }
            if (rng.RollBp(KorstoneAlloyBp))
            {
                inventory.KhansAlloys++;
                text += ", +1 Khan's Alloy";
            }
            return text;
        }

        /// <summary>
        /// Credits a stretch of unobserved hunting: the hero kills what its DPS allows, at the given
        /// efficiency (10000 = live rate, 6000 = offline rate), for at most capSeconds.
        /// </summary>
        public static HuntSettlement Settle(StageConfig stage, HeroStats hero, long seconds, long capSeconds, int efficiencyBp, Inventory inventory, IRandom rng)
        {
            long counted = seconds < 0 ? 0 : seconds > capSeconds ? capSeconds : seconds;
            long avgPack = (stage.PackSizeMin + stage.PackSizeMax) / 2;
            long packHp = stage.MobHp * avgPack;
            // Auto-cast skills add about half again over plain attacks in the live lane (measured by Orsuun.Sim);
            // the run-in is added on top. Re-measure this constant whenever the skill kit changes.
            long ticksPerPack = hero.Attack <= 0
                ? long.MaxValue
                : packHp * hero.AttackIntervalTicks * 100 / (hero.Attack * AutoCastMultiplierPercent) + stage.RunTicks;
            long packs = ticksPerPack == long.MaxValue ? 0 : counted * LaneSim.TicksPerSecond * efficiencyBp / RandomExtensions.FullBp / ticksPerPack;
            long korstones = packs / (stage.PacksBeforeKorstone + 1);
            packs -= korstones;

            long sornBefore = inventory.Sorn;
            for (long p = 0; p < packs; p++)
                for (long m = 0; m < avgPack; m++)
                    LootMob(stage, inventory, rng, out _);
            for (long k = 0; k < korstones; k++)
            {
                if (stage.FinalEncounter == FinalEncounter.Boss) LootBoss(stage, inventory, rng);
                else LootKorstone(stage, inventory, rng);
            }

            return new HuntSettlement(counted, packs, korstones, inventory.Sorn - sornBefore);
        }
    }

    public readonly struct HuntSettlement
    {
        public HuntSettlement(long countedSeconds, long packs, long korstones, long sornEarned)
        {
            CountedSeconds = countedSeconds;
            Packs = packs;
            Korstones = korstones;
            SornEarned = sornEarned;
        }

        public long CountedSeconds { get; }
        public long Packs { get; }
        public long Korstones { get; }
        public long SornEarned { get; }
    }
}
