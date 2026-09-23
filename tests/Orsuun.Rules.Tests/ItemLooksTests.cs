using Orsuun.Rules;
using Xunit;

namespace Orsuun.Rules.Tests;

public class ItemLooksTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(9, 0)]
    [InlineData(10, 1)]
    [InlineData(19, 1)]
    [InlineData(20, 2)]
    [InlineData(105, 10)]
    public void A_new_look_every_ten_levels(int itemLevel, int tier) => Assert.Equal(tier, ItemLooks.Tier(itemLevel));

    [Fact]
    public void The_starter_weapon_keeps_its_name_and_gets_the_level_ten_look()
    {
        var starter = new ItemState(PlayerSession.StarterItemLevel, Rarity.Rare);
        Assert.Equal("Rare Rider's Glaive", starter.DisplayName);
        Assert.Equal("Weapon_T1", starter.LookId);
    }

    [Fact]
    public void Armour_names_and_looks_follow_the_band()
    {
        Assert.Equal("Common Quilted Coat", new ItemState(3, Rarity.Common, EquipSlot.Armor).DisplayName);
        Assert.Equal("Armor_T2", new ItemState(25, Rarity.Epic, EquipSlot.Armor).LookId);
    }

    [Fact]
    public void Stat_only_slots_have_no_look_and_one_name()
    {
        var early = new ItemState(2, Rarity.Rare, EquipSlot.Helmet);
        var late = new ItemState(80, Rarity.Rare, EquipSlot.Helmet);
        Assert.Null(early.LookId);
        Assert.Equal(early.DisplayName, late.DisplayName);
    }
}
