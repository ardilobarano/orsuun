using Orsuun.Rules;
using Orsuun.Rules.Combat;
using Xunit;

namespace Orsuun.Rules.Tests;

/// <summary>The three Banners, the War of Banners race, fortress champions and the shared Commander rank.</summary>
public class BannerTests
{
    [Fact]
    public void Three_banners_red_blue_yellow()
    {
        Assert.Equal(new[] { Banner.Ember, Banner.Sky, Banner.Gold }, Banners.All.Select(b => b.Id));
        Assert.Equal("Break every stone.", Banners.Def(Banner.Ember).Creed);
        Assert.Throws<ArgumentOutOfRangeException>(() => Banners.Def(Banner.None));
    }

    [Fact]
    public void Leader_needs_a_clear_first_place()
    {
        Assert.Equal(Banner.Sky, Banners.Leader(10, 30, 20));
        Assert.Equal(Banner.None, Banners.Leader(30, 30, 20));
        Assert.Equal(Banner.None, Banners.Leader(0, 0, 0));
    }

    [Fact]
    public void Generated_names_are_fixed_by_the_account()
    {
        var id = Guid.Parse("3f2a9c1e-5b7d-4e8a-9c21-7d4e5f6a8b90");
        string name = Banners.GeneratedName(id);
        Assert.Equal(name, Banners.GeneratedName(id));
        Assert.Matches("^[A-Z][a-z]+ [A-Z][a-z]+ [0-9]{4}$", name);
    }

    [Fact]
    public void Champions_map_back_to_their_fortress_and_phase()
    {
        foreach (FortressDef f in Fortresses.All)
            foreach (SiegePhase phase in new[] { SiegePhase.Gate, SiegePhase.Yard, SiegePhase.Hall })
            {
                BossDef c = Fortresses.Champion(f, phase);
                var back = Fortresses.FromChampionId(c.Id);
                Assert.NotNull(back);
                Assert.Equal(f.Id, back!.Value.fortress.Id);
                Assert.Equal(phase, back.Value.phase);
                Assert.Null(Content.Boss(c.Id));
            }
        Assert.Null(Fortresses.FromChampionId(1));
        Assert.True(Fortresses.PhaseHp(SiegePhase.Hall, 1) > Fortresses.PhaseHp(SiegePhase.Gate, 1));
        Assert.Equal(Fortresses.PhaseHp(SiegePhase.Gate, 1) * 10, Fortresses.PhaseHp(SiegePhase.Gate, 10));
    }

    [Fact]
    public void A_plain_hero_can_hurt_the_gate()
    {
        var hero = HeroFactory.FromWeapon(new ItemState(10, Rarity.Rare));
        BossRunResult run = BossRun.Simulate(Fortresses.Champion(Fortresses.All[0], SiegePhase.Gate), hero, new Inventory(), 5UL);
        Assert.True(run.Damage > 0);
    }

    [Fact]
    public void Shared_rank_counts_real_fighters_first()
    {
        BossDef boss = Content.Bosses[0];
        var rng = new XorShiftRandom(1);
        Assert.Equal(1, BossRun.RankShared(boss.Hp, new long[] { 10, 20 }, boss, rng));
        int rank = BossRun.RankShared(100, new long[] { 5_000, 6_000, 7_000 }, boss, rng);
        Assert.InRange(rank, 4, 20);
        var crowd = Enumerable.Range(1, 30).Select(i => (long)i * 1_000).ToArray();
        Assert.Equal(31, BossRun.RankShared(1, crowd, boss, rng));
    }
}
