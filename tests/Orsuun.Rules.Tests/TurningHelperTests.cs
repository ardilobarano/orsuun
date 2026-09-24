using Orsuun.Rules;
using Xunit;

namespace Orsuun.Rules.Tests;

/// <summary>The turning helper (owner, 24 Sep 2026): Bulk Turn toward up to five etchings, each at a tier or better.</summary>
public class TurningHelperTests
{
    private static readonly EtchingPool Pool = EtchingPool.Weapon();

    private static ItemState Piece(Rarity rarity, int etchings)
    {
        var item = new ItemState(50, rarity);
        for (int i = 0; i < etchings; i++) item.Etchings.Add(new Etching(10 + i, 1, Pool.Entries[10 + i].TierValues[0]));
        return item;
    }

    [Fact]
    public void Chance_per_turn_is_the_draw_odds_times_the_tier_odds()
    {
        ItemState item = Piece(Rarity.Legendary, 5);
        // One target at any tier: 5 of 16 entries are drawn.
        Assert.Equal(5.0 / 16, EtchingService.TargetChance(item, Pool, new[] { new TurnTarget(WeaponEtchingIds.Critical, 1) }), 9);
        // Two at any tier: C(14,3) / C(16,5) = 364 / 4368.
        var two = new[] { new TurnTarget(WeaponEtchingIds.Critical, 1), new TurnTarget(WeaponEtchingIds.Piercing, 1) };
        Assert.Equal(364.0 / 4368, EtchingService.TargetChance(item, Pool, two), 9);
        // Tier weights 40/30/17/9/4: T3 or better is 30%.
        Assert.Equal(5.0 / 16 * 0.30, EtchingService.TargetChance(item, Pool, new[] { new TurnTarget(WeaponEtchingIds.Critical, 3) }), 9);
    }

    [Fact]
    public void Chance_matches_what_turning_actually_does()
    {
        ItemState item = Piece(Rarity.Epic, 4);
        var goal = new[] { new TurnTarget(WeaponEtchingIds.Critical, 2), new TurnTarget(WeaponEtchingIds.Str, 1) };
        double expected = EtchingService.TargetChance(item, Pool, goal);

        var rng = new XorShiftRandom(5);
        var service = new EtchingService();
        int hits = 0;
        const int turns = 200_000;
        for (int i = 0; i < turns; i++)
        {
            service.Turn(item, Pool, rng);
            if (EtchingService.MatchesAll(item, goal)) hits++;
        }
        Assert.InRange(hits / (double)turns, expected * 0.9, expected * 1.1);
    }

    [Fact]
    public void Goals_that_can_never_be_met_are_named()
    {
        ItemState three = Piece(Rarity.Rare, 3);
        var four = new[]
        {
            new TurnTarget(0, 1), new TurnTarget(1, 1), new TurnTarget(2, 1), new TurnTarget(3, 1),
        };
        Assert.Contains("at most 3", EtchingService.TargetProblem(three, Pool, four));
        Assert.Contains("once", EtchingService.TargetProblem(three, Pool, new[] { new TurnTarget(1, 1), new TurnTarget(1, 2) }));
        Assert.Contains("up to T3", EtchingService.TargetProblem(Piece(Rarity.Common, 3), Pool, new[] { new TurnTarget(1, 4) }));
        Assert.Null(EtchingService.TargetProblem(three, Pool, new[] { new TurnTarget(1, 5), new TurnTarget(2, 1) }));
        Assert.Equal(0.0, EtchingService.TargetChance(three, Pool, four));

        // Pinning Wax holds etching 0 (entry 10 at T1): it can be part of the goal only at T1, and it takes a slot.
        ItemState pinned = Piece(Rarity.Rare, 3);
        pinned.LockedEtchingIndex = 0;
        Assert.Contains("Pinning Wax", EtchingService.TargetProblem(pinned, Pool, new[] { new TurnTarget(10, 2) }));
        Assert.Contains("pinned", EtchingService.TargetProblem(pinned, Pool, new[] { new TurnTarget(1, 1), new TurnTarget(2, 1), new TurnTarget(3, 1) }));
        var withHeld = new[] { new TurnTarget(10, 1), new TurnTarget(1, 1) };
        Assert.Null(EtchingService.TargetProblem(pinned, Pool, withHeld));
        Assert.Equal(2.0 / 15, EtchingService.TargetChance(pinned, Pool, withHeld), 9);
    }

    [Fact]
    public void Bulk_turn_stops_only_when_every_target_is_there()
    {
        ItemState item = Piece(Rarity.Legendary, 5);
        var goal = new[] { new TurnTarget(WeaponEtchingIds.Critical, 1), new TurnTarget(WeaponEtchingIds.Piercing, 1) };
        var inventory = new Inventory { Turnstones = 2000 };
        var service = new EtchingService();
        var rng = new XorShiftRandom(9);

        bool stopped = false;
        int total = 0;
        while (!stopped && inventory.Turnstones > 0)
        {
            service.TurnUntil(item, Pool, inventory, rng, EtchingService.BulkTurnMax, goal, out int turns, out stopped);
            total += turns;
            Assert.True(stopped || turns == EtchingService.BulkTurnMax || inventory.Turnstones == 0);
        }

        Assert.True(stopped, "about 1 in 12 per turn should hit within 2000");
        Assert.True(EtchingService.MatchesAll(item, goal));
        Assert.Equal(2000 - total, inventory.Turnstones);
    }

    [Fact]
    public void Session_turns_a_bag_piece_without_touching_the_anvil()
    {
        var session = new PlayerSession(new XorShiftRandom(4));
        session.Inventory.Turnstones = 100;
        var helm = new ItemState(40, Rarity.Rare, EquipSlot.Helmet);
        EtchingPool armor = EtchingPool.For(EquipSlot.Helmet);
        for (int i = 0; i < 3; i++) helm.Etchings.Add(new Etching(i, 1, armor.Entries[i].TierValues[0]));
        session.Inventory.Loot.Add(helm);
        ItemState anvil = session.OnAnvil;
        string before = string.Join(",", anvil.Etchings.ConvertAll(e => e.EntryId));

        int turns = session.TurnBulk(helm, 30, new[] { new TurnTarget(ArmorEtchingIds.MaxHp, 1) }, out bool stopped);

        Assert.True(turns > 0);
        Assert.Equal(100 - turns, session.Inventory.Turnstones);
        Assert.Same(anvil, session.OnAnvil);
        Assert.Equal(before, string.Join(",", anvil.Etchings.ConvertAll(e => e.EntryId)));
        Assert.Equal(stopped, EtchingService.Matches(helm, ArmorEtchingIds.MaxHp, 1));

        var stranger = new ItemState(40, Rarity.Rare, EquipSlot.Helmet);
        stranger.Etchings.Add(new Etching(0, 1, 200));
        Assert.Throws<InvalidOperationException>(() => session.TurnBulk(stranger, 5, Array.Empty<TurnTarget>(), out _));
    }

    [Fact]
    public void Session_refuses_a_goal_out_of_reach()
    {
        var session = new PlayerSession(new XorShiftRandom(3));
        session.Inventory.Turnstones = 10;
        int count = session.OnAnvil.Etchings.Count;
        var tooMany = new TurnTarget[count + 1];
        for (int i = 0; i < tooMany.Length; i++) tooMany[i] = new TurnTarget(i, 1);
        Assert.Throws<InvalidOperationException>(() => session.TurnBulk(10, tooMany, out _));
        Assert.Equal(10, session.Inventory.Turnstones);
    }
}
