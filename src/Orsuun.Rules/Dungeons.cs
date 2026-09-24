#nullable enable
using System;
using Orsuun.Rules.Combat;

namespace Orsuun.Rules
{
    public sealed class DungeonDef
    {
        public DungeonDef(int id, string name, int floors, int smithFloor, int rushFloor, int unlockStage, string wardenName, string blurb)
        {
            Id = id;
            Name = name;
            Floors = floors;
            SmithFloor = smithFloor;
            RushFloor = rushFloor;
            UnlockStage = unlockStage;
            WardenName = wardenName;
            Blurb = blurb;
        }

        public int Id { get; }
        public string Name { get; }
        public int Floors { get; }
        /// <summary>The floor where the Chained Smith waits (0: none). It is not fought.</summary>
        public int SmithFloor { get; }
        /// <summary>The Korstone rush: an Elder Korstone the moment the floor opens.</summary>
        public int RushFloor { get; }
        /// <summary>Campaign stage that must be cleared first.</summary>
        public int UnlockStage { get; }
        public string WardenName { get; }
        public string Blurb { get; }
    }

    /// <summary>
    /// Dungeons (GDD section 2: key-gated instanced runs of 3-5 minutes; world bible section 6: the Hollow Spire, nine
    /// floors pushed up from the grave plain, floor 3 a Korstone rush, floor 6 the Chained Smith, floor 9 the Spire
    /// Warden). Two free runs a bounty day (GDD: two dungeon keys a day). A run is fought floor by floor, each a short
    /// lane with every skill on auto-cast and a fresh life (draughts carry over); the first fall ends it. Floors follow
    /// the campaign stage the hero has reached, a little harder each floor, and drop up to Legendary (GDD: Legendary gear
    /// comes from Commander Grounds and dungeons only). The Chained Smith offers one forge at +10 points of success
    /// (ForgeMethod.ChainedSmith, the Forge's own failure rule). The Warden pays a chest.
    /// </summary>
    public static class Dungeons
    {
        public const int FreeRunsPerDay = 2;
        /// <summary>Dungeon floors carry StageNumber 300 + dungeon * 10 + floor, clear of campaign stages and zone ids.</summary>
        public const int FloorStageBase = 300;
        /// <summary>Each floor up is this much harder than the one below (percent of the stage's HP and attack).</summary>
        public const int FloorStepPercent = 5;
        /// <summary>The Warden has this share of the stage boss's HP and attack, on top of the floor step.</summary>
        public const int WardenPercent = 115;
        /// <summary>A floor's Korstone has this share of the stage's, so a floor takes about half a minute.</summary>
        public const int KorstonePercent = 35;

        public static readonly DungeonDef[] All =
        {
            new DungeonDef(1, "The Hollow Spire", 9, smithFloor: 6, rushFloor: 3, unlockStage: 10, "The Spire Warden",
                "Nine floors pushed up from the grave plain. A Korstone rush on floor 3, the Chained Smith on floor 6, the Spire Warden on floor 9."),
        };

        public static DungeonDef? Find(int id)
        {
            foreach (DungeonDef d in All)
                if (d.Id == id) return d;
            return null;
        }

        public static bool IsFloor(int stageNumber) => stageNumber > FloorStageBase && stageNumber < FloorStageBase + 100;

        /// <summary>The campaign stage a run is scaled to: the highest the hero has cleared, at least the unlock stage.</summary>
        public static int Level(DungeonDef dungeon, int highestStageCleared) =>
            Math.Max(dungeon.UnlockStage, Math.Min(Content.TotalStages, highestStageCleared));

        public static bool Fought(DungeonDef dungeon, int floor) => floor >= 1 && floor <= dungeon.Floors && floor != dungeon.SmithFloor;

        /// <summary>The lane of one floor at a run's level.</summary>
        public static StageConfig Floor(DungeonDef dungeon, int floor, int level)
        {
            StageConfig c = Content.Stage(level);
            int pct = 100 + FloorStepPercent * (floor - 1);
            c.StageNumber = FloorStageBase + dungeon.Id * 10 + floor;
            c.Zone = ZoneType.Campaign;
            c.MobHp = c.MobHp * pct / 100;
            c.MobAttack = c.MobAttack * pct / 100;
            c.KorstoneHp = c.KorstoneHp * pct / 100;
            // Sorn and XP follow HP, as everywhere.
            c.SornPerMob = c.SornPerMob * pct / 100;
            c.XpPerMob = c.XpPerMob * pct / 100;
            // Short floors (GDD: a run is 3-5 minutes): one pack, then a lighter Korstone with smaller waves.
            c.PacksBeforeKorstone = 1;
            c.KorstoneHp = c.KorstoneHp * KorstonePercent / 100;
            c.WaveSize = 2;
            c.FinalEncounter = FinalEncounter.Korstone;
            c.ElderEvery = 0;
            c.GearRarityCap = Rarity.Legendary;
            c.GearItemLevel += 5;
            c.BossMechanic = BossMechanic.None;
            if (floor == dungeon.RushFloor)
            {
                // The Korstone rush: an Elder Korstone the moment the floor opens (5x HP, waves of 6, a Scroll of Mercy).
                c.PacksBeforeKorstone = 0;
                c.ElderEvery = 1;
            }
            if (floor == dungeon.Floors)
            {
                c.FinalEncounter = FinalEncounter.Boss;
                c.PacksBeforeKorstone = 1;
                c.BossName = dungeon.WardenName;
                c.BossHp = c.BossHp * pct / 100 * WardenPercent / 100;
                c.BossAttack = c.BossAttack * pct / 100 * WardenPercent / 100;
            }
            return c;
        }

        /// <summary>The Warden's chest, into the inventory; returns what it held.</summary>
        public static string WardenChest(Inventory inventory, int level, IRandom rng)
        {
            int turnstones = 3 + level / 10;
            int rank = Math.Min(Content.KorshardRanks.Length - 1, level / 10);
            inventory.Turnstones += turnstones;
            inventory.EtchingNeedles += 1;
            inventory.Korshards[rank] += 1;
            inventory.HuntMarks += 3;
            string text = $"{turnstones} Turnstones, an Etching Needle, a {Content.KorshardRanks[rank]} Korshard, 3 Hunt Marks";
            if (rng.NextInt(10) == 0)
            {
                inventory.KhansAlloys += 1;
                text += " and a Khan's Alloy";
            }
            return text;
        }
    }
}
