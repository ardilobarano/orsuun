#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using Orsuun.Rules.Combat;

namespace Orsuun.Rules
{
    /// <summary>
    /// Guild war (GDD section 7: 20v20 live on three lanes, Wednesday and Saturday 21:00, the leader signs up; guild rank
    /// and treasury sorn). Built asynchronous like the sieges: the leader signs the guild up before the night; at 21:00
    /// the signed guilds are paired by war rating; for an hour each member may fight a few duels on a lane of their
    /// choice against the other guild's members. A win is a kill and pushes that lane's front one step toward the
    /// enemy (two under the guild's war flag, which the leader or an officer plants); five steps break the lane.
    /// Score = kills + 10 per broken lane. The winner takes treasury sorn, guild XP and rating (Elo).
    /// </summary>
    public static class GuildWars
    {
        public static readonly DayOfWeek[] Nights = { DayOfWeek.Wednesday, DayOfWeek.Saturday };
        public const int StartHour = 21;
        /// <summary>How long a war night stays open for fights (the GDD's live war is 15 minutes).</summary>
        public const int WindowMinutes = 60;
        /// <summary>Members a guild needs to sign up and to be paired.</summary>
        public const int MinMembers = 3;
        public const int Lanes = 3;
        public static readonly string[] LaneNames = { "Left Flank", "Centre", "Right Flank" };
        /// <summary>Duels one member may fight in a war, and the rest between two (the GDD's respawn, asynchronous).</summary>
        public const int FightsPerWar = 6;
        public const int CooldownSeconds = 120;
        /// <summary>Steps of a lane's front from the middle to a broken line.</summary>
        public const int FrontToBreak = 5;
        /// <summary>Steps a win pushes under the guild's war flag (one elsewhere).</summary>
        public const int FlagPush = 2;
        public const int BreakScore = 10;
        public const long WinTreasury = 150_000;
        public const long DrawTreasury = 50_000;
        public const long WinXp = 100;
        public const long DrawXp = 60;
        public const long LossXp = 30;
        public const int StartRating = 1000;
        public const int RatingK = 32;
        /// <summary>Pay per duel fought, like a siege fight (GDD: PvP pays currency, never upgrade protection).</summary>
        public const long FightSorn = 5_000;
        public const int FightMarks = 1;

        /// <summary>The war night running now, or the next one, as its local start time.</summary>
        public static DateTime StartOf(DateTime localNow)
        {
            for (int day = -1; day <= 8; day++)
            {
                DateTime date = localNow.Date.AddDays(day);
                if (Array.IndexOf(Nights, date.DayOfWeek) < 0) continue;
                DateTime start = date.AddHours(StartHour);
                if (start.AddMinutes(WindowMinutes) > localNow) return start;
            }
            throw new InvalidOperationException("No war night within a week.");
        }

        public static string NightKey(DateTime start) => "N" + start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        public static bool Running(DateTime localNow, DateTime start) => localNow >= start && localNow < start.AddMinutes(WindowMinutes);

        /// <summary>
        /// Pairs the night's guilds: the strongest rating with the next, and so on; an odd one out sits the night out
        /// (<paramref name="bye"/>, else -1). Equal ratings keep the input order.
        /// </summary>
        public static List<(int A, int B)> Pair(IReadOnlyList<int> ratings, out int bye)
        {
            var order = new List<int>();
            for (int i = 0; i < ratings.Count; i++) order.Add(i);
            order.Sort((x, y) => ratings[y] != ratings[x] ? ratings[y].CompareTo(ratings[x]) : x.CompareTo(y));
            var pairs = new List<(int, int)>();
            for (int i = 0; i + 1 < order.Count; i += 2) pairs.Add((order[i], order[i + 1]));
            bye = order.Count % 2 == 1 ? order[order.Count - 1] : -1;
            return pairs;
        }

        public static bool Broken(int front) => Math.Abs(front) >= FrontToBreak;

        /// <summary>A lane's front after a win: side A pushes it up, side B down; a broken lane stays where it broke.</summary>
        public static int Push(int front, bool sideA, bool flag)
        {
            if (Broken(front)) return front;
            int step = flag ? FlagPush : 1;
            return Math.Max(-FrontToBreak, Math.Min(FrontToBreak, front + (sideA ? step : -step)));
        }

        public static int LanesBroken(IReadOnlyList<int> fronts, bool sideA)
        {
            int broken = 0;
            foreach (int f in fronts)
                if (sideA ? f >= FrontToBreak : f <= -FrontToBreak) broken++;
            return broken;
        }

        public static int Score(int kills, IReadOnlyList<int> fronts, bool sideA) => kills + BreakScore * LanesBroken(fronts, sideA);

        /// <summary>1 when side A won, 2 when side B won, 3 for a draw.</summary>
        public static int Result(int scoreA, int scoreB) => scoreA > scoreB ? 1 : scoreB > scoreA ? 2 : 3;

        /// <summary>New ratings after a war (Elo, K = 32): result 1 = A won, 2 = B won, 3 = draw.</summary>
        public static (int A, int B) Rate(int ratingA, int ratingB, int result)
        {
            double expectA = 1.0 / (1.0 + Math.Pow(10.0, (ratingB - ratingA) / 400.0));
            double scoreA = result == 1 ? 1.0 : result == 2 ? 0.0 : 0.5;
            int delta = (int)Math.Round(RatingK * (scoreA - expectA), MidpointRounding.AwayFromZero);
            return (ratingA + delta, ratingB - delta);
        }
    }

    /// <summary>
    /// One duel of a guild war or a keep siege, decided by gear (GDD section 7, PvP balance: gear decides more than class,
    /// no class matchup beyond 60:40 at equal gear, a +9 set beats a +7 set about 80% of the time, and in guild war and
    /// siege the stats above the match's median are compressed by 30%). Both heroes are measured with the same
    /// class-neutral stats (their gear and level on the Vanguard's frame), so the class kit never decides; the edge is how
    /// much sooner one would fell the other, and a seeded roll with a little luck on it picks the winner.
    /// The replay is the attacker's own hero against a champion shaped so the fight ends the way the roll said.
    /// </summary>
    public static class Duels
    {
        public const int CompressPercent = 30;
        /// <summary>Luck on the log of the time ratio (tuned so a full +9 set beats a full +7 set about 80% of the time).</summary>
        public const double Sigma = 0.47;
        /// <summary>BossDef id of a duel's champion in replays (Commanders are 1-3, siege champions 201-209).</summary>
        public const int ChampionId = 300;
        /// <summary>The replayed fight lasts about this long before one side falls.</summary>
        private const int TargetSeconds = 24;

        /// <summary>The hero's stats for a duel: gear and level only, on the Vanguard's frame.</summary>
        public static HeroStats Neutral(IEnumerable<ItemState> equipped, int level) => HeroFactory.FromEquipment(equipped, level, HeroClass.Vanguard);

        /// <summary>Stats above the pair's median (with two fighters, their mean) keep only 70% of what they are above it.</summary>
        public static void Compress(HeroStats a, HeroStats b)
        {
            (long x, long y) C(long p, long q)
            {
                long mid = (p + q) / 2;
                long Squeeze(long v) => v > mid ? mid + (v - mid) * (100 - CompressPercent) / 100 : v;
                return (Squeeze(p), Squeeze(q));
            }
            (a.Attack, b.Attack) = C(a.Attack, b.Attack);
            (a.Defense, b.Defense) = C(a.Defense, b.Defense);
            (a.MaxHp, b.MaxHp) = C(a.MaxHp, b.MaxHp);
        }

        private static double CritFactor(HeroStats h) => 1.0 + h.CritChanceBp / 10_000.0 * (h.CritMultiplierPercent / 100.0 - 1.0);

        /// <summary>
        /// ln(time for <paramref name="d"/> to fell <paramref name="a"/> / time for a to fell d): above zero, a is
        /// ahead. Hits lose the defender's Defense (as in the lane) and evasion skips a share of them.
        /// </summary>
        public static double Edge(HeroStats a, HeroStats d)
        {
            double aPerTick = Math.Max(1.0, a.Attack * CritFactor(a) - d.Defense) * (1.0 - d.EvasionBp / 10_000.0) / a.AttackIntervalTicks;
            double dPerTick = Math.Max(1.0, d.Attack * CritFactor(d) - a.Defense) * (1.0 - a.EvasionBp / 10_000.0) / d.AttackIntervalTicks;
            double fellD = d.MaxHp / aPerTick;
            double fellA = a.MaxHp / dPerTick;
            return Math.Log(fellA / fellD);
        }

        /// <summary>The chance the attacker wins with this edge (for the screens; the roll below decides).</summary>
        public static double WinChance(double edge) => 1.0 / (1.0 + Math.Exp(-1.7 * edge / Sigma));

        /// <summary>The attacker wins when the edge plus a roughly normal draw of luck is above zero.</summary>
        public static bool Roll(double edge, IRandom rng)
        {
            // Irwin-Hall: four uniforms, centred and scaled to unit variance.
            double sum = 0;
            for (int i = 0; i < 4; i++) sum += rng.NextInt(1_000_000) / 1_000_000.0;
            double luck = (sum - 2.0) * Math.Sqrt(3.0);
            return edge + luck * Sigma > 0;
        }

        /// <summary>
        /// The champion for the replay: the attacker's own hero (<paramref name="attacker"/>, class and all) fights it with
        /// no draughts under <paramref name="seed"/>. Its blows are sized so the attacker would fall after about
        /// TargetSeconds; its HP is what the attacker deals by three quarters of that (a win) or a quarter more than the
        /// attacker deals before falling (a loss). The fight is checked, so the replay always ends as decided.
        /// </summary>
        public static BossDef Stage(string name, HeroStats attacker, bool win, ulong seed)
        {
            long attack = attacker.Defense + Math.Max(1, attacker.MaxHp / 10);
            long[] trace = Array.Empty<long>();
            int death = -1;
            for (int tries = 0; tries < 12; tries++)
            {
                trace = Trace(attacker, attack, seed, out death);
                int target = TargetSeconds * LaneSim.TicksPerSecond;
                if (death >= 0 && death >= target / 2 && death <= target * 2) break;
                // Too slow (or never) to fall: hit harder; too fast: hit softer.
                attack = death < 0 || death > target * 2 ? attack + Math.Max(1, attack / 2) : Math.Max(attacker.Defense + 1, attack * 2 / 3);
            }

            int end = death >= 0 ? death : trace.Length - 1;
            long hp = win ? Math.Max(1, trace[Math.Max(0, end * 3 / 4)]) : trace[end] * 5 / 4 + 1;
            var champion = new BossDef(ChampionId, 121, name, 1, hp, attack, BossMechanic.None, 0, "");
            for (int check = 0; check < 4; check++)
            {
                bool killed = BossRun.Simulate(champion, attacker, new Inventory(), seed).Killed;
                if (killed == win) return champion;
                hp = win ? Math.Max(1, hp / 2) : hp * 2;
                champion = new BossDef(ChampionId, 121, name, 1, hp, attack, BossMechanic.None, 0, "");
            }
            return champion;
        }

        /// <summary>Damage dealt by each tick against an unbreakable champion; <paramref name="death"/> is the tick the hero fell (-1: never).</summary>
        private static long[] Trace(HeroStats attacker, long attack, ulong seed, out int death)
        {
            var dummy = new BossDef(ChampionId, 121, "", 1, 1_000_000_000_000L, attack, BossMechanic.None, 0, "");
            LaneSim lane = BossRun.Create(dummy, attacker, new Inventory(), seed);
            var trace = new List<long>();
            death = -1;
            for (int tick = 0; tick < BossRun.MaxTicks; tick++)
            {
                lane.Tick();
                lane.DrainEvents();
                trace.Add(lane.BossDamageDealt);
                if (lane.Deaths > 0) { death = tick; break; }
            }
            return trace.ToArray();
        }
    }

    /// <summary>
    /// Fortress keeps (GDD section 7: the top four guilds per fortress bid with the treasury; the Sunday 20:00 siege gives
    /// the fortress for a week). The Banners still besiege the walls all week (the War of Banners); the keep is the
    /// guilds' contest and decides whose flag flies. Bids run all week; at Sunday 20:00 the four highest bidders on each
    /// fortress become its contenders (their bids are spent, the rest go back to their treasuries), and for an hour their
    /// members storm the keep while the holding guild's members hold it. The contender with the most damage takes the
    /// keep if it beat the keep's wall plus what the holders mended; otherwise the holders keep it.
    /// </summary>
    public static class FortressKeeps
    {
        public const int Contenders = 4;
        public const long MinBid = 50_000;
        /// <summary>What the best contender must beat, before the holders' mending.</summary>
        public const long KeepWall = 150_000;
        public const int WindowMinutes = 60;
        /// <summary>A holder's fight mends the keep by this share of its damage (like a Banner defence).</summary>
        public const int MendPercent = 50;
        /// <summary>GDD: the holding guild earns 2% of the Salt Exchange tax (one exchange serves every region for now).</summary>
        public const int TaxSharePercent = 2;

        /// <summary>The keep siege of the bounty week a local time is in: Sunday 20:00, the week's last evening.</summary>
        public static DateTime SiegeStart(DateTime localNow) => Bounties.WeekStart(localNow).AddDays(6);

        /// <summary>The contenders: the highest bids, the earlier one first on a tie; returns their indices.</summary>
        public static List<int> PickContenders(IReadOnlyList<(long Amount, DateTime Utc)> bids)
        {
            var order = new List<int>();
            for (int i = 0; i < bids.Count; i++) order.Add(i);
            order.Sort((x, y) => bids[y].Amount != bids[x].Amount ? bids[y].Amount.CompareTo(bids[x].Amount) : bids[x].Utc.CompareTo(bids[y].Utc));
            return order.GetRange(0, Math.Min(Contenders, order.Count));
        }

        /// <summary>The contender that takes the keep (index into <paramref name="damage"/>), or -1 when the holders keep it.</summary>
        public static int Winner(IReadOnlyList<long> damage, long mended)
        {
            int best = -1;
            for (int i = 0; i < damage.Count; i++)
                if (best < 0 || damage[i] > damage[best]) best = i;
            return best >= 0 && damage[best] > KeepWall + mended ? best : -1;
        }
    }
}
