#nullable enable
using System;
using Orsuun.Rules.Combat;

namespace Orsuun.Rules
{
    /// <summary>
    /// The Endless Tower (owner, 7 Oct 2026: picked "The Endless Tower": "A solo climb in the style of Metin2's Demon Tower:
    /// every floor harder than the last, a weekly ladder by highest floor, rewards at milestone floors and a title for the
    /// top climbers"). A climb starts at floor 1 and goes on until the hero falls; floor n is as hard as campaign stage n
    /// (its boss on every tenth floor), and past the campaign's last stage each floor is PastTopPercent harder than the one
    /// below, so the ladder measures strength, not time. Floors are short like a dungeon's (one pack, then a light Korstone
    /// or the guardian), each with a fresh life (draughts carry over); they pay their monsters' sorn and XP but drop no
    /// pieces. ClimbsPerDay climbs a bounty day. Every MilestoneEvery floors a chest, the first time each week. The week's
    /// best floor is its ladder; when the week turns the best climbers are paid by letter and the top three take a title
    /// for the next week. Assumptions (not stated by the owner): the numbers, the rewards and the titles' names.
    /// </summary>
    public static class Tower
    {
        public const string Name = "The Endless Tower";
        public const int ClimbsPerDay = 3, MaxFloors = 300, MilestoneEvery = 10;
        /// <summary>The last floors of a climb shown on the lane (the server scores every floor).</summary>
        public const int ReplayFloors = 3;
        /// <summary>Tower floors carry StageNumber StageBase + floor, clear of the campaign, zones, dungeons and places.</summary>
        public const int StageBase = 2000;
        /// <summary>Each floor past the campaign's last stage is this much harder than the one below (percent, compounding).</summary>
        public const int PastTopPercent = 102;
        /// <summary>A floor's Korstone has this share of the stage's, as on a dungeon floor.</summary>
        public const int KorstonePercent = 35;
        /// <summary>The week's ladder: this many best climbers are paid when it ends.</summary>
        public const int PaidRanks = 10;

        public static bool IsFloor(int stageNumber) => stageNumber > StageBase && stageNumber <= StageBase + MaxFloors;
        public static int FloorOf(int stageNumber) => stageNumber - StageBase;

        /// <summary>The dungeon whose look a tower floor borrows: the Hollow Spire, Silkmother's Warren and the Carvers'
        /// Archive in turn, ten floors each.</summary>
        public static int LookOf(int floor) => Dungeons.All[(Math.Max(1, floor) - 1) / MilestoneEvery % Dungeons.All.Length].Id;

        /// <summary>The dungeon look of any lane stage: a dungeon's floors their own, the tower's borrowed ones; 0 for none.</summary>
        public static int DungeonLook(int stageNumber) =>
            Dungeons.IsFloor(stageNumber) ? (stageNumber - Dungeons.FloorStageBase) / 10 : IsFloor(stageNumber) ? LookOf(FloorOf(stageNumber)) : 0;

        /// <summary>How much harder than its campaign stage a floor is (percent): 100 up to the campaign's last stage.</summary>
        public static long HardnessPercent(int floor)
        {
            if (floor <= Content.TotalStages) return 100;
            return (long)Math.Round(100 * Math.Pow(PastTopPercent / 100.0, floor - Content.TotalStages));
        }

        /// <summary>The lane of one floor.</summary>
        public static StageConfig Floor(int floor)
        {
            floor = Math.Max(1, Math.Min(MaxFloors, floor));
            bool guardian = floor % MilestoneEvery == 0;
            int stage = floor <= Content.TotalStages ? floor : guardian ? Content.TotalStages : Content.TotalStages - 1;
            StageConfig c = Content.Stage(stage);
            long pct = HardnessPercent(floor);
            c.StageNumber = StageBase + floor;
            c.Zone = ZoneType.Campaign;
            c.MobHp = c.MobHp * pct / 100;
            c.MobAttack = c.MobAttack * pct / 100;
            c.KorstoneHp = c.KorstoneHp * pct / 100 * KorstonePercent / 100;
            c.BossHp = c.BossHp * pct / 100;
            c.BossAttack = c.BossAttack * pct / 100;
            // Sorn and XP follow HP, as everywhere.
            c.SornPerMob = c.SornPerMob * pct / 100;
            c.XpPerMob = c.XpPerMob * pct / 100;
            c.PacksBeforeKorstone = 1;
            c.WaveSize = 2;
            c.ElderEvery = 0;
            c.BossMechanic = BossMechanic.None;
            c.FinalEncounter = guardian ? FinalEncounter.Boss : FinalEncounter.Korstone;
            if (guardian && floor > Content.TotalStages) c.BossName = "The Tower's Shadow";
            return c;
        }

        /// <summary>A milestone floor's chest, into the inventory; returns what it held. Every tenth floor: Turnstones, an
        /// Etching Needle, a Korshard by the floor and Hunt Marks; every thirtieth adds a Technique Scroll of the hero's
        /// class, every fiftieth an Oathstone, every hundredth a Khan's Alloy.</summary>
        public static string Chest(int floor, Inventory inventory, HeroClass heroClass, IRandom rng)
        {
            int turnstones = 2 + floor / MilestoneEvery;
            int rank = Math.Min(Content.KorshardRanks.Length - 1, floor / 25);
            inventory.Turnstones += turnstones;
            inventory.EtchingNeedles += 1;
            inventory.Korshards[rank] += 1;
            inventory.HuntMarks += 2;
            string text = $"{turnstones} Turnstones, an Etching Needle, a {Content.KorshardRanks[rank]} Korshard, 2 Hunt Marks";
            if (floor % 30 == 0)
            {
                int book = Books.Id(heroClass, rng.NextInt(SkillGrades.Slots));
                inventory.Books[book] += 1;
                text += ", a " + Books.Name(book);
            }
            if (floor % 50 == 0)
            {
                inventory.Oathstones += 1;
                text += ", an Oathstone";
            }
            if (floor % 100 == 0)
            {
                inventory.KhansAlloys += 1;
                text += ", a Khan's Alloy";
            }
            return text;
        }

        /// <summary>What a milestone floor's chest holds, in words, before it is opened.</summary>
        public static string ChestPreview(int floor)
        {
            int rank = Math.Min(Content.KorshardRanks.Length - 1, floor / 25);
            string text = $"{2 + floor / MilestoneEvery} Turnstones, an Etching Needle, a {Content.KorshardRanks[rank]} Korshard, 2 Hunt Marks";
            if (floor % 30 == 0) text += ", a Technique Scroll of your class";
            if (floor % 50 == 0) text += ", an Oathstone";
            if (floor % 100 == 0) text += ", a Khan's Alloy";
            return text;
        }

        /// <summary>What the next milestone above a floor is.</summary>
        public static int NextMilestone(int floor) => (floor / MilestoneEvery + 1) * MilestoneEvery;

        /// <summary>The sorn a week's climber at a rank is paid by letter (the ten best), by the floor reached.</summary>
        public static long SeasonSorn(int best, int rank)
        {
            if (rank < 1 || rank > PaidRanks || best <= 0) return 0;
            long perMob = Content.Stage(Math.Min(Content.TotalStages, best)).SornPerMob;
            int mobs = rank == 1 ? 400 : rank == 2 ? 300 : rank == 3 ? 240 : 150;
            return perMob * mobs;
        }

        /// <summary>The title the week's best climbers wear through the next week.</summary>
        public static string? Title(int rank) => rank == 1 ? "Lord of the Endless Tower" : rank <= 3 ? "Tower Veteran" : null;
    }
}
