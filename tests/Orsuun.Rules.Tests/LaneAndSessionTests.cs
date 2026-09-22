using Orsuun.Rules;
using Orsuun.Rules.Combat;
using Xunit;

namespace Orsuun.Rules.Tests;

public class LaneSimTests
{
    private static PlayerSession RunSession(ulong seed, int ticks, int upgradeLevel = 0, bool autoCast = true)
    {
        var session = new PlayerSession(new XorShiftRandom(seed));
        session.Weapon.UpgradeLevel = upgradeLevel;
        session.Lane.SetHero(HeroFactory.FromWeapon(session.Weapon));
        for (int i = 0; i < session.Lane.AutoCast.Length; i++) session.Lane.AutoCast[i] = autoCast;
        for (int t = 0; t < ticks; t++)
        {
            session.Lane.Tick();
            session.Lane.DrainEvents();
        }
        return session;
    }

    [Fact]
    public void Same_seed_gives_the_same_run()
    {
        PlayerSession a = RunSession(77, 6_000);
        PlayerSession b = RunSession(77, 6_000);

        Assert.Equal(a.Inventory.Sorn, b.Inventory.Sorn);
        Assert.Equal(a.Lane.MobsKilled, b.Lane.MobsKilled);
        Assert.Equal(a.Lane.HeroHp, b.Lane.HeroHp);
    }

    [Fact]
    public void The_loop_reaches_and_destroys_korstones()
    {
        PlayerSession session = RunSession(5, 10 * 60 * LaneSim.TicksPerSecond);

        Assert.True(session.Lane.KorstonesDestroyed >= 2, $"Korstones destroyed: {session.Lane.KorstonesDestroyed}");
        Assert.True(session.Inventory.Turnstones > 5);
    }

    [Fact]
    public void A_plus9_weapon_farms_clearly_faster_than_a_plus0()
    {
        int ticks = 10 * 60 * LaneSim.TicksPerSecond;
        PlayerSession plus0 = RunSession(9, ticks, upgradeLevel: 0);
        PlayerSession plus9 = RunSession(9, ticks, upgradeLevel: 9);

        Assert.True(plus9.Lane.MobsKilled > plus0.Lane.MobsKilled * 13 / 10,
            $"+0 killed {plus0.Lane.MobsKilled}, +9 killed {plus9.Lane.MobsKilled}");
    }

    [Fact]
    public void Korstone_calls_four_waves()
    {
        var stage = new StageConfig { PacksBeforeKorstone = 0 };
        var session = new PlayerSession(new XorShiftRandom(11), stage);
        int waves = 0, korstoneDeaths = 0;

        for (int t = 0; t < 5_000 && korstoneDeaths == 0; t++)
        {
            session.Lane.Tick();
            foreach (LaneEvent e in session.Lane.DrainEvents())
            {
                if (e.Kind == LaneEventKind.KorstoneWave) waves++;
                if (e.Kind == LaneEventKind.Loot && e.Text!.StartsWith("Korstone chest")) korstoneDeaths++;
            }
        }

        Assert.Equal(1, korstoneDeaths);
        Assert.Equal(4, waves);
    }

    [Fact]
    public void A_hopeless_hero_dies_and_respawns_without_breaking_the_loop()
    {
        var stage = new StageConfig { MobAttack = 5_000 };
        var session = new PlayerSession(new XorShiftRandom(13), stage);

        for (int t = 0; t < 2_000; t++) session.Lane.Tick();

        Assert.True(session.Lane.Deaths >= 2);
    }
}

public class PlayerSessionTests
{
    [Fact]
    public void Forge_charges_sorn_and_the_scroll()
    {
        var session = new PlayerSession(new XorShiftRandom(21));
        long sornBefore = session.Inventory.Sorn;
        long cost = session.ForgeCost;

        session.Forge(ForgeMethod.ScrollOfMercy);

        Assert.Equal(sornBefore - cost, session.Inventory.Sorn);
        Assert.Equal(1, session.Inventory.ScrollsOfMercy);
    }

    [Fact]
    public void Forge_is_blocked_without_sorn_materials_or_the_consumable()
    {
        var session = new PlayerSession(new XorShiftRandom(22));

        Assert.Equal("No Khan's Alloy", session.ForgeBlocker(ForgeMethod.KhansAlloy));

        session.Weapon.UpgradeLevel = 3;
        session.Inventory.Sorn = 10_000_000;
        Assert.Equal("Not enough Wolf Sinew", session.ForgeBlocker(ForgeMethod.ForgeAlone));

        session.Inventory.Sorn = 0;
        Assert.Equal("Not enough sorn", session.ForgeBlocker(ForgeMethod.ForgeAlone));
        Assert.Throws<InvalidOperationException>(() => session.Forge(ForgeMethod.ForgeAlone));
    }

    [Fact]
    public void An_oathbreak_replaces_the_weapon_with_a_fresh_plus0()
    {
        var session = new PlayerSession(new XorShiftRandom(23));
        session.Inventory.Sorn = long.MaxValue / 2;
        session.Inventory.Materials = 100_000;

        // Forge alone until the blade breaks once; from +3 upward every failure is an Oathbreak.
        for (int i = 0; i < 10_000 && session.WeaponsBroken == 0; i++)
        {
            if (session.Weapon.UpgradeLevel >= ItemState.MaxUpgradeLevel) session.Weapon.UpgradeLevel = 3;
            session.Forge(ForgeMethod.ForgeAlone);
        }

        Assert.Equal(1, session.WeaponsBroken);
        Assert.Equal(0, session.Weapon.UpgradeLevel);
        Assert.False(session.Weapon.Destroyed);
        Assert.Equal(5, session.Weapon.Etchings.Count);
    }

    [Fact]
    public void Turn_spends_a_turnstone()
    {
        var session = new PlayerSession(new XorShiftRandom(24));

        session.Turn();

        Assert.Equal(4, session.Inventory.Turnstones);
    }
}
