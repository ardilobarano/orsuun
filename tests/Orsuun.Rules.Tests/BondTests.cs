using Orsuun.Rules;
using Xunit;

namespace Orsuun.Rules.Tests;

/// <summary>Sworn bonds (7 Oct 2026): the ring grows with time together and raises the XP bonus; together means one party, one map.</summary>
public class BondTests
{
    [Fact]
    public void The_ring_grows_with_hours_together()
    {
        Assert.Equal(0, Bonds.RingLevel(0));
        Assert.Equal(0, Bonds.RingLevel(3599));
        Assert.Equal(1, Bonds.RingLevel(3600));
        Assert.Equal(3, Bonds.RingLevel(6 * 3600));
        Assert.Equal(Bonds.MaxRing, Bonds.RingLevel(1000 * 3600L));
        Assert.Equal(3 * 3600, Bonds.NextRingSeconds(1));
        Assert.Equal(-1, Bonds.NextRingSeconds(Bonds.MaxRing));
        Assert.Equal("Bond Ring", Bonds.RingName(0));
        Assert.Equal("Bond Ring +4", Bonds.RingName(4));
    }

    [Fact]
    public void The_bonus_rises_from_three_to_ten_percent()
    {
        Assert.Equal(300, Bonds.XpBonusBp(0));
        Assert.Equal(1000, Bonds.XpBonusBp(Bonds.MaxRing));
        Assert.Equal(1000, Bonds.XpBonusBp(99));
        Assert.True(Bonds.XpBonusBp(5) > Bonds.XpBonusBp(4));
    }

    [Fact]
    public void Together_means_one_party_one_map_and_the_partner_online()
    {
        var leader = System.Guid.NewGuid();
        Assert.True(Bonds.Together(leader, 3, leader, 8, true));             // the Oathfields' stages 3 and 8
        Assert.False(Bonds.Together(leader, 3, leader, 13, true));           // Gorak Pass
        Assert.False(Bonds.Together(leader, 3, leader, 8, false));           // gone
        Assert.False(Bonds.Together(leader, 3, System.Guid.NewGuid(), 8, true));
        Assert.False(Bonds.Together(null, 3, null, 8, true));                // no party
    }
}
