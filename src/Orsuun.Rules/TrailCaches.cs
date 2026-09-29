using System;

namespace Orsuun.Rules
{
    /// <summary>
    /// Trail caches (owner, 29 Sep 2026: "Trail caches": "Now and then a small chest lies beside the trail; the hero walks up
    /// and you tap it to open it for sorn, Turnstones or a material (the server rolls it, a few a day). Counts toward
    /// bounties."). A hero finds one every <see cref="MinutesBetween"/> minutes, <see cref="PerDay"/> a bounty day (the first
    /// of the day at once); the server rolls what is inside and counts BountyMetric.CachesOpened. Assumptions (not stated by
    /// the owner): the count, the wait and the table.
    /// </summary>
    public static class TrailCaches
    {
        public const int PerDay = 6, MinutesBetween = 15;

        /// <summary>Seconds until the hero's next cache lies on the trail (0: now), or -1 when today's are all opened.</summary>
        public static long SecondsToNext(int openedToday, DateTime? lastOpenedUtc, DateTime nowUtc)
        {
            if (openedToday >= PerDay) return -1;
            if (openedToday == 0 || lastOpenedUtc == null) return 0;
            double left = (lastOpenedUtc.Value.AddMinutes(MinutesBetween) - nowUtc).TotalSeconds;
            return left <= 0 ? 0 : (long)Math.Ceiling(left);
        }

        /// <summary>What a cache holds: sorn most often (15-30 mobs' worth at the furthest stage cleared), else Turnstones,
        /// hunt materials, draughts, and now and then a Scroll of Mercy.</summary>
        public static DailyReward Roll(IRandom rng, int highestStageCleared)
        {
            long mob = Content.Stage(Math.Max(1, Math.Min(Content.TotalStages, highestStageCleared))).SornPerMob;
            int r = rng.NextInt(100);
            if (r < 40) return new DailyReward { Sorn = mob * (15 + rng.NextInt(16)) };
            if (r < 62) return new DailyReward { Turnstones = 2 + rng.NextInt(3) };
            if (r < 80) return new DailyReward { Materials = 3 + rng.NextInt(4) };
            if (r < 95) return new DailyReward { Potions = 3 + rng.NextInt(3) };
            return new DailyReward { ScrollsOfMercy = 1 };
        }
    }
}
