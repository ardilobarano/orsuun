using Orsuun.Rules;
using Xunit;

namespace Orsuun.Rules.Tests;

/// <summary>The Exchange's stackable goods and price history (owner, 26 Sep 2026: "Exchange: materials + prices").</summary>
public class TradeGoodsTests
{
    [Fact]
    public void Every_good_moves_its_own_count_and_nothing_else()
    {
        Assert.Equal(16, TradeGoods.Count);
        for (int id = 0; id < TradeGoods.Count; id++)
        {
            var inv = new Inventory();
            TradeGoods.Add(inv, id, 7);
            Assert.Equal(7, TradeGoods.Held(inv, id));
            for (int other = 0; other < TradeGoods.Count; other++)
                if (other != id) Assert.Equal(0, TradeGoods.Held(inv, other));
            TradeGoods.Add(inv, id, -7);
            Assert.Equal(0, TradeGoods.Held(inv, id));
            Assert.Equal(0, inv.HuntMarks);
            Assert.Equal(0L, inv.Sorn);
        }
    }

    [Fact]
    public void Goods_are_named_and_the_korshards_come_by_rank()
    {
        Assert.Equal("Turnstone", TradeGoods.Name(5));
        Assert.Equal("Master's Needle", TradeGoods.Name(7));
        Assert.Equal("Trooper Korshard", TradeGoods.Name(TradeGoods.FirstKorshard));
        Assert.Equal("Guard of the Khan Korshard", TradeGoods.Name(TradeGoods.Count - 1));
        Assert.False(TradeGoods.Valid(-1));
        Assert.False(TradeGoods.Valid(TradeGoods.Count));
        var inv = new Inventory();
        TradeGoods.Add(inv, TradeGoods.FirstKorshard + 2, 3);
        Assert.Equal(new[] { 0, 0, 3, 0, 0 }, inv.Korshards);
    }

    [Fact]
    public void A_price_history_compares_one_each_and_pieces_of_the_same_band()
    {
        Assert.Equal(1_500, Market.UnitPrice(15_000, 10));
        Assert.Equal(15_000, Market.UnitPrice(15_000, 1));
        Assert.Equal((60, 69), Market.BandLevels(6));
        Assert.Equal((0, 9), Market.BandLevels(0));
        Assert.Equal((100, int.MaxValue), Market.BandLevels(ItemLooks.MaxTier));
        Assert.Equal(14, Market.HistoryDays);
    }
}

/// <summary>The bag (owner, 26 Sep 2026): 120 pieces, new drops left behind when full, pieces sold by hand for sorn.</summary>
public class BagTests
{
    [Fact]
    public void A_full_bag_leaves_new_drops_behind_the_best_rarity_kept_when_some_fit()
    {
        Assert.Equal(120, Bag.Size);
        var common = new ItemState(40, Rarity.Common, EquipSlot.Helmet);
        var epic = new ItemState(40, Rarity.Epic, EquipSlot.Weapon);
        var rare = new ItemState(40, Rarity.Rare, EquipSlot.Shoes);
        var drops = new[] { common, epic, rare };
        Assert.Equal(drops, Bag.Fitting(10, drops));
        Assert.Empty(Bag.Fitting(120, drops));
        Assert.Empty(Bag.Fitting(130, drops));
        Assert.Equal(new[] { epic, rare }, Bag.Fitting(118, drops));
        Assert.Equal(new[] { epic }, Bag.Fitting(119, drops));
    }

    [Fact]
    public void The_merchant_pays_by_level_rarity_and_forge_level()
    {
        Assert.Equal((150 + 30 * 60) * 2, Bag.SellPrice(new ItemState(60, Rarity.Common, EquipSlot.Helmet)));
        Assert.Equal((150 + 30 * 60) * 8, Bag.SellPrice(new ItemState(60, Rarity.Rare, EquipSlot.Helmet)));
        var forged = new ItemState(60, Rarity.Rare, EquipSlot.Helmet) { UpgradeLevel = 4 };
        Assert.Equal((150 + 30 * 60) * 8 * 2, Bag.SellPrice(forged));
        Assert.True(Bag.SellPrice(new ItemState(100, Rarity.Legendary, EquipSlot.Weapon)) > Bag.SellPrice(new ItemState(100, Rarity.Epic, EquipSlot.Weapon)));
        // Far below what the Forge takes to raise a piece one level.
        Assert.True(Bag.SellPrice(forged) < ForgeRules.Cost(60, 4));
    }
}

public class BagOverflowTests
{
    [Fact]
    public void The_lane_leaves_new_drops_behind_and_never_a_stored_piece()
    {
        var session = new PlayerSession(new XorShiftRandom(7));
        var stored = new HashSet<ItemState>();
        for (int i = 0; i < Bag.Size + 2; i++)
        {
            var piece = new ItemState(10, Rarity.Common, EquipSlot.Helmet);
            session.Inventory.Loot.Add(piece);
            stored.Add(piece);
        }
        var drop = new ItemState(10, Rarity.Epic, EquipSlot.Weapon);
        session.Inventory.Loot.Add(drop);
        List<ItemState> left = session.LeaveBehindOverflow(stored.Contains);
        Assert.Equal(new[] { drop }, left);
        Assert.Equal(Bag.Size + 2, session.Inventory.Loot.Count);
        Assert.Empty(session.LeaveBehindOverflow(stored.Contains));
        Assert.Equal(2, session.LeaveBehindOverflow().Count);
        Assert.Equal(Bag.Size, session.Inventory.Loot.Count);
    }
}
