using System.Collections.Generic;
using Orsuun.Rules;
using Orsuun.Rules.Combat;
using Xunit;

namespace Orsuun.Rules.Tests;

/// <summary>The field shows the next pack waiting up the trail (owner, 29 Sep 2026: "Fights at the camps"): the lane tells
/// its size ahead without drawing, so the lane and the server's replay stay the same.</summary>
public class PackPeekTests
{
    [Fact]
    public void The_peeked_size_is_the_pack_that_comes_and_peeking_changes_nothing()
    {
        HeroStats hero = HeroFactory.FromEquipment(new[] { new ItemState(12, Rarity.Rare) { UpgradeLevel = 5 } }, 12, HeroClass.Vanguard);
        StageConfig stage = Content.Stage(5);
        (List<string> log, int checkedPacks) Run(bool peek)
        {
            var lane = new LaneSim(stage, hero, SkillDef.For(hero.Class), new Inventory(), new XorShiftRandom(11));
            for (int i = 0; i < lane.AutoCast.Length; i++) lane.AutoCast[i] = true;
            var log = new List<string>();
            int expected = -1, packs = 0;
            for (int t = 0; t < 20 * 240; t++)
            {
                if (peek && lane.Phase == LanePhase.Running) expected = lane.PeekPackSize();
                bool wasRunning = lane.Phase == LanePhase.Running;
                lane.Tick();
                int spawned = 0;
                foreach (LaneEvent e in lane.DrainEvents())
                {
                    log.Add(e.Kind + ":" + e.EnemyId + ":" + e.Amount);
                    if (e.Kind == LaneEventKind.EnemySpawned) spawned++;
                }
                if (peek && wasRunning && lane.Phase == LanePhase.Fighting && !lane.IsKorstoneEncounter)
                {
                    Assert.Equal(expected, spawned);
                    packs++;
                }
                if (peek && wasRunning && lane.Phase == LanePhase.Fighting && lane.IsKorstoneEncounter) Assert.Equal(-1, expected);
            }
            return (log, packs);
        }
        (List<string> peeked, int packs) = Run(true);
        (List<string> plain, _) = Run(false);
        Assert.True(packs >= 10);
        Assert.Equal(plain, peeked);
    }
}
