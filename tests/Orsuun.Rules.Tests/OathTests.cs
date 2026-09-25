using Orsuun.Rules;
using Orsuun.Rules.Combat;
using Xunit;

namespace Orsuun.Rules.Tests;

/// <summary>
/// Changing Banners once a season for Oathstones (world bible: defection "once per season at a cost") and Oath Renewal
/// (GDD section 12: level 105 back to 1, +3% attack and HP a renewal, up to 10).
/// </summary>
public class OathTests
{
    [Fact]
    public void The_Banner_changes_once_a_season_for_five_Oathstones()
    {
        Assert.Equal(5, Banners.ChangeOathstones);
        Assert.Null(Banners.ChangeProblem(Banner.Ember, Banner.Sky, null, "2026-W39", 5));
        Assert.Null(Banners.ChangeProblem(Banner.Ember, Banner.Gold, "2026-W38", "2026-W39", 9));
        Assert.NotNull(Banners.ChangeProblem(Banner.Ember, Banner.Sky, "2026-W39", "2026-W39", 9));
        Assert.NotNull(Banners.ChangeProblem(Banner.Ember, Banner.Sky, null, "2026-W39", 4));
        Assert.NotNull(Banners.ChangeProblem(Banner.Ember, Banner.Ember, null, "2026-W39", 9));
        Assert.NotNull(Banners.ChangeProblem(Banner.None, Banner.Sky, null, "2026-W39", 9));
        Assert.NotNull(Banners.ChangeProblem(Banner.Sky, Banner.None, null, "2026-W39", 9));
    }

    [Fact]
    public void Oath_Renewal_opens_at_level_105_and_adds_three_percent_a_renewal_up_to_ten()
    {
        Assert.Equal(105, OathRenewal.RequiredLevel);
        Assert.NotNull(OathRenewal.Problem(104, 0));
        Assert.Null(OathRenewal.Problem(105, 0));
        Assert.Null(OathRenewal.Problem(105, 9));
        Assert.NotNull(OathRenewal.Problem(105, 10));
        Assert.Equal(0, OathRenewal.BonusPercent(0));
        Assert.Equal(9, OathRenewal.BonusPercent(3));
        Assert.Equal(30, OathRenewal.BonusPercent(10));
        Assert.Equal(30, OathRenewal.BonusPercent(14));

        var gear = new[] { new ItemState(60, Rarity.Epic, EquipSlot.Weapon) { UpgradeLevel = 9 }, new ItemState(60, Rarity.Epic, EquipSlot.Armor) { UpgradeLevel = 9 } };
        HeroStats plain = HeroFactory.FromEquipment(gear, 1);
        HeroStats renewed = HeroFactory.FromEquipment(gear, 1, renewals: 10);
        Assert.Equal(plain.Attack * 130 / 100, renewed.Attack);
        Assert.Equal(plain.MaxHp * 130 / 100, renewed.MaxHp);
        Assert.Equal(plain.Defense, renewed.Defense);
    }

    [Fact]
    public void The_session_counts_its_renewals_in_the_hero()
    {
        var session = new PlayerSession(new XorShiftRandom(7));
        long before = session.Hero.Attack;
        session.SetRenewals(2);
        Assert.Equal(2, session.Renewals);
        Assert.Equal(before * 106 / 100, session.Hero.Attack);
    }

    [Fact]
    public void The_Last_Carver_pays_an_Oathstone_and_two_with_the_vault_open()
    {
        DungeonDef archive = Dungeons.Find(3)!;
        var shut = new Inventory();
        Assert.Contains("an Oathstone", Dungeons.WardenChest(shut, 40, new XorShiftRandom(5), archive));
        Assert.Equal(1, shut.Oathstones);
        var open = new Inventory();
        Assert.Contains("2 Oathstones", Dungeons.WardenChest(open, 40, new XorShiftRandom(5), archive, vaultOpen: true));
        Assert.Equal(2, open.Oathstones);
        var spire = new Inventory();
        Dungeons.WardenChest(spire, 40, new XorShiftRandom(5), Dungeons.Find(1), vaultOpen: true);
        Assert.Equal(0, spire.Oathstones);
    }
}
