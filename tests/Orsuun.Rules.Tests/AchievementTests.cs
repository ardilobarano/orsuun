using System.Linq;
using Orsuun.Rules;
using Xunit;

namespace Orsuun.Rules.Tests;

/// <summary>Titles and achievements (owner, 27 Sep 2026).</summary>
public class AchievementTests
{
    [Fact]
    public void Ids_are_unique_and_every_map_has_its_own()
    {
        Assert.Equal(Achievements.All.Length, Achievements.All.Select(a => a.Id).Distinct().Count());
        foreach (MapDef map in Content.Maps)
            Assert.Equal(map.Name, Achievements.Find(100 + map.Id)!.Name);
        Assert.All(Achievements.All, a => Assert.True(a.Honor > 0 && a.Target > 0));
        Assert.Contains(Achievements.All, a => a.Title == "Peerless");
        Assert.Equal(Achievements.All.Count(a => a.Title != null), Achievements.All.Where(a => a.Title != null).Select(a => a.Title).Distinct().Count());
    }

    [Fact]
    public void Counters_round_trip_and_bests_keep_the_larger()
    {
        var c = new FeatCounters();
        c.Add(FeatMetric.Korstones, 12);
        c.Add(FeatMetric.Korstones, 3);
        c.Add(FeatMetric.SornDonated, 2_000_000);
        c.Raise(FeatMetric.BestUpgrade, 8);
        c.Raise(FeatMetric.BestUpgrade, 5);
        FeatCounters back = FeatCounters.Parse(c.Serialize());
        Assert.Equal(15, back[FeatMetric.Korstones]);
        Assert.Equal(2_000_000, back[FeatMetric.SornDonated]);
        Assert.Equal(8, back[FeatMetric.BestUpgrade]);
        Assert.Equal(0, back[FeatMetric.Turns]);
        Assert.Equal(new[] { 3, 7, 101 }, Achievements.ParseClaimed(Achievements.SerializeClaimed(new[] { 101, 3, 7 })).OrderBy(i => i));
    }

    [Fact]
    public void Progress_reads_the_right_thing()
    {
        var s = new FeatSnapshot { Level = 61, HighestStageCleared = 37, PitWins = 12, BestOwnedUpgrade = 7, BestSkillGrade = 10 };
        s.Counters.Add(FeatMetric.Korstones, 1000);
        Assert.True(Achievements.Done(Achievements.Find(3)!, s));        // 1,000 Korstones
        Assert.False(Achievements.Done(Achievements.Find(4)!, s));
        Assert.True(Achievements.Done(Achievements.Find(22)!, s));       // level 60
        Assert.Equal(3, Achievements.Progress(Achievements.Find(104)!, s)); // three maps cleared
        Assert.True(Achievements.Done(Achievements.Find(103)!, s));
        Assert.False(Achievements.Done(Achievements.Find(104)!, s));
        Assert.True(Achievements.Done(Achievements.Find(32)!, s));       // +7 owned
        Assert.False(Achievements.Done(Achievements.Find(33)!, s));
        s.Counters.Raise(FeatMetric.BestUpgrade, 9);                      // a +9 since broken still counts
        Assert.True(Achievements.Done(Achievements.Find(34)!, s));
        Assert.True(Achievements.Done(Achievements.Find(70)!, s));
        Assert.True(Achievements.Done(Achievements.Find(50)!, s));
        Assert.False(Achievements.Done(Achievements.Find(65)!, s));
        int ready = Achievements.Ready(s, Achievements.ParseClaimed("1,2"));
        Assert.Equal(Achievements.All.Count(a => Achievements.Done(a, s)) - 2, ready);
    }

    [Fact]
    public void Sorn_grows_with_the_hero()
    {
        AchievementDef d = Achievements.Find(2)!;
        Assert.Equal(d.SornMobs * Content.Stage(1).SornPerMob, Achievements.Sorn(d, 0));
        Assert.True(Achievements.Sorn(d, 100) > Achievements.Sorn(d, 10));
    }
}
