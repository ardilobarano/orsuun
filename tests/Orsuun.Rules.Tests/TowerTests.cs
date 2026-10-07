using Orsuun.Rules;
using Orsuun.Rules.Combat;
using Xunit;

namespace Orsuun.Rules.Tests;

/// <summary>The Endless Tower (7 Oct 2026): floors as hard as the campaign's stages, then harder still; chests; the ladder.</summary>
public class TowerTests
{
    private static HeroStats Geared(int level, int itemLevel, int upgrade)
    {
        var items = new List<ItemState>();
        for (int s = 0; s < 8; s++) items.Add(new ItemState(itemLevel, Rarity.Rare, (EquipSlot)s) { UpgradeLevel = upgrade });
        return HeroFactory.FromEquipment(items, level);
    }

    /// <summary>The highest floor a climb clears (it stops at the first fall), as the server scores it.</summary>
    private static int Climb(HeroStats hero, ulong seed)
    {
        var inventory = new Inventory { Potions = 20 };
        for (int floor = 1; floor <= Tower.MaxFloors; floor++)
            if (!StageRun.Simulate(Tower.Floor(floor), hero, inventory, seed * 1000 + (ulong)floor).Cleared) return floor - 1;
        return Tower.MaxFloors;
    }

    [Fact]
    public void Floors_follow_the_campaign_then_grow_harder()
    {
        StageConfig first = Tower.Floor(1), stage1 = Content.Stage(1);
        Assert.Equal(stage1.MobHp, first.MobHp);
        Assert.Equal(1, first.PacksBeforeKorstone);
        Assert.Equal(FinalEncounter.Korstone, first.FinalEncounter);
        StageConfig tenth = Tower.Floor(10);
        Assert.Equal(FinalEncounter.Boss, tenth.FinalEncounter);
        Assert.Equal(Content.Stage(10).BossName, tenth.BossName);
        Assert.Equal(BossMechanic.None, tenth.BossMechanic);
        // Past the campaign's last stage: 2% harder a floor, compounding.
        Assert.Equal(100, Tower.HardnessPercent(Content.TotalStages));
        Assert.Equal(102, Tower.HardnessPercent(Content.TotalStages + 1));
        Assert.True(Tower.Floor(149).MobHp > Tower.Floor(139).MobHp * 12 / 10);
        Assert.Equal("The Tower's Shadow", Tower.Floor(130).BossName);
        Assert.True(Tower.IsFloor(Tower.Floor(37).StageNumber) && !Dungeons.IsFloor(Tower.Floor(37).StageNumber) && !Tower.IsFloor(40));
        Assert.Equal(37, Tower.FloorOf(Tower.Floor(37).StageNumber));
        Assert.Equal("The Endless Tower, floor 37", Content.StageName(Tower.Floor(37).StageNumber));
    }

    [Fact]
    public void Floors_borrow_the_dungeons_looks_ten_at_a_time()
    {
        Assert.Equal(1, Tower.LookOf(1));
        Assert.Equal(1, Tower.LookOf(10));
        Assert.Equal(2, Tower.LookOf(11));
        Assert.Equal(3, Tower.LookOf(25));
        Assert.Equal(1, Tower.LookOf(31));
        Assert.Equal(2, Tower.DungeonLook(Tower.Floor(15).StageNumber));
        Assert.Equal(3, Tower.DungeonLook(Dungeons.Floor(Dungeons.Find(3)!, 2, 40).StageNumber));
        Assert.Equal(0, Tower.DungeonLook(55));
    }

    [Fact]
    public void A_stronger_hero_climbs_higher()
    {
        int weak = 0, strong = 0;
        for (ulong run = 1; run <= 5; run++)
        {
            weak += Climb(Geared(20, 20, 6), run);
            strong += Climb(Geared(60, 60, 8), run);
        }
        Assert.True(weak > 0, "a level-20 hero clears the first floors");
        Assert.True(strong > weak + 5 * 15, $"strong {strong / 5} vs weak {weak / 5}");
        Assert.True(strong / 5 < Tower.MaxFloors, "nobody tops the tower");
    }

    [Fact]
    public void Milestone_chests_grow_with_the_floor()
    {
        var inventory = new Inventory();
        string ten = Tower.Chest(10, inventory, HeroClass.Kestrel, new XorShiftRandom(3));
        Assert.Contains("Turnstones", ten);
        Assert.DoesNotContain("Technique Scroll", ten);
        string thirty = Tower.Chest(30, inventory, HeroClass.Kestrel, new XorShiftRandom(3));
        Assert.Contains("Technique Scroll", thirty);
        int kestrelBooks = 0;
        for (int slot = 0; slot < SkillGrades.Slots; slot++) kestrelBooks += inventory.Books[Books.Id(HeroClass.Kestrel, slot)];
        Assert.Equal(1, kestrelBooks);
        string hundred = Tower.Chest(100, inventory, HeroClass.Kestrel, new XorShiftRandom(3));
        Assert.Contains("Oathstone", hundred);
        Assert.Contains("Khan's Alloy", hundred);
        Assert.Equal(20, Tower.NextMilestone(10));
        Assert.Equal(10, Tower.NextMilestone(0));
    }

    [Fact]
    public void The_weeks_best_climbers_are_paid_and_the_top_three_titled()
    {
        Assert.True(Tower.SeasonSorn(80, 1) > Tower.SeasonSorn(80, 2));
        Assert.True(Tower.SeasonSorn(80, 10) > 0);
        Assert.Equal(0, Tower.SeasonSorn(80, 11));
        Assert.Equal("Lord of the Endless Tower", Tower.Title(1));
        Assert.Equal("Tower Veteran", Tower.Title(3));
        Assert.Null(Tower.Title(4));
    }
}
