using System.Linq;
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

public class PartyDungeonTests
{
    [Fact]
    public void The_party_chest_is_one_wardens_chest_a_clearer_dealt_out_evenly()
    {
        DungeonDef spire = Dungeons.Find(1)!;
        for (int clearers = 1; clearers <= 4; clearers++)
        {
            PartyDungeons.Share[] shares = PartyDungeons.Pool(spire, 30, clearers, new XorShiftRandom((ulong)(7 + clearers)));
            Assert.Equal(clearers, shares.Length);
            // The same pool rolled again, whole.
            var pool = new Inventory();
            var rng = new XorShiftRandom((ulong)(7 + clearers));
            for (int i = 0; i < clearers; i++) Dungeons.WardenChest(pool, 30, rng, spire);
            for (int good = 0; good < TradeGoods.Count; good++)
            {
                int[] got = shares.Select(s => s.Goods.TryGetValue(good, out int n) ? n : 0).ToArray();
                Assert.Equal(TradeGoods.Held(pool, good), got.Sum());
                Assert.True(got.Max() - got.Min() <= 1, $"good {good}: {string.Join(",", got)}");
            }
            Assert.Equal(pool.Books.Sum(), shares.Sum(s => s.Books.Values.Sum()));
            Assert.All(shares, s => Assert.False(s.Empty));
        }
        Assert.Empty(PartyDungeons.Pool(spire, 30, 0, new XorShiftRandom(1)));
    }

    [Fact]
    public void Party_play_has_a_daily_bounty_and_achievements_with_titles_for_the_long_ones()
    {
        BountyDef bounty = Bounties.Find(9)!;
        Assert.Equal(BountyPeriod.Daily, bounty.Period);
        Assert.Equal(FeatMetric.PartyDungeonClears, Bounties.FeatOf(bounty.Metric));
        Assert.False(CampaignTrail.PaysTrail(bounty));   // only the first seven metrics' bounties pay Trail XP
        Assert.Equal(FeatMetric.PartySeconds, Achievements.Find(88)!.Metric);
        Assert.Equal(FeatMetric.PartyDungeonClears, Achievements.Find(89)!.Metric);
        Assert.Equal(FeatMetric.PartyChests, Achievements.Find(91)!.Metric);
        Assert.Null(Achievements.Find(89)!.Title);
        Assert.Equal("Delve-Captain", Achievements.Find(90)!.Title);
        Assert.Equal("Keeper of the Spoils", Achievements.Find(92)!.Title);
    }
}
