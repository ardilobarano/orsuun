using System;
using Orsuun.Rules;
using Xunit;

namespace Orsuun.Rules.Tests;

/// <summary>Every item follows the weapon's rules (owner decision, 23 Sep 2026).</summary>
public class AnyItemForgeTests
{
    private static (PlayerSession session, ItemState helmet) WithHelmet(ulong seed)
    {
        var session = new PlayerSession(new XorShiftRandom(seed));
        var helmet = new ItemState(10, Rarity.Rare, EquipSlot.Helmet);
        session.Inventory.Loot.Add(helmet);
        session.Equip(helmet);
        session.Inventory.Sorn = 50_000_000;
        session.Inventory.Materials = 10_000;
        session.Inventory.Turnstones = 100;
        return (session, helmet);
    }

    [Fact]
    public void The_anvil_starts_on_the_weapon_and_refuses_an_empty_slot()
    {
        var session = new PlayerSession(new XorShiftRandom(1));
        Assert.Equal(EquipSlot.Weapon, session.AnvilSlot);
        Assert.Same(session.Weapon, session.OnAnvil);
        Assert.Throws<InvalidOperationException>(() => session.PutOnAnvil(EquipSlot.Shoes));
    }

    [Fact]
    public void Forging_the_helmet_upgrades_the_helmet_and_leaves_the_weapon_alone()
    {
        (PlayerSession session, ItemState helmet) = WithHelmet(2);
        session.PutOnAnvil(EquipSlot.Helmet);
        Assert.Equal(ForgeRules.Cost(helmet.ItemLevel, 0), session.ForgeCost);
        session.Inventory.ScrollsOfMercy = 50;
        for (int i = 0; i < 30 && helmet.UpgradeLevel < 3; i++) session.Forge(ForgeMethod.ScrollOfMercy);
        Assert.True(helmet.UpgradeLevel >= 1);
        Assert.Equal(0, session.Weapon.UpgradeLevel);
    }

    [Fact]
    public void An_Oathbreak_replaces_the_helmet_with_a_starter_helmet()
    {
        (PlayerSession session, ItemState helmet) = WithHelmet(3);
        session.PutOnAnvil(EquipSlot.Helmet);
        ItemState weapon = session.Weapon;
        for (int i = 0; i < 10_000 && session.ItemsBroken == 0; i++)
        {
            if (session.OnAnvil.UpgradeLevel >= ItemState.MaxUpgradeLevel) session.OnAnvil.UpgradeLevel = 3;
            if (session.OnAnvil.UpgradeLevel < 3) session.OnAnvil.UpgradeLevel = 3;
            session.Forge(ForgeMethod.ForgeAlone);
        }
        Assert.Equal(1, session.ItemsBroken);
        Assert.Equal(0, session.WeaponsBroken);
        Assert.Same(weapon, session.Weapon);
        ItemState fresh = session.Equipped(EquipSlot.Helmet)!;
        Assert.NotSame(helmet, fresh);
        Assert.Equal(EquipSlot.Helmet, fresh.Slot);
        Assert.Equal(0, fresh.UpgradeLevel);
        Assert.Equal(ItemState.MaxEtchings, fresh.Etchings.Count);
    }

    [Fact]
    public void Turning_the_helmet_rolls_from_the_armour_pool()
    {
        (PlayerSession session, ItemState helmet) = WithHelmet(4);
        var etch = new EtchingService();
        for (int i = 0; i < 3; i++) etch.TryAdd(helmet, EtchingPool.For(EquipSlot.Helmet), NeedleKind.EtchingNeedle, new XorShiftRandom(40 + (ulong)i));
        session.PutOnAnvil(EquipSlot.Helmet);
        Assert.Same(EtchingPool.For(EquipSlot.Helmet), session.Pool);
        session.TurnBulk(20, null, 1, out _);
        Assert.NotEmpty(helmet.Etchings);
        foreach (Etching e in helmet.Etchings)
            Assert.InRange(e.EntryId, 0, EtchingPool.For(EquipSlot.Helmet).Entries.Count - 1);
    }
}
