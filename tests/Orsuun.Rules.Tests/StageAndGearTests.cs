using System.Linq;
using Orsuun.Rules;
using Orsuun.Rules.Combat;
using Xunit;

namespace Orsuun.Rules.Tests;

public class StageAndGearTests
{
    [Fact]
    public void Content_has_ten_stages_with_a_boss_on_the_last()
    {
        Assert.Equal(10, Content.TotalStages);
        Assert.Equal(FinalEncounter.Korstone, Content.Stage(1).FinalEncounter);
        Assert.Equal(FinalEncounter.Boss, Content.Stage(10).FinalEncounter);
        Assert.Equal("The Oathfields 7", Content.StageName(7));
        Assert.True(Content.Stage(10).MobHp > Content.Stage(1).MobHp * 2);
    }

    [Fact]
    public void Push_is_deterministic_for_a_seed()
    {
        StageConfig stage = Content.Stage(1);
        HeroStats hero = HeroFactory.FromWeapon(new ItemState(10, Rarity.Rare));

        StageRunResult a = StageRun.Simulate(stage, hero, new Inventory(), 4242);
        StageRunResult b = StageRun.Simulate(stage, hero, new Inventory(), 4242);

        Assert.Equal(a.Cleared, b.Cleared);
        Assert.Equal(a.Ticks, b.Ticks);
    }

    [Fact]
    public void A_starter_hero_clears_stage_1_but_not_the_boss_stage()
    {
        HeroStats hero = HeroFactory.FromWeapon(new ItemState(10, Rarity.Rare));
        int clears1 = 0, clears10 = 0;
        for (ulong seed = 1; seed <= 20; seed++)
        {
            if (StageRun.Simulate(Content.Stage(1), hero, new Inventory(), seed).Cleared) clears1++;
            if (StageRun.Simulate(Content.Stage(10), hero, new Inventory(), seed).Cleared) clears10++;
        }
        Assert.True(clears1 >= 18, $"stage 1 clears: {clears1}/20");
        Assert.True(clears10 <= 2, $"stage 10 clears: {clears10}/20");
    }

    [Fact]
    public void A_full_plus6_set_clears_the_boss_stage()
    {
        var set = Enumerable.Range(0, 8).Select(s => new ItemState(10, Rarity.Rare, (EquipSlot)s) { UpgradeLevel = 6 }).ToList();
        HeroStats hero = HeroFactory.FromEquipment(set);
        int clears = 0;
        for (ulong seed = 1; seed <= 20; seed++)
            if (StageRun.Simulate(Content.Stage(10), hero, new Inventory(), seed).Cleared) clears++;
        Assert.True(clears >= 15, $"stage 10 clears with +6 set: {clears}/20");
    }

    [Fact]
    public void Gear_drops_follow_the_rarity_table_and_the_cap()
    {
        var rng = new XorShiftRandom(9);
        StageConfig stage = Content.Stage(3);
        stage.GearRarityCap = Rarity.Rare;
        var inventory = new Inventory();
        for (int i = 0; i < 20_000; i++) HuntYield.DropGear(stage, inventory, rng, stage.GearRarityCap);

        int common = inventory.Loot.Count(i => i.Rarity == Rarity.Common);
        Assert.InRange(common / 20_000.0, 0.60, 0.64);
        Assert.DoesNotContain(inventory.Loot, i => i.Rarity > Rarity.Rare);
        Assert.All(inventory.Loot.Where(i => i.Rarity == Rarity.Rare), i => Assert.Equal(2, i.Etchings.Count));
        Assert.All(inventory.Loot.Where(i => i.Rarity == Rarity.Common), i => Assert.Empty(i.Etchings));
    }

    [Fact]
    public void Equip_swaps_the_slot_and_returns_the_old_piece_to_loot()
    {
        var session = new PlayerSession(new XorShiftRandom(5));
        var armor = new ItemState(10, Rarity.Epic, EquipSlot.Armor);
        session.Inventory.Loot.Add(armor);
        long hpBefore = session.Hero.MaxHp;

        session.Equip(armor);

        Assert.Same(armor, session.Equipped(EquipSlot.Armor));
        Assert.True(session.Hero.MaxHp > hpBefore);
        Assert.True(session.Hero.Defense > 0);
        Assert.Empty(session.Inventory.Loot);
    }

    [Fact]
    public void Park_only_allows_unlocked_stages()
    {
        var session = new PlayerSession(new XorShiftRandom(6));
        Assert.Throws<InvalidOperationException>(() => session.Park(2));
        session.Push(out _);
        if (session.HighestStageCleared >= 1)
        {
            session.Park(2);
            Assert.Equal(2, session.ParkedStage);
        }
    }
}
