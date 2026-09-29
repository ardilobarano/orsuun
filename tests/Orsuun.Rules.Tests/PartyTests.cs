using Orsuun.Rules;
using Xunit;

namespace Orsuun.Rules.Tests;

public class PartyTests
{
    [Fact]
    public void Bonus_is_five_percent_a_partymate_up_to_three()
    {
        Assert.Equal(0, Parties.BonusBp(0));
        Assert.Equal(500, Parties.BonusBp(1));
        Assert.Equal(1500, Parties.BonusBp(3));
        Assert.Equal(1500, Parties.BonusBp(9));
        Assert.Equal(0, Parties.BonusBp(-2));
    }

    [Fact]
    public void Heroes_on_one_map_hunt_together_wherever_on_it()
    {
        Assert.True(Parties.Together(1, 10));             // the Oathfields' first and last stages
        Assert.False(Parties.Together(10, 11));           // the Oathfields and Gorak Pass
        Assert.True(Parties.Together(Content.SaltFlats, Content.SaltFlats));
        Assert.False(Parties.Together(Content.SaltFlats, Content.FrostPasture));
        Assert.False(Parties.Together(Content.EmberSteppe, 1));
    }

    [Fact]
    public void Dungeon_floors_are_never_together()
    {
        int floor = Dungeons.FloorStageBase + 10 + 1;
        Assert.True(Dungeons.IsFloor(floor));
        Assert.False(Parties.Together(floor, floor));
    }
}
