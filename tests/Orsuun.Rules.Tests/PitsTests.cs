using Orsuun.Rules;
using Orsuun.Rules.Combat;
using Xunit;

namespace Orsuun.Rules.Tests;

/// <summary>The Pits (GDD section 7): leagues, rating moves, shades cut from the attacker's gear, the attacker's edge.</summary>
public class PitsTests
{
    [Fact]
    public void Leagues_run_from_bronze_to_khagan()
    {
        Assert.Equal("Bronze", Pits.League(Pits.StartRating));
        Assert.Equal("Iron", Pits.League(1100));
        Assert.Equal("Gold", Pits.League(1450));
        Assert.Equal("Khagan", Pits.League(2100));
    }

    [Fact]
    public void A_real_defender_moves_half_as_far_and_a_shade_not_at_all()
    {
        Assert.Equal((1016, 992), Pits.Rate(1000, 1000, attackerWon: true, realDefender: true));
        Assert.Equal((984, 1008), Pits.Rate(1000, 1000, attackerWon: false, realDefender: true));
        Assert.Equal((1016, 1000), Pits.Rate(1000, 1000, attackerWon: true, realDefender: false));
    }

    [Fact]
    public void Shades_are_the_attackers_gear_a_forge_level_apart()
    {
        var gear = new List<ItemState> { new(30, Rarity.Rare, EquipSlot.Weapon) { UpgradeLevel = 9 }, new(30, Rarity.Epic, EquipSlot.Armor) { UpgradeLevel = 0 } };
        List<ItemState> weaker = Pits.ShadeGear(gear, -1), stronger = Pits.ShadeGear(gear, 1);
        Assert.Equal(new[] { 8, 0 }, weaker.Select(i => i.UpgradeLevel));
        Assert.Equal(new[] { 9, 1 }, stronger.Select(i => i.UpgradeLevel));
        Assert.Equal(9, gear[0].UpgradeLevel);                              // the hero's own pieces are untouched

        // With the attacker's edge an even shade is a little better than a coin flip; a stronger one is harder.
        var set = new List<ItemState>();
        for (int s = 0; s < 8; s++) set.Add(new ItemState(30, Rarity.Rare, (EquipSlot)s) { UpgradeLevel = 5 });
        HeroStats me = Duels.Neutral(set, 30);
        double even = Duels.Edge(me, Duels.Neutral(Pits.ShadeGear(set, 0), 30)) + Pits.AttackerEdge;
        double hard = Duels.Edge(me, Duels.Neutral(Pits.ShadeGear(set, 1), 30)) + Pits.AttackerEdge;
        Assert.InRange(Duels.WinChance(even), 0.55, 0.7);
        Assert.True(Duels.WinChance(hard) < 0.5);
    }

    [Fact]
    public void The_pit_shop_sells_korshards_for_laurels()
    {
        PitShopItem rider = Pits.ShopItem(2)!;
        Assert.Equal(1, rider.KorshardRank);
        Assert.True(rider.Laurels > Pits.ShopItem(1)!.Laurels);
        Assert.Null(Pits.ShopItem(9));
    }

    [Fact]
    public void A_season_ends_with_Laurels_by_league_a_title_for_the_top_three_and_a_soft_reset()
    {
        Assert.Equal(3, Pits.SeasonMinFights);
        Assert.Equal(20, Pits.SeasonReward(1000, 10));
        Assert.Equal(40, Pits.SeasonReward(1100, 10));
        Assert.Equal(220 + 100, Pits.SeasonReward(1850, 1));
        Assert.Equal(160 + 50, Pits.SeasonReward(1650, 3));
        Assert.Equal(110, Pits.SeasonReward(1450, 4));
        Assert.Equal("Champion of the Pits", Pits.Title(1));
        Assert.Equal("Pit Veteran", Pits.Title(2));
        Assert.Null(Pits.Title(4));
        Assert.Equal(1400, Pits.SoftReset(1800));
        Assert.Equal(950, Pits.SoftReset(900));
        Assert.Equal(1000, Pits.SoftReset(1000));
        Assert.Equal(Bounties.WeekKey(new DateTime(2026, 9, 25, 12, 0, 0)), Pits.SeasonKey(new DateTime(2026, 9, 25, 12, 0, 0)));
    }

    [Fact]
    public void The_Pit_shop_sells_currencies_never_upgrade_protection()
    {
        var inv = new Inventory();
        foreach (PitShopItem item in Pits.Shop) item.GrantTo(inv);
        Assert.Equal(new[] { 1, 1, 1, 0, 0 }, inv.Korshards);
        Assert.Equal(5, inv.Turnstones);
        Assert.Equal(1, inv.EtchingNeedles);
        Assert.Equal(1, inv.PinningWax);
        Assert.Equal(1, inv.Oathstones);
        Assert.Equal(0, inv.AnvilWards);
        Assert.Equal(0, inv.ScrollsOfMercy);
        Assert.Equal(0, inv.KhansAlloys);
    }
}
