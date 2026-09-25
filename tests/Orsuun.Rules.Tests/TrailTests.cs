using Orsuun.Rules;
using Xunit;

namespace Orsuun.Rules.Tests;

/// <summary>The Campaign Trail (GDD section 9): an 8 week season of 50 tiers, a free track and a paid one bought with Amber.</summary>
public class TrailTests
{
    private static readonly TrailSeason One = CampaignTrail.Season(1);

    private static (int Turnstones, int Scrolls, int Alloys, int Wards, List<string> Pieces) Track(bool paid)
    {
        int turn = 0, scrolls = 0, alloys = 0, wards = 0;
        var pieces = new List<string>();
        for (int t = 1; t <= CampaignTrail.Tiers; t++)
        {
            TrailReward r = CampaignTrail.Reward(t, paid, One);
            turn += r.Turnstones; scrolls += r.ScrollsOfMercy; alloys += r.KhansAlloys; wards += r.AnvilWards;
            if (r.Piece != null) pieces.Add(r.Piece);
            Assert.False(string.IsNullOrEmpty(r.Text));
        }
        return (turn, scrolls, alloys, wards, pieces);
    }

    [Fact]
    public void The_paid_track_holds_what_the_gdd_lists()
    {
        var paid = Track(true);
        Assert.Equal(300, paid.Turnstones);
        Assert.Equal(20, paid.Alloys);
        Assert.Equal(3, paid.Wards);
        Assert.Equal(new[] { "amber-road-regalia", "amber-road-courser" }, paid.Pieces);
        Assert.Equal(WardrobeKind.Skin, Wardrobe.Find(One.Costume)!.Kind);
        Assert.Equal(WardrobeKind.Mount, Wardrobe.Find(One.Mount)!.Kind);
        Assert.False(Wardrobe.Find(One.Costume)!.Sold);
        Assert.True(CampaignTrail.IsTrailPiece(One.Mount));
        Assert.False(CampaignTrail.IsTrailPiece("hollow-steed"));
    }

    [Fact]
    public void The_free_track_pays_turnstones_scrolls_and_a_khans_alloy_every_ten_tiers()
    {
        var free = Track(false);
        Assert.Equal(CampaignTrail.Tiers / 10, free.Alloys);
        for (int t = 10; t <= CampaignTrail.Tiers; t += 10) Assert.Equal(1, CampaignTrail.Free(t).KhansAlloys);
        Assert.True(free.Turnstones > 0 && free.Scrolls > 0);
        Assert.Equal(0, free.Wards);
        Assert.Empty(free.Pieces);
        Assert.True(free.Turnstones < Track(true).Turnstones);
    }

    [Fact]
    public void Seasons_last_eight_weeks_from_the_bounty_week_of_21_September_2026()
    {
        Assert.Equal(56, CampaignTrail.SeasonDays);
        var start = new DateTime(2026, 9, 21, 20, 0, 0);
        Assert.Equal(1, CampaignTrail.Season(start).Number);
        Assert.Equal(1, CampaignTrail.Season(start.AddDays(55.9)).Number);
        Assert.Equal(2, CampaignTrail.Season(start.AddDays(56)).Number);
        Assert.Equal(1, CampaignTrail.Season(start.AddDays(-3)).Number);          // before the first: the first
        Assert.Equal(DayOfWeek.Monday, CampaignTrail.Season(2).StartLocal.DayOfWeek);
        Assert.Equal(Bounties.WeekStart(CampaignTrail.Season(3).StartLocal), CampaignTrail.Season(3).StartLocal);
        Assert.Equal(new DateTime(2026, 11, 16, 20, 0, 0), One.EndLocal);
        Assert.Equal(CampaignTrail.MinPieceDays, CampaignTrail.PieceDays(One, One.EndLocal.AddHours(-2)));
        Assert.Equal(53, CampaignTrail.PieceDays(One, new DateTime(2026, 9, 25, 12, 0, 0)));    // 52 days 8 hours, rounded up
    }

    [Fact]
    public void A_player_who_takes_every_daily_and_one_weekly_bounty_finishes_the_season()
    {
        int dailies = Bounties.All.Count(b => b.Period == BountyPeriod.Daily);
        int weeklies = Bounties.All.Count(b => b.Period == BountyPeriod.Weekly);
        Assert.Equal(CampaignTrail.DailyBountyXp, CampaignTrail.BountyXp(Bounties.All.First(b => b.Period == BountyPeriod.Daily)));
        long all = CampaignTrail.SeasonDays * dailies * (long)CampaignTrail.DailyBountyXp + CampaignTrail.SeasonDays / 7 * weeklies * (long)CampaignTrail.WeeklyBountyXp;
        long steady = CampaignTrail.SeasonDays * dailies * (long)CampaignTrail.DailyBountyXp + CampaignTrail.SeasonDays / 7 * (long)CampaignTrail.WeeklyBountyXp;
        long needed = CampaignTrail.Tiers * (long)CampaignTrail.XpPerTier;
        Assert.True(steady >= needed, $"{steady} < {needed}");
        Assert.True(all < needed * 3 / 2, "the whole board should not finish it in five weeks");
        Assert.True(CampaignTrail.SeasonDays * dailies * (long)CampaignTrail.DailyBountyXp < needed, "dailies alone fall short");
    }

    [Fact]
    public void The_trail_costs_the_amber_of_the_999_pack_and_plus_the_1999_pack_with_ten_tiers()
    {
        Assert.Equal(Amber.Packs.Single(p => p.PriceCents == 999).Amber, CampaignTrail.Price(TrailPass.None, TrailPass.Trail));
        Assert.Equal(Amber.Packs.Single(p => p.PriceCents == 1999).Amber, CampaignTrail.Price(TrailPass.None, TrailPass.Plus));
        Assert.Equal(CampaignTrail.PlusAmber - CampaignTrail.TrailAmber, CampaignTrail.Price(TrailPass.Trail, TrailPass.Plus));
        Assert.Equal(-1, CampaignTrail.Price(TrailPass.Plus, TrailPass.Trail));
        Assert.Equal(-1, CampaignTrail.Price(TrailPass.Trail, TrailPass.Trail));

        var p = new TrailProgress();
        p.Roll(1, out _);
        p.AddXp(1250);
        Assert.Equal(2, p.Tier);
        Assert.Equal(50, p.XpIntoTier);
        Assert.False(p.Ready(1, true));
        p.Buy(TrailPass.Trail);
        Assert.True(p.Ready(1, true));
        p.Buy(TrailPass.Plus);
        Assert.Equal(12, p.Tier);
        Assert.Throws<InvalidOperationException>(() => p.Buy(TrailPass.Plus));
    }

    [Fact]
    public void Rewards_are_claimed_once_and_a_new_season_hands_over_what_was_left()
    {
        var p = new TrailProgress();
        Assert.Empty(p.Roll(1, out int first));
        Assert.Equal(0, first);
        p.AddXp(3 * CampaignTrail.XpPerTier);
        Assert.Equal(new[] { (2, false) }, p.ClaimReady(2));
        Assert.Empty(p.ClaimReady(2));
        Assert.False(p.Ready(4, false));                                          // not reached
        Assert.Equal(new[] { (1, false), (3, false) }, p.ClaimReady());
        p.Buy(TrailPass.Trail);
        p.AddXp(CampaignTrail.XpPerTier);

        TrailProgress q = TrailProgress.Parse(p.Serialize());
        Assert.Equal(p.Serialize(), q.Serialize());
        Assert.Equal(TrailPass.Trail, q.Pass);
        Assert.True(q.Claimed(3, false) && !q.Claimed(3, true));

        Assert.Empty(q.Roll(1, out _));                                            // same season: nothing moves
        List<(int Tier, bool Paid)> owed = q.Roll(2, out int old);
        Assert.Equal(1, old);
        Assert.Equal(new[] { (1, true), (2, true), (3, true), (4, false), (4, true) }, owed);
        Assert.Equal(2, q.Season);
        Assert.Equal(0, q.Tier);
        Assert.Equal(TrailPass.None, q.Pass);
        Assert.Equal(0, q.FreeClaimed | q.PaidClaimed);
        Assert.Equal(0, TrailProgress.Parse("garbage").Season);
    }

    [Fact]
    public void A_season_piece_goes_to_the_wardrobe_drops_for_the_rest_of_the_season()
    {
        var inv = new Inventory();
        CampaignTrail.Paid(1, One).GrantTo(inv, 30);
        CampaignTrail.Paid(15, One).GrantTo(inv, 30);
        CampaignTrail.Free(4).GrantTo(inv, 30);
        Assert.Equal(new[] { "amber-road-regalia:30" }, inv.WardrobeDrops);
        Assert.Equal(8, inv.Turnstones);
        Assert.Equal(2, inv.KhansAlloys);
        Assert.Equal(1, inv.AnvilWards);
        Assert.Equal(1, inv.ScrollsOfMercy);
    }
}
