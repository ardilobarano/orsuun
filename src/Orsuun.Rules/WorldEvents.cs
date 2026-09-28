#nullable enable
using System;
using System.Collections.Generic;

namespace Orsuun.Rules
{
    /// <summary>Kinds of timed world event (stored by number on the server's calendar: append, never renumber).</summary>
    public enum WorldEventKind
    {
        None = 0,
        /// <summary>Hunting pays double sorn.</summary>
        DoubleSorn = 1,
        /// <summary>Every Forge attempt has +5% chance.</summary>
        LuckyForge = 2,
        /// <summary>Commanders come back every 15 minutes instead of 45.</summary>
        CommanderRush = 3,
        /// <summary>The heaviest fish landed at Old Nergui's river win prizes (Rules.Fishing, since 28 Sep 2026).</summary>
        FishingContest = 4,
    }

    public sealed class WorldEventDef
    {
        public WorldEventDef(WorldEventKind kind, string name, string effect, DayOfWeek[] days, int startHour, int hours)
        {
            Kind = kind;
            Name = name;
            Effect = effect;
            Days = days;
            StartHour = startHour;
            Hours = hours;
        }

        public WorldEventKind Kind { get; }
        public string Name { get; }
        /// <summary>What it does, for the HUD, chat and the notification.</summary>
        public string Effect { get; }
        /// <summary>The weekly calendar: the days it starts on (server time), its start hour and length.</summary>
        public DayOfWeek[] Days { get; }
        public int StartHour { get; }
        public int Hours { get; }
    }

    /// <summary>
    /// Weekend events (owner, 27 Sep 2026: "Weekend events", offered as timed world events announced in chat and on the
    /// HUD, set on the server's calendar so none needs an app update). Each week the server's calendar gets a double sorn
    /// weekend (Saturday and Sunday), a lucky forge hour on Saturday and Sunday at 20:00, just before the Evening Bells,
    /// a Commander rush on Friday and Saturday nights, and (since 28 Sep 2026) a fishing contest from Saturday noon to
    /// Sunday's Evening Bells; moderators add more or call one off. Times are server-local.
    /// </summary>
    public static class WorldEvents
    {
        /// <summary>A double sorn weekend adds this much to hunting's sorn.</summary>
        public const int SornBonusPercent = 100;
        /// <summary>The lucky forge hour's extra chance on every Forge attempt (+5%).</summary>
        public const int ForgeLuckBp = 500;
        /// <summary>During a Commander rush a Commander comes back this soon after its spawn (its window is 10 minutes).</summary>
        public const int RushRespawnSeconds = 15 * 60;
        /// <summary>The longest event a moderator can set (a week).</summary>
        public const int MaxHours = 7 * 24;

        public static readonly WorldEventDef[] All =
        {
            new WorldEventDef(WorldEventKind.DoubleSorn, "Double Sorn Weekend", "every hunt pays double sorn",
                new[] { DayOfWeek.Saturday }, 0, 48),
            new WorldEventDef(WorldEventKind.LuckyForge, "Lucky Forge Hour", "+5% chance on every Forge",
                new[] { DayOfWeek.Saturday, DayOfWeek.Sunday }, 20, 1),
            new WorldEventDef(WorldEventKind.CommanderRush, "Commander Rush", "Commanders return every 15 minutes",
                new[] { DayOfWeek.Friday, DayOfWeek.Saturday }, 20, 4),
            // Saturday noon to Sunday's Evening Bells.
            new WorldEventDef(WorldEventKind.FishingContest, "Fishing Contest", "the heaviest fish at Old Nergui's river win prizes",
                new[] { DayOfWeek.Saturday }, 12, 32),
        };

        public static WorldEventDef? Def(WorldEventKind kind)
        {
            foreach (WorldEventDef d in All)
                if (d.Kind == kind)
                    return d;
            return null;
        }

        /// <summary>The weekly calendar's events that start in [fromLocal, fromLocal + days), in server-local time.</summary>
        public static List<(WorldEventKind Kind, DateTime Start, DateTime End)> Weekly(DateTime fromLocal, int days)
        {
            var list = new List<(WorldEventKind, DateTime, DateTime)>();
            DateTime day = fromLocal.Date;
            for (int i = 0; i <= days; i++, day = day.AddDays(1))
                foreach (WorldEventDef d in All)
                {
                    if (Array.IndexOf(d.Days, day.DayOfWeek) < 0) continue;
                    DateTime start = day.AddHours(d.StartHour);
                    if (start < fromLocal || start >= fromLocal.AddDays(days)) continue;
                    list.Add((d.Kind, start, start.AddHours(d.Hours)));
                }
            return list;
        }

        /// <summary>
        /// The sorn bonus for hunting over [from, to): a double sorn event pays its bonus for the share of the time it
        /// covers, so an evening away that ran into the weekend earns it for the weekend's part only.
        /// </summary>
        public static int SornBonus(DateTime from, DateTime to, IEnumerable<(DateTime Start, DateTime End)> doubleSorn)
        {
            double total = (to - from).TotalSeconds;
            if (total <= 0) return 0;
            double covered = 0;
            foreach ((DateTime start, DateTime end) in doubleSorn)
            {
                DateTime a = start > from ? start : from, b = end < to ? end : to;
                if (b > a) covered += (b - a).TotalSeconds;
            }
            return (int)Math.Round(SornBonusPercent * Math.Min(1.0, covered / total));
        }

        /// <summary>When a Commander that spawned at <paramref name="spawn"/> spawns next.</summary>
        public static DateTime NextSpawn(DateTime spawn, int respawnSeconds, bool rushAtSpawn) =>
            spawn.AddSeconds(rushAtSpawn ? Math.Min(respawnSeconds, RushRespawnSeconds) : respawnSeconds);
    }
}
