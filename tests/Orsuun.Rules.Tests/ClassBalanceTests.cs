using System.Collections.Generic;
using Orsuun.Rules;
using Orsuun.Rules.Combat;
using Xunit;
using Xunit.Abstractions;

/// <summary>Every class must push at about the Vanguard's pace and earn about the same for aimed play (GDD ~130%).</summary>
public class ClassBalanceTests
{
    private readonly ITestOutputHelper _out;
    public ClassBalanceTests(ITestOutputHelper output) => _out = output;

    private static ItemState Starter() => new ItemState(10, Rarity.Rare);

    [Theory]
    [InlineData(HeroClass.Kestrel)]
    [InlineData(HeroClass.Wraithsworn)]
    [InlineData(HeroClass.Drumcaller)]
    public void Pushes_at_about_the_Vanguard_pace(HeroClass cls)
    {
        long vanguard = 0, other = 0;
        int vClears = 0, oClears = 0;
        for (int stage = 1; stage <= 5; stage++)
            for (ulong seed = 1; seed <= 6; seed++)
            {
                StageRunResult v = StageRun.Simulate(Content.Stage(stage), HeroFactory.FromEquipment(new[] { Starter() }, stage, HeroClass.Vanguard), new Inventory { Potions = 30 }, seed);
                StageRunResult o = StageRun.Simulate(Content.Stage(stage), HeroFactory.FromEquipment(new[] { Starter() }, stage, cls), new Inventory { Potions = 30 }, seed);
                vanguard += v.Ticks;
                other += o.Ticks;
                if (v.Cleared) vClears++;
                if (o.Cleared) oClears++;
            }
        double ratio = other / (double)vanguard;
        _out.WriteLine($"{cls} / Vanguard push time over stages 1-5: {ratio:0.00}, clears {oClears} vs {vClears}");
        Assert.InRange(ratio, 0.75, 1.25);
        Assert.True(oClears >= vClears - 3);
    }

    [Theory]
    [InlineData(HeroClass.Vanguard)]
    [InlineData(HeroClass.Kestrel)]
    [InlineData(HeroClass.Wraithsworn)]
    [InlineData(HeroClass.Drumcaller)]
    public void Aimed_play_pays_about_the_same_for_every_class(HeroClass cls)
    {
        SkillDef[] skills = SkillDef.For(cls);
        long sum = 0;
        int n = 0;
        foreach (int st in new[] { 1, 3, 5 })
            for (ulong seed = 1; seed <= 8; seed++)
            {
                HeroStats hero = HeroFactory.FromEquipment(new[] { Starter() }, st, cls);
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
        _out.WriteLine($"{cls} aimed play over {n} loops: {average / 100}% of auto-cast pace");
        Assert.InRange(average, 11800, 13600);
    }
}
