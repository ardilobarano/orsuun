using Orsuun.Rules;
using Xunit;

namespace Orsuun.Rules.Tests;

/// <summary>The daily login calendar (owner, 27 Sep 2026).</summary>
public class DailyLoginTests
{
    [Fact]
    public void Seven_days_then_back_to_the_first()
    {
        Assert.Equal(1, DailyLogin.NextDay(0));
        Assert.Equal(2, DailyLogin.NextDay(1));
        Assert.Equal(7, DailyLogin.NextDay(6));
        Assert.Equal(1, DailyLogin.NextDay(7));
    }

    [Fact]
    public void The_gifts_are_what_the_owner_picked_and_sorn_grows_with_the_hero()
    {
        var inv = new Inventory();
        for (int day = 1; day <= DailyLogin.Days; day++) DailyLogin.Reward(day, 60).GrantTo(inv);
        long mob = Content.Stage(60).SornPerMob;
        Assert.Equal(mob * 140, inv.Sorn);
        Assert.Equal(15, inv.Turnstones);
        Assert.Equal(10, inv.Potions);
        Assert.Equal(1, inv.ScrollsOfMercy);
        Assert.Equal(new[] { 0, 1, 0, 0, 0 }, inv.Korshards);
        Assert.True(DailyLogin.Reward(1, 100).Sorn > DailyLogin.Reward(1, 10).Sorn);
        Assert.True(DailyLogin.Reward(1, 0).Sorn > 0);
        Assert.Equal("a Rider Korshard", DailyLogin.Reward(7, 1).Text);
    }
}
