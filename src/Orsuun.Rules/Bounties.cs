#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Orsuun.Rules
{
    /// <summary>What a bounty counts. The server counts every one from its own verdicts, never from the client.</summary>
    public enum BountyMetric
    {
        Korstones,
        HuntSeconds,
        ForgeAttempts,
        Turns,
        CommanderFights,
        Pushes,
        SiegeFights,
    }

    public enum BountyPeriod
    {
        Daily,
        Weekly,
    }

    public sealed class BountyDef
    {
        public BountyDef(int id, BountyPeriod period, BountyMetric metric, int target, int marks, string title)
        {
            Id = id;
            Period = period;
            Metric = metric;
            Target = target;
            Marks = marks;
            Title = title;
        }

        public int Id { get; }
        public BountyPeriod Period { get; }
        public BountyMetric Metric { get; }
        public int Target { get; }
        public int Marks { get; }
        public string Title { get; }
    }

    /// <summary>
    /// Hunt Marks bounties (GDD, retention: "daily and weekly bounties pay Hunt Marks; a Hunt Marks shop sells Etching
    /// Needles and Pinning Wax"). The day turns at 20:00 server time (GDD: daily reset at 20:00, so the evening session
    /// opens with fresh bounties); the week turns at Monday 20:00.
    /// </summary>
    public static class Bounties
    {
        public const int ResetHour = 20;

        public static readonly BountyDef[] All =
        {
            new BountyDef(1, BountyPeriod.Daily, BountyMetric.Korstones, 20, 3, "Break 20 Korstones"),
            new BountyDef(2, BountyPeriod.Daily, BountyMetric.HuntSeconds, 30 * 60, 2, "Hunt for 30 minutes"),
            new BountyDef(3, BountyPeriod.Daily, BountyMetric.ForgeAttempts, 5, 2, "Forge 5 times"),
            new BountyDef(4, BountyPeriod.Daily, BountyMetric.Turns, 25, 2, "Turn etchings 25 times"),
            new BountyDef(5, BountyPeriod.Daily, BountyMetric.CommanderFights, 1, 3, "Fight a Commander"),
            new BountyDef(11, BountyPeriod.Weekly, BountyMetric.Korstones, 200, 12, "Break 200 Korstones"),
            new BountyDef(12, BountyPeriod.Weekly, BountyMetric.CommanderFights, 5, 10, "Fight 5 Commanders"),
            new BountyDef(13, BountyPeriod.Weekly, BountyMetric.Pushes, 10, 8, "Push 10 times"),
            new BountyDef(14, BountyPeriod.Weekly, BountyMetric.SiegeFights, 3, 10, "Join 3 fortress sieges"),
        };

        public static BountyDef? Find(int id)
        {
            foreach (BountyDef b in All)
                if (b.Id == id) return b;
            return null;
        }

        /// <summary>The bounty day a server-local time falls in: it starts at 20:00, so 19:59 still counts as the day before.</summary>
        public static DateTime DayStart(DateTime local)
        {
            DateTime today = local.Date.AddHours(ResetHour);
            return local >= today ? today : today.AddDays(-1);
        }

        /// <summary>The bounty week: from Monday 20:00 to the next Monday 20:00.</summary>
        public static DateTime WeekStart(DateTime local)
        {
            DateTime day = DayStart(local);
            int sinceMonday = ((int)day.DayOfWeek + 6) % 7;
            return day.AddDays(-sinceMonday);
        }

        public static string DayKey(DateTime local) => DayStart(local).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        public static string WeekKey(DateTime local) => "W" + WeekStart(local).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        public static int SecondsToDailyReset(DateTime local) => (int)Math.Ceiling((DayStart(local).AddDays(1) - local).TotalSeconds);
        public static int SecondsToWeeklyReset(DateTime local) => (int)Math.Ceiling((WeekStart(local).AddDays(7) - local).TotalSeconds);
    }

    /// <summary>
    /// One account's counts for the current day and week, and which bounties it has claimed. A new day (or week)
    /// clears that period's counts and claims. Stored as one short text column.
    /// </summary>
    public sealed class BountyProgress
    {
        private static readonly int Metrics = Enum.GetValues(typeof(BountyMetric)).Length;

        public string DayKey { get; private set; } = "";
        public string WeekKey { get; private set; } = "";
        public long[] Daily { get; } = new long[Metrics];
        public long[] Weekly { get; } = new long[Metrics];
        private readonly HashSet<int> _claimed = new HashSet<int>();

        /// <summary>Moves to the given day and week, clearing whatever belongs to a period that has ended.</summary>
        public void Roll(string dayKey, string weekKey)
        {
            if (dayKey != DayKey)
            {
                DayKey = dayKey;
                Array.Clear(Daily, 0, Daily.Length);
                foreach (BountyDef b in Bounties.All) if (b.Period == BountyPeriod.Daily) _claimed.Remove(b.Id);
            }
            if (weekKey != WeekKey)
            {
                WeekKey = weekKey;
                Array.Clear(Weekly, 0, Weekly.Length);
                foreach (BountyDef b in Bounties.All) if (b.Period == BountyPeriod.Weekly) _claimed.Remove(b.Id);
            }
        }

        public void Add(BountyMetric metric, long amount)
        {
            if (amount <= 0) return;
            Daily[(int)metric] += amount;
            Weekly[(int)metric] += amount;
        }

        public long Count(BountyDef b) => (b.Period == BountyPeriod.Daily ? Daily : Weekly)[(int)b.Metric];
        public bool Claimed(BountyDef b) => _claimed.Contains(b.Id);
        public bool Claimable(BountyDef b) => !Claimed(b) && Count(b) >= b.Target;

        /// <summary>Claims a finished bounty and returns its Hunt Marks.</summary>
        public int Claim(BountyDef b)
        {
            if (Claimed(b)) throw new InvalidOperationException("Already claimed.");
            if (Count(b) < b.Target) throw new InvalidOperationException("Not finished yet.");
            _claimed.Add(b.Id);
            return b.Marks;
        }

        /// <summary>"day|week|d0,d1,...|w0,w1,...|claimed ids".</summary>
        public string Serialize()
        {
            var sb = new StringBuilder();
            sb.Append(DayKey).Append('|').Append(WeekKey).Append('|');
            sb.Append(string.Join(",", Daily)).Append('|').Append(string.Join(",", Weekly)).Append('|');
            sb.Append(string.Join(",", _claimed));
            return sb.ToString();
        }

        public static BountyProgress Parse(string? text)
        {
            var p = new BountyProgress();
            if (string.IsNullOrEmpty(text)) return p;
            string[] parts = text!.Split('|');
            if (parts.Length != 5) return p;
            p.DayKey = parts[0];
            p.WeekKey = parts[1];
            Fill(parts[2], p.Daily);
            Fill(parts[3], p.Weekly);
            foreach (string id in parts[4].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                if (int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)) p._claimed.Add(v);
            return p;
        }

        private static void Fill(string csv, long[] into)
        {
            string[] values = csv.Split(',');
            for (int i = 0; i < into.Length && i < values.Length; i++)
                if (long.TryParse(values[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out long v)) into[i] = v;
        }
    }

    public sealed class ShopItem
    {
        public ShopItem(int id, string name, int marks, string detail, Action<Inventory> grant)
        {
            Id = id;
            Name = name;
            Marks = marks;
            Detail = detail;
            Grant = grant;
        }

        public int Id { get; }
        public string Name { get; }
        public int Marks { get; }
        public string Detail { get; }
        public Action<Inventory> Grant { get; }
    }

    /// <summary>The Hunt Marks shop: tools for etchings and a few staples, never power that money could buy.</summary>
    public static class HuntShop
    {
        public static readonly ShopItem[] Items =
        {
            new ShopItem(1, "Etching Needle", 4, "Adds an etching (1st to 4th)", i => i.EtchingNeedles++),
            new ShopItem(2, "Pinning Wax", 6, "Holds one etching through turns", i => i.PinningWax++),
            new ShopItem(3, "10 Turnstones", 3, "Turn etchings", i => i.Turnstones += 10),
            new ShopItem(4, "Scroll of Mercy", 8, "A failed Forge only loses a level", i => i.ScrollsOfMercy++),
            new ShopItem(5, "10 Draughts", 1, "Healing draughts for the hunt", i => i.Potions += 10),
        };

        public static ShopItem? Find(int id)
        {
            foreach (ShopItem s in Items)
                if (s.Id == id) return s;
            return null;
        }

        /// <summary>Buys count of an item with Hunt Marks.</summary>
        public static void Buy(Inventory inventory, int itemId, int count)
        {
            ShopItem item = Find(itemId) ?? throw new InvalidOperationException("Unknown shop item.");
            if (count < 1 || count > 99) throw new InvalidOperationException("Buy 1 to 99 at a time.");
            int cost = item.Marks * count;
            if (inventory.HuntMarks < cost) throw new InvalidOperationException("Not enough Hunt Marks.");
            inventory.HuntMarks -= cost;
            for (int i = 0; i < count; i++) item.Grant(inventory);
        }
    }
}
