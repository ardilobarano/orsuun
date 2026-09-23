using System;
using Orsuun.Rules;
using Xunit;

namespace Orsuun.Rules.Tests;

/// <summary>Owner, 24 Sep 2026: pieces in the bag can be forged and turned without equipping them.</summary>
public class BagForgeTests
{
    private static (PlayerSession session, ItemState bagSword) WithBagSword(ulong seed)
    {
        var session = new PlayerSession(new XorShiftRandom(seed));
        var sword = new ItemState(20, Rarity.Epic, EquipSlot.Weapon);
        var etch = new EtchingService();
        for (int i = 0; i < 3; i++) etch.TryAdd(sword, EtchingPool.For(EquipSlot.Weapon), NeedleKind.EtchingNeedle, new XorShiftRandom(90 + (ulong)i));
        session.Inventory.Loot.Add(sword);
        session.Inventory.Sorn = 50_000_000;
        session.Inventory.Materials = 10_000;
        session.Inventory.Turnstones = 100;
        session.Inventory.ScrollsOfMercy = 50;
        return (session, sword);
    }

    [Fact]
    public void A_bag_piece_goes_on_the_anvil_and_a_stranger_does_not()
    {
        (PlayerSession session, ItemState sword) = WithBagSword(1);
        session.PutOnAnvil(sword);
        Assert.Same(sword, session.OnAnvil);
        Assert.False(session.AnvilWorn);
        Assert.Throws<InvalidOperationException>(() => session.PutOnAnvil(new ItemState(20, Rarity.Epic)));
    }

    [Fact]
    public void Forging_a_bag_piece_leaves_the_hero_as_he_is()
    {
        (PlayerSession session, ItemState sword) = WithBagSword(2);
        long attack = session.Hero.Attack;
        session.PutOnAnvil(sword);
        for (int i = 0; i < 30 && sword.UpgradeLevel < 3; i++) session.Forge(ForgeMethod.ScrollOfMercy);
        Assert.True(sword.UpgradeLevel >= 1);
        Assert.Equal(0, session.Weapon.UpgradeLevel);
        Assert.Equal(attack, session.Hero.Attack);
        session.Equip(sword);
        Assert.True(session.Hero.Attack > attack);    // the level it earned in the bag counts once worn
    }

    [Fact]
    public void An_Oathbreak_in_the_bag_destroys_the_piece_and_gives_no_starter()
    {
        (PlayerSession session, ItemState sword) = WithBagSword(3);
        ItemState worn = session.Weapon;
        session.PutOnAnvil(sword);
        for (int i = 0; i < 10_000 && session.ItemsBroken == 0; i++)
        {
            if (sword.UpgradeLevel < 3 || sword.UpgradeLevel >= ItemState.MaxUpgradeLevel) sword.UpgradeLevel = 3;
            session.Forge(ForgeMethod.ForgeAlone);
        }
        Assert.Equal(1, session.ItemsBroken);
        Assert.Equal(1, session.WeaponsBroken);
        Assert.DoesNotContain(sword, session.Inventory.Loot);
        Assert.Empty(session.Inventory.Loot);
        Assert.Same(worn, session.Weapon);
        Assert.Same(worn, session.OnAnvil);           // the anvil falls back to the worn weapon
    }

    [Fact]
    public void Turning_a_bag_piece_spends_Turnstones_on_that_piece()
    {
        (PlayerSession session, ItemState sword) = WithBagSword(4);
        string before = Text(sword);
        string wornBefore = Text(session.Weapon);
        session.PutOnAnvil(sword);
        session.TurnBulk(10, null, 1, out _);
        Assert.True(session.Inventory.Turnstones < 100);
        Assert.NotEqual(before, Text(sword));
        Assert.Equal(wornBefore, Text(session.Weapon));
    }

    private static string Text(ItemState item)
    {
        var parts = new System.Collections.Generic.List<string>();
        foreach (Etching e in item.Etchings) parts.Add(e.EntryId + ":" + e.Tier + ":" + e.Value);
        return string.Join(",", parts);
    }
}
