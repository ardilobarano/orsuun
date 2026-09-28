using Orsuun.Rules;
using Orsuun.Rules.Combat;
using Xunit;

namespace Orsuun.Rules.Tests;

/// <summary>Temper past +9 (GDD section 12): ten steps, +1% base stats each, 60%, a failure drops a step, never the piece.</summary>
public class TemperTests
{
    [Fact]
    public void Each_step_adds_a_point_of_base_stats()
    {
        var weapon = new ItemState(50, Rarity.Rare) { UpgradeLevel = 9 };
        Assert.Equal(168, ForgeRules.StatPercent(weapon));
        long before = HeroFactory.FromEquipment(new[] { weapon }, 50).Attack;
        weapon.Temper = 10;
        Assert.Equal(178, ForgeRules.StatPercent(weapon));
        Assert.True(HeroFactory.FromEquipment(new[] { weapon }, 50).Attack > before);
    }

    [Fact]
    public void Only_a_plus_nine_takes_a_temper_and_a_failure_never_breaks_it()
    {
        var inv = new Inventory { Sorn = long.MaxValue / 4, Materials = 10_000 };
        Assert.NotNull(Tempering.Blocker(new ItemState(50, Rarity.Rare) { UpgradeLevel = 8 }, inv));
        var piece = new ItemState(50, Rarity.Rare) { UpgradeLevel = 9 };
        Assert.Null(Tempering.Blocker(piece, inv));
        var rng = new XorShiftRandom(9);
        int ups = 0;
        const int tries = 20_000;
        for (int i = 0; i < tries; i++)
        {
            piece.Temper = 5;
            TemperResult r = Tempering.Attempt(piece, rng);
            Assert.False(piece.Destroyed);
            if (r.Success) { ups++; Assert.Equal(6, r.After); } else Assert.Equal(4, r.After);
        }
        Assert.InRange(ups / (double)tries, 0.58, 0.62);
        piece.Temper = 0;
        while (Tempering.Attempt(piece, rng).Success) piece.Temper = 0;
        Assert.Equal(0, piece.Temper);
        Assert.True(Tempering.Cost(50, 9) > Tempering.Cost(50, 0));
    }
}

/// <summary>The sixth etching (GDD section 12): a Grandmaster's Needle, 10%, Epic and Legendary with five T3+.</summary>
public class SixthEtchingTests
{
    private static ItemState Five(Rarity rarity, int tier)
    {
        var item = new ItemState(60, rarity, EquipSlot.Armor);
        for (int i = 0; i < 5; i++) item.Etchings.Add(new Etching(i, tier, 10));
        return item;
    }

    [Fact]
    public void Only_epic_and_legendary_pieces_with_five_t3_etchings_take_a_sixth()
    {
        var inv = new Inventory { GrandmasterNeedles = 1 };
        Assert.NotNull(EtchingActions.EtchBlocker(Five(Rarity.Rare, 5), inv));
        Assert.NotNull(EtchingActions.EtchBlocker(Five(Rarity.Epic, 2), inv));
        Assert.Null(EtchingActions.EtchBlocker(Five(Rarity.Epic, 3), inv));
        Assert.NotNull(EtchingActions.EtchBlocker(Five(Rarity.Epic, 3), new Inventory()));
        Assert.Equal(1000, EtchingActions.EtchChanceBp(Five(Rarity.Legendary, 4)));
    }

    [Fact]
    public void A_grandmasters_needle_takes_one_time_in_ten_and_a_failure_costs_only_the_needle()
    {
        var rng = new XorShiftRandom(21);
        var service = new EtchingService();
        int took = 0;
        const int tries = 20_000;
        for (int i = 0; i < tries; i++)
        {
            ItemState item = Five(Rarity.Legendary, 4);
            var inv = new Inventory { GrandmasterNeedles = 1 };
            bool ok = EtchingActions.Etch(item, inv, service, rng);
            Assert.Equal(0, inv.GrandmasterNeedles);
            Assert.Equal(ok ? 6 : 5, item.Etchings.Count);
            if (ok) { took++; Assert.Equal(6, item.Etchings.Select(e => e.EntryId).Distinct().Count()); }
        }
        Assert.InRange(took / (double)tries, 0.09, 0.11);
    }
}
