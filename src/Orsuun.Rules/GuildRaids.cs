#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Orsuun.Rules.Combat;

namespace Orsuun.Rules
{
    /// <summary>
    /// The guild raid (owner, 27 Sep 2026: "Guild raid"): each week (the bounties' week, Monday 20:00 server time) a guild
    /// faces one boss with one pool of HP its members wear down together, a few fights a day each. The boss is its map's
    /// own, the map most of the guild has reached (the median member's), made HpPercent as hard and given a Commander's
    /// trick by map; the pool holds FightsToFell full fights for each member (at least MinMembers). Whoever fought when it
    /// falls is paid by his share of the damage: Guild Tallies, and sorn by letter; the guild gains XP.
    /// </summary>
    public static class GuildRaids
    {
        public const int FightsPerDay = 3;
        public const int FightsToFell = 5;
        public const int MinMembers = 3;
        public const int HpPercent = 300;
        public const int AttackPercent = 120;
        public const long GuildXp = 200;

        /// <summary>The map whose boss the guild faces: the median member's furthest (1 before any map is cleared).</summary>
        public static int MapFor(IEnumerable<int> highestStagesCleared)
        {
            int[] maps = highestStagesCleared.Select(s => Math.Max(1, Math.Min(Content.Maps.Length, s / MapDef.StagesPerMap + 1))).OrderBy(m => m).ToArray();
            return maps.Length == 0 ? 1 : maps[(maps.Length - 1) / 2];
        }

        /// <summary>A Commander's trick by map. Never the captains' shield: a member who cannot kill the captains would deal nothing.</summary>
        public static BossMechanic MechanicFor(int map) => map % 2 == 0 ? BossMechanic.MirrorImages : BossMechanic.PackCaller;

        /// <summary>One raid fight: the map's last stage, its boss alone, tougher, with the trick.</summary>
        public static StageConfig Stage(int map)
        {
            map = Math.Max(1, Math.Min(Content.Maps.Length, map));
            StageConfig c = Content.Stage(map * MapDef.StagesPerMap);
            c.FinalEncounter = FinalEncounter.Boss;
            c.PacksBeforeKorstone = 0;
            c.BossHp = c.BossHp * HpPercent / 100;
            c.BossAttack = c.BossAttack * AttackPercent / 100;
            c.BossMechanic = MechanicFor(map);
            c.RunTicks = 2 * LaneSim.TicksPerSecond;
            return c;
        }

        public static long Pool(int map, int members) => Stage(map).BossHp * Math.Max(MinMembers, members) * FightsToFell;

        /// <summary>A fighter's pay when the boss falls, by his share of the pool: Guild Tallies and mobs' worth of sorn.</summary>
        public static (int Tallies, int SornMobs) Reward(long damage, long pool)
        {
            double share = pool <= 0 ? 0 : Math.Min(1.0, (double)damage / pool);
            return (Math.Min(120, 20 + (int)Math.Round(200 * share)), 100 + (int)Math.Round(900 * share));
        }
    }
}
