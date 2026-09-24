#nullable enable
using System;
using Orsuun.Rules.Combat;

namespace Orsuun.Rules
{
    /// <summary>The three Banners a player swears to (world bible section 3). None until the oath.</summary>
    public enum Banner
    {
        None = 0,
        Ember = 1,
        Sky = 2,
        Gold = 3,
    }

    public sealed class BannerDef
    {
        public BannerDef(Banner id, string name, string colorHex, string creed, string culture, string capital, string startingTown)
        {
            Id = id;
            Name = name;
            ColorHex = colorHex;
            Creed = creed;
            Culture = culture;
            Capital = capital;
            StartingTown = startingTown;
        }

        public Banner Id { get; }
        public string Name { get; }
        /// <summary>The Banner's colour, "#RRGGBB".</summary>
        public string ColorHex { get; }
        public string Creed { get; }
        public string Culture { get; }
        public string Capital { get; }
        public string StartingTown { get; }
    }

    /// <summary>
    /// Owner, 24 Sep 2026: three Banners, red, blue and yellow. The world bible's creeds stay; the colours moved:
    /// Ember keeps crimson and "break every stone"; Sky (blue) takes the monasteries' "reseal what was sealed";
    /// Gold (yellow) takes the salt-road merchants' "every stone has a price". The War of Banners is a point race
    /// per season (a week in the playtest); the last season's winner hunts with a bonus, and each fortress a Banner
    /// holds adds a little more.
    /// </summary>
    public static class Banners
    {
        public static readonly BannerDef[] All =
        {
            new BannerDef(Banner.Ember, "Ember Banner", "#C0281E", "Break every stone.",
                "Rider clans of the open steppe: proud, blunt, settled by honour duels.", "Karsun", "Emberhearth"),
            new BannerDef(Banner.Sky, "Sky Banner", "#2F6FD0", "Reseal what was sealed.",
                "Mountain monasteries under the eternal sky: archivists, drum shamans and grave wardens.", "Ostrakh", "Wardenstep"),
            new BannerDef(Banner.Gold, "Gold Banner", "#E0A81C", "Every stone has a price.",
                "Caravan cities of the salt roads: contracts, guild banks and hired blades.", "Velimar", "Scalehouse"),
        };

        public static BannerDef Def(Banner banner)
        {
            foreach (BannerDef d in All)
                if (d.Id == banner) return d;
            throw new ArgumentOutOfRangeException(nameof(banner), "No such Banner.");
        }

        /// <summary>Sorn and XP bonus for everyone under the Banner that won the last season.</summary>
        public const int WinnerBonusPercent = 5;
        /// <summary>Sorn bonus per fortress the Banner holds.</summary>
        public const int FortressBonusPercent = 3;

        // War of Banners points.
        public const int PointsPerKorstone = 1;
        public const long CommanderDamagePerPoint = 2_000;
        public const int PointsCommanderSlain = 50;
        public const int PointsPushCleared = 5;
        public const long SiegeDamagePerPoint = 2_000;
        public const int PointsSiegePhase = 30;
        public const int PointsFortressTaken = 200;

        /// <summary>A season of the War of Banners: the bounty week in the playtest.</summary>
        public static string SeasonKey(DateTime local) => Bounties.WeekKey(local);

        /// <summary>The Banner with the most points, or None on a tie for first or an empty season.</summary>
        public static Banner Leader(long ember, long sky, long gold)
        {
            long best = Math.Max(ember, Math.Max(sky, gold));
            if (best <= 0) return Banner.None;
            int atBest = (ember == best ? 1 : 0) + (sky == best ? 1 : 0) + (gold == best ? 1 : 0);
            if (atBest > 1) return Banner.None;
            return ember == best ? Banner.Ember : sky == best ? Banner.Sky : Banner.Gold;
        }

        private static readonly string[] Epithets =
        {
            "Swift", "Iron", "Grey", "Red", "Silent", "Bold", "Old", "Wild", "Stone", "Ash", "Storm", "Salt", "Bright", "Dusk", "Frost", "Amber",
        };

        private static readonly string[] Beasts =
        {
            "Falcon", "Wolf", "Stag", "Horse", "Raven", "Bear", "Eagle", "Boar", "Lynx", "Hawk", "Yak", "Fox", "Owl", "Ram", "Crane", "Viper",
        };

        /// <summary>
        /// A steppe name for an account, fixed by its id ("Swift Falcon 4821"). Players are shown to each other by it
        /// (boss ranks, sieges) until they choose their own; generated names cannot offend.
        /// </summary>
        public static string GeneratedName(Guid accountId)
        {
            byte[] b = accountId.ToByteArray();
            int number = ((b[2] << 8) | b[3]) % 9000 + 1000;
            return Epithets[b[0] % Epithets.Length] + " " + Beasts[b[1] % Beasts.Length] + " " + number;
        }
    }

    public enum SiegePhase
    {
        Gate = 0,
        Yard = 1,
        Hall = 2,
    }

    public sealed class FortressDef
    {
        public FortressDef(int id, string name, Banner firstHolder, string region)
        {
            Id = id;
            Name = name;
            FirstHolder = firstHolder;
            Region = region;
        }

        public int Id { get; }
        public string Name { get; }
        public Banner FirstHolder { get; }
        public string Region { get; }
    }

    /// <summary>
    /// Fortress sieges (GDD: Stagfort, Saltgate and Ravenmoot; Gate, Yard and Hall). Until guilds exist the Banners
    /// hold the fortresses. Any sworn player not of the holding Banner may attack: each attack is a scored fight
    /// against the current phase's champion and its damage comes off the phase's shared wall. Breaking the Hall hands
    /// the fortress to the attacking Banner that dealt the most damage in the siege. Players of the holding Banner
    /// may defend: a defence fight mends the wall by half its damage.
    /// </summary>
    public static class Fortresses
    {
        public static readonly FortressDef[] All =
        {
            new FortressDef(1, "Stagfort", Banner.Ember, "The Oathfields"),
            new FortressDef(2, "Saltgate", Banner.Gold, "The Salt Flats"),
            new FortressDef(3, "Ravenmoot", Banner.Sky, "The Frost Pasture"),
        };

        public static FortressDef? Find(int id)
        {
            foreach (FortressDef f in All)
                if (f.Id == id) return f;
            return null;
        }

        // Siege pay: every fight, the fight that breaks a phase, and the fight that takes the Hall.
        public const long FightSorn = 5_000;
        public const int FightMarks = 1;
        public const long BreakSorn = 10_000;
        public const int BreakMarks = 3;
        public const int CaptureMarks = 10;

        /// <summary>Minutes between two siege fights of one player.</summary>
        public const int CooldownMinutes = 10;
        /// <summary>A defence fight mends the wall by this share of its damage.</summary>
        public const int DefenceMendPercent = 50;

        public static string PhaseName(SiegePhase phase) => phase == SiegePhase.Gate ? "Gate" : phase == SiegePhase.Yard ? "Yard" : "Hall";

        /// <summary>
        /// The wall of a phase, sized by how many players fought on the server lately (at least one), so a small
        /// server can still take a fortress and a big one needs the whole Banner.
        /// </summary>
        public static long PhaseHp(SiegePhase phase, int activePlayers)
        {
            long per = phase == SiegePhase.Gate ? 60_000 : phase == SiegePhase.Yard ? 90_000 : 120_000;
            return per * Math.Max(1, Math.Min(200, activePlayers));
        }

        /// <summary>
        /// The champion fought at a phase: its HP is the ceiling of one fight's damage, like a Commander's.
        /// Ids 201.. keep clear of the Commanders; the zone gives the lane its ground.
        /// </summary>
        public static BossDef Champion(FortressDef fortress, SiegePhase phase)
        {
            int id = 200 + (fortress.Id - 1) * 3 + (int)phase + 1;
            string name = phase == SiegePhase.Gate ? "Gate Warden of " + fortress.Name
                : phase == SiegePhase.Yard ? "Yard Captain of " + fortress.Name : "Lord of " + fortress.Name + " Hall";
            long hp = phase == SiegePhase.Gate ? 60_000 : phase == SiegePhase.Yard ? 70_000 : 90_000;
            long attack = phase == SiegePhase.Gate ? 50 : phase == SiegePhase.Yard ? 70 : 95;
            // The Gate's warden fights alone, the Yard's captain calls the garrison, and the lord of the Hall hides
            // behind his captains: the last wall is the hardest.
            BossMechanic mechanic = phase == SiegePhase.Gate ? BossMechanic.None : phase == SiegePhase.Yard ? BossMechanic.PackCaller : BossMechanic.CaptainShield;
            return new BossDef(id, 121, name, 1, hp, attack, mechanic, 0, "");
        }

        /// <summary>The fortress and phase a champion id belongs to (for replays), or null.</summary>
        public static (FortressDef fortress, SiegePhase phase)? FromChampionId(int id)
        {
            int k = id - 201;
            if (k < 0 || k >= All.Length * 3) return null;
            return (All[k / 3], (SiegePhase)(k % 3));
        }
    }
}
