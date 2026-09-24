#nullable enable
using System;
using System.Collections.Generic;
using Orsuun.Rules.Combat;

namespace Orsuun.Rules
{
    public sealed class MapDef
    {
        public MapDef(int id, string name, int levelMin, int levelMax, string material, string bossName, string[] mobNames)
        {
            Id = id;
            Name = name;
            LevelMin = levelMin;
            LevelMax = levelMax;
            Material = material;
            BossName = bossName;
            MobNames = mobNames;
        }

        public int Id { get; }
        public string Name { get; }
        public int LevelMin { get; }
        public int LevelMax { get; }
        public string Material { get; }
        public string BossName { get; }
        public string[] MobNames { get; }
        public const int StagesPerMap = 10;
    }

    /// <summary>A farm destination outside the campaign (GDD section 13). Ids start at 100; campaign stages are 1..N.</summary>
    public sealed class ZoneDef
    {
        public ZoneDef(int id, string name, ZoneType type, int tier, int unlockStage, int levelMin, int levelMax, bool offlineAllowed, bool pvp)
        {
            Id = id;
            Name = name;
            Type = type;
            Tier = tier;
            UnlockStage = unlockStage;
            LevelMin = levelMin;
            LevelMax = levelMax;
            OfflineAllowed = offlineAllowed;
            Pvp = pvp;
        }

        public int Id { get; }
        public string Name { get; }
        public ZoneType Type { get; }
        public int Tier { get; }
        /// <summary>Campaign stage that must be cleared before this zone opens.</summary>
        public int UnlockStage { get; }
        public int LevelMin { get; }
        public int LevelMax { get; }
        public bool OfflineAllowed { get; }
        public bool Pvp { get; }
    }

    public sealed class BossDef
    {
        public BossDef(int id, int zoneId, string name, int tier, long hp, long attack, BossMechanic mechanic, int respawnSeconds, string skinName)
        {
            Id = id;
            ZoneId = zoneId;
            Name = name;
            Tier = tier;
            Hp = hp;
            Attack = attack;
            Mechanic = mechanic;
            RespawnSeconds = respawnSeconds;
            SkinName = skinName;
        }

        public int Id { get; }
        public int ZoneId { get; }
        public string Name { get; }
        public int Tier { get; }
        public long Hp { get; }
        public long Attack { get; }
        public BossMechanic Mechanic { get; }
        public int RespawnSeconds { get; }
        /// <summary>The cosmetic only this Commander drops.</summary>
        public string SkinName { get; }
        /// <summary>How long a spawned Commander stays up for fights.</summary>
        public const int WindowSeconds = 10 * 60;
    }

    /// <summary>
    /// Static game content. One source of truth compiled into server and client; a JSON export from
    /// designer tooling replaces this file once there is more than one map.
    /// </summary>
    public static class Content
    {
        public const int FirstZoneId = 100;
        public const int MaxLevel = 105;

        /// <summary>
        /// The campaign maps (world bible section 6: twelve at launch, ten stages each). The first four are built: their
        /// enemies and backdrops exist (the Salt Sea and Whitefang Range share the Salt Flats' and Frost Pasture's).
        /// </summary>
        public static readonly MapDef[] Maps =
        {
            new MapDef(1, "The Oathfields", 1, 10, "Wolf Sinew", "Old Greyjaw", new[] { "Hollowed Wolf", "Hollowed Boar", "Deserter" }),
            new MapDef(2, "Gorak Pass", 10, 20, "Marauder Brand", "Warlord Tul-Gorak", new[] { "War Hound", "Gorak Marauder", "Gorak Raider" }),
            new MapDef(3, "The Salt Sea", 20, 30, "Scorpion Glass", "The Mirage Queen", new[] { "Salt Scorpion", "Glass Snake", "Caravan Ghoul" }),
            new MapDef(4, "Whitefang Range", 30, 40, "Frozen Marrow", "Nine-Winters", new[] { "Frost Bear", "Ice Wight", "Snow Hag" }),
        };

        public static readonly ZoneDef[] Zones =
        {
            new ZoneDef(101, "Ember Steppe", ZoneType.HuntingGround, 1, 1, 1, 40, offlineAllowed: true, pvp: false),
            new ZoneDef(102, "Salt Flats", ZoneType.HuntingGround, 2, 4, 40, 75, offlineAllowed: true, pvp: false),
            new ZoneDef(103, "Frost Pasture", ZoneType.HuntingGround, 3, 8, 75, 105, offlineAllowed: true, pvp: false),
            new ZoneDef(111, "Korstone Field I", ZoneType.KorstoneField, 1, 1, 1, 30, offlineAllowed: true, pvp: false),
            new ZoneDef(112, "Korstone Field II", ZoneType.KorstoneField, 2, 3, 30, 55, offlineAllowed: true, pvp: false),
            new ZoneDef(113, "Korstone Field III", ZoneType.KorstoneField, 3, 6, 55, 75, offlineAllowed: true, pvp: true),
            new ZoneDef(114, "Korstone Field IV", ZoneType.KorstoneField, 4, 9, 75, 90, offlineAllowed: false, pvp: true),
            new ZoneDef(115, "Korstone Field V", ZoneType.KorstoneField, 5, 10, 90, 105, offlineAllowed: false, pvp: true),
            new ZoneDef(121, "Gorak War Camp", ZoneType.CommanderGround, 1, 5, 20, 40, offlineAllowed: false, pvp: true),
        };

        public static readonly BossDef[] Bosses =
        {
            new BossDef(1, 121, "Warlord Tul-Gorak", 1, 60_000, 90, BossMechanic.CaptainShield, 45 * 60, "Tul-Gorak's Warmask"),
            new BossDef(2, 121, "The Mirage Queen", 1, 50_000, 70, BossMechanic.MirrorImages, 45 * 60, "Mirage Veil"),
            new BossDef(3, 121, "Old Greyjaw", 1, 45_000, 60, BossMechanic.PackCaller, 45 * 60, "Greyjaw Pelt Cloak"),
        };

        /// <summary>Base names of the stat-only slots; weapon and armour names come from ItemLooks by level band.</summary>
        public static readonly string[] SlotBaseNames =
        {
            "Rider's Glaive", "Lamellar Coat", "Steppe Helm", "Round Shield", "Bone Bracelet", "Tamga Necklace", "Iron Earrings", "Felt Boots",
        };

        public static readonly string[] KorshardRanks = { "Trooper", "Rider", "Captain", "Commander", "Guard of the Khan" };

        public static int TotalStages => Maps.Length * MapDef.StagesPerMap;

        public static bool IsZone(int parkId) => parkId >= FirstZoneId;

        public static ZoneDef? Zone(int id)
        {
            foreach (ZoneDef z in Zones) if (z.Id == id) return z;
            return null;
        }

        public static BossDef? Boss(int id)
        {
            foreach (BossDef b in Bosses) if (b.Id == id) return b;
            return null;
        }

        /// <summary>Campaign stages and zones the player may park in, given the highest cleared campaign stage.</summary>
        public static bool IsUnlocked(int parkId, int highestStageCleared)
        {
            if (!IsZone(parkId)) return parkId >= 1 && parkId <= Math.Min(TotalStages, highestStageCleared + 1);
            ZoneDef? zone = Zone(parkId);
            return zone != null && highestStageCleared >= zone.UnlockStage;
        }

        public static MapDef MapOfStage(int stage) => Maps[(ClampStage(stage) - 1) / MapDef.StagesPerMap];

        public static int StageInMap(int stage) => (ClampStage(stage) - 1) % MapDef.StagesPerMap + 1;

        /// <summary>Display name for a campaign stage or a zone id.</summary>
        public static string StageName(int parkId)
        {
            if (IsZone(parkId)) return Zone(parkId)?.Name ?? "Unknown zone";
            return MapOfStage(parkId).Name + " " + StageInMap(parkId);
        }

        /// <summary>Lane configuration for a campaign stage or a zone id.</summary>
        public static StageConfig Stage(int parkId) => IsZone(parkId) ? ZoneStage(parkId) : CampaignStage(parkId);

        private static StageConfig CampaignStage(int stage)
        {
            int s = ClampStage(stage);
            MapDef map = MapOfStage(s);
            int inMap = StageInMap(s);
            // The Oathfields: +12% HP and +9% attack per stage (stage 10 mobs about 2.1x / 1.8x stage 1). Past it the
            // curve climbs about as fast as a hero grows over the levels a map spans (+19.7 and +13.8 points a stage,
            // simulated), so each map boss is a power check the last map's gear cannot pass (GDD section 2) and a new
            // map opens just above the last one's mobs. Sorn and XP follow HP, so they stay the same per point of
            // damage and the zones keep their farming roles.
            int past = Math.Max(0, s - MapDef.StagesPerMap);
            int early = Math.Min(s, MapDef.StagesPerMap) - 1;
            int hpPct = 100 + 12 * early + 197 * past / 10;
            int atkPct = 100 + 9 * early + 138 * past / 10;
            return new StageConfig
            {
                StageNumber = s,
                Zone = ZoneType.Campaign,
                MobHp = 250 * hpPct / 100,
                MobAttack = 45 * atkPct / 100,
                KorstoneHp = 4000 * hpPct / 100,
                SornPerMob = 150 * hpPct / 100,
                XpPerMob = 10 * hpPct / 100,
                FinalEncounter = inMap == MapDef.StagesPerMap ? FinalEncounter.Boss : FinalEncounter.Korstone,
                BossHp = 9000 * hpPct / 100,
                BossAttack = 95 * atkPct / 100,
                BossName = map.BossName,
                MaterialName = map.Material,
                GearItemLevel = map.LevelMin + (map.LevelMax - map.LevelMin) * (inMap - 1) / (MapDef.StagesPerMap - 1),
            };
        }

        private static StageConfig ZoneStage(int zoneId)
        {
            ZoneDef zone = Zone(zoneId) ?? throw new ArgumentOutOfRangeException(nameof(zoneId));
            // Zone tiers sit on the same curve as campaign stages: tier 1 ~ stage 2, tier 5 ~ stage 10.
            int hpPct = 100 + 25 * (zone.Tier - 1) + 12;
            int atkPct = 100 + 20 * (zone.Tier - 1) + 9;
            var config = new StageConfig
            {
                StageNumber = zoneId,
                Zone = zone.Type,
                MobHp = 250 * hpPct / 100,
                MobAttack = 45 * atkPct / 100,
                KorstoneHp = 4000 * hpPct / 100,
                SornPerMob = 150 * hpPct / 100,
                XpPerMob = 10 * hpPct / 100,
                MaterialName = Maps[0].Material,
                GearItemLevel = zone.LevelMin,
            };

            switch (zone.Type)
            {
                case ZoneType.HuntingGround:
                    // Sorn and levels: dense packs, no Korstones, double sorn and XP, nothing above Uncommon.
                    config.FinalEncounter = FinalEncounter.None;
                    config.PacksBeforeKorstone = 1;
                    config.PackSizeMin = 8;
                    config.PackSizeMax = 10;
                    config.RunTicks = 5 * LaneSim.TicksPerSecond;
                    config.SornPerMob *= 2;
                    config.XpPerMob *= 2;
                    config.GearRarityCap = Rarity.Uncommon;
                    config.MaterialYieldPercent = 0;
                    break;

                case ZoneType.KorstoneField:
                    // Materials and etchings: a Korstone after every pack, Elder every 10th, shards by tier.
                    config.FinalEncounter = FinalEncounter.Korstone;
                    config.PacksBeforeKorstone = 1;
                    config.KorshardRank = zone.Tier - 1;
                    config.ElderEvery = 10;
                    config.MaterialYieldPercent = 150 + 25 * (zone.Tier - 1);
                    config.SornPerMob = config.SornPerMob / 2;
                    break;

                case ZoneType.CommanderGround:
                    // Bosses are fought through BossRun; the lane between fights is a thin patrol.
                    config.FinalEncounter = FinalEncounter.None;
                    config.PacksBeforeKorstone = 1;
                    config.PackSizeMin = 3;
                    config.PackSizeMax = 5;
                    config.RunTicks = 8 * LaneSim.TicksPerSecond;
                    config.GearRarityCap = Rarity.Rare;
                    config.MaterialYieldPercent = 50;
                    break;
            }

            return config;
        }

        /// <summary>Lane configuration for a Commander fight: the boss alone, no packs before it.</summary>
        public static StageConfig BossStage(BossDef boss)
        {
            ZoneDef zone = Zone(boss.ZoneId)!;
            StageConfig config = ZoneStage(zone.Id);
            config.FinalEncounter = FinalEncounter.Boss;
            config.PacksBeforeKorstone = 0;
            config.BossHp = boss.Hp;
            config.BossAttack = boss.Attack;
            config.BossName = boss.Name;
            config.BossMechanic = boss.Mechanic;
            config.RunTicks = 2 * LaneSim.TicksPerSecond;
            return config;
        }

        public static string ItemName(ItemState item) => item.Rarity + " " + ItemLooks.BaseName(item.Slot, item.ItemLevel);

        /// <summary>Gear rarity weights from GDD section 5, in basis points: Common..Legendary.</summary>
        public static readonly int[] RarityWeightsBp = { 6200, 2500, 1000, 270, 30 };

        /// <summary>Etchings an item carries when it drops, by rarity. Legendary rolls 3 or 4.</summary>
        public static int EtchingsAtDrop(Rarity rarity, IRandom rng)
        {
            switch (rarity)
            {
                case Rarity.Common: return 0;
                case Rarity.Uncommon: return 1;
                case Rarity.Rare: return 2;
                case Rarity.Epic: return 3;
                default: return 3 + rng.NextInt(2);
            }
        }

        public static Rarity RollRarity(IRandom rng, Rarity cap)
        {
            int roll = rng.NextInt(RandomExtensions.FullBp);
            int acc = 0;
            for (int i = 0; i < RarityWeightsBp.Length; i++)
            {
                acc += RarityWeightsBp[i];
                if (roll < acc) return (Rarity)Math.Min(i, (int)cap);
            }
            return cap;
        }

        /// <summary>
        /// Character level from total XP: level n needs XpPerLevelSquare x n^2 XP in total, capped at MaxLevel.
        /// At 300 a stage-2 farmer offline reaches level 30 in about 20 hours and needs weeks for 105.
        /// </summary>
        public const long XpPerLevelSquare = 300;

        public static int LevelFor(long xp)
        {
            int level = 1;
            while (level < MaxLevel && xp >= XpPerLevelSquare * (level + 1) * (level + 1)) level++;
            return level;
        }

        public static long XpForLevel(int level) => XpPerLevelSquare * level * level;

        private static int ClampStage(int stage) => Math.Max(1, Math.Min(TotalStages, stage));
    }
}
