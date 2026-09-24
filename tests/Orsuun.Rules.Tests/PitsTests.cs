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
}
