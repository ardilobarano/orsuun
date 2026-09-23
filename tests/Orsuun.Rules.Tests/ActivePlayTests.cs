using System;
using System.Collections.Generic;
using Orsuun.Rules;
using Orsuun.Rules.Combat;
using Xunit;
using Xunit.Abstractions;

public class ActivePlayTests
{
    private const int Potions = 30;
    private readonly ITestOutputHelper _out;

    public ActivePlayTests(ITestOutputHelper output) => _out = output;

    private static HeroStats Hero() => HeroFactory.FromWeapon(new ItemState(10, Rarity.Rare));

    /// <summary>What the client does: one lane per loop, taps applied between ticks and recorded with the tick.</summary>
    private static (int ticks, List<CastInput> casts) PlayLoop(StageConfig stage, ulong seed, int loop, bool[] autoCast, Func<LaneSim, IEnumerable<int>> taps)
    {
        SkillDef[] skills = SkillDef.VanguardWrath();
        LaneSim lane = ActivePlay.NewLoop(stage, Hero(), skills, new Inventory { Potions = Potions }, seed, loop);
        Array.Copy(autoCast, lane.AutoCast, autoCast.Length);
        var casts = new List<CastInput>();
        while (lane.CurrentTick < ActivePlay.MaxLoopTicks)
        {
            foreach (int skill in taps(lane))
                if (lane.TryCast(skill)) casts.Add(new CastInput(lane.CurrentTick, skill));
            lane.Tick();
            lane.DrainEvents();
            if (lane.Clears > 0) return (lane.CurrentTick, casts);
        }
        return (-1, casts);
    }

    /// <summary>A present player: Area on cooldown, Burst and Haste saved for the Korstone.</summary>
    private static IEnumerable<int> HoldBurstsForKorstone(LaneSim lane)
    {
        if (lane.Phase != LanePhase.Fighting) yield break;
        yield return 1;
        if (lane.IsKorstoneEncounter) { yield return 2; yield return 0; }
    }

    /// <summary>A present player: aimed Burst on cooldown, Iron Whirl into groups, Blood Fury saved for the Korstone.</summary>
    private static IEnumerable<int> AimedPlay(LaneSim lane)
    {
        if (lane.Phase != LanePhase.Fighting) yield break;
        yield return 0;
        if (lane.Enemies.Count >= 3 || lane.IsKorstoneEncounter) yield return 1;
        if (lane.IsKorstoneEncounter) yield return 2;
    }

    private static readonly bool[] NoAuto = { false, false, false };
    private static readonly bool[] AllAuto = { true, true, true };

    private static LoopVerdict Verify(StageConfig stage, ulong seed, int loop, bool[] auto, List<CastInput> casts, int ticks) =>
        ActivePlay.Verify(stage, Hero(), SkillDef.VanguardWrath(), seed, loop, auto, casts, Potions, ticks);

    [Fact]
    public void Replay_reproduces_a_client_loop_exactly()
    {
        StageConfig stage = Content.Stage(1);
        (int ticks, List<CastInput> casts) = PlayLoop(stage, 42, 3, NoAuto, HoldBurstsForKorstone);
        LoopVerdict v = Verify(stage, 42, 3, NoAuto, casts, ticks);
        Assert.True(v.Accepted, v.Reason);
        Assert.Equal(ticks, v.Ticks);
    }

    [Fact]
    public void Plain_auto_cast_earns_exactly_the_base_rate()
    {
        StageConfig stage = Content.Stage(1);
        (int ticks, List<CastInput> casts) = PlayLoop(stage, 7, 0, AllAuto, _ => Array.Empty<int>());
        LoopVerdict v = Verify(stage, 7, 0, AllAuto, casts, ticks);
        Assert.True(v.Accepted, v.Reason);
        Assert.Equal(RandomExtensions.FullBp, v.EfficiencyBp);
    }

    [Fact]
    public void A_forged_loop_length_is_rejected_at_the_base_rate()
    {
        StageConfig stage = Content.Stage(1);
        (int ticks, List<CastInput> casts) = PlayLoop(stage, 9, 1, NoAuto, HoldBurstsForKorstone);
        LoopVerdict v = Verify(stage, 9, 1, NoAuto, casts, ticks - 40);
        Assert.False(v.Accepted);
        Assert.Equal(RandomExtensions.FullBp, v.EfficiencyBp);
    }

    [Fact]
    public void A_different_seed_or_loop_does_not_replay_the_same_fight()
    {
        StageConfig stage = Content.Stage(1);
        (int ticks, List<CastInput> casts) = PlayLoop(stage, 11, 2, NoAuto, HoldBurstsForKorstone);
        Assert.False(Verify(stage, 12, 2, NoAuto, casts, ticks).Accepted && Verify(stage, 11, 3, NoAuto, casts, ticks).Accepted);
        Assert.NotEqual(ActivePlay.LoopSeed(11, 2), ActivePlay.LoopSeed(11, 3));
    }

    /// <summary>
    /// GDD: active play reaches about 130% through manual timing. With aimed casts (auto-cast has no target logic) and
    /// the aimed weak point, a present player's loops must credit 120-135% on the early stages.
    /// </summary>
    [Fact]
    public void Credited_efficiency_stays_between_the_base_rate_and_the_cap()
    {
        foreach (int stageNumber in new[] { 1, 3, 5 })
        {
            StageConfig stage = Content.Stage(stageNumber);
            long sum = 0; int loops = 0; double rawSum = 0;
            for (int loop = 0; loop < 20; loop++)
            {
                (int ticks, List<CastInput> casts) = PlayLoop(stage, 2026, loop, NoAuto, AimedPlay);
                LoopVerdict v = Verify(stage, 2026, loop, NoAuto, casts, ticks);
                Assert.True(v.Accepted, v.Reason);
                Assert.InRange(v.EfficiencyBp, RandomExtensions.FullBp, ActivePlay.MaxEfficiencyBp);
                sum += v.EfficiencyBp; loops++;
                rawSum += (double)v.BaselineTicks / v.Ticks;
            }
            _out.WriteLine($"stage {stageNumber}: aimed play {rawSum / loops * 100:F1}% of auto-cast pace, credited {sum / loops / 100.0:F1}%");
            Assert.InRange(sum / loops, 12000, 13500);
        }
    }
}
