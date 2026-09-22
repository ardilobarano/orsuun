using System.Linq;
using Orsuun.Rules;
using Xunit;

namespace Orsuun.Rules.Tests;

public class EtchingTests
{
    private static ItemState FullItem(Rarity rarity, IRandom rng)
    {
        var item = new ItemState(60, rarity);
        var service = new EtchingService();
        EtchingPool pool = EtchingPool.Weapon();
        while (item.Etchings.Count < ItemState.MaxEtchings)
        {
            NeedleKind needle = item.Etchings.Count == 4 ? NeedleKind.MastersNeedle : NeedleKind.EtchingNeedle;
            service.TryAdd(item, pool, needle, rng);
        }
        return item;
    }

    [Fact]
    public void Weapon_pool_has_16_entries()
    {
        Assert.Equal(16, EtchingPool.Weapon().Entries.Count);
    }

    [Fact]
    public void Fifth_etching_needs_a_masters_needle()
    {
        var rng = new XorShiftRandom(1);
        var item = new ItemState(60, Rarity.Rare);
        var service = new EtchingService();
        EtchingPool pool = EtchingPool.Weapon();
        while (item.Etchings.Count < 4) service.TryAdd(item, pool, NeedleKind.EtchingNeedle, rng);

        Assert.Throws<InvalidOperationException>(() => service.TryAdd(item, pool, NeedleKind.EtchingNeedle, rng));
        Assert.Throws<InvalidOperationException>(() => service.TryAdd(new ItemState(60, Rarity.Rare), pool, NeedleKind.MastersNeedle, rng));
    }

    [Fact]
    public void Turn_keeps_etchings_distinct_and_respects_the_rarity_cap()
    {
        var rng = new XorShiftRandom(2);
        ItemState item = FullItem(Rarity.Common, rng);
        var service = new EtchingService();
        EtchingPool pool = EtchingPool.Weapon();

        for (int i = 0; i < 2_000; i++)
        {
            Assert.Equal(1, service.Turn(item, pool, rng));
            Assert.Equal(5, item.Etchings.Select(e => e.EntryId).Distinct().Count());
            Assert.All(item.Etchings, e => Assert.InRange(e.Tier, 1, 3));
        }
    }

    [Fact]
    public void Pinning_wax_keeps_the_locked_etching_and_doubles_the_cost()
    {
        var rng = new XorShiftRandom(3);
        ItemState item = FullItem(Rarity.Rare, rng);
        item.LockedEtchingIndex = 2;
        Etching locked = item.Etchings[2];
        var service = new EtchingService();
        EtchingPool pool = EtchingPool.Weapon();

        for (int i = 0; i < 500; i++)
        {
            Assert.Equal(2, service.Turn(item, pool, rng));
            Assert.Equal(locked.EntryId, item.Etchings[2].EntryId);
            Assert.Equal(locked.Value, item.Etchings[2].Value);
            Assert.Equal(5, item.Etchings.Select(e => e.EntryId).Distinct().Count());
        }
    }

    [Fact]
    public void Turnstone_odds_match_the_gdd_table()
    {
        var rng = new XorShiftRandom(4);
        ItemState item = FullItem(Rarity.Rare, rng);
        var service = new EtchingService();
        EtchingPool pool = EtchingPool.Weapon();
        const int turns = 400_000;
        int anyOathsworn = 0, oathswornT5 = 0, pairT3 = 0;

        for (int i = 0; i < turns; i++)
        {
            service.Turn(item, pool, rng);
            int oath = TierOf(item, WeaponEtchingIds.StrongAgainstOathsworn);
            int crit = TierOf(item, WeaponEtchingIds.Critical);
            if (oath > 0) anyOathsworn++;
            if (oath == 5) oathswornT5++;
            if (oath >= 3 && crit >= 3) pairT3++;
        }

        Assert.InRange(anyOathsworn / (double)turns, 0.3075, 0.3175);   // GDD: 31.25%
        Assert.InRange(oathswornT5 / (double)turns, 0.0110, 0.0140);    // GDD: 1.25%
        Assert.InRange(pairT3 / (double)turns, 0.0065, 0.0085);         // GDD: 0.75%
    }

    private static int TierOf(ItemState item, int entryId)
    {
        foreach (Etching e in item.Etchings)
            if (e.EntryId == entryId) return e.Tier;
        return 0;
    }
}

public class OfflineRewardTests
{
    private static OfflineInput Input() => new OfflineInput
    {
        SecondsAway = 3600,
        SnapshotDps = 1_000,
        PackHp = 10_000,
        SpawnPacksPerHour = 600,
        LootValuePerPack = 100,
    };

    [Fact]
    public void One_hour_pays_60_percent_of_live_loot()
    {
        OfflineResult result = OfflineRewards.Compute(Input());

        Assert.Equal(360, result.PacksKilled);           // 3600 s x 1000 dps / 10000 hp
        Assert.Equal(360 * 100 * 60 / 100, result.LootValue);
    }

    [Fact]
    public void Time_is_capped_at_twelve_hours()
    {
        OfflineInput input = Input();
        input.SecondsAway = 40 * 3600;

        Assert.Equal(OfflineRewards.FreeCapSeconds, OfflineRewards.Compute(input).CountedSeconds);
    }

    [Fact]
    public void Kill_rate_is_capped_by_the_stage_spawn_rate()
    {
        OfflineInput input = Input();
        input.SnapshotDps = 1_000_000;

        Assert.Equal(600, OfflineRewards.Compute(input).PacksKilled);
    }

    [Fact]
    public void Afk_bonus_is_capped_at_50_percent()
    {
        OfflineInput input = Input();
        input.AfkBonusBp = 9_000;

        Assert.Equal(360 * 100 * 60 / 100 * 150 / 100, OfflineRewards.Compute(input).LootValue);
    }
}
