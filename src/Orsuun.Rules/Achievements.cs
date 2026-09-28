#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Orsuun.Rules
{
    /// <summary>
    /// A hero's lifetime counters for achievements, stored by number (append, never renumber). The first seven follow
    /// BountyMetric, so every bounty count also counts here.
    /// </summary>
    public enum FeatMetric
    {
        Korstones = 0,
        HuntSeconds = 1,
        ForgeAttempts = 2,
        Turns = 3,
        CommanderFights = 4,
        Pushes = 5,
        SiegeFights = 6,
        CommanderLastBlows = 7,
        DungeonClears = 8,
        BountiesClaimed = 9,
        GiftsClaimed = 10,
        SornDonated = 11,
        /// <summary>The highest upgrade a Forge (or the Chained Smith) has ever given this hero's pieces.</summary>
        BestUpgrade = 12,
        // Since 28 Sep 2026 (the townsfolk's errands, Rules.Errands).
        FishEaten = 13,
        BazaarCalls = 14,
        ItemsSold = 15,
        PitWins = 16,
    }

    /// <summary>What an achievement measures: a lifetime counter, or the hero as it stands.</summary>
    public enum FeatSource
    {
        Counter,
        Level,
        MapsCleared,
        PitWins,
        WeaponsBroken,
        InGuild,
        BestSkillGrade,
        BestUpgrade,
    }

    public sealed class AchievementDef
    {
        public AchievementDef(int id, string name, string text, FeatSource source, long target, int honor, int sornMobs,
            string? title = null, FeatMetric metric = FeatMetric.Korstones)
        {
            Id = id;
            Name = name;
            Text = text;
            Source = source;
            Target = target;
            Honor = honor;
            SornMobs = sornMobs;
            Title = title;
            Metric = metric;
        }

        /// <summary>Stored on heroes when claimed (and as the chosen title): never renumber.</summary>
        public int Id { get; }
        public string Name { get; }
        public string Text { get; }
        public FeatSource Source { get; }
        public FeatMetric Metric { get; }
        public long Target { get; }
        public int Honor { get; }
        /// <summary>The sorn reward, in mobs' worth at the hero's furthest stage (so it keeps its weight as he climbs).</summary>
        public int SornMobs { get; }
        /// <summary>The title claiming it unlocks, or null.</summary>
        public string? Title { get; }
    }

    /// <summary>Everything an achievement can look at, gathered from the hero.</summary>
    public sealed class FeatSnapshot
    {
        public FeatCounters Counters = new FeatCounters();
        public int Level;
        public int HighestStageCleared;
        public int PitWins;
        public int WeaponsBroken;
        public bool InGuild;
        public int BestSkillGrade;
        /// <summary>The highest upgrade among the pieces the hero owns now.</summary>
        public int BestOwnedUpgrade;
    }

    /// <summary>Lifetime counters, stored as "metric:value,metric:value".</summary>
    public sealed class FeatCounters
    {
        private readonly Dictionary<FeatMetric, long> _values = new Dictionary<FeatMetric, long>();

        public long this[FeatMetric metric] => _values.TryGetValue(metric, out long v) ? v : 0;

        public void Add(FeatMetric metric, long amount)
        {
            if (amount <= 0) return;
            _values[metric] = this[metric] + amount;
        }

        /// <summary>Keeps the larger (a best, not a sum).</summary>
        public void Raise(FeatMetric metric, long value)
        {
            if (value > this[metric]) _values[metric] = value;
        }

        public static FeatCounters Parse(string? text)
        {
            var c = new FeatCounters();
            if (string.IsNullOrEmpty(text)) return c;
            foreach (string part in text!.Split(','))
            {
                int colon = part.IndexOf(':');
                if (colon <= 0) continue;
                if (int.TryParse(part.Substring(0, colon), NumberStyles.Integer, CultureInfo.InvariantCulture, out int m)
                    && long.TryParse(part.Substring(colon + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out long v))
                    c._values[(FeatMetric)m] = v;
            }
            return c;
        }

        public string Serialize()
        {
            var sb = new StringBuilder();
            foreach (KeyValuePair<FeatMetric, long> kv in _values.OrderBy(kv => (int)kv.Key))
            {
                if (kv.Value <= 0) continue;
                if (sb.Length > 0) sb.Append(',');
                sb.Append(((int)kv.Key).ToString(CultureInfo.InvariantCulture)).Append(':').Append(kv.Value.ToString(CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// Titles and achievements (owner, 27 Sep 2026: "Titles & achievements"): lifetime feats a hero claims on the
    /// ACHIEVEMENTS screen for Honor and sorn; the greatest also grant a title, which the hero may wear before his name
    /// in chat and on the Pits' boards (one at a time; the Pits' own season title shows when none is worn).
    /// </summary>
    public static class Achievements
    {
        public static readonly AchievementDef[] All = Build();

        private static AchievementDef[] Build()
        {
            var list = new List<AchievementDef>
            {
                // Korstones and the hunt.
                new AchievementDef(1, "First Crack", "Break a Korstone.", FeatSource.Counter, 1, 5, 20, metric: FeatMetric.Korstones),
                new AchievementDef(2, "Stone Breaker", "Break 100 Korstones.", FeatSource.Counter, 100, 20, 100, metric: FeatMetric.Korstones),
                new AchievementDef(3, "Quarry Master", "Break 1,000 Korstones.", FeatSource.Counter, 1000, 60, 300, "Stonebreaker", FeatMetric.Korstones),
                new AchievementDef(4, "Korstone Bane", "Break 10,000 Korstones.", FeatSource.Counter, 10000, 150, 800, "Korstone Bane", FeatMetric.Korstones),
                new AchievementDef(5, "Out on the Steppe", "Hunt for an hour while you watch.", FeatSource.Counter, 3600, 5, 20, metric: FeatMetric.HuntSeconds),
                new AchievementDef(6, "Long Rides", "Hunt for a day while you watch.", FeatSource.Counter, 24 * 3600, 30, 150, metric: FeatMetric.HuntSeconds),
                new AchievementDef(7, "Steppe-Born", "Hunt for 200 hours while you watch.", FeatSource.Counter, 200 * 3600, 120, 600, "Steppe-Born", FeatMetric.HuntSeconds),
                // The hero.
                new AchievementDef(20, "Blooded", "Reach level 10.", FeatSource.Level, 10, 10, 50),
                new AchievementDef(21, "Seasoned", "Reach level 30.", FeatSource.Level, 30, 30, 150),
                new AchievementDef(22, "Veteran", "Reach level 60.", FeatSource.Level, 60, 60, 300, "Veteran of the Steppe"),
                new AchievementDef(23, "Warlord", "Reach level 90.", FeatSource.Level, 90, 100, 500, "Warlord"),
                new AchievementDef(24, "Legend", "Reach level " + Content.MaxLevel + ".", FeatSource.Level, Content.MaxLevel, 150, 800, "Legend of the Banners"),
                // The Forge.
                new AchievementDef(30, "First Sparks", "Make 10 Forge attempts.", FeatSource.Counter, 10, 5, 20, metric: FeatMetric.ForgeAttempts),
                new AchievementDef(31, "Anvil Regular", "Make 200 Forge attempts.", FeatSource.Counter, 200, 40, 200, metric: FeatMetric.ForgeAttempts),
                new AchievementDef(32, "Gleam", "Forge a piece to +7.", FeatSource.BestUpgrade, 7, 20, 100),
                new AchievementDef(33, "Gold Shine", "Forge a piece to +8.", FeatSource.BestUpgrade, 8, 40, 200),
                new AchievementDef(34, "Ember-Gold", "Forge a piece to +9.", FeatSource.BestUpgrade, 9, 100, 500, "Oathsmith"),
                new AchievementDef(35, "Oathbreaker", "Lose 10 weapons to an Oathbreak.", FeatSource.WeaponsBroken, 10, 30, 150, "Oathbreaker"),
                new AchievementDef(36, "Etcher", "Turn etchings 100 times.", FeatSource.Counter, 100, 20, 100, metric: FeatMetric.Turns),
                // Commanders and sieges.
                new AchievementDef(40, "Commander Hunter", "Fight a Commander 10 times.", FeatSource.Counter, 10, 20, 100, metric: FeatMetric.CommanderFights),
                new AchievementDef(41, "Last Blow", "Strike a Commander's last blow.", FeatSource.Counter, 1, 40, 200, metric: FeatMetric.CommanderLastBlows),
                new AchievementDef(42, "Commander's Bane", "Strike 10 Commanders' last blows.", FeatSource.Counter, 10, 120, 600, "Commander's Bane", FeatMetric.CommanderLastBlows),
                new AchievementDef(43, "Siege Hand", "Fight 20 siege battles.", FeatSource.Counter, 20, 30, 150, metric: FeatMetric.SiegeFights),
                new AchievementDef(44, "Wall-Breaker", "Fight 200 siege battles.", FeatSource.Counter, 200, 100, 500, "Wall-Breaker", FeatMetric.SiegeFights),
                // The Pits and the dungeons.
                new AchievementDef(50, "Pit Blooded", "Win 10 Pit fights.", FeatSource.PitWins, 10, 20, 100),
                new AchievementDef(51, "Pit Fighter", "Win 100 Pit fights.", FeatSource.PitWins, 100, 60, 300, "Pit Fighter"),
                new AchievementDef(52, "Pit Legend", "Win 500 Pit fights.", FeatSource.PitWins, 500, 150, 800, "Pit Legend"),
                new AchievementDef(55, "Delver", "Clear 5 dungeons.", FeatSource.Counter, 5, 30, 150, metric: FeatMetric.DungeonClears),
                new AchievementDef(56, "Deep Delver", "Clear 50 dungeons.", FeatSource.Counter, 50, 100, 500, "Deep Delver", FeatMetric.DungeonClears),
                // Bounties, gifts, the guild, the skills.
                new AchievementDef(60, "Bounty Hunter", "Claim 50 bounties.", FeatSource.Counter, 50, 50, 250, "Bounty Hunter", FeatMetric.BountiesClaimed),
                new AchievementDef(61, "Faithful", "Take 7 daily gifts.", FeatSource.Counter, 7, 20, 100, metric: FeatMetric.GiftsClaimed),
                new AchievementDef(62, "Old Friend of the Caravan", "Take 30 daily gifts.", FeatSource.Counter, 30, 60, 300, metric: FeatMetric.GiftsClaimed),
                new AchievementDef(65, "Banner-Brother", "Join a guild.", FeatSource.InGuild, 1, 10, 50),
                new AchievementDef(66, "Patron", "Give 1,000,000 sorn to your guild.", FeatSource.Counter, 1000000, 60, 300, "Patron", FeatMetric.SornDonated),
                new AchievementDef(70, "Mastered", "Raise a skill to M10.", FeatSource.BestSkillGrade, SkillGrades.MasteredSteps, 30, 150),
                new AchievementDef(71, "Peerless", "Raise a skill to Peerless.", FeatSource.BestSkillGrade, SkillGrades.Max, 150, 800, "Peerless"),
            };
            // The campaign: every map's last stage (100 + map).
            foreach (MapDef map in Content.Maps)
            {
                string? title = map.Id == 6 ? "Wayfarer" : map.Id == Content.Maps.Length ? "Walker of the Twelve Roads" : null;
                int honor = 10 + map.Id * 8;
                list.Add(new AchievementDef(100 + map.Id, map.Name, "Clear " + map.Name + ".", FeatSource.MapsCleared, map.Id, honor, honor * 2, title));
            }
            return list.OrderBy(Order).ToArray();
        }

        /// <summary>The screen's order: the hunt, the campaign, the hero, then the rest by id.</summary>
        private static int Order(AchievementDef d) => d.Id < 10 ? d.Id : d.Id > 100 ? 10 + d.Id : d.Id + 1000;

        public static AchievementDef? Find(int id)
        {
            foreach (AchievementDef d in All)
                if (d.Id == id)
                    return d;
            return null;
        }

        public static long Progress(AchievementDef def, FeatSnapshot s)
        {
            switch (def.Source)
            {
                case FeatSource.Counter: return s.Counters[def.Metric];
                case FeatSource.Level: return s.Level;
                case FeatSource.MapsCleared: return s.HighestStageCleared / MapDef.StagesPerMap;
                case FeatSource.PitWins: return s.PitWins;
                case FeatSource.WeaponsBroken: return s.WeaponsBroken;
                case FeatSource.InGuild: return s.InGuild ? 1 : 0;
                case FeatSource.BestSkillGrade: return s.BestSkillGrade;
                case FeatSource.BestUpgrade: return Math.Max(s.Counters[FeatMetric.BestUpgrade], s.BestOwnedUpgrade);
                default: return 0;
            }
        }

        public static bool Done(AchievementDef def, FeatSnapshot s) => Progress(def, s) >= def.Target;

        /// <summary>The sorn a claim pays at the hero's furthest stage.</summary>
        public static long Sorn(AchievementDef def, int highestStageCleared) =>
            def.SornMobs * Content.Stage(Math.Max(1, highestStageCleared)).SornPerMob;

        /// <summary>Claimed ids, stored as "1,5,9".</summary>
        public static HashSet<int> ParseClaimed(string? text)
        {
            var set = new HashSet<int>();
            if (string.IsNullOrEmpty(text)) return set;
            foreach (string part in text!.Split(','))
                if (int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
                    set.Add(id);
            return set;
        }

        public static string SerializeClaimed(IEnumerable<int> ids) =>
            string.Join(",", ids.OrderBy(i => i).Select(i => i.ToString(CultureInfo.InvariantCulture)));

        /// <summary>Done but not yet claimed: the badge on MENU.</summary>
        public static int Ready(FeatSnapshot s, HashSet<int> claimed) => All.Count(d => !claimed.Contains(d.Id) && Done(d, s));

        /// <summary>The title a claimed achievement lets the hero wear, or null.</summary>
        public static string? TitleOf(int id) => Find(id)?.Title;
    }
}
