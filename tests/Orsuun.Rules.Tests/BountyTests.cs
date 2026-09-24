using Orsuun.Rules;
using Xunit;

namespace Orsuun.Rules.Tests;

/// <summary>Hunt Marks bounties, the Hunt Marks shop, and the two etching tools it sells.</summary>
public class BountyTests
{
    [Fact]
    public void The_day_turns_at_20_00_and_the_week_on_monday_20_00()
    {
        Assert.Equal("2026-09-23", Bounties.DayKey(new DateTime(2026, 9, 24, 19, 59, 0)));
        Assert.Equal("2026-09-24", Bounties.DayKey(new DateTime(2026, 9, 24, 20, 0, 0)));
        // Monday 28 Sep 2026: before 20:00 it is still the week that began Monday 21 Sep.
        Assert.Equal("W2026-09-21", Bounties.WeekKey(new DateTime(2026, 9, 28, 19, 0, 0)));
        Assert.Equal("W2026-09-28", Bounties.WeekKey(new DateTime(2026, 9, 28, 20, 30, 0)));
        Assert.Equal("W2026-09-21", Bounties.WeekKey(new DateTime(2026, 9, 27, 23, 0, 0)));
        Assert.Equal(60, Bounties.SecondsToDailyReset(new DateTime(2026, 9, 24, 19, 59, 0)));
    }

    [Fact]
    public void Counts_and_claims_reset_with_their_period()
    {
        var p = new BountyProgress();
        p.Roll("2026-09-24", "W2026-09-21");
        p.Add(BountyMetric.Korstones, 25);
        BountyDef daily = Bounties.Find(1)!;
        BountyDef weekly = Bounties.Find(11)!;
        Assert.True(p.Claimable(daily));
        Assert.Equal(3, p.Claim(daily));
        Assert.False(p.Claimable(daily));
        Assert.Throws<InvalidOperationException>(() => p.Claim(daily));
        Assert.Throws<InvalidOperationException>(() => p.Claim(weekly));

        BountyProgress back = BountyProgress.Parse(p.Serialize());
        Assert.True(back.Claimed(daily));
        Assert.Equal(25, back.Count(weekly));

        back.Roll("2026-09-25", "W2026-09-21");          // a new day, the same week
        Assert.False(back.Claimed(daily));
        Assert.Equal(0, back.Count(daily));
        Assert.Equal(25, back.Count(weekly));
        back.Roll("2026-09-28", "W2026-09-28");          // a new week
        Assert.Equal(0, back.Count(weekly));
    }

    [Fact]
    public void Shop_spends_hunt_marks()
    {
        var inv = new Inventory { HuntMarks = 20 };
        HuntShop.Buy(inv, 1, 2);
        Assert.Equal(2, inv.EtchingNeedles);
        Assert.Equal(12, inv.HuntMarks);
        HuntShop.Buy(inv, 2, 2);
        Assert.Equal(2, inv.PinningWax);
        Assert.Equal(0, inv.HuntMarks);
        Assert.Throws<InvalidOperationException>(() => HuntShop.Buy(inv, 5, 1));
        Assert.Throws<InvalidOperationException>(() => HuntShop.Buy(inv, 99, 1));
    }

    [Fact]
    public void Needles_add_etchings_up_to_the_fourth()
    {
        var item = new ItemState(30, Rarity.Epic);
        var inv = new Inventory { EtchingNeedles = 20 };
        var rng = new XorShiftRandom(3);
        var service = new EtchingService();
        Assert.True(EtchingActions.Etch(item, inv, service, rng));   // the first always takes
        Assert.Single(item.Etchings);
        while (item.Etchings.Count < 4 && inv.EtchingNeedles > 0) EtchingActions.Etch(item, inv, service, rng);
        Assert.Equal(4, item.Etchings.Count);
        Assert.Contains("Master's Needle", EtchingActions.EtchBlocker(item, inv));
        Assert.Throws<InvalidOperationException>(() => EtchingActions.Etch(item, inv, service, rng));

        var empty = new Inventory();
        Assert.Contains("No Etching Needles", EtchingActions.EtchBlocker(new ItemState(30, Rarity.Epic), empty));
    }

    [Fact]
    public void Pinning_wax_holds_an_etching_and_unpinning_spends_it()
    {
        var item = new ItemState(30, Rarity.Epic);
        EtchingPool pool = EtchingPool.Weapon();
        for (int i = 0; i < 3; i++) item.Etchings.Add(new Etching(i, 1, pool.Entries[i].TierValues[0]));
        var inv = new Inventory { PinningWax = 1, Turnstones = 10 };

        EtchingActions.Pin(item, 1, inv);
        Assert.Equal(1, item.LockedEtchingIndex);
        Assert.Equal(0, inv.PinningWax);
        Etching held = item.Etchings[1];
        var service = new EtchingService();
        Assert.Equal(2, service.Turn(item, pool, new XorShiftRandom(8)));    // a pinned turn costs two
        Assert.Equal(held.EntryId, item.Etchings[1].EntryId);

        Assert.Contains("No Pinning Wax", EtchingActions.PinBlocker(item, 0, inv));
        EtchingActions.Pin(item, 1, inv);                                   // unpin: free, the wax is gone
        Assert.Equal(-1, item.LockedEtchingIndex);
        Assert.Equal(0, inv.PinningWax);
    }
}
