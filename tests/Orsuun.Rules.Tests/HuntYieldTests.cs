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
}
