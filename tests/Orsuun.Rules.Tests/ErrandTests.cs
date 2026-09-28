using System;
using Orsuun.Rules;
using Xunit;

namespace Orsuun.Rules.Tests;

public class ErrandTests
{
    [Fact]
    public void Each_townsman_alternates_his_two_errands_by_day()
    {
        ErrandDef a = Errands.For(0, "2026-09-28", 80), b = Errands.For(0, "2026-09-29", 80);
        Assert.NotEqual(a.Id, b.Id);
        Assert.Equal(a.Id, Errands.For(0, "2026-09-30", 80).Id);
        for (int g = 0; g < Errands.Givers; g++) Assert.Equal(g, Errands.For(g, "2026-09-28", 80).Giver);
    }

    [Fact]
    public void An_errand_the_hero_cannot_do_yet_becomes_another()
    {
        // Ilke's Bazaar call needs level 20: a younger hero sells to the merchant instead, whatever the day.
        Assert.Equal(FeatMetric.ItemsSold, Errands.For(1, "2026-09-28", 10).Metric);
        Assert.Equal(FeatMetric.ItemsSold, Errands.For(1, "2026-09-29", 10).Metric);
        // Bora's Pits and Commanders are both shut at level 1: he asks for a spell of hunting.
        Assert.Equal(FeatMetric.HuntSeconds, Errands.For(3, "2026-09-28", 1).Metric);
    }

    [Fact]
    public void A_day_counts_its_deeds_and_its_pay_and_the_next_starts_empty()
    {
        var p = ErrandProgress.Parse("", "2026-09-28");
        p.Add(FeatMetric.ForgeAttempts, 2);
        p.Pay(0);
        var again = ErrandProgress.Parse(p.Serialize(), "2026-09-28");
        Assert.Equal(2, again.Count(FeatMetric.ForgeAttempts));
        Assert.True(again.Paid(0));
        Assert.False(again.Paid(1));
        var tomorrow = ErrandProgress.Parse(p.Serialize(), "2026-09-29");
        Assert.Equal(0, tomorrow.Count(FeatMetric.ForgeAttempts));
        Assert.False(tomorrow.Paid(0));
    }

    [Fact]
    public void New_counters_are_appended_after_the_old_ones()
    {
        Assert.Equal(12, (int)FeatMetric.BestUpgrade);
        Assert.Equal(13, (int)FeatMetric.FishEaten);
        Assert.Equal(16, (int)FeatMetric.PitWins);
    }
}

public class ContestTests
{
    [Fact]
    public void Every_fish_weighs_within_its_range_and_big_ones_are_rare()
    {
        var rng = new XorShiftRandom(42);
        foreach (FishDef fish in Fishing.Fish)
        {
            (int min, int max) = Fishing.Grams[fish.Id];
            int heavy = 0;
            for (int i = 0; i < 2000; i++)
            {
                int g = Fishing.Weigh(fish, rng);
                Assert.InRange(g, min, max);
                if (g > min + (max - min) * 3 / 4) heavy++;
            }
            // The top quarter of the range comes about one time in seven (a draw squared).
            Assert.InRange(heavy, 150, 450);
        }
        Assert.Equal("3.2 kg", Fishing.Kilos(3200));
    }

    [Fact]
    public void The_contest_pays_ten_and_the_first_most()
    {
        (long first, int firstGood, int firstCount) = Fishing.ContestPrize(1, 50);
        (long fourth, _, _) = Fishing.ContestPrize(4, 50);
        Assert.True(first > Fishing.ContestPrize(2, 50).Sorn && Fishing.ContestPrize(2, 50).Sorn > Fishing.ContestPrize(3, 50).Sorn);
        Assert.True(Fishing.ContestPrize(3, 50).Sorn > fourth);
        Assert.Equal(TradeGoods.FirstPearl + (int)Pearl.Heart, firstGood);
        Assert.Equal(1, firstCount);
        Assert.Equal(10, Fishing.ContestPaid);
    }

    [Fact]
    public void The_weekly_calendar_holds_a_fishing_contest_on_saturday()
    {
        var saturday = new DateTime(2026, 10, 3);
        var week = WorldEvents.Weekly(saturday.AddDays(-1), 7);
        Assert.Contains(week, e => e.Kind == WorldEventKind.FishingContest && e.Start == saturday.AddHours(12) && e.End == saturday.AddHours(44));
    }
}
