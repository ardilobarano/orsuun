using System.Collections.Generic;
using System.Linq;
using Orsuun.Rules;
using Orsuun.Rules.Combat;
using Xunit;

namespace Orsuun.Rules.Tests;

/// <summary>Commanders on every map (owner, 29 Sep 2026): the map bosses of maps 4-12 as world Commanders.</summary>
public class MapCommanderTests
{
    private static HeroStats Geared(int level, int itemLevel, int upgrade)
    {
        var items = new List<ItemState>();
        for (int s = 0; s < 8; s++) items.Add(new ItemState(itemLevel, Rarity.Rare, (EquipSlot)s) { UpgradeLevel = upgrade });
        return HeroFactory.FromEquipment(items, level);
    }

    [Fact]
    public void Maps_four_to_twelve_each_have_their_boss_as_a_commander()
    {
        for (int map = 4; map <= 12; map++)
        {
            BossDef boss = Content.Bosses.Single(b => Content.OnMap(b) && Content.MapOfStage(b.ZoneId).Id == map);
            MapDef def = Content.Maps[map - 1];
            Assert.Equal(def.BossName, boss.Name);
            Assert.Equal((map - 1) * 10 + 1, boss.ZoneId);
            Assert.Equal(def.LevelMax, Content.CommanderGearLevel(boss));
            Assert.Equal(Content.Stage(map * 10).BossHp * Content.CommanderHpPercent / 100, boss.Hp);
            Assert.True(Content.IsUnlocked(boss.ZoneId, (map - 1) * 10) && !Content.IsUnlocked(boss.ZoneId, (map - 1) * 10 - 1));
        }
        // The war camp's three stay Commander Ground Commanders.
        Assert.Equal(3, Content.Bosses.Count(b => !Content.OnMap(b)));
        Assert.Equal(20, Content.CommanderGearLevel(Content.Boss(1)!));
    }

    [Theory]
    [InlineData(4, 7)]
    [InlineData(6, 8)]
    [InlineData(8, 9)]
    [InlineData(11, 9)]
    [InlineData(12, 9)]
    public void A_hero_geared_for_the_map_takes_a_share_and_lives(int map, int upgrade)
    {
        BossDef boss = Content.Bosses.Single(b => Content.OnMap(b) && Content.MapOfStage(b.ZoneId).Id == map);
        MapDef def = Content.Maps[map - 1];
        int cleared = 0;
        for (ulong seed = 1; seed <= 20; seed++)
            if (BossRun.Simulate(boss, Geared(def.LevelMax, def.LevelMax, upgrade), new Inventory { Potions = 5 }, seed).Killed) cleared++;
        Assert.True(cleared >= 18, $"{boss.Name}: {cleared}/20");
        // A shared boss, not a gate (the map boss is that): a hero new to the map, still in the last map's gear, adds at
        // least a third of a share.
        MapDef last = Content.Maps[map - 2];
        long damage = 0;
        for (ulong seed = 1; seed <= 20; seed++)
            damage += BossRun.Simulate(boss, Geared(def.LevelMin, last.LevelMax, upgrade - 1), new Inventory { Potions = 5 }, seed).Damage;
        Assert.True(damage / 20 >= boss.Hp / 3, $"{boss.Name}, last map's gear: {damage / 20} of {boss.Hp}");
    }

    [Fact]
    public void A_map_commanders_chest_drops_the_maps_top_gear()
    {
        BossDef hurm = Content.Bosses.Single(b => b.Name == "Hurm the Unburied");
        var inventory = new Inventory();
        string chest = HuntYield.LootCommander(hurm, 1, inventory, new XorShiftRandom(3));
        Assert.StartsWith("Commander's chest from Hurm the Unburied: +" + 30_000 * hurm.Tier + " sorn", chest);
        Assert.All(inventory.Loot.Where(i => !i.Kin), i => Assert.Equal(82, i.ItemLevel));
    }
}

/// <summary>Commander pushes (owner, 29 Sep 2026): where each Commander stands, for the heroes to call.</summary>
public class CommanderPlaceTests
{
    [Fact]
    public void Each_commander_stands_on_its_maps()
    {
        Assert.Equal(new[] { 1001, Content.EmberSteppe }, Content.CommanderPlaces(Content.Boss(3)!));
        Assert.Equal(new[] { 1002, Content.GorakWarCamp }, Content.CommanderPlaces(Content.Boss(1)!));
        Assert.Equal(new[] { 1003, Content.SaltFlats }, Content.CommanderPlaces(Content.Boss(2)!));
        for (int id = 4; id <= 12; id++) Assert.Equal(new[] { 1000 + id }, Content.CommanderPlaces(Content.Boss(id)!));
        // A place's stages: a map's ten, a zone alone.
        Assert.Equal((31, 40), Parties.Stages(1004));
        Assert.Equal((Content.SaltFlats, Content.SaltFlats), Parties.Stages(Content.SaltFlats));
        Assert.True(Parties.Looking(System.DateTime.UtcNow.AddMinutes(-29), System.DateTime.UtcNow));
        Assert.False(Parties.Looking(System.DateTime.UtcNow.AddMinutes(-31), System.DateTime.UtcNow));
    }
}
