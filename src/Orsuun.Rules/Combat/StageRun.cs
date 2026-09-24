#nullable enable
namespace Orsuun.Rules.Combat
{
    // Callers pass a StageConfig; apply EveningBells.Apply(...) before calling so client and server replay the same bell.
    public readonly struct StageRunResult
    {
        public StageRunResult(bool cleared, int ticks, int deaths)
        {
            Cleared = cleared;
            Ticks = ticks;
            Deaths = deaths;
        }

        public bool Cleared { get; }
        public int Ticks { get; }
        public int Deaths { get; }
    }

    /// <summary>
    /// A push: one full loop of a stage with every skill on auto-cast and no player input. The server runs it
    /// with a seed to decide the outcome; the client runs the same seed to show it. Both must match exactly.
    /// </summary>
    public static class StageRun
    {
        /// <summary>A push gives up after this long, or on the first death.</summary>
        public const int MaxTicks = 5 * 60 * LaneSim.TicksPerSecond;

        public static LaneSim Create(StageConfig stage, HeroStats hero, Inventory inventory, ulong seed)
        {
            var lane = new LaneSim(stage, hero, SkillDef.For(hero.Class), inventory, new XorShiftRandom(seed));
            for (int i = 0; i < lane.AutoCast.Length; i++) lane.AutoCast[i] = true;
            return lane;
        }

        public static StageRunResult Simulate(StageConfig stage, HeroStats hero, Inventory inventory, ulong seed)
        {
            LaneSim lane = Create(stage, hero, inventory, seed);
            int ticks = 0;
            while (ticks < MaxTicks && lane.Clears == 0 && lane.Deaths == 0)
            {
                lane.Tick();
                lane.DrainEvents();
                ticks++;
            }
            return new StageRunResult(lane.Clears > 0, ticks, lane.Deaths);
        }
    }
}
