#nullable enable
using System;

namespace Orsuun.Rules
{
    /// <summary>The three pearls of the river mussels (world bible: White, Blue, Blood Pearl became Moon, Tide, Heart).</summary>
    public enum Pearl
    {
        Moon,
        Tide,
        Heart,
    }

    /// <summary>A fish: its share of the fish caught, and what eating it adds to hunting for a while.</summary>
    public sealed class FishDef
    {
        public FishDef(int id, string name, string icon, int weight, int xpPercent, int sornPercent, int minutes, int fight)
        {
            Fight = fight;
            Id = id;
            Name = name;
            Icon = icon;
            Weight = weight;
            XpPercent = xpPercent;
            SornPercent = sornPercent;
            Minutes = minutes;
        }

        public int Id { get; }
        public string Name { get; }
        public string Icon { get; }
        public int Weight { get; }
        public int XpPercent { get; }
        public int SornPercent { get; }
        public int Minutes { get; }
        /// <summary>How hard it fights on the line (0 calm .. 3 wild): faster darts and a quicker drain in the catch.</summary>
        public int Fight { get; }

        public string BoostText =>
            (XpPercent > 0 && SornPercent > 0 ? $"+{XpPercent}% hunting XP and sorn"
                : XpPercent > 0 ? $"+{XpPercent}% hunting XP" : $"+{SornPercent}% hunting sorn") + $" for {Minutes} minutes";
    }

    public enum CatchKind
    {
        Escaped,
        Fish,
        Mussel,
    }

    /// <summary>
    /// Fishing (owner, 28 Sep 2026: "we need a fishing map just like our game but we need to see the char from behind ...
    /// there is no afk farm there. the auto fishing is buyable with real money. make it a very very mini game that is basic.
    /// different fishes, that gives boost to player when it gets eaten. mussel and pearl is good as well"). The hero goes to
    /// Old Nergui's river from ZONES; the hunt stops there. CAST, wait for the float to go under, REEL in time: a fish or a
    /// river mussel, else it gets away; a fish then has to be landed (keep it in the catch box until the bar fills: the
    /// rarer, the wilder). A fish eaten adds hunting XP or sorn for a while, each fish on its own clock beside
    /// the others ("the different fishes effects can stay on at the same time"). Nergui opens mussels (GDD: a Moon Pearl 6% of the time, a Tide Pearl 2%, a Heart Pearl 0.5%); a pearl
    /// pays the materials of the +7, +8 or +9 attempt (GDD's table). The Tireless Rod (Amber, held for days) fishes by
    /// itself while the hero stays at the river, one catch every 30 seconds (the GDD's pace), online or away. Assumptions
    /// (not stated by the owner): the fish, their boosts and shares, the mussel share, the bite timing, the rod's prices,
    /// the level the river opens at (Unlocks) and pearls and fish being tradeable on the Exchange.
    /// </summary>
    public static class Fishing
    {
        /// <summary>Stored by id on heroes, listings and letters: append, never renumber. The boosts were halved or so by
        /// the balance pass (28 Sep 2026): all five running were +55% XP and sorn, and an hour of the Tireless Rod kept
        /// them all going for six hours of hunting; now all five make +25% of each (Orsuun.Sim, FISH MEALS).</summary>
        public static readonly FishDef[] Fish =
        {
            new FishDef(0, "Steppe Carp", "FishCarp", 40, 5, 0, 30, fight: 0),
            new FishDef(1, "Silver Grayling", "FishGrayling", 30, 0, 5, 30, fight: 1),
            new FishDef(2, "River Pike", "FishPike", 12, 8, 0, 30, fight: 2),
            new FishDef(3, "Spotted Lenok", "FishLenok", 12, 0, 8, 30, fight: 1),
            new FishDef(4, "Golden Taimen", "FishTaimen", 6, 12, 12, 60, fight: 3),
        };

        public static readonly string[] PearlNames = { "Moon Pearl", "Tide Pearl", "Heart Pearl" };
        public static readonly string[] PearlIcons = { "MoonPearl", "TidePearl", "HeartPearl" };

        /// <summary>A catch's chance of being a mussel rather than a fish, in basis points.</summary>
        public const int MusselBp = 2000;

        /// <summary>A mussel's chance of each pearl (Moon, Tide, Heart), in basis points (GDD: 6%, 2%, 0.5%).</summary>
        public static readonly int[] PearlBp = { 600, 200, 50 };

        /// <summary>The float goes under this long after the cast (the server draws it), and stays under this long.</summary>
        public const int BiteMinMs = 1800, BiteMaxMs = 6000, WindowMs = 1300;

        /// <summary>Slack for the phone's round trip: a reel counts from a little before the bite to this long after the window.</summary>
        public const int EarlyMs = 300, LateMs = 1500;

        /// <summary>
        /// The catch (owner, 28 Sep 2026: "the fish goes up or down randomly, you need to keep the fish inside bar for a
        /// while to catch the fish"): a hooked fish is landed by keeping it in the catch box until the progress bar fills.
        /// The client plays it; the server takes a landing only this long after the hook at the soonest (a full bar from
        /// its start cannot fill faster) and at the latest.
        /// </summary>
        public const int LandMinMs = 2000, LandMaxMs = 90000;

        /// <summary>The Tireless Rod: one catch every 30 seconds (GDD), and at most this long counted between two visits.</summary>
        public const int AutoSeconds = 30;
        public const int AutoCapHours = 12;

        /// <summary>The Tireless Rod's days and their Amber (the Caravan).</summary>
        public static readonly int[] RodDays = { 1, 3, 7, 14 };
        public static readonly int[] RodAmber = { 50, 120, 250, 420 };

        /// <summary>A fish's boost runs on its own clock, beside the others (owner, 28 Sep 2026: "the different fishes effects
        /// can stay on at the same time"); eating one that is running adds its time, up to this long ahead.</summary>
        public const int MealMaxMinutes = 180;

        /// <summary>When a fish eaten at <paramref name="now"/> runs until, given when its boost ran until before.</summary>
        public static DateTime MealUntil(FishDef fish, DateTime now, DateTime? before)
        {
            DateTime start = before != null && before.Value > now ? before.Value : now;
            DateTime until = start.AddMinutes(fish.Minutes);
            DateTime cap = now.AddMinutes(MealMaxMinutes);
            return until > cap ? cap : until;
        }

        /// <summary>
        /// The hunting boost of every fish eaten over an interval, in basis points of the hunt's gain (+10% = 1000): each
        /// fish's percent for the share of the interval its clock covered. <paramref name="until"/> is per fish id.
        /// </summary>
        public static (long xpBp, long sornBp) MealBonusBp(DateTime from, DateTime to, DateTime?[] until)
        {
            long xp = 0, sorn = 0;
            for (int i = 0; i < Fish.Length && i < until.Length; i++)
            {
                int share = MealShareBp(from, to, until[i]);
                if (share <= 0) continue;
                xp += (long)Fish[i].XpPercent * share / 100;
                sorn += (long)Fish[i].SornPercent * share / 100;
            }
            return (xp, sorn);
        }

        /// <summary>Mussels opened in one go at most (OPEN ALL).</summary>
        public const int OpenMax = 200;

        public static FishDef? FishById(int id) => id >= 0 && id < Fish.Length ? Fish[id] : null;

        /// <summary>A reel in time: a mussel, else a fish by the fish's shares.</summary>
        public static (CatchKind kind, int fish) Land(IRandom rng)
        {
            if (rng.RollBp(MusselBp)) return (CatchKind.Mussel, -1);
            int total = 0;
            foreach (FishDef f in Fish) total += f.Weight;
            int roll = rng.NextInt(total);
            foreach (FishDef f in Fish)
            {
                if (roll < f.Weight) return (CatchKind.Fish, f.Id);
                roll -= f.Weight;
            }
            return (CatchKind.Fish, 0);
        }

        /// <summary>Whether a reel <paramref name="elapsedMs"/> after the cast was answered lands the bite at <paramref name="biteMs"/>.</summary>
        public static bool InTime(long elapsedMs, int biteMs) => elapsedMs >= biteMs - EarlyMs && elapsedMs <= biteMs + WindowMs + LateMs;

        /// <summary>Whole Tireless Rod catches between two moments, at most twelve hours'.</summary>
        public static int AutoCatches(DateTime from, DateTime to)
        {
            if (to <= from) return 0;
            long catches = (to - from).Ticks / TimeSpan.TicksPerSecond / AutoSeconds;
            return (int)Math.Min(AutoCap, catches);
        }

        public static int AutoCap => AutoCapHours * 3600 / AutoSeconds;

        /// <summary>Opens one mussel: the pearl inside, or null for an empty shell.</summary>
        public static Pearl? Open(IRandom rng)
        {
            int roll = rng.NextInt(RandomExtensions.FullBp);
            if (roll < PearlBp[(int)Pearl.Heart]) return Pearl.Heart;
            roll -= PearlBp[(int)Pearl.Heart];
            if (roll < PearlBp[(int)Pearl.Tide]) return Pearl.Tide;
            roll -= PearlBp[(int)Pearl.Tide];
            if (roll < PearlBp[(int)Pearl.Moon]) return Pearl.Moon;
            return null;
        }

        /// <summary>The pearl that pays the materials of the attempt to <paramref name="targetLevel"/> (GDD: +7 a Moon Pearl,
        /// +8 a Tide Pearl, +9 a Heart Pearl), or null below +7.</summary>
        public static Pearl? PearlFor(int targetLevel) => targetLevel switch
        {
            7 => Pearl.Moon,
            8 => Pearl.Tide,
            9 => Pearl.Heart,
            _ => (Pearl?)null,
        };

        /// <summary>The share of an interval, in basis points, that a meal lasting until <paramref name="until"/> covered
        /// (hunting pay is settled by interval, and a meal may run out inside one).</summary>
        public static int MealShareBp(DateTime from, DateTime to, DateTime? until)
        {
            if (until == null) return 0;
            if (to <= from) return until.Value > to ? RandomExtensions.FullBp : 0;
            long covered = Math.Min(until.Value.Ticks, to.Ticks) - from.Ticks;
            if (covered <= 0) return 0;
            return (int)Math.Min(RandomExtensions.FullBp, covered * RandomExtensions.FullBp / (to.Ticks - from.Ticks));
        }

        // ---- Fishing contests (owner, 28 Sep 2026: picked "Fishing contests": "A weekend event: the biggest catches on a
        // leaderboard, prizes by letter, and a river title for the best angler of the week"). A fish landed by hand is
        // weighed; during the contest (WorldEventKind.FishingContest) each hero's heaviest stands on its board; when it
        // ends the first ContestPaid are paid by letter and the heaviest wears AnglerTitle for AnglerDays. The Tireless
        // Rod's catches are not weighed (the contest is the hand's). Assumptions (not stated by the owner): the weights,
        // the prizes, ten paid, a week's title.

        /// <summary>Each fish's weight in grams, lightest to heaviest (by fish id).</summary>
        public static readonly (int Min, int Max)[] Grams = { (800, 4500), (300, 1800), (1500, 9000), (1000, 6000), (5000, 30000) };

        public const int ContestPaid = 10, AnglerDays = 7;
        public const string AnglerTitle = "Angler of the Week";

        /// <summary>A landed fish's weight: big ones are rare (the square of a draw spreads the range).</summary>
        public static int Weigh(FishDef fish, IRandom rng)
        {
            (int min, int max) = Grams[fish.Id];
            long u = rng.NextInt(10000);
            return min + (int)((max - min) * u * u / 100_000_000L);
        }

        /// <summary>A weight as anglers say it: "3.2 kg".</summary>
        public static string Kilos(int grams) => (grams / 1000.0).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " kg";

        /// <summary>A contest's prize by rank (1..ContestPaid): sorn by the furthest stage cleared, and pearls.</summary>
        public static (long Sorn, int PearlGood, int Pearls) ContestPrize(int rank, int highestStageCleared)
        {
            long mob = Content.Stage(Math.Max(1, Math.Min(Content.TotalStages, highestStageCleared))).SornPerMob;
            return rank switch
            {
                1 => (mob * 300, TradeGoods.FirstPearl + (int)Pearl.Heart, 1),
                2 => (mob * 200, TradeGoods.FirstPearl + (int)Pearl.Tide, 2),
                3 => (mob * 150, TradeGoods.FirstPearl + (int)Pearl.Moon, 3),
                _ => (mob * 60, TradeGoods.FirstPearl + (int)Pearl.Moon, 1),
            };
        }
    }
}
