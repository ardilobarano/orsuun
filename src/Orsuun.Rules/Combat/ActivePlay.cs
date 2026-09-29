#nullable enable
using System;
using System.Collections.Generic;

namespace Orsuun.Rules.Combat
{
    /// <summary>A manual skill cast: the skill index and the lane tick (from the loop start) it was pressed on.</summary>
    public readonly struct CastInput
    {
        public CastInput(int tick, int skill)
        {
            Tick = tick;
            Skill = skill;
        }

        public int Tick { get; }
        public int Skill { get; }
    }

    /// <summary>The server's judgement of one reported loop.</summary>
    public readonly struct LoopVerdict
    {
        public LoopVerdict(bool accepted, int ticks, int baselineTicks, int efficiencyBp, string reason)
        {
            Accepted = accepted;
            Ticks = ticks;
            BaselineTicks = baselineTicks;
            EfficiencyBp = efficiencyBp;
            Reason = reason;
        }

        public bool Accepted { get; }
        /// <summary>Loop length the replay produced with the player's inputs (-1 if it did not clear in time).</summary>
        public int Ticks { get; }
        /// <summary>Loop length with every skill on auto-cast and no manual input.</summary>
        public int BaselineTicks { get; }
        /// <summary>Hunting efficiency this loop earns: 10000 = auto-cast pace, up to MaxEfficiencyBp.</summary>
        public int EfficiencyBp { get; }
        public string Reason { get; }

        public static LoopVerdict Reject(string reason, int ticks = -1) => new LoopVerdict(false, ticks, 0, RandomExtensions.FullBp, reason);
    }

    /// <summary>
    /// Active play (GDD: "about 130% through manual skill timing, not through a flat bonus"). Online, every lane loop
    /// (packs, then the final encounter) starts a fresh LaneSim from a seed derived from the server's lane seed and the
    /// loop number. The client reports each finished loop with its length and manual casts; the server replays it
    /// twice from the same seed, once with those inputs and once on plain auto-cast. If the replay reproduces the
    /// reported length, the speed-up over auto-cast is the loop's efficiency, clamped to 100%..MaxEfficiencyBp.
    /// A mismatch earns 100%, never less: no penalty for a desync, no reward for a forged report.
    /// </summary>
    public static class ActivePlay
    {
        public const int MaxEfficiencyBp = 13500;
        /// <summary>A loop longer than this is not replayed (a stuck lane, or a report meant to burn server time).</summary>
        public const int MaxLoopTicks = 20 * 60 * LaneSim.TicksPerSecond;
        public const int MaxCastsPerLoop = 2000;

        public static ulong LoopSeed(ulong laneSeed, int loop)
        {
            // SplitMix64 over (seed, loop): neighbouring loops get unrelated streams.
            ulong z = laneSeed + 0x9E3779B97F4A7C15UL * (ulong)(loop + 1);
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>The lane for loop number <paramref name="loop"/>. Client and server must build it the same way.</summary>
        public static LaneSim NewLoop(StageConfig stage, HeroStats hero, SkillDef[] skills, Inventory inventory, ulong laneSeed, int loop) =>
            new LaneSim(stage, hero, skills, inventory, new XorShiftRandom(LoopSeed(laneSeed, loop)));

        /// <summary>
        /// Runs one loop to the end of its first encounter cycle (LaneSim.Cycles) and returns its length in ticks, or -1
        /// if it has not finished by maxTicks.
        /// A cast at tick t is applied before the lane advances from t, which is when the client applies a tap.
        /// </summary>
        public static int RunLoop(StageConfig stage, HeroStats hero, SkillDef[] skills, ulong laneSeed, int loop,
            IReadOnlyList<bool> autoCast, IReadOnlyList<CastInput> casts, int potions, int maxTicks, IReadOnlyCollection<int>? elitePacks = null)
        {
            LaneSim lane = NewLoop(stage, hero, skills, new Inventory { Potions = potions }, laneSeed, loop);
            // The loop's elite packs (Rules.EliteCamps), marked before any of them spawns, as the client marked them.
            if (elitePacks != null) foreach (int pack in elitePacks) lane.ElitePacks.Add(pack);
            for (int i = 0; i < lane.AutoCast.Length && i < autoCast.Count; i++) lane.AutoCast[i] = autoCast[i];

            int next = 0;
            while (lane.CurrentTick < maxTicks)
            {
                while (next < casts.Count && casts[next].Tick <= lane.CurrentTick)
                {
                    CastInput c = casts[next++];
                    if (c.Tick == lane.CurrentTick && c.Skill >= 0 && c.Skill < skills.Length) lane.TryCast(c.Skill);
                }
                lane.Tick();
                if (lane.Cycles > 0) return lane.CurrentTick;
                if ((lane.CurrentTick & 255) == 0) lane.DrainEvents();
            }
            return -1;
        }

        public static LoopVerdict Verify(StageConfig stage, HeroStats hero, SkillDef[] skills, ulong laneSeed, int loop,
            IReadOnlyList<bool> autoCast, IReadOnlyList<CastInput> casts, int potions, int reportedTicks, IReadOnlyCollection<int>? elitePacks = null)
        {
            if (reportedTicks <= 0 || reportedTicks > MaxLoopTicks) return LoopVerdict.Reject("loop length out of range");
            if (casts.Count > MaxCastsPerLoop) return LoopVerdict.Reject("too many casts");
            for (int i = 1; i < casts.Count; i++)
                if (casts[i].Tick < casts[i - 1].Tick) return LoopVerdict.Reject("casts out of order");

            int played = RunLoop(stage, hero, skills, laneSeed, loop, autoCast, casts, potions, reportedTicks, elitePacks);
            if (played != reportedTicks) return LoopVerdict.Reject("replay did not match", played);

            var allAuto = new bool[skills.Length];
            for (int i = 0; i < allAuto.Length; i++) allAuto[i] = true;
            // The baseline meets the same elite packs, so the pace compares like with like.
            int baseline = RunLoop(stage, hero, skills, laneSeed, loop, allAuto, Array.Empty<CastInput>(), potions, MaxLoopTicks, elitePacks);
            if (baseline < 0) baseline = MaxLoopTicks;

            long bp = (long)baseline * RandomExtensions.FullBp / played;
            int efficiency = (int)Math.Max(RandomExtensions.FullBp, Math.Min(MaxEfficiencyBp, bp));
            return new LoopVerdict(true, played, baseline, efficiency, "ok");
        }
    }
}
