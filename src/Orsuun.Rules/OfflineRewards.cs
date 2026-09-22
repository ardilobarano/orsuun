#nullable enable
using System;

namespace Orsuun.Rules
{
    /// <summary>Server-side inputs for the offline chest. Nothing here is ever reported by the client.</summary>
    public sealed class OfflineInput
    {
        /// <summary>Server time between the last heartbeat and this login.</summary>
        public long SecondsAway { get; set; }
        /// <summary>12 h free, 16 h with Hearthfire Blessing.</summary>
        public long CapSeconds { get; set; } = OfflineRewards.FreeCapSeconds;
        /// <summary>DPS recomputed by the server from equipped gear and skill grades.</summary>
        public long SnapshotDps { get; set; }
        public long PackHp { get; set; }
        /// <summary>The stage cannot feed packs faster than this, which pushes overgeared players onward.</summary>
        public int SpawnPacksPerHour { get; set; }
        /// <summary>Expected loot value per pack from the stage drop table.</summary>
        public long LootValuePerPack { get; set; }
        /// <summary>Pets, guild skills and Campaign Trail, in basis points. Capped at +50%.</summary>
        public int AfkBonusBp { get; set; }
    }

    public readonly struct OfflineResult
    {
        public OfflineResult(long countedSeconds, long packsKilled, long lootValue)
        {
            CountedSeconds = countedSeconds;
            PacksKilled = packsKilled;
            LootValue = lootValue;
        }

        public long CountedSeconds { get; }
        public long PacksKilled { get; }
        public long LootValue { get; }
    }

    /// <summary>GDD section 3: R = min(t, Tcap) x packs per second x loot per pack x 0.60 x (1 + Bafk).</summary>
    public static class OfflineRewards
    {
        public const long FreeCapSeconds = 12 * 3600;
        public const long BlessingCapSeconds = 16 * 3600;
        public const int OfflineEfficiencyBp = 6000;
        public const int MaxAfkBonusBp = 5000;

        public static OfflineResult Compute(OfflineInput input)
        {
            if (input.PackHp <= 0) throw new ArgumentOutOfRangeException(nameof(input), "PackHp must be positive.");

            long seconds = Math.Min(Math.Max(0, input.SecondsAway), input.CapSeconds);
            long byDps = seconds * Math.Max(0, input.SnapshotDps) / input.PackHp;
            long bySpawn = seconds * Math.Max(0, input.SpawnPacksPerHour) / 3600;
            long packs = Math.Min(byDps, bySpawn);

            int bonus = Math.Min(Math.Max(0, input.AfkBonusBp), MaxAfkBonusBp);
            long loot = packs * input.LootValuePerPack * OfflineEfficiencyBp / RandomExtensions.FullBp
                        * (RandomExtensions.FullBp + bonus) / RandomExtensions.FullBp;

            return new OfflineResult(seconds, packs, loot);
        }
    }
}
