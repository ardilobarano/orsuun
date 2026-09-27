using System.Linq;
using Orsuun.Rules;
using Orsuun.Rules.Combat;
using Xunit;

namespace Orsuun.Rules.Tests;

/// <summary>The guild raid (owner, 27 Sep 2026).</summary>
public class GuildRaidTests
{
    [Fact]
    public void The_guild_faces_its_median_members_map()
    {
        Assert.Equal(1, GuildRaids.MapFor(new int[0]));
        Assert.Equal(1, GuildRaids.MapFor(new[] { 0, 3, 9 }));
        Assert.Equal(3, GuildRaids.MapFor(new[] { 5, 25, 29, 90, 120 }));        // maps 1, 3, 3, 10, 12: the middle is 3
        Assert.Equal(Content.Maps.Length, GuildRaids.MapFor(new[] { 120, 120 }));
    }

    [Fact]
    public void A_raid_boss_is_its_maps_boss_made_harder_with_a_trick()
    {
        StageConfig plain = Content.Stage(40), raid = GuildRaids.Stage(4);
        Assert.Equal(plain.BossHp * GuildRaids.HpPercent / 100, raid.BossHp);
        Assert.Equal(plain.BossAttack * GuildRaids.AttackPercent / 100, raid.BossAttack);
        Assert.Equal(plain.BossName, raid.BossName);
        Assert.NotEqual(BossMechanic.None, raid.BossMechanic);
        Assert.All(Content.Maps, m => Assert.NotEqual(BossMechanic.CaptainShield, GuildRaids.MechanicFor(m.Id)));
        Assert.Equal(0, raid.PacksBeforeKorstone);
        Assert.Equal(raid.BossHp * GuildRaids.MinMembers * GuildRaids.FightsToFell, GuildRaids.Pool(4, 1));
        Assert.Equal(raid.BossHp * 12 * GuildRaids.FightsToFell, GuildRaids.Pool(4, 12));
    }

    [Fact]
    public void A_fight_replays_the_same_and_deals_damage()
    {
        HeroStats hero = HeroFactory.FromWeapon(new ItemState(10, Rarity.Rare) { UpgradeLevel = 6 });
        BossRunResult a = BossRun.Simulate(GuildRaids.Stage(1), hero, new Inventory { Potions = 10 }, 77);
        BossRunResult b = BossRun.Simulate(GuildRaids.Stage(1), hero, new Inventory { Potions = 10 }, 77);
        Assert.True(a.Damage > 0);
        Assert.True(BossRun.Simulate(GuildRaids.Stage(2), hero, new Inventory { Potions = 10 }, 77).Damage > 0);
        Assert.Equal(a.Damage, b.Damage);
        Assert.Equal(a.Ticks, b.Ticks);
    }

    [Fact]
    public void Pay_grows_with_the_share_and_is_capped()
    {
        (int t0, int s0) = GuildRaids.Reward(0, 1000);
        (int t1, int s1) = GuildRaids.Reward(100, 1000);
        (int tAll, _) = GuildRaids.Reward(5000, 1000);
        Assert.Equal(20, t0);
        Assert.Equal(100, s0);
        Assert.True(t1 > t0 && s1 > s0);
        Assert.Equal(120, tAll);
    }
}
