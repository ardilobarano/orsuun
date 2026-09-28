#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Orsuun.Rules
{
    /// <summary>A townsman's errand: what he asks for (a lifetime counter rising by Target in the day) and who can do it.</summary>
    public sealed class ErrandDef
    {
        public ErrandDef(int id, int giver, string text, FeatMetric metric, long target, Feature? needs = null, int level = 0)
        {
            Id = id;
            Giver = giver;
            Text = text;
            Metric = metric;
            Target = target;
            Needs = needs;
            Level = level;
        }

        public int Id { get; }
        /// <summary>0 Forgemaster Dorun, 1 Ilke of the Scales, 2 Elder Tamir, 3 Pitmaster Bora; -1 anyone's.</summary>
        public int Giver { get; }
        public string Text { get; }
        public FeatMetric Metric { get; }
        public long Target { get; }
        public Feature? Needs { get; }
        public int Level { get; }
    }

    /// <summary>
    /// Daily errands (owner, 28 Sep 2026: picked "Daily errands": "Each townsman gives one small task a day (forge once,
    /// eat a fish, call the Bazaar, win a Pit fight) for sorn and materials, handed in by walking over to him"). Each of
    /// the four townsfolk has two errands that take turns by bounty day (20:00 to 20:00); one the hero cannot do yet (its
    /// screen opens at a higher level) becomes his other, or a spell of hunting. An errand counts what the hero does that
    /// day (the lifetime counters of Rules.Achievements, counted again per day in ErrandProgress) and pays, handed in in
    /// the town square, SornMobs mobs' sorn at the furthest stage cleared and Materials hunt materials. Assumptions (not
    /// stated by the owner): the errands, one a day each, the pay.
    /// </summary>
    public static class Errands
    {
        public const int Givers = 4;
        public const int SornMobs = 60, Materials = 5;

        public static readonly ErrandDef[] All =
        {
            new ErrandDef(0, 0, "Forge any piece once", FeatMetric.ForgeAttempts, 1),
            new ErrandDef(1, 0, "Turn a piece's etchings once", FeatMetric.Turns, 1),
            new ErrandDef(2, 1, "Call the Bazaar once", FeatMetric.BazaarCalls, 1, level: Chat.TradeLevel),
            new ErrandDef(3, 1, "Sell a piece to the merchant", FeatMetric.ItemsSold, 1),
            new ErrandDef(4, 2, "Eat a fish from the river", FeatMetric.FishEaten, 1, Feature.Fishing),
            new ErrandDef(5, 2, "Claim a bounty", FeatMetric.BountiesClaimed, 1, Feature.Bounties),
            new ErrandDef(6, 3, "Win a fight in the Pits", FeatMetric.PitWins, 1, Feature.Pits),
            new ErrandDef(7, 3, "Fight a Commander", FeatMetric.CommanderFights, 1, Feature.Commanders),
        };

        /// <summary>What a townsman asks of a hero who can do neither of his errands yet.</summary>
        public static readonly ErrandDef Hunt = new ErrandDef(8, -1, "Hunt for ten minutes", FeatMetric.HuntSeconds, 600);

        /// <summary>A bounty day's number (days since 1 Jan 2026) from its key (Bounties.DayKey).</summary>
        public static int DayNumber(string dayKey) =>
            DateTime.TryParseExact(dayKey, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime d)
                ? (int)(d - new DateTime(2026, 1, 1)).TotalDays : 0;

        /// <summary>A townsman's errand on a day for a hero of a level.</summary>
        public static ErrandDef For(int giver, string dayKey, int level)
        {
            ErrandDef[] his = All.Where(e => e.Giver == giver).ToArray();
            if (his.Length == 0) return Hunt;
            ErrandDef pick = his[Math.Abs(DayNumber(dayKey) + giver) % his.Length];
            if (Open(pick, level)) return pick;
            return his.FirstOrDefault(e => Open(e, level)) ?? Hunt;
        }

        public static bool Open(ErrandDef errand, int level) =>
            level >= errand.Level && (errand.Needs == null || level >= Unlocks.Level(errand.Needs.Value));

        /// <summary>An errand's sorn, by the furthest stage the hero has cleared.</summary>
        public static long Sorn(int highestStageCleared) =>
            Content.Stage(Math.Max(1, Math.Min(Content.TotalStages, highestStageCleared))).SornPerMob * SornMobs;
    }

    /// <summary>
    /// A hero's errand day: how much each lifetime counter rose that day, and which townsfolk have paid. Stored as
    /// "day|metric:count,...|paid" (paid a bit per giver); a new day starts empty.
    /// </summary>
    public sealed class ErrandProgress
    {
        private readonly Dictionary<FeatMetric, long> _counts = new Dictionary<FeatMetric, long>();
        public string Day { get; private set; } = "";
        private int _paid;

        public static ErrandProgress Parse(string? text, string today)
        {
            var p = new ErrandProgress();
            string[] parts = (text ?? "").Split('|');
            if (parts.Length == 3 && parts[0] == today)
            {
                p.Day = today;
                foreach (string pair in parts[1].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    int colon = pair.IndexOf(':');
                    if (colon > 0 && int.TryParse(pair.Substring(0, colon), NumberStyles.Integer, CultureInfo.InvariantCulture, out int m)
                        && long.TryParse(pair.Substring(colon + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out long v))
                        p._counts[(FeatMetric)m] = v;
                }
                int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out p._paid);
            }
            p.Day = today;
            return p;
        }

        public string Serialize() =>
            Day + "|" + string.Join(",", _counts.OrderBy(kv => (int)kv.Key).Select(kv => ((int)kv.Key).ToString(CultureInfo.InvariantCulture) + ":" + kv.Value.ToString(CultureInfo.InvariantCulture)))
            + "|" + _paid.ToString(CultureInfo.InvariantCulture);

        public void Add(FeatMetric metric, long amount)
        {
            if (amount > 0) _counts[metric] = Count(metric) + amount;
        }

        public long Count(FeatMetric metric) => _counts.TryGetValue(metric, out long v) ? v : 0;

        public bool Done(ErrandDef errand) => Count(errand.Metric) >= errand.Target;

        public bool Paid(int giver) => giver >= 0 && (_paid & (1 << giver)) != 0;

        public void Pay(int giver)
        {
            if (giver >= 0) _paid |= 1 << giver;
        }
    }
}
