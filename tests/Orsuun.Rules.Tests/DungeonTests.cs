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
        Assert.True(Dungeons.IsFloor(warden.StageNumber) && !Dungeons.IsFloor(40) && !Dungeons.IsFloor(Content.GorakWarCamp));
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

    private static bool ClearsAll(DungeonDef dungeon, HeroStats hero, int level, ulong run)
    {
        var inventory = new Inventory { Potions = 20 };
        for (int floor = 1; floor <= dungeon.Floors; floor++)
        {
            if (!Dungeons.Fought(dungeon, floor)) continue;
            if (!StageRun.Simulate(Dungeons.Floor(dungeon, floor, level), hero, inventory, run * 97 + (ulong)floor).Cleared) return false;
        }
        return true;
    }

    [Fact]
    public void The_warren_has_two_levels_and_the_silkmother_and_the_archive_a_rune_lock()
    {
        DungeonDef warren = Dungeons.Find(2)!, archive = Dungeons.Find(3)!;
        Assert.Equal(3, Dungeons.All.Length);
        Assert.Equal("Silkmother's Warren", warren.Name);
        Assert.Equal(30, warren.UnlockStage);                               // under the Salt Sea: after its boss
        Assert.Equal(DungeonPause.None, warren.Pause);
        Assert.True(Enumerable.Range(1, warren.Floors).All(f => Dungeons.Fought(warren, f)));
        Assert.Equal("Upper Galleries 3", Dungeons.FloorName(warren, 3));
        Assert.Equal("Brood Deep 1", Dungeons.FloorName(warren, 4));
        Assert.Equal(1, Dungeons.Floor(warren, 4, 30).ElderEvery);          // the egg-nest rush opens the Deep
        Assert.Equal("The Silkmother", Dungeons.Floor(warren, 6, 30).BossName);

        Assert.Equal(DungeonPause.RuneLock, archive.Pause);
        Assert.Equal(40, archive.UnlockStage);
        Assert.False(Dungeons.Fought(archive, 3));                         // the rune lock is not fought
        Assert.Equal("The Last Carver", Dungeons.Floor(archive, 5, 40).BossName);
        Assert.Equal(DungeonPause.Smith, Spire.Pause);
    }

    [Theory]
    [InlineData(2, 30, 7)]
    [InlineData(3, 40, 7)]
    [InlineData(3, 60, 8)]
    public void The_new_dungeons_ask_what_the_campaign_asks(int id, int level, int upgrade)
    {
        DungeonDef dungeon = Dungeons.Find(id)!;
        int ready = 0, behind = 0;
        for (ulong run = 1; run <= 10; run++)
        {
            if (ClearsAll(dungeon, Geared(level, level, upgrade), level, run)) ready++;
            if (ClearsAll(dungeon, Geared(level - 5, level - 5, upgrade - 1), level, run)) behind++;
        }
        Assert.True(ready >= 7, $"geared: {ready}/10");
        Assert.True(behind <= 4, $"behind: {behind}/10");
    }

    [Fact]
    public void Every_rune_lock_offers_three_runes_with_its_answer()
    {
        Assert.Equal(12, Dungeons.Riddles.Length);
        var positions = new HashSet<int>();
        for (long run = 1; run <= 72; run++)
        {
            var (text, runes, answer) = Dungeons.RiddleFor(run);
            Assert.False(string.IsNullOrWhiteSpace(text));
            Assert.Equal(3, runes.Distinct().Count());
            Assert.Contains(answer, runes);
            positions.Add(Array.IndexOf(runes, answer));
            Assert.Equal(runes, Dungeons.RiddleFor(run).Runes);           // both sides draw the same
        }
        Assert.Equal(3, positions.Count);                                  // the answer is not always in one place
    }

    [Fact]
    public void The_silkmother_pays_a_khans_alloy_and_the_open_vault_a_masters_needle()
    {
        var warren = new Inventory();
        Assert.Contains("Khan's Alloy", Dungeons.WardenChest(warren, 30, new XorShiftRandom(5), Dungeons.Find(2)));
        Assert.True(warren.KhansAlloys >= 1);
        var open = new Inventory();
        Assert.Contains("Master's Needle", Dungeons.WardenChest(open, 40, new XorShiftRandom(5), Dungeons.Find(3), vaultOpen: true));
        Assert.Equal(1, open.MastersNeedles);
        int shut = 0;
        for (ulong seed = 1; seed <= 200; seed++)
        {
            var inv = new Inventory();
            Dungeons.WardenChest(inv, 40, new XorShiftRandom(seed), Dungeons.Find(3));
            shut += inv.MastersNeedles;
        }
        Assert.InRange(shut, 5, 40);                                        // about 10% with the vault shut
        var spire = new Inventory();
        Dungeons.WardenChest(spire, 40, new XorShiftRandom(5), Spire, vaultOpen: true);
        Assert.Equal(0, spire.MastersNeedles);
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
