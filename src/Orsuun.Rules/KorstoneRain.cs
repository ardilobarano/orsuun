#nullable enable
using System;
using Orsuun.Rules.Combat;

namespace Orsuun.Rules
{
    /// <summary>
    /// Korstone Rain (owner, 7 Oct 2026: picked "Korstone Rain": "A world event: every few hours a giant Korstone falls on a
    /// map with a sky streak and a horn. Every hero there hits the same stone (shared health, like the Commanders), and
    /// breaking it showers the map with loot and rare Korshards"). Every IntervalMinutes the world clock drops a Giant
    /// Korstone on one campaign map (one where heroes hunt now, else any), at one of its camps, for UpMinutes. Its health is
    /// HpPerHero for each hero hunting that map when it falls (at least MinHeroes). A hero on that map strikes it up to
    /// StrikesPerHero times: a StrikeSeconds lane against the stone and the map's monsters it calls up, every skill on auto;
    /// the damage comes off the shared health. When it breaks, every hero who struck it is showered by the share they dealt
    /// (sorn, Turnstones, Korshards of the map's rank with a chance of the rank above), and every other hero hunting the map
    /// gets a smaller shower; all by letter. Assumptions (not stated by the owner): every number here, and the rewards.
    /// </summary>
    public static class KorstoneRain
    {
        public const string StoneName = "Giant Korstone";
        public const int IntervalMinutes = 180, UpMinutes = 15, StrikesPerHero = 3, StrikeSeconds = 60, MinHeroes = 2;
        public const int StrikeTicks = StrikeSeconds * LaneSim.TicksPerSecond;
        /// <summary>A rare Korshard: the shower's Korshards are of the rank above this often (basis points).</summary>
        public const int RareShardBp = 1500;
        /// <summary>The stone's health for each hero on its map, as a multiple of the map's boss health.</summary>
        public const int HpPerHeroPercent = 200;

        /// <summary>A strike's lane: the stone (the map's last stage, its boss swapped for the stone) calling up the map's
        /// monsters. The stone strikes nothing itself and outlasts any one strike; the shared health is the server's.</summary>
        public static StageConfig Stage(int map)
        {
            map = Math.Max(1, Math.Min(Content.Maps.Length, map));
            StageConfig c = Content.Stage(map * MapDef.StagesPerMap);
            c.FinalEncounter = FinalEncounter.Boss;
            c.PacksBeforeKorstone = 0;
            c.BossName = StoneName;
            c.BossHp = 1_000_000_000_000L;
            c.BossAttack = 1;
            c.BossMechanic = BossMechanic.PackCaller;
            c.ElderEvery = 0;
            return c;
        }

        /// <summary>The stone's health per hero hunting its map when it falls.</summary>
        public static long HpPerHero(int map) => Content.Stage(Math.Max(1, Math.Min(Content.Maps.Length, map)) * MapDef.StagesPerMap).BossHp * HpPerHeroPercent / 100;

        /// <summary>The stone's whole health for the heroes hunting its map when it falls.</summary>
        public static long HpFor(int map, int heroesOnMap) => HpPerHero(map) * Math.Max(MinHeroes, heroesOnMap);

        /// <summary>The Korshard rank a map's stone showers (the Oathfields' Trooper .. the last maps' Guard of the Khan).</summary>
        public static int ShardRank(int map) => Math.Max(0, Math.Min(Content.KorshardRanks.Length - 1, (map - 1) / 3));

        /// <summary>
        /// A hero's shower when the stone breaks: a striker's grows with the share of the damage they dealt (permille), a
        /// bystander's (hunting the map, never struck) is small. Returns (sorn, Turnstones, Korshard rank, Korshards).
        /// </summary>
        public static (long Sorn, int Turnstones, int ShardRank, int Shards) Shower(int map, bool striker, int sharePermille, IRandom rng)
        {
            long perMob = Content.Stage(Math.Max(1, Math.Min(Content.Maps.Length, map)) * MapDef.StagesPerMap).SornPerMob;
            int rank = ShardRank(map);
            if (rank < Content.KorshardRanks.Length - 1 && rng.RollBp(RareShardBp)) rank++;
            if (!striker) return (perMob * 30, 0, rank, 1);
            int share = Math.Max(0, Math.Min(1000, sharePermille));
            return (perMob * (80 + 320 * share / 1000), 3 + 5 * share / 1000, rank, share >= 250 ? 3 : 2);
        }
    }
}
