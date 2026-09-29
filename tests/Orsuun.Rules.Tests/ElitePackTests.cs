using System;
using System.Collections.Generic;
using System.Linq;
using Orsuun.Rules;
using Orsuun.Rules.Combat;
using Xunit;

namespace Orsuun.Rules.Tests;

/// <summary>Truly tougher elite packs (owner, 29 Sep 2026): elite in the lane itself, and in the server's replay.</summary>
public class ElitePackTests
{
    private const int Potions = 30;
    private static HeroStats Hero() => HeroFactory.FromWeapon(new ItemState(10, Rarity.Rare) { UpgradeLevel = 4 });
    private static readonly bool[] AllAuto = { true, true, true };

    /// <summary>What the client does: plays one loop on auto, marking the pack with index <paramref name="elite"/> as it
    /// walks to it (as LaneView does near a golden banner). Returns its length and the packs it marked.</summary>
    private static (int ticks, int[] elite, List<(int id, long hp, long attack, bool isElite)> mobs) Play(StageConfig stage, ulong seed, int loop, int elite)
    {
        LaneSim lane = ActivePlay.NewLoop(stage, Hero(), SkillDef.VanguardWrath(), new Inventory { Potions = Potions }, seed, loop);
        Array.Copy(AllAuto, lane.AutoCast, AllAuto.Length);
        var mobs = new List<(int, long, long, bool)>();
        var seen = new HashSet<int>();
        while (lane.CurrentTick < ActivePlay.MaxLoopTicks)
        {
            if (elite >= 0 && lane.Phase == LanePhase.Running && lane.EncounterIndex == elite) lane.MarkNextPackElite();
            lane.Tick();
            lane.DrainEvents();
            foreach (Enemy e in lane.Enemies)
                if (e.Kind == EnemyKind.Mob && seen.Add(e.Id)) mobs.Add((e.Id, e.MaxHp, e.Attack, e.IsElite));
            if (lane.Cycles > 0) return (lane.CurrentTick, lane.ElitePacks.OrderBy(i => i).ToArray(), mobs);
        }
        return (-1, lane.ElitePacks.ToArray(), mobs);
    }

    [Fact]
    public void An_elite_pack_is_tougher_and_the_lane_before_it_is_the_same()
    {
        StageConfig stage = Content.Stage(3);
        (int plainTicks, int[] none, var plain) = Play(stage, 77, 0, -1);
        (int eliteTicks, int[] marked, var elite) = Play(stage, 77, 0, 2);
        Assert.Empty(none);
        Assert.Equal(new[] { 2 }, marked);
        // Up to the marked pack the lane is the same (nothing is drawn to mark it); its monsters come in elite strength.
        // (Its longer fight draws more rolls, so later packs differ: the replay marks the same pack and follows.)
        int before = elite.FindIndex(m => m.isElite);
        Assert.True(before > 0);
        Assert.Equal(plain.Take(before), elite.Take(before));
        foreach (var m in elite.Where(m => m.isElite))
        {
            Assert.Equal(stage.MobHp * EliteCamps.HpPercent / 100, m.hp);
            Assert.Equal(stage.MobAttack * EliteCamps.AttackPercent / 100, m.attack);
        }
        Assert.All(elite.Where(m => !m.isElite), m => Assert.Equal(stage.MobHp, m.hp));
        // Tougher only slows a loop.
        Assert.True(eliteTicks > plainTicks, $"{eliteTicks} vs {plainTicks}");
    }

    [Fact]
    public void The_replay_fights_the_elite_packs_a_loop_names()
    {
        StageConfig stage = Content.Stage(3);
        (int ticks, int[] marked, _) = Play(stage, 1234, 5, 1);
        LoopVerdict named = ActivePlay.Verify(stage, Hero(), SkillDef.VanguardWrath(), 1234, 5, AllAuto, new List<CastInput>(), Potions, ticks, marked);
        Assert.True(named.Accepted, named.Reason);
        // Without them the replay runs faster and does not match: the loop earns the plain pace, never less.
        LoopVerdict unnamed = ActivePlay.Verify(stage, Hero(), SkillDef.VanguardWrath(), 1234, 5, AllAuto, new List<CastInput>(), Potions, ticks);
        Assert.False(unnamed.Accepted);
    }

    [Fact]
    public void A_report_may_name_only_its_packs()
    {
        StageConfig stage = Content.Stage(3);
        Assert.Equal(new[] { 0, 3 }, EliteCamps.ValidPacks(new[] { 3, -1, 0, 3, stage.PacksBeforeKorstone, 99 }, stage));
        Assert.Empty(EliteCamps.ValidPacks(null, stage));
    }
}
