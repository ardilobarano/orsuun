using System;
using Orsuun.Rules;
using Orsuun.Rules.Combat;
using Xunit;

namespace Orsuun.Rules.Tests;

/// <summary>Elite camps (owner, 29 Sep 2026): a golden banner at one camp of a place, ten minutes of every half hour.</summary>
public class EliteCampTests
{
    private static readonly DateTime Noon = new DateTime(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Every_half_hour_one_camp_flies_the_banner_for_ten_minutes()
    {
        for (int place = 0; place < 40; place++)
            for (int w = 0; w < 30; w++)
            {
                DateTime start = Noon.AddMinutes(w * EliteCamps.WindowMinutes);
                (int camp, DateTime from, DateTime until) = EliteCamps.Roll(place, start);
                Assert.InRange(camp, 0, EliteCamps.Camps - 1);
                Assert.True(from >= start && until <= start.AddMinutes(EliteCamps.WindowMinutes));
                Assert.Equal(EliteCamps.UpMinutes * 60, EliteCamps.SecondsUp(place, start, start.AddMinutes(EliteCamps.WindowMinutes)));
                Assert.True(EliteCamps.Up(place, from.AddSeconds(1), out int up, out long left));
                Assert.Equal(camp, up);
                Assert.Equal(EliteCamps.UpMinutes * 60 - 1, left);
                // The next window's banner may rise the moment this one comes down.
                Assert.Equal(EliteCamps.Roll(place, until).From == until, EliteCamps.Up(place, until, out _, out _));
            }
    }

    [Fact]
    public void The_roll_is_the_same_for_everyone_and_differs_by_place()
    {
        Assert.Equal(EliteCamps.Roll(1001, Noon), EliteCamps.Roll(1001, Noon.AddMinutes(1)));
        int differ = 0;
        for (int place = 1001; place < 1013; place++) if (EliteCamps.Roll(place, Noon).Camp != EliteCamps.Roll(1001, Noon).Camp) differ++;
        Assert.True(differ > 4);
        Assert.False(EliteCamps.Up(-1, Noon, out _, out _));
    }

    [Fact]
    public void One_pack_in_six_is_elite_while_the_banner_flies()
    {
        var rng = new XorShiftRandom(9);
        Assert.Equal(10, EliteCamps.ElitePacks(60, 600, 600, rng));
        Assert.Equal(5, EliteCamps.ElitePacks(60, 600, 300, rng));
        Assert.Equal(0, EliteCamps.ElitePacks(60, 600, 0, rng));
        long total = 0;
        for (int i = 0; i < 6000; i++) total += EliteCamps.ElitePacks(1, 30, 30, rng);
        Assert.InRange(total, 850, 1150);
    }

    [Fact]
    public void Elite_packs_pay_their_loot_twice_more_and_sometimes_gear()
    {
        StageConfig stage = Content.Stage(3);
        var plain = new Inventory();
        var rng = new XorShiftRandom(4);
        var elite = new Inventory();
        string text = EliteCamps.Loot(stage, elite, rng, 30);
        Assert.StartsWith("30 elite packs: +", text);
        long perMob = stage.SornPerMob;
        long monsters = (stage.PackSizeMin + stage.PackSizeMax) / 2;
        Assert.InRange(elite.Sorn, perMob * monsters * 30 * 2 * 80 / 100, perMob * monsters * 30 * 2 * 120 / 100);
        Assert.NotEmpty(elite.Loot);
        Assert.Equal("", EliteCamps.Loot(stage, plain, rng, 0));
        Assert.Equal(0, plain.Sorn);
    }
}
