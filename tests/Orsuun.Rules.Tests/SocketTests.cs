using Orsuun.Rules;
using Orsuun.Rules.Combat;
using Xunit;

namespace Orsuun.Rules.Tests;

public class SocketTests
{
    private sealed class FixedRandom : IRandom
    {
        private readonly int _value;
        public FixedRandom(int value) => _value = value;
        public int NextInt(int maxExclusive) => _value % maxExclusive;
    }

    [Fact]
    public void Socket_count_follows_rarity()
    {
        Assert.Single(new ItemState(10, Rarity.Common).Sockets);
        Assert.Equal(2, new ItemState(10, Rarity.Rare).Sockets.Length);
        Assert.Equal(3, new ItemState(10, Rarity.Legendary).Sockets.Length);
    }

    [Fact]
    public void Insert_spends_the_shard_and_sets_it_on_success()
    {
        var item = new ItemState(10, Rarity.Rare);
        var inventory = new Inventory();
        inventory.Korshards[2] = 1;

        bool ok = new SocketService().TryInsert(item, 0, ShardType.Piercer, 2, inventory, new FixedRandom(0));

        Assert.True(ok);
        Assert.Equal(0, inventory.Korshards[2]);
        Assert.Equal(ShardType.Piercer, item.Sockets[0].Type);
        Assert.Equal(2, item.Sockets[0].Rank);
    }

    [Fact]
    public void Failed_insert_leaves_a_dead_shard_that_costs_sorn_to_clear()
    {
        var item = new ItemState(10, Rarity.Rare);
        var inventory = new Inventory { Sorn = 100_000 };
        inventory.Korshards[0] = 1;
        var service = new SocketService();

        bool ok = service.TryInsert(item, 1, ShardType.Deathdealer, 0, inventory, new FixedRandom(9999));

        Assert.False(ok);
        Assert.True(item.Sockets[1].Dead);
        Assert.Equal("Socket holds a Dead Shard", service.InsertBlocker(item, 1, ShardType.Piercer, 0, inventory));

        service.Clear(item, 1, inventory);
        Assert.True(item.Sockets[1].IsEmpty);
        Assert.Equal(100_000 - SocketRules.ClearCost(item), inventory.Sorn);
    }

    [Fact]
    public void Shards_must_fit_the_slot_and_be_owned()
    {
        var service = new SocketService();
        var inventory = new Inventory();
        inventory.Korshards[1] = 1;
        Assert.Contains("does not fit", service.InsertBlocker(new ItemState(10, Rarity.Rare, EquipSlot.Armor), 0, ShardType.Piercer, 1, inventory));
        Assert.Contains("does not fit", service.InsertBlocker(new ItemState(10, Rarity.Rare), 0, ShardType.Vigor, 1, inventory));
        Assert.Contains("No Captain shard", service.InsertBlocker(new ItemState(10, Rarity.Rare), 0, ShardType.Piercer, 2, inventory));
        Assert.Null(service.InsertBlocker(new ItemState(10, Rarity.Rare), 0, ShardType.Piercer, 1, inventory));
    }

    [Fact]
    public void Set_shards_change_the_hero()
    {
        var weapon = new ItemState(10, Rarity.Epic);
        var armor = new ItemState(10, Rarity.Epic, EquipSlot.Armor);
        HeroStats before = HeroFactory.FromEquipment(new[] { weapon, armor }, 1);

        weapon.Sockets[0] = new Socket(ShardType.Piercer, 4);
        weapon.Sockets[1] = new Socket(ShardType.Deathdealer, 4);
        weapon.Sockets[2] = new Socket(ShardType.BeastSlayer, 4);
        armor.Sockets[0] = new Socket(ShardType.Haste, 4);
        armor.Sockets[1] = new Socket(ShardType.Evasion, 4);
        armor.Sockets[2] = new Socket(ShardType.Warding, 4);
        HeroStats after = HeroFactory.FromEquipment(new[] { weapon, armor }, 1);

        Assert.Equal(before.Attack + 25, after.Attack);
        Assert.Equal(250, after.CritMultiplierPercent);
        Assert.Equal(20, after.BeastDamagePercent);
        Assert.Equal(10, after.AttackIntervalTicks);
        Assert.Equal(1000, after.EvasionBp);
        Assert.Equal(75, after.CommanderDamageTakenPercent);
    }

    [Fact]
    public void Beast_slayer_speeds_up_farming_and_warding_softens_bosses()
    {
        var plain = new ItemState(10, Rarity.Epic) { UpgradeLevel = 3 };
        var slayer = new ItemState(10, Rarity.Epic) { UpgradeLevel = 3 };
        slayer.Sockets[0] = new Socket(ShardType.BeastSlayer, 4);
        slayer.Sockets[1] = new Socket(ShardType.BeastSlayer, 4);

        int Kills(ItemState w)
        {
            var lane = new LaneSim(Content.Stage(101), HeroFactory.FromWeapon(w), SkillDef.VanguardWrath(), new Inventory { Potions = 50 }, new XorShiftRandom(5));
            for (int i = 0; i < lane.AutoCast.Length; i++) lane.AutoCast[i] = true;
            for (int t = 0; t < 300 * LaneSim.TicksPerSecond; t++) { lane.Tick(); lane.DrainEvents(); }
            return lane.MobsKilled;
        }
        // +40% damage lands as roughly +10% kills once run-in and mob attack exchanges are counted.
        Assert.True(Kills(slayer) > Kills(plain) * 108 / 100, $"plain {Kills(plain)} slayer {Kills(slayer)}");

        var armor = new ItemState(10, Rarity.Epic, EquipSlot.Armor);
        armor.Sockets[0] = new Socket(ShardType.Warding, 4);
        armor.Sockets[1] = new Socket(ShardType.Warding, 4);
        HeroStats warded = HeroFactory.FromEquipment(new[] { plain, armor }, 1);
        HeroStats bare = HeroFactory.FromEquipment(new[] { plain, new ItemState(10, Rarity.Epic, EquipSlot.Armor) }, 1);
        BossDef boss = Content.Boss(1)!;
        long DamageTaken(HeroStats h)
        {
            LaneSim lane = BossRun.Create(boss, h, new Inventory(), 11);
            long taken = 0;
            for (int t = 0; t < 20 * LaneSim.TicksPerSecond && lane.Deaths == 0; t++)
            {
                lane.Tick();
                foreach (LaneEvent e in lane.DrainEvents())
                    if (e.Kind == LaneEventKind.HeroDamaged) taken += e.Amount;
            }
            return taken;
        }
        Assert.True(DamageTaken(warded) < DamageTaken(bare) * 90 / 100, $"warded {DamageTaken(warded)} bare {DamageTaken(bare)}");
    }
}
