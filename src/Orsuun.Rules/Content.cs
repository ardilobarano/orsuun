#nullable enable
using System;
using System.Collections.Generic;
using Orsuun.Rules.Combat;

namespace Orsuun.Rules
{
    public sealed class MapDef
    {
        public MapDef(int id, string name, int levelMin, int levelMax, string material, string bossName, string[] mobNames, int bossPercent = 100)
        {
            BossPercent = bossPercent;
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
        /// <summary>The map boss's HP and attack against the curve (100: on it). Past the +9 cap the gear climb is item level
        /// and rarity only, so the last boss needs a little more to stay a gate.</summary>
        public int BossPercent { get; }
        public const int StagesPerMap = 10;
    }

    /// <summary>A farm destination outside the campaign (GDD section 13). Ids start at Content.FirstZoneId (201); campaign stages are 1..N.</summary>
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
        /// <summary>
        /// Zone ids start here (201 the Ember Steppe). Campaign stages run 1..TotalStages (120 with twelve maps). The zones
        /// were 101-121 until maps 11 and 12 (26 Sep 2026, migration MoveZones moved Accounts.ParkedStage); campaign
        /// stages may now reach 200 before they move again. Dungeon floors are 301-399.
        /// </summary>
        public const int FirstZoneId = 201;

        public const int EmberSteppe = 201, SaltFlats = 202, FrostPasture = 203;
        public const int KorstoneFieldI = 211, KorstoneFieldII = 212, KorstoneFieldIII = 213, KorstoneFieldIV = 214, KorstoneFieldV = 215;
        public const int GorakWarCamp = 221;
        public const int MaxLevel = 105;

        /// <summary>
        /// The campaign maps (world bible section 6: twelve at launch, ten stages each). All twelve are built (maps 11 and 12 on
        /// 26 Sep 2026); the Salt Sea and Whitefang Range share the Salt Flats' and Frost Pasture's enemies and backdrops.
        /// </summary>
        public static readonly MapDef[] Maps =
        {
            new MapDef(1, "The Oathfields", 1, 10, "Wolf Sinew", "Old Greyjaw", new[] { "Hollowed Wolf", "Hollowed Boar", "Deserter" }),
            new MapDef(2, "Gorak Pass", 10, 20, "Marauder Brand", "Warlord Tul-Gorak", new[] { "War Hound", "Gorak Marauder", "Gorak Raider" }),
            new MapDef(3, "The Salt Sea", 20, 30, "Scorpion Glass", "The Mirage Queen", new[] { "Salt Scorpion", "Glass Snake", "Caravan Ghoul" }),
            new MapDef(4, "Whitefang Range", 30, 40, "Frozen Marrow", "Nine-Winters", new[] { "Frost Bear", "Ice Wight", "Snow Hag" }),
            new MapDef(5, "The Cinder Marches", 40, 50, "Cinder Heart", "Azhdar the Furnace Wyrm", new[] { "Ash Fiend", "Magma Hound", "Flame Cultist" }),
            new MapDef(6, "Whisperwood", 50, 58, "Whisper Bark", "The Lantern Widow", new[] { "Hollowed Dead", "Hanging Spirit", "Lantern Wisp" }),
            new MapDef(7, "The Bloodbirch", 58, 66, "Bloodbirch Resin", "The Rootfather", new[] { "Red Treant", "Birch Stalker", "Sap Horror" }),
            new MapDef(8, "The Drowned Steppe", 66, 74, "Serpent Scale", "The Coil Mother", new[] { "Marsh Serpent", "Bog Rider", "Leech Swarm" }, bossPercent: 105),
            new MapDef(9, "Colossus Graves", 74, 82, "Giant's Knuckle", "Hurm the Unburied", new[] { "Stone Giant", "Bone Picker", "Siege Beast" }, bossPercent: 105),
            new MapDef(10, "The Sunken Bazaar", 82, 90, "Gilded Cog", "The Last Merchant-Prince", new[] { "Khan Cultist", "Gilded Construct", "Debt Wraith" }, bossPercent: 105),
            new MapDef(11, "The Thousand Markers", 90, 98, "Marker Dust", "Varkesh of the Left Wing", new[] { "Risen Trooper", "Risen Rider", "Risen Captain" }, bossPercent: 105),
            new MapDef(12, "The Hollow Throne", 98, 105, "Throne Shard", "The Khan's Shadow", new[] { "Throne Guard", "Khan's Hound", "Oath Chanter" }, bossPercent: 105),
        };

        public static readonly ZoneDef[] Zones =
        {
            new ZoneDef(EmberSteppe, "Ember Steppe", ZoneType.HuntingGround, 1, 1, 1, 40, offlineAllowed: true, pvp: false),
            new ZoneDef(SaltFlats, "Salt Flats", ZoneType.HuntingGround, 2, 4, 40, 75, offlineAllowed: true, pvp: false),
            new ZoneDef(FrostPasture, "Frost Pasture", ZoneType.HuntingGround, 3, 8, 75, 105, offlineAllowed: true, pvp: false),
            new ZoneDef(KorstoneFieldI, "Korstone Field I", ZoneType.KorstoneField, 1, 1, 1, 30, offlineAllowed: true, pvp: false),
            new ZoneDef(KorstoneFieldII, "Korstone Field II", ZoneType.KorstoneField, 2, 3, 30, 55, offlineAllowed: true, pvp: false),
            new ZoneDef(KorstoneFieldIII, "Korstone Field III", ZoneType.KorstoneField, 3, 6, 55, 75, offlineAllowed: true, pvp: true),
            new ZoneDef(KorstoneFieldIV, "Korstone Field IV", ZoneType.KorstoneField, 4, 9, 75, 90, offlineAllowed: false, pvp: true),
            new ZoneDef(KorstoneFieldV, "Korstone Field V", ZoneType.KorstoneField, 5, 10, 90, 105, offlineAllowed: false, pvp: true),
            new ZoneDef(GorakWarCamp, "Gorak War Camp", ZoneType.CommanderGround, 1, 5, 20, 40, offlineAllowed: false, pvp: true),
        };

        public static readonly BossDef[] Bosses =
        {
            new BossDef(1, GorakWarCamp, "Warlord Tul-Gorak", 1, 60_000, 90, BossMechanic.CaptainShield, 45 * 60, "Tul-Gorak's Warmask"),
            new BossDef(2, GorakWarCamp, "The Mirage Queen", 1, 50_000, 70, BossMechanic.MirrorImages, 45 * 60, "Mirage Veil"),
            new BossDef(3, GorakWarCamp, "Old Greyjaw", 1, 45_000, 60, BossMechanic.PackCaller, 45 * 60, "Greyjaw Pelt Cloak"),
            // The map bosses of maps 4-12 as world Commanders (owner, 29 Sep 2026: "Commanders on every map"): each stands at
            // its map's landmark while up (the client's MapCommander) and is fought like the war camp's three, with a clock,
            // a shared pool and chests of its own. Their trophies are Caravan pieces (the war camp's three have their own).
            MapCommander(4, 4, 2, BossMechanic.PackCaller, "Whitefang Yak"),
            MapCommander(5, 5, 2, BossMechanic.None, "Ember Warhorse"),
            MapCommander(6, 6, 2, BossMechanic.PackCaller, "Barrow Raven"),
            MapCommander(7, 7, 3, BossMechanic.PackCaller, "Sky Stag"),
            MapCommander(8, 8, 3, BossMechanic.None, "Steppe Lynx Kit"),
            MapCommander(9, 9, 3, BossMechanic.PackCaller, "Grave Warden Shroud"),
            MapCommander(10, 10, 4, BossMechanic.None, "Salt Road Camel"),
            MapCommander(11, 11, 4, BossMechanic.CaptainShield, "Hollow Steed"),
            MapCommander(12, 12, 4, BossMechanic.PackCaller, "Khagan's Eagle"),
        };

        /// <summary>
        /// A map Commander's health and attack, as percents of its map boss's (the map's last stage): the war camp's three
        /// stand at about these shares of Gorak Pass's boss (Tul-Gorak's 60 000 health is 165% of its 36 450; a Commander's
        /// attack is kept low, since a fight runs three minutes). The health is a share of the shared pool per fighter.
        /// </summary>
        public const int CommanderHpPercent = 130, CommanderAttackPercent = 25;

        /// <summary>A map's boss as a world Commander, fought by heroes who can hunt the map (its first stage is its place).</summary>
        private static BossDef MapCommander(int id, int map, int tier, BossMechanic mechanic, string trophy)
        {
            StageConfig top = CampaignStage(map * MapDef.StagesPerMap);
            return new BossDef(id, (map - 1) * MapDef.StagesPerMap + 1, Maps[map - 1].BossName, tier, top.BossHp * CommanderHpPercent / 100,
                top.BossAttack * CommanderAttackPercent / 100, mechanic, 45 * 60, trophy);
        }

        /// <summary>A Commander of a big map (not of a Commander Ground).</summary>
        public static bool OnMap(BossDef boss) => !IsZone(boss.ZoneId);

        /// <summary>The item level of a Commander's gear: the war camp's lowest, or a map's highest (its boss's).</summary>
        public static int CommanderGearLevel(BossDef boss) => IsZone(boss.ZoneId) ? Zone(boss.ZoneId)!.LevelMin : MapOfStage(boss.ZoneId).LevelMax;

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

        /// <summary>
        /// Whether a won push moves the hunt to the stage it cleared (27 Sep 2026: a new player otherwise farmed the
        /// Oathfields 1 while pushing on to stage 40, the move hidden in ZONES): yes for a hero hunting the campaign at its
        /// front (the stage last cleared, or stage 1 before any); a hero hunting a zone, or an older stage by choice, stays.
        /// </summary>
        public static bool HuntFollowsPush(int parkedStage, int clearedBefore) =>
            parkedStage >= 1 && parkedStage <= TotalStages && parkedStage >= Math.Max(1, clearedBefore);

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
            if (Dungeons.IsFloor(parkId))
            {
                DungeonDef? dungeon = Dungeons.Find((parkId - Dungeons.FloorStageBase) / 10);
                return (dungeon?.Name ?? "A dungeon") + ", floor " + parkId % 10;
            }
            if (IsZone(parkId)) return Zone(parkId)?.Name ?? "Unknown zone";
            return MapOfStage(parkId).Name + " " + StageInMap(parkId);
        }

        /// <summary>Lane configuration for a campaign stage or a zone id.</summary>
        public static StageConfig Stage(int parkId) => IsZone(parkId) ? ZoneStage(parkId) : CampaignStage(parkId);

        /// <summary>A map boss's health (percent) where heroes meet it with four skills, and with five (SkillDef unlocks).</summary>
        public const int BossHpWithFourthSkill = 112;
        public const int BossHpWithFifthSkill = 125;

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
            // The fourth and fifth skills (owner, 26 Sep 2026) add about a tenth, then a quarter, to a hero's damage by
            // the map boss's level; the boss has as much more health, so its gear check holds.
            int skillPct = map.LevelMax >= SkillDef.FifthSkillLevel ? BossHpWithFifthSkill : map.LevelMax >= SkillDef.FourthSkillLevel ? BossHpWithFourthSkill : 100;
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
                BossHp = 9000L * hpPct / 100 * map.BossPercent / 100 * skillPct / 100,
                BossAttack = 95L * atkPct / 100 * map.BossPercent / 100,
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
            // A map Commander fights on its map boss's stage (its mobs answer a pack call, its gear level drops).
            StageConfig config = IsZone(boss.ZoneId) ? ZoneStage(boss.ZoneId) : CampaignStage(MapOfStage(boss.ZoneId).Id * MapDef.StagesPerMap);
            config.FinalEncounter = FinalEncounter.Boss;
            config.PacksBeforeKorstone = 0;
            config.BossHp = boss.Hp;
            config.BossAttack = boss.Attack;
            config.BossName = boss.Name;
            config.BossMechanic = boss.Mechanic;
            config.RunTicks = 2 * LaneSim.TicksPerSecond;
            return config;
        }

        public static string ItemName(ItemState item) =>
            item.Kin ? item.Rarity + " Bannerkin " + Bannerkin.SlotName(item.Slot) : item.Rarity + " " + ItemLooks.BaseName(item.Slot, item.ItemLevel);

        /// <summary>The name as a hero of <paramref name="cls"/> knows the piece (the server names pieces this way).</summary>
        public static string ItemName(ItemState item, HeroClass cls) =>
            item.Kin ? ItemName(item) : item.Rarity + " " + ItemLooks.BaseName(item.Slot, item.ItemLevel, cls);

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
