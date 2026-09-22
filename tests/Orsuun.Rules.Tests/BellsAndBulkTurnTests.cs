using Orsuun.Rules;
using Orsuun.Rules.Combat;
using Xunit;

namespace Orsuun.Rules.Tests;

public class BellsAndBulkTurnTests
{
    [Theory]
    [InlineData(20, 59, Bell.None)]
    [InlineData(21, 0, Bell.KorstoneBell)]
    [InlineData(21, 29, Bell.KorstoneBell)]
    [InlineData(21, 30, Bell.LevelBell)]
    [InlineData(22, 5, Bell.LootBell)]
    [InlineData(22, 45, Bell.MaterialBell)]
    [InlineData(23, 10, Bell.WarBell)]
    [InlineData(23, 30, Bell.None)]
    [InlineData(3, 0, Bell.None)]
    public void Bells_follow_the_evening_schedule(int hour, int minute, Bell expected)
    {
        Assert.Equal(expected, EveningBells.Active(new DateTime(2026, 9, 22, hour, minute, 0)));
    }

    [Fact]
    public void Next_bell_counts_down_and_wraps_past_midnight()
    {
        Assert.Equal(Bell.KorstoneBell, EveningBells.Next(new DateTime(2026, 9, 22, 20, 45, 0), out int m1));
        Assert.Equal(15, m1);
        Assert.Equal(Bell.WarBell, EveningBells.Next(new DateTime(2026, 9, 22, 22, 50, 0), out int m2));
        Assert.Equal(10, m2);
        Assert.Equal(Bell.KorstoneBell, EveningBells.Next(new DateTime(2026, 9, 22, 23, 40, 0), out int m3));
        Assert.Equal(21 * 60 + 20, m3);
    }

    [Fact]
    public void Korstone_bell_doubles_the_chest_and_war_bell_raises_damage()
    {
        var inventory = new Inventory();
        var plain = new Inventory();
        StageConfig bell = EveningBells.Apply(Content.Stage(111), Bell.KorstoneBell);
        HuntYield.LootKorstone(bell, inventory, new XorShiftRandom(1));
        HuntYield.LootKorstone(Content.Stage(111), plain, new XorShiftRandom(1));
        Assert.Equal(plain.Sorn * 2, inventory.Sorn);
        Assert.True(inventory.Turnstones >= plain.Turnstones * 2 - 1);

        StageConfig war = EveningBells.Apply(Content.Stage(1), Bell.WarBell);
        Assert.Equal(125, war.DamagePercent);
        StageConfig loot = EveningBells.Apply(Content.Stage(1), Bell.LootBell);
        Assert.Equal(Rarity.Legendary, loot.GearRarityCap);
    }

    [Fact]
    public void Bulk_turn_stops_on_the_rule_and_never_overspends()
    {
        var session = new PlayerSession(new XorShiftRandom(21));
        session.Inventory.Turnstones = 50;

        int turns = session.TurnBulk(50, WeaponEtchingIds.StrongAgainstOathsworn, 1, out bool stopped);

        Assert.True(stopped, "31% per turn should hit within 50 turns");
        Assert.Equal(50 - turns, session.Inventory.Turnstones);
        Assert.True(EtchingService.Matches(session.Weapon, WeaponEtchingIds.StrongAgainstOathsworn, 1));

        session.Inventory.Turnstones = 3;
        int few = session.TurnBulk(10, WeaponEtchingIds.Critical, 5, out bool stopped2);
        Assert.True(few <= 3);
        Assert.Equal(3 - few, session.Inventory.Turnstones);
    }

    [Fact]
    public void Bulk_turn_is_capped_at_fifty()
    {
        var session = new PlayerSession(new XorShiftRandom(22));
        session.Inventory.Turnstones = 200;
        int turns = session.TurnBulk(500, null, 1, out _);
        Assert.Equal(EtchingService.BulkTurnMax, turns);
    }
}
