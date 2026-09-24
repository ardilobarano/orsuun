using System;
using System.Collections.Generic;
using System.Linq;
using Orsuun.Rules;
using Orsuun.Rules.Combat;
using Xunit;
using Xunit.Abstractions;

/// <summary>Kestrel, the second playable class (24 Sep 2026): her own kit and stat shape, same rules everywhere.</summary>
public class KestrelTests
{
    private readonly ITestOutputHelper _out;
    public KestrelTests(ITestOutputHelper output) => _out = output;

    private static ItemState Starter() => new ItemState(10, Rarity.Rare);

    [Fact]
    public void Kestrel_is_quicker_and_critier_but_thinner()
    {
        HeroStats v = HeroFactory.FromEquipment(new[] { Starter() }, 5, HeroClass.Vanguard);
        HeroStats k = HeroFactory.FromEquipment(new[] { Starter() }, 5, HeroClass.Kestrel);
        Assert.Equal(HeroClass.Kestrel, k.Class);
        Assert.True(k.AttackIntervalTicks < v.AttackIntervalTicks);
        Assert.Equal(v.CritChanceBp + 700, k.CritChanceBp);
        Assert.True(k.MaxHp < v.MaxHp);
        Assert.Equal(new[] { "Heartseeker", "Knife Fan", "Kestrel's Dive" }, SkillDef.For(HeroClass.Kestrel).Select(s => s.Name).ToArray());
        Assert.Equal("Rending Arc", SkillDef.For(HeroClass.Vanguard)[0].Name);
    }

    [Fact]
    public void Both_classes_clear_the_first_stages_at_a_similar_pace()
    {
        long vTicks = 0, kTicks = 0;
        for (int stage = 1; stage <= 5; stage++)
        {
            for (ulong seed = 1; seed <= 6; seed++)
            {
                StageRunResult v = StageRun.Simulate(Content.Stage(stage), HeroFactory.FromEquipment(new[] { Starter() }, stage, HeroClass.Vanguard), new Inventory { Potions = 30 }, seed);
                StageRunResult k = StageRun.Simulate(Content.Stage(stage), HeroFactory.FromEquipment(new[] { Starter() }, stage, HeroClass.Kestrel), new Inventory { Potions = 30 }, seed);
                vTicks += v.Ticks;
                kTicks += k.Ticks;
            }
        }
        double ratio = kTicks / (double)vTicks;
        _out.WriteLine($"Kestrel / Vanguard push time over stages 1-5: {ratio:0.00}");
        Assert.InRange(ratio, 0.75, 1.25);
    }

    /// <summary>Same present-player policy as ActivePlayTests.AimedPlay, over stages 1, 3 and 5.</summary>
    [Fact]
    public void Aimed_play_pays_Kestrel_about_as_much_as_the_Vanguard()
    {
        SkillDef[] skills = SkillDef.For(HeroClass.Kestrel);
        long sum = 0;
        int n = 0;
        foreach (int st in new[] { 1, 3, 5 })
            for (ulong seed = 1; seed <= 8; seed++)
            {
                HeroStats hero = HeroFactory.FromEquipment(new[] { Starter() }, st, HeroClass.Kestrel);
                LaneSim lane = ActivePlay.NewLoop(Content.Stage(st), hero, skills, new Inventory { Potions = 30 }, seed, 0);
                var casts = new List<CastInput>();
                while (lane.CurrentTick < ActivePlay.MaxLoopTicks && lane.Cycles == 0)
                {
                    if (lane.Phase == LanePhase.Fighting)
                    {
                        if (lane.TryCast(0)) casts.Add(new CastInput(lane.CurrentTick, 0));
                        if ((lane.Enemies.Count >= 3 || lane.IsKorstoneEncounter) && lane.TryCast(1)) casts.Add(new CastInput(lane.CurrentTick, 1));
                        if (lane.IsKorstoneEncounter && lane.TryCast(2)) casts.Add(new CastInput(lane.CurrentTick, 2));
                    }
                    lane.Tick();
                    lane.DrainEvents();
                }
                LoopVerdict v = ActivePlay.Verify(Content.Stage(st), hero, skills, seed, 0, new[] { false, false, false }, casts, 30, lane.CurrentTick);
                Assert.True(v.Accepted, v.Reason);
                sum += (long)v.BaselineTicks * 10000 / v.Ticks;
                n++;
            }
        long average = sum / n;
        _out.WriteLine($"Kestrel aimed play over {n} loops: {average / 100}% of auto-cast pace");
        Assert.InRange(average, 11800, 13500);
    }

    [Fact]
    public void A_Kestrel_loop_replays_on_the_server_and_pays_for_aimed_play()
    {
        const ulong seed = 0xBEEF;
        var session = new PlayerSession(new XorShiftRandom(9));
        session.SetClass(HeroClass.Kestrel);
        session.SetLaneSeed(seed, 0);
        Assert.Equal("Heartseeker", session.Lane.Skills[0].Name);
        HeroStats hero = session.Hero;
        for (int guard = 0; guard < ActivePlay.MaxLoopTicks; guard++)
        {
            if (session.Lane.Phase == LanePhase.Fighting)
            {
                session.Cast(0);
                if (session.Lane.Enemies.Count >= 3 || session.Lane.IsKorstoneEncounter) session.Cast(1);
                if (session.Lane.IsKorstoneEncounter) session.Cast(2);
            }
            session.Lane.Tick();
            session.Lane.DrainEvents();
            if (session.CloseLoopIfDone()) break;
        }
        LoopReport r = session.TakeReports().Single();
        LoopVerdict v = ActivePlay.Verify(EveningBells.Apply(Content.Stage(session.ParkedStage), Bell.None), hero, SkillDef.For(hero.Class),
            seed, r.Loop, r.AutoCast, r.Casts, r.Potions, r.Ticks);
        _out.WriteLine($"Kestrel aimed loop: {v.Ticks} ticks vs auto {v.BaselineTicks}, {v.EfficiencyBp / 100}%");
        Assert.True(v.Accepted, v.Reason);
        Assert.True(v.EfficiencyBp > 10000);
    }
}
