using Orsuun.Rules;
using Orsuun.Rules.Combat;
using Xunit;

namespace Orsuun.Rules.Tests;

/// <summary>The Bannerkin (GDD section 12, owner 28 Sep 2026): six pieces of its own, Hunter's Blessing and Mending Song.</summary>
public class BannerkinTests
{
    [Fact]
    public void Its_gear_is_its_own_and_its_score_grows_with_level_rarity_and_forge()
    {
        var set = Bannerkin.StarterSet(34);
        Assert.Equal(6, set.Count);
        Assert.All(set, p => { Assert.True(p.Kin); Assert.Empty(p.Sockets); Assert.Equal(30, p.ItemLevel); });
        Assert.StartsWith("Rare Bannerkin", set[0].DisplayName);
        KinStats plain = Bannerkin.Stats(set)!;
        set[0].UpgradeLevel = 9;
        KinStats forged = Bannerkin.Stats(set)!;
        Assert.True(forged.Score > plain.Score);
        Assert.True(forged.FocusBp > plain.FocusBp);
        Assert.Null(Bannerkin.Stats(new ItemState[0]));
        // The hero's stats leave its pieces out.
        var hero = HeroFactory.FromEquipment(set, 34, HeroClass.Vanguard, kin: set);
        Assert.Equal(HeroFactory.FromEquipment(new ItemState[0], 34, HeroClass.Vanguard).Attack, hero.Attack);
        Assert.NotNull(hero.Kin);
    }

    [Fact]
    public void It_blesses_and_heals_in_the_lane_and_the_replay_matches()
    {
        var kin = Bannerkin.StarterSet(40);
        HeroStats hero = HeroFactory.FromEquipment(new[] { new ItemState(40, Rarity.Rare) { UpgradeLevel = 3 } }, 40, HeroClass.Vanguard, kin: kin);
        StageConfig stage = Content.Stage(40);
        LaneSim Run(out int blessings, out int songs, out long healed)
        {
            var lane = new LaneSim(stage, hero, SkillDef.For(hero.Class), new Inventory(), new XorShiftRandom(5));
            for (int i = 0; i < lane.AutoCast.Length; i++) lane.AutoCast[i] = true;
            blessings = songs = 0;
            healed = 0;
            for (int t = 0; t < 20 * 90; t++)
            {
                lane.Tick();
                foreach (LaneEvent e in lane.DrainEvents())
                {
                    if (e.Kind != LaneEventKind.KinCast) continue;
                    if (e.Text == "Mending Song") { songs++; healed += e.Amount; } else blessings++;
                }
            }
            return lane;
        }
        LaneSim a = Run(out int blessA, out int songA, out long healA);
        LaneSim b = Run(out int blessB, out int songB, out long healB);
        Assert.True(blessA >= 3);
        Assert.Equal((blessA, songA, healA, a.MobsKilled, a.HeroHp), (blessB, songB, healB, b.MobsKilled, b.HeroHp));
    }

    [Fact]
    public void Bosses_and_commanders_drop_its_pieces()
    {
        int kin = 0;
        for (ulong seed = 1; seed <= 200; seed++)
        {
            var inventory = new Inventory();
            HuntYield.LootBoss(Content.Stage(10), inventory, new XorShiftRandom(seed));
            kin += inventory.Loot.Count(i => i.Kin);
        }
        Assert.InRange(kin, 30, 70);   // a quarter of 200
    }
}
