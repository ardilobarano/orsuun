using System.Linq;
using Orsuun.Rules;
using Orsuun.Rules.Combat;
using Xunit;

namespace Orsuun.Rules.Tests;

public class ZoneAndBossTests
{
    private static Inventory Farm(int parkId, int seconds, ulong seed, int upgradeLevel = 3)
    {
        var inventory = new Inventory { Potions = 50 };
        var weapon = new ItemState(10, Rarity.Rare) { UpgradeLevel = upgradeLevel };
        var lane = new LaneSim(Content.Stage(parkId), HeroFactory.FromWeapon(weapon), SkillDef.VanguardWrath(), inventory, new XorShiftRandom(seed));
        for (int i = 0; i < lane.AutoCast.Length; i++) lane.AutoCast[i] = true;
        for (int t = 0; t < seconds * LaneSim.TicksPerSecond; t++) { lane.Tick(); lane.DrainEvents(); }
        return inventory;
    }

    [Fact]
    public void Hunting_ground_pays_more_sorn_and_xp_and_no_materials()
    {
        Inventory hunt = Farm(Content.EmberSteppe, 600, 1);
        Inventory field = Farm(Content.KorstoneFieldI, 600, 1);

        Assert.True(hunt.Sorn > field.Sorn * 2, $"hunt {hunt.Sorn} vs field {field.Sorn}");
        Assert.True(hunt.Xp > field.Xp);
        Assert.Equal(0, hunt.Materials);
        Assert.Equal(0, hunt.Turnstones);
        Assert.DoesNotContain(hunt.Loot, i => i.Rarity > Rarity.Uncommon);
    }

    [Fact]
    public void Korstone_field_pays_materials_turnstones_and_shards_of_its_tier()
    {
        Inventory field1 = Farm(Content.KorstoneFieldI, 600, 2);
        Assert.True(field1.Turnstones >= 10, $"turnstones {field1.Turnstones}");
        Assert.True(field1.Materials >= 5);
        Assert.True(field1.Korshards[0] >= 5, $"trooper shards {field1.Korshards[0]}");
        Assert.Equal(0, field1.Korshards[1]);

        Inventory field3 = Farm(Content.KorstoneFieldIII, 600, 2, upgradeLevel: 9);
        Assert.True(field3.Korshards[2] >= 1, $"captain shards {field3.Korshards[2]}");
    }

    [Fact]
    public void Every_tenth_field_korstone_is_an_elder_with_a_scroll()
    {
        var inventory = new Inventory { Potions = 50 };
        var weapon = new ItemState(10, Rarity.Rare) { UpgradeLevel = 9 };
        var lane = new LaneSim(Content.Stage(Content.KorstoneFieldI), HeroFactory.FromWeapon(weapon), SkillDef.VanguardWrath(), inventory, new XorShiftRandom(3));
        for (int i = 0; i < lane.AutoCast.Length; i++) lane.AutoCast[i] = true;
        int elders = 0;
        for (int t = 0; t < 20 * 60 * LaneSim.TicksPerSecond && lane.KorstonesDestroyed < 12; t++)
        {
            lane.Tick();
            foreach (LaneEvent e in lane.DrainEvents())
                if (e.Kind == LaneEventKind.Loot && e.Text!.StartsWith("Elder")) elders++;
        }
        Assert.True(lane.KorstonesDestroyed >= 10, $"korstones {lane.KorstonesDestroyed}");
        Assert.Equal(1, elders);
        Assert.True(inventory.ScrollsOfMercy >= 1);
    }

    [Fact]
    public void Zone_unlocks_follow_campaign_progress()
    {
        Assert.True(Content.IsUnlocked(Content.EmberSteppe, 1));
        Assert.True(Content.IsUnlocked(Content.KorstoneFieldI, 1));
        Assert.False(Content.IsUnlocked(Content.GorakWarCamp, 4));
        Assert.True(Content.IsUnlocked(Content.GorakWarCamp, 5));
        Assert.False(Content.IsUnlocked(Content.KorstoneFieldV, 9));
        Assert.False(Content.IsUnlocked(999, 10));
    }

    [Fact]
    public void Levels_rise_with_xp_and_strengthen_the_hero()
    {
        Assert.Equal(1, Content.LevelFor(0));
        Assert.Equal(1, Content.LevelFor(Content.XpForLevel(2) - 1));
        Assert.Equal(2, Content.LevelFor(Content.XpForLevel(2)));
        Assert.Equal(10, Content.LevelFor(Content.XpForLevel(10)));
        Assert.Equal(Content.MaxLevel, Content.LevelFor(long.MaxValue / 4));

        var weapon = new ItemState(10, Rarity.Rare);
        Assert.True(HeroFactory.FromEquipment(new[] { weapon }, 20).Attack > HeroFactory.FromEquipment(new[] { weapon }, 1).Attack);
    }

    [Fact]
    public void Tul_gorak_cannot_be_hurt_while_captains_stand()
    {
        BossDef boss = Content.Boss(1)!;
        var lane = BossRun.Create(boss, HeroFactory.FromWeapon(new ItemState(10, Rarity.Rare) { UpgradeLevel = 6 }), new Inventory { Potions = 30 }, 7);
        bool sawShield = false;
        long bossDamageWhileCaptains = 0;
        for (int t = 0; t < 40 * LaneSim.TicksPerSecond; t++)
        {
            lane.Tick();
            bool captains = lane.Enemies.Any(e => e.Kind == EnemyKind.Captain);
            foreach (LaneEvent e in lane.DrainEvents())
            {
                if (e.Kind == LaneEventKind.Shielded) sawShield = true;
                if (e.Kind == LaneEventKind.EnemyDamaged && captains && lane.Enemies.Any(x => x.Id == e.EnemyId && x.IsBoss)) bossDamageWhileCaptains += e.Amount;
            }
        }
        Assert.True(sawShield);
        Assert.Equal(0, bossDamageWhileCaptains);
    }

    [Fact]
    public void Mirage_queen_images_reflect_and_pack_caller_calls_packs()
    {
        var hero = HeroFactory.FromWeapon(new ItemState(10, Rarity.Rare) { UpgradeLevel = 9 });
        LaneSim queen = BossRun.Create(Content.Boss(2)!, hero, new Inventory { Potions = 30 }, 8);
        int images = 0;
        for (int t = 0; t < BossRun.MaxTicks && queen.BossesKilled == 0 && queen.Deaths == 0; t++)
        {
            queen.Tick();
            foreach (LaneEvent e in queen.DrainEvents())
                if (e.Kind == LaneEventKind.BossMechanic && e.Text!.Contains("images")) images++;
        }
        Assert.Equal(2, images);

        LaneSim greyjaw = BossRun.Create(Content.Boss(3)!, hero, new Inventory { Potions = 30 }, 9);
        int calls = 0;
        for (int t = 0; t < 40 * LaneSim.TicksPerSecond; t++)
        {
            greyjaw.Tick();
            foreach (LaneEvent e in greyjaw.DrainEvents())
                if (e.Kind == LaneEventKind.BossMechanic && e.Text!.Contains("pack")) calls++;
        }
        Assert.InRange(calls, 2, 3);
    }

    [Fact]
    public void Boss_run_is_deterministic_and_rank_pays_the_right_chest()
    {
        BossDef boss = Content.Boss(1)!;
        var hero = HeroFactory.FromWeapon(new ItemState(10, Rarity.Rare) { UpgradeLevel = 6 });
        BossRunResult a = BossRun.Simulate(boss, hero, new Inventory { Potions = 30 }, 99);
        BossRunResult b = BossRun.Simulate(boss, hero, new Inventory { Potions = 30 }, 99);
        Assert.Equal(a.Damage, b.Damage);
        Assert.True(a.Damage > 0);

        var inventory = new Inventory();
        string chest = HuntYield.LootCommander(boss, 1, inventory, new XorShiftRandom(1));
        Assert.StartsWith("Commander's chest", chest);
        Assert.Single(inventory.Loot);
        Assert.True(inventory.Loot[0].Rarity >= Rarity.Epic);
        Assert.StartsWith("No chest", HuntYield.LootCommander(boss, 21, inventory, new XorShiftRandom(1)));
    }
}
