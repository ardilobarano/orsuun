using Orsuun.Rules;
using Xunit;

namespace Orsuun.Rules.Tests;

/// <summary>Direct trade (GDD section 8): a two-step confirm with a 5 second hold after any change, level 30, 72 hours, 2% tax.</summary>
public class TradeTests
{
    [Fact]
    public void Both_lock_then_both_confirm_and_nothing_moves_during_the_hold()
    {
        Assert.Equal(5, DirectTrade.LockSeconds);
        Assert.Null(DirectTrade.Advance(TradeStep.Offering, TradeStep.Offering, lockLeft: 3));
        Assert.Equal(TradeStep.Locked, DirectTrade.Advance(TradeStep.Offering, TradeStep.Offering, 0));
        Assert.Null(DirectTrade.Advance(TradeStep.Locked, TradeStep.Offering, 0));          // they have not locked
        Assert.Equal(TradeStep.Confirmed, DirectTrade.Advance(TradeStep.Locked, TradeStep.Locked, 0));
        Assert.Equal(TradeStep.Confirmed, DirectTrade.Advance(TradeStep.Locked, TradeStep.Confirmed, 0));
        Assert.Null(DirectTrade.Advance(TradeStep.Confirmed, TradeStep.Locked, 0));
        Assert.False(DirectTrade.Complete(TradeStep.Confirmed, TradeStep.Locked));
        Assert.True(DirectTrade.Complete(TradeStep.Confirmed, TradeStep.Confirmed));

        var changed = new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(5, DirectTrade.LockLeft(changed, changed));
        Assert.Equal(1, DirectTrade.LockLeft(changed, changed.AddSeconds(4.2)));
        Assert.Equal(0, DirectTrade.LockLeft(changed, changed.AddSeconds(5)));
    }

    [Fact]
    public void Level_30_a_72_hour_account_and_a_2_percent_tax()
    {
        Assert.NotNull(DirectTrade.Problem(29, 500));
        Assert.NotNull(DirectTrade.Problem(40, 71.9));
        Assert.Null(DirectTrade.Problem(40, 71.9, checkAge: false));
        Assert.Null(DirectTrade.Problem(30, 72));
        Assert.Equal(98_000, DirectTrade.Received(100_000));
        Assert.Equal(2_000, DirectTrade.Tax(100_000));
        Assert.Equal(0, DirectTrade.Received(0));
    }

    [Fact]
    public void Offers_hold_at_most_eight_pieces_and_the_sorn_held()
    {
        Assert.Null(DirectTrade.OfferProblem(8, 1_000, 1_000));
        Assert.NotNull(DirectTrade.OfferProblem(9, 0, 0));
        Assert.NotNull(DirectTrade.OfferProblem(0, 1_001, 1_000));
        Assert.NotNull(DirectTrade.OfferProblem(0, -1, 1_000));
        Assert.NotNull(DirectTrade.OfferProblem(0, DirectTrade.MaxSorn + 1, long.MaxValue));
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        Assert.Equal(new[] { a, b }, DirectTrade.ParseIds(DirectTrade.FormatIds(new[] { a, b, a })));
        Assert.Empty(DirectTrade.ParseIds("not,guids"));
    }
}
