#nullable enable
using System;
using System.Collections.Generic;
using Orsuun.Rules.Combat;

namespace Orsuun.Rules
{
    /// <summary>Where tapping a goal takes the player.</summary>
    public enum GoalScreen
    {
        Hunt,
        Forge,
        Gear,
        Push,
        Bounties,
        Guild,
        Zones,
        Pits,
    }

    public sealed class Goal
    {
        public Goal(string id, string text, int current, int target, GoalScreen screen, int step)
        {
            Id = id;
            Text = text;
            Current = current;
            Target = target;
            Screen = screen;
            Step = step;
        }

        public string Id { get; }
        public string Text { get; }
        public int Current { get; }
        public int Target { get; }
        public GoalScreen Screen { get; }
        /// <summary>Index on the goal chain, or -1 for a reminder that comes and goes (a piece to wear, a bounty to claim).</summary>
        public int Step { get; }
    }

    /// <summary>What the goal line needs from the server's side: none of it exists in local play.</summary>
    public struct GoalWorld
    {
        public bool Online;
        public bool BountyReady;
        public bool InGuild;
        /// <summary>The hero's lifetime counts (the server's feat counters, since 27 Sep 2026).</summary>
        public long CommanderFights;
        public long DungeonClears;
        public long BountiesClaimed;
        public int PitWins;
    }

    /// <summary>
    /// The "next goal" line of the first sessions: one thing to do at a time, in an order that walks a new player
    /// through the Forge, pushes, gear, levels and a guild. Guidance only: no rewards, nothing the server counts.
    /// A chain step, once met, stays behind the player even if its condition lapses (a forged level lost, a guild
    /// left): the caller keeps the furthest step reached and passes it back. Reminders (a bounty to claim, an empty
    /// slot with a piece for it in the bag) go before the chain while they hold.
    /// </summary>
    public static class Goals
    {
        private sealed class Step
        {
            public Step(string id, GoalScreen screen, int target, Func<PlayerSession, GoalWorld, int> current, Func<string> text, bool onlineOnly = false,
                Feature? feature = null)
            {
                Id = id;
                Screen = screen;
                Target = target;
                Current = current;
                Text = text;
                OnlineOnly = onlineOnly;
                Feature = feature;
            }

            public string Id { get; }
            public GoalScreen Screen { get; }
            public int Target { get; }
            public Func<PlayerSession, GoalWorld, int> Current { get; }
            public Func<string> Text { get; }
            public bool OnlineOnly { get; }
            /// <summary>The screen it needs (Rules.Unlocks): until its level the goal is to reach that level.</summary>
            public Feature? Feature { get; }
        }

        private static readonly Step[] Chain =
        {
            new Step("forge-1", GoalScreen.Forge, 1, (s, w) => s.Weapon.UpgradeLevel, () => "Forge your weapon to +1"),
            new Step("push-1", GoalScreen.Push, 1, (s, w) => s.HighestStageCleared, () => Clear(1)),
            new Step("wear-3", GoalScreen.Gear, 3, (s, w) => Worn(s), () => "Wear gear in 3 slots"),
            new Step("push-3", GoalScreen.Push, 3, (s, w) => s.HighestStageCleared, () => Clear(3)),
            new Step("forge-3", GoalScreen.Forge, 3, (s, w) => BestWorn(s), () => "Forge any piece you wear to +3"),
            // Levels 10-30 (27 Sep 2026): each screen as it opens (Rules.Unlocks), in the order a hero meets them, with
            // pushes between; a first hour's play reaches stage 40 and level 20 (tools' first-hour simulation).
            new Step("push-5", GoalScreen.Push, 5, (s, w) => s.HighestStageCleared, () => Clear(5)),
            new Step("bounty-1", GoalScreen.Bounties, 1, (s, w) => (int)Math.Min(1, w.BountiesClaimed), () => "Finish a bounty and claim its Hunt Marks",
                onlineOnly: true, feature: Rules.Feature.Bounties),
            new Step("wear-8", GoalScreen.Gear, 8, (s, w) => Worn(s), () => "Wear gear in all 8 slots"),
            new Step("push-10", GoalScreen.Push, 10, (s, w) => s.HighestStageCleared, () => Clear(10)),
            new Step("push-15", GoalScreen.Push, 15, (s, w) => s.HighestStageCleared, () => Clear(15)),
            new Step("commander-1", GoalScreen.Zones, 1, (s, w) => (int)Math.Min(1, w.CommanderFights), () => "Fight a Commander from ZONES",
                onlineOnly: true, feature: Rules.Feature.Commanders),
            new Step("push-20", GoalScreen.Push, 20, (s, w) => s.HighestStageCleared, () => Clear(20)),
            new Step("guild", GoalScreen.Guild, 1, (s, w) => w.InGuild ? 1 : 0, () => "Join a guild", onlineOnly: true, feature: Rules.Feature.Guild),
            new Step("forge-7", GoalScreen.Forge, 7, (s, w) => s.Weapon.UpgradeLevel, () => "Forge your weapon to +7: it starts to glow"),
            new Step("push-30", GoalScreen.Push, 30, (s, w) => s.HighestStageCleared, () => Clear(30)),
            new Step("push-40", GoalScreen.Push, 40, (s, w) => s.HighestStageCleared, () => Clear(40)),
            new Step("dungeon-1", GoalScreen.Zones, 1, (s, w) => (int)Math.Min(1, w.DungeonClears), () => "Clear a dungeon from ZONES",
                onlineOnly: true, feature: Rules.Feature.Dungeons),
            new Step("pits-1", GoalScreen.Pits, 1, (s, w) => Math.Min(1, w.PitWins), () => "Win a duel in the Pits",
                onlineOnly: true, feature: Rules.Feature.Pits),
            new Step("push-50", GoalScreen.Push, 50, (s, w) => s.HighestStageCleared, () => Clear(50)),
            new Step("forge-9", GoalScreen.Forge, 9, (s, w) => s.Weapon.UpgradeLevel, () => "Forge your weapon to +9"),
            new Step("level-30", GoalScreen.Hunt, SkillDef.FourthSkillLevel, (s, w) => s.Level, () => $"Reach level {SkillDef.FourthSkillLevel}: your fourth skill opens"),
            new Step("push-60", GoalScreen.Push, 60, (s, w) => s.HighestStageCleared, () => Clear(60)),
            new Step("push-80", GoalScreen.Push, 80, (s, w) => s.HighestStageCleared, () => Clear(80)),
            new Step("push-100", GoalScreen.Push, 100, (s, w) => s.HighestStageCleared, () => Clear(100)),
            new Step("push-120", GoalScreen.Push, Content.TotalStages, (s, w) => s.HighestStageCleared, () => Clear(Content.TotalStages)),
        };

        public static int ChainLength => Chain.Length;

        /// <summary>
        /// The goal to show, or null when the chain is done and no reminder holds. <paramref name="reached"/> is the
        /// furthest chain step shown before (0 at the start); steps before it count as met.
        /// </summary>
        public static Goal? Next(PlayerSession session, GoalWorld world, int reached) => Reminder(session, world) ?? OnChain(session, world, reached);

        /// <summary>A reminder that holds right now (a finished bounty, an empty slot with a piece for it), or null.</summary>
        public static Goal? Reminder(PlayerSession session, GoalWorld world)
        {
            if (world.Online && world.BountyReady)
                return new Goal("bounty", "A bounty is done: claim its Hunt Marks", 0, 1, GoalScreen.Bounties, -1);

            ItemState? spare = SpareForEmptySlot(session);
            if (spare != null)
                return new Goal("wear-" + spare.Slot, $"Wear the {Content.ItemName(spare)} from your bag", 0, 1, GoalScreen.Gear, -1);
            return null;
        }

        /// <summary>The first chain step from <paramref name="reached"/> on that is not met, or null when the chain is done.</summary>
        public static Goal? OnChain(PlayerSession session, GoalWorld world, int reached)
        {
            for (int i = Math.Max(0, reached); i < Chain.Length; i++)
            {
                Step step = Chain[i];
                if (step.OnlineOnly && !world.Online) continue;
                int current = step.Current(session, world);
                if (current >= step.Target) continue;
                // Its screen still locked: the goal is the level that opens it.
                if (step.Feature is Feature locked && !Unlocks.Open(locked, session.Level))
                    return new Goal(step.Id, $"Reach level {Unlocks.Level(locked)} to open {Unlocks.Name(locked)}", session.Level, Unlocks.Level(locked), GoalScreen.Hunt, i);
                return new Goal(step.Id, step.Text(), Math.Max(0, current), step.Target, step.Screen, i);
            }
            return null;
        }

        /// <summary>"Clear The Oathfields 3", or the map boss for a map's last stage, naming what the clear opens.</summary>
        private static string Clear(int stage)
        {
            var opens = new List<string>();
            foreach (ZoneDef zone in Content.Zones)
                if (zone.UnlockStage == stage) opens.Add(zone.Name);
            MapDef map = Content.MapOfStage(stage);
            string text = Content.StageInMap(stage) == MapDef.StagesPerMap
                ? $"PUSH to beat {map.BossName} and clear {map.Name}"
                : "PUSH to clear " + Content.StageName(stage);
            return opens.Count == 0 ? text : text + ": " + string.Join(" and ", opens) + (opens.Count == 1 ? " opens" : " open");
        }

        private static int Worn(PlayerSession session)
        {
            int count = 0;
            foreach (ItemState _ in session.Equipment) count++;
            return count;
        }

        private static int BestWorn(PlayerSession session)
        {
            int best = 0;
            foreach (ItemState item in session.Equipment) best = Math.Max(best, item.UpgradeLevel);
            return best;
        }

        /// <summary>The highest-level bag piece for the first empty slot that has one, if any.</summary>
        private static ItemState? SpareForEmptySlot(PlayerSession session)
        {
            ItemState? best = null;
            foreach (ItemState item in session.Inventory.Loot)
            {
                if (item.Destroyed || session.Equipped(item.Slot) != null) continue;
                if (best == null || item.Slot < best.Slot || (item.Slot == best.Slot && item.ItemLevel > best.ItemLevel)) best = item;
            }
            return best;
        }
    }
}
