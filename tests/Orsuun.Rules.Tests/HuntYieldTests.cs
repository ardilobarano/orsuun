using Orsuun.Rules;
using Orsuun.Rules.Combat;
using Xunit;

namespace Orsuun.Rules.Tests;

public class HuntYieldTests
{
    [Fact]
    public void Settlement_tracks_the_live_lane_within_20_percent()
    {
        // Live: 10 minutes of auto-cast farming on the default stage.
        var session = new PlayerSession(new XorShiftRandom(31));
        for (int s = 0; s < session.Lane.AutoCast.Length; s++) session.Lane.AutoCast[s] = true;
        long liveStart = session.Inventory.Sorn;
        for (int t = 0; t < 600 * LaneSim.TicksPerSecond; t++) session.Lane.Tick();
        long liveSorn = session.Inventory.Sorn - liveStart;

        // Settled: the same 10 minutes at the live rate.
        var inventory = new Inventory();
        HuntSettlement settled = HuntYield.Settle(new StageConfig(), HeroFactory.FromWeapon(session.Weapon), 600, 600, RandomExtensions.FullBp, inventory, new XorShiftRandom(32));

        Assert.InRange(settled.SornEarned / (double)liveSorn, 0.8, 1.2);
    }

    [Fact]
    public void Settlement_respects_the_cap_and_efficiency()
    {
        var stage = new StageConfig();
        HeroStats hero = HeroFactory.FromWeapon(new ItemState(10, Rarity.Rare));

        HuntSettlement capped = HuntYield.Settle(stage, hero, 40 * 3600, OfflineRewards.FreeCapSeconds, OfflineRewards.OfflineEfficiencyBp, new Inventory(), new XorShiftRandom(1));
        HuntSettlement full = HuntYield.Settle(stage, hero, OfflineRewards.FreeCapSeconds, OfflineRewards.FreeCapSeconds, RandomExtensions.FullBp, new Inventory(), new XorShiftRandom(1));

        Assert.Equal(OfflineRewards.FreeCapSeconds, capped.CountedSeconds);
        Assert.InRange((capped.Packs + capped.Korstones) / (double)(full.Packs + full.Korstones), 0.58, 0.62);
    }

    [Fact]
    public void Heartbeats_every_30_seconds_pay_what_one_long_settlement_does()
    {
        // A new hero on the first stage clears about three packs a heartbeat: before the carry, 3 / 6 was never a Korstone.
        StageConfig stage = Content.Stage(1);
        HeroStats hero = HeroFactory.FromWeapon(new ItemState(1, Rarity.Common));
        HuntSettlement whole = HuntYield.Settle(stage, hero, 600, 600, RandomExtensions.FullBp, new Inventory(), new XorShiftRandom(1));

        var carry = new HuntCarry();
        long packs = 0, korstones = 0;
        for (int beat = 0; beat < 20; beat++)
        {
            HuntSettlement s = HuntYield.Settle(stage, hero, 30, 180, RandomExtensions.FullBp, new Inventory(), new XorShiftRandom((ulong)beat + 1), carry);
            Assert.True(s.Packs + s.Korstones < 6, "a heartbeat holds under one loop");
            packs += s.Packs;
            korstones += s.Korstones;
        }

        Assert.True(whole.Korstones >= 3, $"ten minutes on stage 1 break {whole.Korstones} Korstones");
        Assert.Equal(whole.Packs, packs);
        Assert.Equal(whole.Korstones, korstones);
    }

    [Fact]
    public void An_away_hunt_says_when_the_bag_fills()
    {
        StageConfig stage = Content.Stage(20);
        HeroStats hero = HeroFactory.FromWeapon(new ItemState(20, Rarity.Rare));
        long? nearlyFull = Bag.SecondsUntilFull(stage, hero, Bag.Size - 5, new XorShiftRandom(3));
        long? half = Bag.SecondsUntilFull(stage, hero, Bag.Size / 2, new XorShiftRandom(3));
        Assert.NotNull(nearlyFull);
        Assert.True(nearlyFull > 0 && nearlyFull < 3 * 3600, $"five free slots fill in {nearlyFull} s");
        Assert.True(half == null || half > nearlyFull);
        Assert.Equal(0, Bag.SecondsUntilFull(stage, hero, Bag.Size, new XorShiftRandom(3)));
    }
}

