using Orsuun.Rules;
using Orsuun.Rules.Combat;
using Xunit;

namespace Orsuun.Rules.Tests;

/// <summary>Dungeons (GDD section 2, world bible section 6): the Hollow Spire's floors, its smith, its Warden.</summary>
public class DungeonTests
{
    private static readonly DungeonDef Spire = Dungeons.Find(1)!;

    private static HeroStats Geared(int level, int itemLevel, int upgrade)
    {
        var items = new List<ItemState>();
        for (int s = 0; s < 8; s++) items.Add(new ItemState(itemLevel, Rarity.Rare, (EquipSlot)s) { UpgradeLevel = upgrade });
        return HeroFactory.FromEquipment(items, level);
    }

    [Fact]
    public void The_hollow_spire_has_nine_floors_a_rush_a_smith_and_a_warden()
    {
        Assert.Equal("The Hollow Spire", Spire.Name);
        Assert.Equal(9, Spire.Floors);
        Assert.False(Dungeons.Fought(Spire, 6));                          // the Chained Smith is not fought
        Assert.True(Dungeons.Fought(Spire, 5));
        StageConfig rush = Dungeons.Floor(Spire, 3, 20);
        Assert.Equal(0, rush.PacksBeforeKorstone);
        Assert.Equal(1, rush.ElderEvery);                                  // an Elder at once
        StageConfig warden = Dungeons.Floor(Spire, 9, 20);
        Assert.Equal(FinalEncounter.Boss, warden.FinalEncounter);
        Assert.Equal("The Spire Warden", warden.BossName);
        Assert.Equal(Rarity.Legendary, warden.GearRarityCap);              // GDD: Legendary from dungeons
        Assert.True(Dungeons.Floor(Spire, 8, 20).MobHp > Dungeons.Floor(Spire, 1, 20).MobHp);
        Assert.True(Dungeons.IsFloor(warden.StageNumber) && !Dungeons.IsFloor(40) && !Dungeons.IsFloor(121));
        Assert.Equal("The Hollow Spire, floor 9", Content.StageName(warden.StageNumber));
    }

    [Fact]
    public void A_run_follows_the_stage_the_hero_has_reached()
    {
        Assert.Equal(10, Dungeons.Level(Spire, 3));                         // never below its unlock stage
        Assert.Equal(27, Dungeons.Level(Spire, 27));
        Assert.Equal(Content.TotalStages, Dungeons.Level(Spire, 999));
    }

    [Theory]
    [InlineData(20)]
    [InlineData(40)]
    public void A_hero_geared_for_their_stage_clears_it_one_behind_falls(int level)
    {
        int upgrade = level >= 30 ? 7 : 6;
        int ready = 0, behind = 0;
        for (ulong run = 1; run <= 10; run++)
        {
            if (Clears(Geared(level, level, upgrade), level, run)) ready++;
            if (Clears(Geared(level - 5, level - 5, upgrade - 1), level, run)) behind++;
        }
        Assert.True(ready >= 8, $"geared: {ready}/10");
        Assert.True(behind <= 3, $"behind: {behind}/10");
    }

    private static bool Clears(HeroStats hero, int level, ulong run)
    {
        var inventory = new Inventory { Potions = 20 };
        for (int floor = 1; floor <= Spire.Floors; floor++)
        {
            if (!Dungeons.Fought(Spire, floor)) continue;
            if (!StageRun.Simulate(Dungeons.Floor(Spire, floor, level), hero, inventory, run * 97 + (ulong)floor).Cleared) return false;
        }
        return true;
    }

    [Fact]
    public void The_chained_smith_adds_ten_points_and_the_warden_pays_a_chest()
    {
        var item = new ItemState(20, Rarity.Rare) { UpgradeLevel = 5 };
        var forge = new ForgeService();
        Assert.Equal(forge.ChanceBp(item, ForgeMethod.ForgeAlone) + 1000, forge.ChanceBp(item, ForgeMethod.ChainedSmith));

        var inventory = new Inventory();
        string chest = Dungeons.WardenChest(inventory, 20, new XorShiftRandom(3));
        Assert.Equal(5, inventory.Turnstones);
        Assert.Equal(1, inventory.EtchingNeedles);
        Assert.Equal(1, inventory.Korshards[2]);                            // a Captain Korshard at level 20
        Assert.Contains("Turnstones", chest);
    }
}
