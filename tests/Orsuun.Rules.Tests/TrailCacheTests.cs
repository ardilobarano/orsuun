using System;
using Orsuun.Rules;
using Xunit;

namespace Orsuun.Rules.Tests;

/// <summary>Trail caches (owner, 29 Sep 2026): a few a day, a wait between them, and what they hold.</summary>
public class TrailCacheTests
{
    [Fact]
    public void The_first_of_the_day_lies_there_at_once_and_the_next_after_the_wait()
    {
        var now = new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(0, TrailCaches.SecondsToNext(0, null, now));
        Assert.Equal(TrailCaches.MinutesBetween * 60, TrailCaches.SecondsToNext(1, now, now));
        Assert.Equal(0, TrailCaches.SecondsToNext(1, now.AddMinutes(-TrailCaches.MinutesBetween), now));
        Assert.Equal(-1, TrailCaches.SecondsToNext(TrailCaches.PerDay, now.AddHours(-5), now));
    }

    [Fact]
    public void A_cache_always_holds_something_and_counts_its_own_feat()
    {
        var rng = new XorShiftRandom(3);
        for (int i = 0; i < 500; i++)
        {
            DailyReward r = TrailCaches.Roll(rng, 40);
            Assert.False(string.IsNullOrEmpty(r.Text));
            var inventory = new Inventory();
            r.GrantTo(inventory);
            Assert.True(inventory.Sorn + inventory.Turnstones + inventory.Materials + inventory.Potions + inventory.ScrollsOfMercy > 0);
        }
        Assert.Equal(FeatMetric.CachesOpened, Bounties.FeatOf(BountyMetric.CachesOpened));
        Assert.Equal(FeatMetric.Pushes, Bounties.FeatOf(BountyMetric.Pushes));
    }
}
