#nullable enable
using System;
using Orsuun.Rules.Combat;

namespace Orsuun.Rules
{
    /// <summary>What waits on a dungeon's pause floor: nothing, the Chained Smith, or the Carvers' rune lock.</summary>
    public enum DungeonPause { None = 0, Smith = 1, RuneLock = 2 }

    public sealed class DungeonDef
    {
        public DungeonDef(int id, string name, int floors, int smithFloor, int rushFloor, int unlockStage, string wardenName, string blurb,
            DungeonPause pause = DungeonPause.Smith)
        {
            Pause = smithFloor > 0 ? pause : DungeonPause.None;
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
        /// <summary>The floor where the run pauses (0: none): the Chained Smith or the rune lock. It is not fought.</summary>
        public int SmithFloor { get; }
        public DungeonPause Pause { get; }
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
        /// <summary>
        /// The top floor is this much harder than the first (percent of the stage's HP and attack), in even steps: the
        /// Spire's nine floors climb 5% a floor, shorter dungeons faster, so every Warden asks the same.
        /// </summary>
        public const int TopFloorPercent = 40;
        /// <summary>The Warden has this share of the stage boss's HP and attack, on top of the floor step.</summary>
        public const int WardenPercent = 115;
        /// <summary>A floor's Korstone has this share of the stage's, so a floor takes about half a minute.</summary>
        public const int KorstonePercent = 35;

        public static readonly DungeonDef[] All =
        {
            new DungeonDef(1, "The Hollow Spire", 9, smithFloor: 6, rushFloor: 3, unlockStage: 10, "The Spire Warden",
                "Nine floors pushed up from the grave plain. A Korstone rush on floor 3, the Chained Smith on floor 6, the Spire Warden on floor 9."),
            // World bible section 6 (owner, 25 Sep 2026: "More dungeons"): under the Salt Sea, two levels, the Silkmother,
            // Khan's Alloy.
            new DungeonDef(2, "Silkmother's Warren", 6, smithFloor: 0, rushFloor: 4, unlockStage: 30, "The Silkmother",
                "Under the Salt Sea, two levels deep: the Upper Galleries and the Brood Deep, opened by an egg-nest rush. The Silkmother waits at the bottom; her chest always holds a Khan's Alloy.",
                DungeonPause.None),
            // World bible: a Sky Banner vault, a puzzle-light run, the main source of Master's Needles (Oathstones wait for
            // Oath Renewal, not built).
            new DungeonDef(3, "The Carvers' Archive", 5, smithFloor: 3, rushFloor: 0, unlockStage: 40, "The Last Carver",
                "A Sky Banner vault in the mountains. On floor 3 a rune lock asks the riddle carved in its door: the right rune opens the vault, and the Last Carver's chest then holds a Master's Needle.",
                DungeonPause.RuneLock),
        };

        /// <summary>The Warren's two levels: floors 1-3 the Upper Galleries, 4-6 the Brood Deep.</summary>
        public static string FloorName(DungeonDef dungeon, int floor) =>
            dungeon.Id == 2 ? (floor <= 3 ? "Upper Galleries " + floor : "Brood Deep " + (floor - 3)) : "Floor " + floor;

        /// <summary>A Master's Needle in the Last Carver's chest when the rune lock was not opened.</summary>
        public const int MastersNeedleShutBp = 1000;

        /// <summary>
        /// The Carvers' riddles, one carved in each rune lock (puzzle-light: the answer is one of three runes). Original
        /// steppe riddles; a run's riddle and the order of its runes follow from its id, so both sides draw the same.
        /// </summary>
        public static readonly (string Text, string Answer, string DecoyA, string DecoyB)[] Riddles =
        {
            ("I have a mouth that never speaks and a bed where I never sleep; I run all my life and never leave home.", "River", "Wolf", "Tent"),
            ("Higher than the Khan's banner I circle, and the whole steppe is my map.", "Hawk", "Horse", "Salt"),
            ("White as bone and sold for gold, I was a sea before I was a road.", "Salt", "Birch", "Moon"),
            ("Feathered at one end and sharp at the other, I fly once and never come home.", "Arrow", "Hawk", "Drum"),
            ("I call the whole host without a mouth; strike my face and I answer.", "Drum", "Banner", "Wolf"),
            ("I rise with no legs and set with no fall, and every rider turns to me at dawn.", "Sun", "Horse", "Arrow"),
            ("My coat is white in summer and in winter, and in the red wood I bleed.", "Birch", "Salt", "Moon"),
            ("One leg in the ground and a round roof on my head, I keep the sleepers dry.", "Tent", "River", "Drum"),
            ("Four legs for the grass and a rider on my back, I drink where the river bends.", "Horse", "Wolf", "Tent"),
            ("I hunt with my kin and sing to a silver face at night.", "Wolf", "Hawk", "Drum"),
            ("I have no light of my own; I borrow the sun's to guide the night caravan.", "Moon", "Sun", "Salt"),
            ("Every Banner flies me, yet I never leave my pole.", "Banner", "Arrow", "Hawk"),
        };

        /// <summary>The riddle of a run's rune lock and its three runes, in the order shown.</summary>
        public static (string Text, string[] Runes, string Answer) RiddleFor(long runId)
        {
            long n = Math.Abs(runId);
            var r = Riddles[(int)(n % Riddles.Length)];
            string[] runes = { r.Answer, r.DecoyA, r.DecoyB };
            int turn = (int)(n / Riddles.Length % 3);
            string[] shown = { runes[turn % 3], runes[(turn + 1) % 3], runes[(turn + 2) % 3] };
            return (r.Text, shown, r.Answer);
        }

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
            int pct = 100 + TopFloorPercent * (floor - 1) / Math.Max(1, dungeon.Floors - 1);
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

        /// <summary>
        /// The Warden's chest, into the inventory; returns what it held. The Silkmother's always holds a Khan's Alloy; the
        /// Last Carver's a Master's Needle when the rune lock was opened (<paramref name="vaultOpen"/>), rarely otherwise.
        /// </summary>
        public static string WardenChest(Inventory inventory, int level, IRandom rng, DungeonDef? dungeon = null, bool vaultOpen = false)
        {
            int turnstones = 3 + level / 10;
            int rank = Math.Min(Content.KorshardRanks.Length - 1, level / 10);
            inventory.Turnstones += turnstones;
            inventory.EtchingNeedles += 1;
            inventory.Korshards[rank] += 1;
            inventory.HuntMarks += 3;
            string text = $"{turnstones} Turnstones, an Etching Needle, a {Content.KorshardRanks[rank]} Korshard, 3 Hunt Marks";
            if (dungeon?.Id == 2)
            {
                inventory.KhansAlloys += 1;
                text += ", the Silkmother's Khan's Alloy";
            }
            if (dungeon?.Pause == DungeonPause.RuneLock && (vaultOpen || rng.RollBp(MastersNeedleShutBp)))
            {
                inventory.MastersNeedles += 1;
                text += ", a Master's Needle";
            }
            if (rng.NextInt(10) == 0)
            {
                inventory.KhansAlloys += 1;
                text += " and a Khan's Alloy";
            }
            string? piece = Wardrobe.RollDrop(inventory, Wardrobe.WardenDropBp, rng);
            if (piece != null) text += "; " + piece;
            return text;
        }
    }
}
