using Orsuun.Rules;
using Orsuun.Rules.Combat;
using Xunit;

namespace Orsuun.Rules.Tests;

/// <summary>Korstone Rain (7 Oct 2026): the giant stone's strike lane, its health for the heroes on the map, the shower.</summary>
public class KorstoneRainTests
{
    private static HeroStats Geared(int level, int itemLevel, int upgrade)
    {
        var items = new List<ItemState>();
        for (int s = 0; s < 8; s++) items.Add(new ItemState(itemLevel, Rarity.Epic, (EquipSlot)s) { UpgradeLevel = upgrade });
        return HeroFactory.FromEquipment(items, level);
    }

    private static long Strike(HeroStats hero, int map, ulong seed)
    {
        var lane = BossRun.Create(KorstoneRain.Stage(map), hero, new Inventory { Potions = 20 }, seed);
        while (lane.CurrentTick < KorstoneRain.StrikeTicks && lane.Deaths == 0)
        {
            lane.Tick();
            lane.DrainEvents();
        }
        return lane.BossDamageDealt;
    }

    [Fact]
    public void The_stone_outlasts_a_strike_and_calls_the_maps_monsters()
    {
        StageConfig stone = KorstoneRain.Stage(5);
        Assert.Equal(KorstoneRain.StoneName, stone.BossName);
        Assert.Equal(FinalEncounter.Boss, stone.FinalEncounter);
        Assert.Equal(0, stone.PacksBeforeKorstone);
        Assert.Equal(BossMechanic.PackCaller, stone.BossMechanic);
        Assert.Equal(5 * MapDef.StagesPerMap, stone.StageNumber);   // the lane shows the stone's own map
        long damage = Strike(Geared(50, 50, 7), 5, 1);
        Assert.True(damage > 0 && damage < stone.BossHp);
    }

    [Fact]
    public void Its_health_needs_about_two_strikes_from_each_hero_on_the_map()
    {
        foreach (int map in new[] { 1, 6, 12 })
        {
            int level = Content.Maps[map - 1].LevelMax;
            long damage = 0;
            for (ulong seed = 1; seed <= 4; seed++) damage += Strike(Geared(level, level, 7), map, seed);
            double strikes = (double)KorstoneRain.HpPerHero(map) / (damage / 4);
            Assert.InRange(strikes, 1.0, KorstoneRain.StrikesPerHero);
        }
        Assert.Equal(KorstoneRain.HpPerHero(3) * KorstoneRain.MinHeroes, KorstoneRain.HpFor(3, 1));
        Assert.Equal(KorstoneRain.HpPerHero(3) * 7, KorstoneRain.HpFor(3, 7));
    }

    [Fact]
    public void Strikers_are_showered_by_their_share_and_bystanders_a_little()
    {
        var big = KorstoneRain.Shower(7, true, 600, new XorShiftRandom(5));
        var small = KorstoneRain.Shower(7, true, 50, new XorShiftRandom(5));
        var bystander = KorstoneRain.Shower(7, false, 0, new XorShiftRandom(5));
        Assert.True(big.Sorn > small.Sorn && small.Sorn > bystander.Sorn);
        Assert.Equal(3, big.Shards);
        Assert.Equal(2, small.Shards);
        Assert.Equal(1, bystander.Shards);
        Assert.Equal(0, bystander.Turnstones);
        Assert.Equal(0, KorstoneRain.ShardRank(1));
        Assert.Equal(3, KorstoneRain.ShardRank(12));   // Commander Korshards; the Guard of the Khan only as the rare roll
        // A rare Korshard: the rank above, now and then.
        int rare = 0;
        var rng = new XorShiftRandom(9);
        for (int i = 0; i < 1000; i++) if (KorstoneRain.Shower(1, true, 300, rng).ShardRank > 0) rare++;
        Assert.InRange(rare, 100, 220);
    }
}
