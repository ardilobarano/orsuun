using Orsuun.Rules;
using Xunit;

namespace Orsuun.Rules.Tests;

/// <summary>Feature unlocks (owner, 27 Sep 2026): screens open as the hero levels, each told once.</summary>
public class UnlockTests
{
    [Fact]
    public void Features_open_in_order_and_after_the_start()
    {
        int last = 1;
        foreach (Feature feature in Unlocks.All)
        {
            Assert.True(Unlocks.Level(feature) > last, $"{feature} opens after the one before it");
            last = Unlocks.Level(feature);
            Assert.False(Unlocks.Open(feature, Unlocks.Level(feature) - 1));
            Assert.True(Unlocks.Open(feature, Unlocks.Level(feature)));
            Assert.NotEmpty(Unlocks.Name(feature));
            Assert.NotEmpty(Unlocks.Tip(feature));
        }
        // The goal line asks a hero to join a guild after level 10: the guild must be open by then.
        Assert.True(Unlocks.Level(Feature.Guild) <= 10);
    }

    [Fact]
    public void A_level_up_tells_what_it_opened_once()
    {
        Assert.Empty(Unlocks.Between(1, 2));
        Assert.Equal(new[] { Feature.Bounties }, Unlocks.Between(2, 3));
        Assert.Equal(new[] { Feature.Bounties, Feature.Shards }, Unlocks.Between(1, 5));
        Assert.Empty(Unlocks.Between(3, 3));
        Assert.Equal(Unlocks.All.Length, Unlocks.Between(0, Content.MaxLevel).Count);
    }
}
