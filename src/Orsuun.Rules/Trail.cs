#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Orsuun.Rules
{
    /// <summary>What a character bought of this season's Trail: nothing, the paid track, or the paid track and ten tiers.</summary>
    public enum TrailPass { None = 0, Trail = 1, Plus = 2 }

    /// <summary>One tier's reward on one track.</summary>
    public sealed class TrailReward
    {
        public int Turnstones { get; set; }
        public int ScrollsOfMercy { get; set; }
        public int KhansAlloys { get; set; }
        public int AnvilWards { get; set; }
        /// <summary>A wardrobe piece (the season's costume or mount), held until the season ends.</summary>
        public string? Piece { get; set; }

        public string Text
        {
            get
            {
                var parts = new List<string>();
                if (Piece != null) parts.Add(Wardrobe.Find(Piece)?.Name ?? Piece);
                if (KhansAlloys > 0) parts.Add(KhansAlloys + (KhansAlloys == 1 ? " Khan's Alloy" : " Khan's Alloys"));
                if (AnvilWards > 0) parts.Add(AnvilWards + (AnvilWards == 1 ? " Anvil Ward" : " Anvil Wards"));
                if (ScrollsOfMercy > 0) parts.Add(ScrollsOfMercy + (ScrollsOfMercy == 1 ? " Scroll of Mercy" : " Scrolls of Mercy"));
                if (Turnstones > 0) parts.Add(Turnstones + " Turnstones");
                return string.Join(" + ", parts);
            }
        }

        /// <summary>Adds the reward to the inventory; a piece goes to the pending wardrobe drops for <paramref name="pieceDays"/>.</summary>
        public void GrantTo(Inventory inventory, int pieceDays)
        {
            inventory.Turnstones += Turnstones;
            inventory.ScrollsOfMercy += ScrollsOfMercy;
            inventory.KhansAlloys += KhansAlloys;
            inventory.AnvilWards += AnvilWards;
            if (Piece != null) inventory.WardrobeDrops.Add(Piece + ":" + pieceDays.ToString(CultureInfo.InvariantCulture));
        }
    }

    public sealed class TrailSeason
    {
        public TrailSeason(int number, string name, string costume, string mount, DateTime startLocal)
        {
            Number = number;
            Name = name;
            Costume = costume;
            Mount = mount;
            StartLocal = startLocal;
        }

        public int Number { get; }
        public string Name { get; }
        /// <summary>The season's costume (paid tier 1) and mount (paid tier 50): wardrobe ids.</summary>
        public string Costume { get; }
        public string Mount { get; }
        public DateTime StartLocal { get; }
        public DateTime EndLocal => StartLocal.AddDays(CampaignTrail.SeasonDays);
    }

    /// <summary>
    /// The Campaign Trail (GDD section 9, the battle pass; owner, 25 Sep 2026: built first of "Campaign Trail, Maps 5 and 6,
    /// Player-to-player trade, More dungeons"). An 8 week season of 50 tiers. Bounties pay Trail XP (GDD: the day's
    /// missions give "Campaign Trail XP"). The free track pays Turnstones, Scrolls of Mercy and a Khan's Alloy every 10
    /// tiers; the paid track the season costume, the season mount, 300 Turnstones, 20 Khan's Alloys and 3 Anvil Wards
    /// (GDD). The paid track costs the Amber of the $9.99 pack, Trail Plus the Amber of the $19.99 pack and adds 10 tiers
    /// (GDD: "9.99, premium plus 19.99"). Each character climbs its own Trail (Amber is the account's, the rest is the
    /// character's: owner, 25 Sep 2026).
    /// </summary>
    public static class CampaignTrail
    {
        public const int Tiers = 50;
        public const int XpPerTier = 600;
        public const int DailyBountyXp = 100;
        public const int WeeklyBountyXp = 500;
        public const int SeasonDays = 56;
        public const int TrailAmber = 650;
        public const int PlusAmber = 1400;
        public const int PlusTiers = 10;
        /// <summary>A season piece is held until the season ends, and never for less than this.</summary>
        public const int MinPieceDays = 14;

        /// <summary>Season 1 opens with the bounty week of Monday 21 Sep 2026, 20:00 server time.</summary>
        public static readonly DateTime Epoch = new DateTime(2026, 9, 21, Bounties.ResetHour, 0, 0);

        /// <summary>Each season's name, costume and mount; a season past the list repeats the last one until its art is made.</summary>
        private static readonly (string Name, string Costume, string Mount)[] Themes =
        {
            ("The Amber Road", "amber-road-regalia", "amber-road-courser"),
            ("The White Steppe", "white-steppe-regalia", "white-steppe-courser"),
        };

        public static TrailSeason Season(DateTime local)
        {
            int index = Math.Max(0, (int)Math.Floor((local - Epoch).TotalDays / SeasonDays));
            return Season(index + 1);
        }

        public static TrailSeason Season(int number)
        {
            int n = Math.Max(1, number);
            var theme = Themes[Math.Min(n, Themes.Length) - 1];
            return new TrailSeason(n, theme.Name, theme.Costume, theme.Mount, Epoch.AddDays((n - 1) * (double)SeasonDays));
        }

        /// <summary>True for a season's costume or mount: won on the Trail, never sold or dropped.</summary>
        public static bool IsTrailPiece(string? id)
        {
            foreach (var theme in Themes)
                if (theme.Costume == id || theme.Mount == id) return true;
            return false;
        }

        /// <summary>Trail XP a claimed bounty pays. The trail-cache bounty (29 Sep 2026) pays Hunt Marks only: the season is
        /// paced for the five dailies and one weekly a week (TrailTests), and a sixth daily would let the dailies alone finish it.</summary>
        public static int BountyXp(BountyDef bounty) => !PaysTrail(bounty) ? 0 : bounty.Period == BountyPeriod.Daily ? DailyBountyXp : WeeklyBountyXp;

        public static bool PaysTrail(BountyDef bounty) => bounty.Metric != BountyMetric.CachesOpened;

        /// <summary>Free track: a Khan's Alloy every 10 tiers, 5 Turnstones on odd tiers, a Scroll of Mercy on the rest.</summary>
        public static TrailReward Free(int tier)
        {
            if (tier % 10 == 0) return new TrailReward { KhansAlloys = 1 };
            return tier % 2 == 1 ? new TrailReward { Turnstones = 5 } : new TrailReward { ScrollsOfMercy = 1 };
        }

        /// <summary>
        /// Paid track: the costume at tier 1, the mount at tier 50, 2 Khan's Alloys every 5 tiers, an Anvil Ward at 15, 30
        /// and 45, and 8 or 7 Turnstones on the other tiers (300 in all).
        /// </summary>
        public static TrailReward Paid(int tier, TrailSeason season)
        {
            var r = new TrailReward();
            if (tier % 5 == 0)
            {
                r.KhansAlloys = 2;
                if (tier % 15 == 0) r.AnvilWards = 1;
            }
            else r.Turnstones = tier % 2 == 1 ? 8 : 7;
            if (tier == 1) r.Piece = season.Costume;
            if (tier == Tiers) r.Piece = season.Mount;
            return r;
        }

        public static TrailReward Reward(int tier, bool paid, TrailSeason season) => paid ? Paid(tier, season) : Free(tier);

        /// <summary>Days a season piece claimed now is held: to the season's end, at least MinPieceDays.</summary>
        public static int PieceDays(TrailSeason season, DateTime local) =>
            Math.Max(MinPieceDays, (int)Math.Ceiling((season.EndLocal - local).TotalDays));

        /// <summary>Amber to go from one pass to another (the step from the Trail to Plus costs the difference); -1 if not sold.</summary>
        public static int Price(TrailPass from, TrailPass to)
        {
            if (to <= from) return -1;
            int paid = from == TrailPass.Trail ? TrailAmber : 0;
            return (to == TrailPass.Plus ? PlusAmber : TrailAmber) - paid;
        }

        public static int SecondsLeft(DateTime local) => Math.Max(0, (int)Math.Ceiling((Season(local).EndLocal - local).TotalSeconds));
    }

    /// <summary>One character's Trail for one season. Stored as one short text column: "season|xp|pass|free|paid".</summary>
    public sealed class TrailProgress
    {
        public int Season { get; private set; }
        public long Xp { get; private set; }
        public TrailPass Pass { get; private set; }
        /// <summary>Claimed tiers as bits (bit t-1 for tier t).</summary>
        public long FreeClaimed { get; private set; }
        public long PaidClaimed { get; private set; }

        public int Tier => (int)Math.Min(CampaignTrail.Tiers, Xp / CampaignTrail.XpPerTier);
        /// <summary>XP into the next tier (0 at tier 50).</summary>
        public int XpIntoTier => Tier >= CampaignTrail.Tiers ? 0 : (int)(Xp % CampaignTrail.XpPerTier);

        private static long Bit(int tier) => 1L << (tier - 1);

        public bool Claimed(int tier, bool paid) => ((paid ? PaidClaimed : FreeClaimed) & Bit(tier)) != 0;

        public bool Ready(int tier, bool paid) =>
            tier >= 1 && tier <= Tier && !Claimed(tier, paid) && (!paid || Pass != TrailPass.None);

        public void AddXp(long xp)
        {
            if (xp > 0) Xp += xp;
        }

        /// <summary>Buys the paid track (or Plus, which also climbs PlusTiers tiers).</summary>
        public void Buy(TrailPass pass)
        {
            if (CampaignTrail.Price(Pass, pass) < 0) throw new InvalidOperationException("Already bought.");
            if (pass == TrailPass.Plus) Xp += CampaignTrail.PlusTiers * (long)CampaignTrail.XpPerTier;
            Pass = pass;
        }

        /// <summary>Marks a reward claimed; false when it is not ready.</summary>
        public bool Claim(int tier, bool paid)
        {
            if (!Ready(tier, paid)) return false;
            if (paid) PaidClaimed |= Bit(tier);
            else FreeClaimed |= Bit(tier);
            return true;
        }

        /// <summary>Claims every reward ready (tier 0) or one tier's (both tracks); returns what was claimed.</summary>
        public List<(int Tier, bool Paid)> ClaimReady(int onlyTier = 0)
        {
            var claimed = new List<(int, bool)>();
            for (int t = 1; t <= CampaignTrail.Tiers; t++)
            {
                if (onlyTier != 0 && t != onlyTier) continue;
                if (Claim(t, false)) claimed.Add((t, false));
                if (Claim(t, true)) claimed.Add((t, true));
            }
            return claimed;
        }

        /// <summary>
        /// Moves to <paramref name="season"/>. A new season starts over (no XP, no pass); the rewards the old season had
        /// ready and unclaimed are returned (claimed) so the server can hand them over rather than let them lapse.
        /// </summary>
        public List<(int Tier, bool Paid)> Roll(int season, out int oldSeason)
        {
            oldSeason = Season;
            if (season == Season) return new List<(int, bool)>();
            List<(int, bool)> owed = ClaimReady();
            Season = season;
            Xp = 0;
            Pass = TrailPass.None;
            FreeClaimed = 0;
            PaidClaimed = 0;
            return owed;
        }

        public string Serialize()
        {
            var sb = new StringBuilder();
            sb.Append(Season.ToString(CultureInfo.InvariantCulture)).Append('|');
            sb.Append(Xp.ToString(CultureInfo.InvariantCulture)).Append('|');
            sb.Append(((int)Pass).ToString(CultureInfo.InvariantCulture)).Append('|');
            sb.Append(FreeClaimed.ToString(CultureInfo.InvariantCulture)).Append('|');
            sb.Append(PaidClaimed.ToString(CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        public static TrailProgress Parse(string? text)
        {
            var p = new TrailProgress();
            if (string.IsNullOrEmpty(text)) return p;
            string[] parts = text!.Split('|');
            if (parts.Length != 5) return p;
            p.Season = ParseInt(parts[0]);
            p.Xp = ParseLong(parts[1]);
            int pass = ParseInt(parts[2]);
            p.Pass = pass >= 0 && pass <= (int)TrailPass.Plus ? (TrailPass)pass : TrailPass.None;
            p.FreeClaimed = ParseLong(parts[3]);
            p.PaidClaimed = ParseLong(parts[4]);
            return p;
        }

        private static int ParseInt(string s) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;
        private static long ParseLong(string s) => long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long v) ? v : 0;
    }
}
