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
        public const int GearDropBp = 400;
        public const int KorstoneSornPacks = 20;
        public const int KorstoneScrollBp = 5000;
        public const int KorstoneAlloyBp = 1000;
        public const int KorstoneNeedleBp = 2500;
        public const int ElderMarkerBp = 200;
        public const int AutoCastMultiplierPercent = 150;

        public static void LootMob(StageConfig stage, Inventory inventory, IRandom rng, out string? drop)
        {
            inventory.Sorn += stage.SornPerMob * (80 + rng.NextInt(41)) / 100;
            inventory.Xp += stage.XpPerMob * stage.XpPercent / 100;
            drop = null;
            if (rng.RollBp(PotionDropBp))
            {
                inventory.Potions++;
                drop = "+1 Bloodroot Draught";
            }
            if (rng.RollBp(MaterialDropBp * stage.MaterialYieldPercent / 100 * stage.MaterialPercent / 100))
            {
                inventory.Materials++;
                drop = "+1 " + stage.MaterialName;
            }
            if (rng.RollBp(GearDropBp))
                drop = DropGear(stage, inventory, rng, stage.GearRarityCap).DisplayName;
        }

        public static string LootKorstone(StageConfig stage, Inventory inventory, IRandom rng, bool elder = false)
        {
            // Field tier, Elder status and the Korstone Bell all scale the chest; the Material Bell scales materials again.
            int yield = stage.MaterialYieldPercent * stage.KorstoneChestPercent / 100;
            int mult = elder ? 3 : 1;
            long sorn = stage.SornPerMob * KorstoneSornPacks * mult * stage.KorstoneChestPercent / 100;
            int turnstones = (1 + rng.NextInt(3)) * yield / 100 * mult;
            int materials = (1 + rng.NextInt(2)) * yield / 100 * mult * stage.MaterialPercent / 100;
            inventory.Sorn += sorn;
            inventory.Turnstones += turnstones;
            inventory.Materials += materials;

            string text = (elder ? "Elder Korstone chest: +" : "Korstone chest: +") + sorn + " sorn, +" + turnstones + " Turnstone, +" + materials + " " + stage.MaterialName;
            if (stage.Zone == ZoneType.KorstoneField)
            {
                inventory.Korshards[stage.KorshardRank]++;
                text += ", +1 " + Content.KorshardRanks[stage.KorshardRank] + " shard";
                if (rng.RollBp(KorstoneNeedleBp))
                {
                    inventory.EtchingNeedles++;
                    text += ", +1 Etching Needle";
                }
            }
            if (elder || rng.RollBp(KorstoneScrollBp * yield / 100))
            {
                inventory.ScrollsOfMercy++;
                text += ", +1 Scroll of Mercy";
            }
            if (rng.RollBp(KorstoneAlloyBp * yield / 100))
            {
                inventory.KhansAlloys++;
                text += ", +1 Khan's Alloy";
            }
            if (elder && rng.RollBp(ElderMarkerBp))
            {
                inventory.SummoningMarkers++;
                text += ", +1 Summoning Marker";
            }
            return text;
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
            // A piece for the Bannerkin (Rules.Bannerkin), a quarter of the time.
            ItemState? kin = Bannerkin.RollDrop(stage.GearItemLevel, Bannerkin.BossDropBp, Rarity.Rare, Rarity.Legendary, inventory, rng);
            if (kin != null) text += ", " + kin.DisplayName;
            // Bosses from Gorak Pass's level on can drop a short wardrobe piece.
            if (stage.GearItemLevel >= Wardrobe.MinDropLevel)
            {
                string? piece = Wardrobe.RollDrop(inventory, Wardrobe.BossDropBp, rng);
                if (piece != null) text += ", " + piece;
            }
            return text;
        }

        /// <summary>
        /// A Commander chest by damage rank (GDD section 13): rank 1 the Commander's chest, 2-5 an Officer's,
        /// 6-20 a Trooper's, beyond that nothing. The Commander's wardrobe trophy comes only from the first two.
        /// </summary>
        /// <summary>A Grandmaster's Needle in a Commander's own chest (rank 1), in basis points.</summary>
        public const int GrandmasterNeedleBp = 500;

        public static string LootCommander(BossDef boss, int rank, Inventory inventory, IRandom rng)
        {
            if (rank > 20) return "No chest: rank " + rank + " on " + boss.Name;

            string chest = rank == 1 ? "Commander's chest" : rank <= 5 ? "Officer's chest" : "Trooper's chest";
            int tierMult = boss.Tier;
            long sorn = (rank == 1 ? 30_000L : rank <= 5 ? 15_000L : 6_000L) * tierMult;
            int materials = (rank == 1 ? 6 : rank <= 5 ? 3 : 1) * tierMult;
            inventory.Sorn += sorn;
            inventory.Materials += materials;
            string text = chest + " from " + boss.Name + ": +" + sorn + " sorn, +" + materials + " materials";

            int legendaryBp = rank == 1 ? 1000 : rank <= 5 ? 300 : 0;
            int alloyBp = rank == 1 ? 5000 : rank <= 5 ? 2500 : 1000;

            var stage = new StageConfig { GearItemLevel = Content.CommanderGearLevel(boss) };
            ItemState gear = DropGear(stage, inventory, rng, rng.RollBp(legendaryBp) ? Rarity.Legendary : Rarity.Epic, minimum: rank <= 5 ? Rarity.Epic : Rarity.Rare);
            text += ", " + gear.DisplayName;
            if (rng.RollBp(alloyBp))
            {
                inventory.KhansAlloys++;
                text += ", +1 Khan's Alloy";
            }
            // The Commander's own chest may hold a Grandmaster's Needle (the sixth etching).
            if (rank == 1 && rng.RollBp(GrandmasterNeedleBp))
            {
                inventory.GrandmasterNeedles++;
                text += ", +1 Grandmaster's Needle";
            }
            ItemState? kin = Bannerkin.RollDrop(stage.GearItemLevel, Bannerkin.CommanderDropBp(rank), rank <= 5 ? Rarity.Epic : Rarity.Rare, Rarity.Legendary, inventory, rng);
            if (kin != null) text += ", " + kin.DisplayName;
            // The Commander's own trophy, a short wardrobe skin (the old permanent trophy names stay in Inventory.Skins).
            string? trophy = Wardrobe.RollDrop(inventory, Wardrobe.CommanderDropBp(rank), rng, Wardrobe.FindByName(boss.SkinName));
            if (trophy != null) text += ", " + trophy;
            return text;
        }

        /// <summary>Rolls one piece of gear for the stage's level band and adds it to the loot list.</summary>
        public static ItemState DropGear(StageConfig stage, Inventory inventory, IRandom rng, Rarity cap, Rarity minimum = Rarity.Common)
        {
            Rarity rarity = Content.RollRarity(rng, cap);
            if (rarity < minimum) rarity = minimum;
            var slot = (EquipSlot)rng.NextInt(8);
            var item = new ItemState(Math.Max(1, stage.GearItemLevel), rarity, slot);
            WeaponRolls.Roll(item, rng);

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

        /// <summary>
        /// Credits a stretch of unobserved hunting: the hero kills what its DPS allows, at the given
        /// efficiency (10000 = live rate, 6000 = offline rate), for at most capSeconds. carry: what the last settlement
        /// left unfinished (part of an encounter, the place in the loop); it is used and updated, so settlements every
        /// 30 seconds pay what one long one would (without it a hero clearing under six encounters a heartbeat was never
        /// paid a Korstone, 27 Sep 2026).
        /// </summary>
        public static HuntSettlement Settle(StageConfig stage, HeroStats hero, long seconds, long capSeconds, int efficiencyBp, Inventory inventory, IRandom rng,
            HuntCarry? carry = null)
        {
            long counted = seconds < 0 ? 0 : seconds > capSeconds ? capSeconds : seconds;
            long avgPack = (stage.PackSizeMin + stage.PackSizeMax) / 2;
            long packHp = stage.MobHp * avgPack;
            // Auto-cast skills add about half again over plain attacks in the live lane (measured by Orsuun.Sim);
            // the run-in is added on top. Re-measure this constant whenever the skill kit changes. A mounted hero
            // casts nothing (owner, 26 Sep 2026), so he hunts at his plain attacks' pace.
            // The weapon's rolls (WeaponRolls) scale each share: average damage the plain attacks, skill damage the rest.
            int skillsPercent = (100 + hero.AverageDamagePercent)
                                + (hero.Mounted ? 0 : (AutoCastMultiplierPercent - 100) * (100 + hero.SkillDamagePercent) / 100)
                                + (hero.Kin?.HuntPercent ?? 0);   // the Bannerkin's blessing lends crits (Rules.Bannerkin)
            long ticksPerPack = hero.Attack <= 0
                ? long.MaxValue
                : packHp * hero.AttackIntervalTicks * 100 / (hero.Attack * Math.Max(1, skillsPercent)) + stage.RunTicks;
            long packs = 0;
            if (ticksPerPack != long.MaxValue)
            {
                long work = counted * LaneSim.TicksPerSecond * efficiencyBp / RandomExtensions.FullBp;
                // A stronger hero needs fewer ticks a pack: the carried part never counts as a whole one.
                if (carry != null) work += Math.Max(0, Math.Min(carry.Ticks, ticksPerPack - 1));
                packs = work / ticksPerPack;
                if (carry != null) carry.Ticks = work - packs * ticksPerPack;
            }

            long finals = 0;
            if (stage.FinalEncounter != FinalEncounter.None)
            {
                // A Korstone costs about as much time as its waves: count it as one extra pack per loop. Encounters run
                // on from where the last settlement left the loop; the loop's last one is the Korstone.
                long loop = stage.PacksBeforeKorstone + 1;
                long done = carry != null ? Math.Max(0, Math.Min(carry.Encounter, loop - 1)) : 0;
                finals = (done + packs) / loop;
                if (carry != null) carry.Encounter = (int)((done + packs) % loop);
                packs -= finals;
            }
            else if (carry != null) carry.Encounter = 0;

            long sornBefore = inventory.Sorn;
            for (long p = 0; p < packs; p++)
                for (long m = 0; m < avgPack; m++)
                    LootMob(stage, inventory, rng, out _);
            for (long k = 0; k < finals; k++)
            {
                if (stage.FinalEncounter == FinalEncounter.Boss) LootBoss(stage, inventory, rng);
                else LootKorstone(stage, inventory, rng, elder: stage.ElderEvery > 0 && (k + 1) % stage.ElderEvery == 0);
            }

            return new HuntSettlement(counted, packs, finals, inventory.Sorn - sornBefore);
        }
    }

    /// <summary>What a settlement leaves unfinished for the next (HuntYield.Settle): the ticks toward the next encounter
    /// and how many encounters of the current loop are done. A new stage starts both at 0.</summary>
    public sealed class HuntCarry
    {
        public long Ticks;
        public int Encounter;
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
