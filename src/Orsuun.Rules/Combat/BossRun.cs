#nullable enable
using System;

namespace Orsuun.Rules.Combat
{
    public readonly struct BossRunResult
    {
        public BossRunResult(long damage, bool killed, int ticks)
        {
            Damage = damage;
            Killed = killed;
            Ticks = ticks;
        }

        public long Damage { get; }
        public bool Killed { get; }
        public int Ticks { get; }
    }

    /// <summary>
    /// One Commander fight: the hero against the boss and its mechanic, every skill on auto-cast, until the boss
    /// dies, the hero dies, or the fight timer runs out. Damage dealt is what the damage bracket ranks.
    /// </summary>
    public static class BossRun
    {
        public const int MaxTicks = 3 * 60 * LaneSim.TicksPerSecond;
        public const int SimulatedRivals = 19;

        public static LaneSim Create(BossDef boss, HeroStats hero, Inventory inventory, ulong seed, Bell bell = Bell.None)
        {
            var lane = new LaneSim(EveningBells.Apply(Content.BossStage(boss), bell), hero, SkillDef.VanguardWrath(), inventory, new XorShiftRandom(seed));
            for (int i = 0; i < lane.AutoCast.Length; i++) lane.AutoCast[i] = true;
            return lane;
        }

        public static BossRunResult Simulate(BossDef boss, HeroStats hero, Inventory inventory, ulong seed, Bell bell = Bell.None)
        {
            LaneSim lane = Create(boss, hero, inventory, seed, bell);
            int ticks = 0;
            while (ticks < MaxTicks && lane.BossesKilled == 0 && lane.Deaths == 0)
            {
                lane.Tick();
                lane.DrainEvents();
                ticks++;
            }
            return new BossRunResult(lane.BossDamageDealt, lane.BossesKilled > 0, ticks);
        }

        /// <summary>
        /// Damage rank among simulated rivals until shared boss pools exist: rival damage is skewed low
        /// (u squared of the boss HP), so a hero that deals a third of the boss lands near the top.
        /// </summary>
        public static int Rank(long damage, BossDef boss, IRandom rng)
        {
            int rank = 1;
            for (int i = 0; i < SimulatedRivals; i++)
            {
                long u = rng.NextInt(1000);
                long rival = boss.Hp * u * u / 1_000_000;
                if (rival > damage) rank++;
            }
            return rank;
        }
    }
}
