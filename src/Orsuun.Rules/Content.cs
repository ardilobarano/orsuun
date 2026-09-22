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

    /// <summary>
    /// Static game content. One source of truth compiled into server and client; a JSON export from
    /// designer tooling replaces this file once there is more than one map.
    /// </summary>
    public static class Content
    {
        public static readonly MapDef[] Maps =
        {
            new MapDef(1, "The Oathfields", 1, 10, "Wolf Sinew", "Old Greyjaw", new[] { "Hollowed Wolf", "Hollowed Boar", "Deserter" }),
        };

        public static readonly string[] SlotBaseNames =
        {
            "Rider's Glaive", "Lamellar Coat", "Steppe Helm", "Round Shield", "Bone Bracelet", "Tamga Necklace", "Iron Earrings", "Felt Boots",
        };

        public static int TotalStages => Maps.Length * MapDef.StagesPerMap;

        public static MapDef MapOfStage(int stage) => Maps[(Clamp(stage) - 1) / MapDef.StagesPerMap];

        public static int StageInMap(int stage) => (Clamp(stage) - 1) % MapDef.StagesPerMap + 1;

        public static string StageName(int stage) => MapOfStage(stage).Name + " " + StageInMap(stage);

        /// <summary>Global stage number 1..TotalStages to the lane configuration. Stage 10 of a map ends in its boss.</summary>
        public static StageConfig Stage(int stage)
        {
            int s = Clamp(stage);
            MapDef map = MapOfStage(s);
            int inMap = StageInMap(s);
            // +12% HP and +9% attack per stage: stage 10 mobs are about 2.6x / 2.2x stage 1.
            int hpPct = 100 + 12 * (inMap - 1) + 100 * (map.Id - 1);
            int atkPct = 100 + 9 * (inMap - 1) + 80 * (map.Id - 1);
            return new StageConfig
            {
                StageNumber = s,
                MobHp = 250 * hpPct / 100,
                MobAttack = 45 * atkPct / 100,
                KorstoneHp = 4000 * hpPct / 100,
                SornPerMob = 150 * hpPct / 100,
                FinalEncounter = inMap == MapDef.StagesPerMap ? FinalEncounter.Boss : FinalEncounter.Korstone,
                BossHp = 9000 * hpPct / 100,
                BossAttack = 95 * atkPct / 100,
                BossName = map.BossName,
                MaterialName = map.Material,
                GearItemLevel = map.LevelMin + (map.LevelMax - map.LevelMin) * (inMap - 1) / (MapDef.StagesPerMap - 1),
            };
        }

        public static string ItemName(ItemState item) => item.Rarity + " " + SlotBaseNames[(int)item.Slot];

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

        private static int Clamp(int stage) => Math.Max(1, Math.Min(TotalStages, stage));
    }
}
