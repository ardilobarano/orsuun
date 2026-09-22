using Orsuun.Rules;
using Xunit;

namespace Orsuun.Rules.Tests;

public class ForgeTests
{
    /// <summary>Always rolls the same number, to force success (0) or failure (9999).</summary>
    private sealed class FixedRandom : IRandom
    {
        private readonly int _value;
        public FixedRandom(int value) => _value = value;
        public int NextInt(int maxExclusive) => _value % maxExclusive;
    }

    private static readonly IRandom AlwaysSucceed = new FixedRandom(0);
    private static readonly IRandom AlwaysFail = new FixedRandom(9999);

    private static ItemState ItemAt(int level) => new ItemState(60, Rarity.Rare) { UpgradeLevel = level };

    [Fact]
    public void Plus9_has_168_percent_of_base_stats()
    {
        Assert.Equal(100, ForgeRules.StatPercent(0));
        Assert.Equal(130, ForgeRules.StatPercent(6));
        Assert.Equal(168, ForgeRules.StatPercent(9));
    }

    [Fact]
    public void Cost_matches_the_gdd_example_for_a_level_60_weapon()
    {
        Assert.Equal(60_000, ForgeRules.Cost(60, 0));
        Assert.Equal(2_576_980, ForgeRules.Cost(60, 8));
    }

    [Fact]
    public void Expected_scrolls_to_plus9_match_the_gdd()
    {
        Assert.Equal(52.58, ForgeAnalysis.ExpectedAttemptsTotal(ForgeMethod.ScrollOfMercy), 2);
        Assert.Equal(26.22, ForgeAnalysis.ExpectedAttemptsTotal(ForgeMethod.KhansAlloy), 2);

        double[] mercy = ForgeAnalysis.ExpectedAttemptsPerStep(ForgeMethod.ScrollOfMercy);
        Assert.Equal(26.93, mercy[8], 2);
    }

    [Fact]
    public void Raw_gamble_chances_match_the_gdd()
    {
        Assert.Equal(0.02016, ForgeAnalysis.StraightRunChance(3, 9), 5);
        Assert.Equal(0.06, ForgeAnalysis.StraightRunChance(6, 9), 5);
    }

    [Fact]
    public void Success_raises_the_level_and_clears_patience()
    {
        ItemState item = ItemAt(7);
        item.PatienceBp = 300;

        ForgeResult result = new ForgeService().Attempt(item, ForgeMethod.ForgeAlone, AlwaysSucceed);

        Assert.Equal(ForgeOutcome.Success, result.Outcome);
        Assert.Equal(8, item.UpgradeLevel);
        Assert.Equal(0, item.PatienceBp);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    public void Forge_alone_never_breaks_an_item_below_the_plus4_attempt(int level, int expectedAfter)
    {
        ItemState item = ItemAt(level);

        ForgeResult result = new ForgeService().Attempt(item, ForgeMethod.ForgeAlone, AlwaysFail);

        Assert.Equal(ForgeOutcome.LevelLost, result.Outcome);
        Assert.False(item.Destroyed);
        Assert.Equal(expectedAfter, item.UpgradeLevel);
    }

    [Theory]
    [InlineData(ForgeMethod.ForgeAlone)]
    [InlineData(ForgeMethod.ChainedSmith)]
    public void Unprotected_failure_from_the_plus4_attempt_is_an_oathbreak(ForgeMethod method)
    {
        ItemState item = ItemAt(3);

        ForgeResult result = new ForgeService().Attempt(item, method, AlwaysFail);

        Assert.Equal(ForgeOutcome.Oathbreak, result.Outcome);
        Assert.True(item.Destroyed);
        Assert.Throws<InvalidOperationException>(() => new ForgeService().Attempt(item, method, AlwaysSucceed));
    }

    [Theory]
    [InlineData(ForgeMethod.ScrollOfMercy)]
    [InlineData(ForgeMethod.KhansAlloy)]
    public void Scroll_and_alloy_lose_one_level_instead_of_the_item(ForgeMethod method)
    {
        ItemState item = ItemAt(8);

        ForgeResult result = new ForgeService().Attempt(item, method, AlwaysFail);

        Assert.Equal(ForgeOutcome.LevelLost, result.Outcome);
        Assert.Equal(7, item.UpgradeLevel);
        Assert.False(item.Destroyed);
    }

    [Fact]
    public void Anvil_ward_keeps_the_level()
    {
        ItemState item = ItemAt(8);

        ForgeResult result = new ForgeService().Attempt(item, ForgeMethod.AnvilWard, AlwaysFail);

        Assert.Equal(ForgeOutcome.LevelKept, result.Outcome);
        Assert.Equal(8, item.UpgradeLevel);
    }

    [Fact]
    public void Alloy_adds_ten_points_and_chance_never_exceeds_100_percent()
    {
        var forge = new ForgeService();
        Assert.Equal(4000, forge.ChanceBp(ItemAt(8), ForgeMethod.KhansAlloy));
        Assert.Equal(10000, forge.ChanceBp(ItemAt(0), ForgeMethod.KhansAlloy));
    }

    [Fact]
    public void Patience_grows_one_point_per_failure_from_plus7_and_caps_at_ten()
    {
        var forge = new ForgeService();
        ItemState item = ItemAt(8);

        for (int i = 0; i < 15; i++) forge.Attempt(item, ForgeMethod.AnvilWard, AlwaysFail);

        Assert.Equal(ForgeRules.PatienceMaxBp, item.PatienceBp);
        Assert.Equal(4000, forge.ChanceBp(item, ForgeMethod.AnvilWard));
    }

    [Fact]
    public void Failures_below_the_plus7_attempt_earn_no_patience()
    {
        ItemState item = ItemAt(5);

        new ForgeService().Attempt(item, ForgeMethod.ScrollOfMercy, AlwaysFail);

        Assert.Equal(0, item.PatienceBp);
    }

    [Fact]
    public void Monte_carlo_with_scrolls_lands_on_the_analytic_average()
    {
        var forge = new ForgeService(patienceEnabled: false);
        var rng = new XorShiftRandom(20260922);
        const int runs = 20_000;
        long attempts = 0;

        for (int i = 0; i < runs; i++)
        {
            ItemState item = ItemAt(0);
            while (item.UpgradeLevel < ItemState.MaxUpgradeLevel)
            {
                forge.Attempt(item, ForgeMethod.ScrollOfMercy, rng);
                attempts++;
            }
        }

        Assert.InRange(attempts / (double)runs, 51.5, 53.7);
    }
}
