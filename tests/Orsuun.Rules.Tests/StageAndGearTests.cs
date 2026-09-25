using System.Linq;
using Orsuun.Rules;
using Orsuun.Rules.Combat;
using Xunit;

namespace Orsuun.Rules.Tests;

public class StageAndGearTests
{
    [Fact]
    public void Six_maps_of_ten_stages_each_end_at_their_boss()
    {
        Assert.Equal(60, Content.TotalStages);
        Assert.Equal(FinalEncounter.Korstone, Content.Stage(1).FinalEncounter);
        Assert.Equal(FinalEncounter.Boss, Content.Stage(10).FinalEncounter);
        Assert.Equal("The Oathfields 7", Content.StageName(7));
        Assert.True(Content.Stage(10).MobHp > Content.Stage(1).MobHp * 2);
        // World bible section 6: Gorak Pass (Tul-Gorak), the Salt Sea (the Mirage Queen), Whitefang Range (Nine-Winters).
        Assert.Equal("Gorak Pass 1", Content.StageName(11));
        Assert.Equal("Warlord Tul-Gorak", Content.Stage(20).BossName);
        Assert.Equal("The Mirage Queen", Content.Stage(30).BossName);
        Assert.Equal("Nine-Winters", Content.Stage(40).BossName);
        // The Cinder Marches (Azhdar the Furnace Wyrm, levels 40-50) and Whisperwood (the Lantern Widow, levels 50-58).
        Assert.Equal("The Cinder Marches 1", Content.StageName(41));
        Assert.Equal("Azhdar the Furnace Wyrm", Content.Stage(50).BossName);
        Assert.Equal("The Lantern Widow", Content.Stage(60).BossName);
        Assert.Equal("Whisper Bark", Content.Stage(55).MaterialName);
        Assert.Equal(58, Content.Stage(60).GearItemLevel);
        Assert.Equal(FinalEncounter.Korstone, Content.Stage(35).FinalEncounter);
        // Each map's gear levels continue the last: Gorak Pass drops item level 10 to 20.
        Assert.Equal(10, Content.Stage(11).GearItemLevel);
        Assert.Equal(40, Content.Stage(40).GearItemLevel);
        // A new map opens a little above the last boss stage's mobs, never below.
        for (int map = 2; map <= 6; map++)
            Assert.True(Content.Stage(map * 10 - 9).MobHp >= Content.Stage(map * 10 - 10).MobHp * 9 / 10);
    }

    private static HeroStats Geared(int level, int itemLevel, int upgrade)
    {
        var items = new List<ItemState>();
        for (int s = 0; s < 8; s++) items.Add(new ItemState(itemLevel, Rarity.Rare, (EquipSlot)s) { UpgradeLevel = upgrade });
        return HeroFactory.FromEquipment(items, level);
    }

    [Theory]
    [InlineData(2, 20, 20, 6, 10, 10, 6)]
    [InlineData(3, 30, 30, 7, 20, 20, 6)]
    [InlineData(4, 40, 40, 7, 30, 30, 7)]
    [InlineData(5, 50, 50, 7, 40, 40, 7)]
    [InlineData(6, 58, 58, 8, 50, 50, 7)]
    public void Each_map_boss_is_a_power_check(int map, int level, int itemLevel, int upgrade, int lastLevel, int lastItemLevel, int lastUpgrade)
    {
        // GDD section 2: map bosses gate the next map. A hero geared for the map clears its boss; the last map's cannot.
        StageConfig boss = Content.Stage(map * 10);
        int ready = 0, early = 0;
        for (ulong seed = 1; seed <= 20; seed++)
        {
            if (StageRun.Simulate(boss, Geared(level, itemLevel, upgrade), new Inventory { Potions = 5 }, seed).Cleared) ready++;
            if (StageRun.Simulate(boss, Geared(lastLevel, lastItemLevel, lastUpgrade), new Inventory { Potions = 5 }, seed).Cleared) early++;
        }
        Assert.True(ready >= 15, $"map {map} boss, geared: {ready}/20");
        Assert.True(early <= 2, $"map {map} boss, last map's gear: {early}/20");
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
        HeroStats hero = HeroFactory.FromEquipment(set, 1);
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
