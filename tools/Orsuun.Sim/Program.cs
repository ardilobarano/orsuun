// Balance simulator: prints the numbers the GDD quotes, measured by Monte Carlo on the real rules code.
// Run it after every rate change:  dotnet run --project tools/Orsuun.Sim -c Release [runs] [seed]

using System.Globalization;
using Orsuun.Rules;
using Orsuun.Rules.Combat;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

int runs = args.Length > 0 ? int.Parse(args[0]) : 200_000;
ulong seed = args.Length > 1 ? ulong.Parse(args[1]) : 20260922UL;
var rng = new XorShiftRandom(seed);

Console.WriteLine($"Orsuun balance simulator   runs={runs:N0}   seed={seed}");
Console.WriteLine();

// --- Forge: consumables from +0 to +9 -------------------------------------------------------------
Console.WriteLine("FORGE: attempts from +0 to +9 (one consumable per attempt)");
Console.WriteLine($"{"Method",-16}{"Analytic",10}{"Simulated",11}{"With Patience",15}{"Median",8}{"P90",7}{"Avg sorn (lvl 60)",20}");
foreach (ForgeMethod method in new[] { ForgeMethod.ScrollOfMercy, ForgeMethod.KhansAlloy })
{
    ClimbStats plain = Climb(method, patience: false);
    ClimbStats patient = Climb(method, patience: true);
    Console.WriteLine($"{method,-16}{ForgeAnalysis.ExpectedAttemptsTotal(method),10:F2}{plain.Mean,11:F2}{patient.Mean,15:F2}{patient.Median,8}{patient.P90,7}{patient.MeanSorn,20:N0}");
}
Console.WriteLine();

// --- Forge: the raw gamble ------------------------------------------------------------------------
Console.WriteLine("FORGE: raw gamble, Forge alone, stop at +9 or Oathbreak");
Console.WriteLine($"{"Start",-8}{"Analytic",10}{"Simulated",11}{"With Patience",15}");
foreach (int start in new[] { 3, 6 })
    Console.WriteLine($"+{start,-7}{ForgeAnalysis.StraightRunChance(start, 9),10:P2}{RawGamble(start, false),11:P2}{RawGamble(start, true),15:P2}");
Console.WriteLine();

// --- Turnstones -----------------------------------------------------------------------------------
Console.WriteLine("TURNSTONES: chance per turn on a 5-etching Rare weapon");
var goals = new (string Name, double Gdd, Func<ItemState, bool> Hit)[]
{
    ("Oathsworn, any value", 0.3125, i => Tier(i, WeaponEtchingIds.StrongAgainstOathsworn) >= 1),
    ("Oathsworn at T5", 0.0125, i => Tier(i, WeaponEtchingIds.StrongAgainstOathsworn) == 5),
    ("Oathsworn + critical, both T3+", 0.0075, i => Tier(i, WeaponEtchingIds.StrongAgainstOathsworn) >= 3 && Tier(i, WeaponEtchingIds.Critical) >= 3),
    ("Oathsworn + critical, both T4+", 0.001408, i => Tier(i, WeaponEtchingIds.StrongAgainstOathsworn) >= 4 && Tier(i, WeaponEtchingIds.Critical) >= 4),
    ("Oathsworn + crit + piercing, all T3+", 0.000482, i => Tier(i, WeaponEtchingIds.StrongAgainstOathsworn) >= 3 && Tier(i, WeaponEtchingIds.Critical) >= 3 && Tier(i, WeaponEtchingIds.Piercing) >= 3),
};

var etchings = new EtchingService();
EtchingPool pool = EtchingPool.Weapon();
var weapon = new ItemState(60, Rarity.Rare);
while (weapon.Etchings.Count < ItemState.MaxEtchings)
    etchings.TryAdd(weapon, pool, weapon.Etchings.Count == 4 ? NeedleKind.MastersNeedle : NeedleKind.EtchingNeedle, rng);

int turns = runs * 10;
var hits = new long[goals.Length];
for (int t = 0; t < turns; t++)
{
    etchings.Turn(weapon, pool, rng);
    for (int g = 0; g < goals.Length; g++)
        if (goals[g].Hit(weapon)) hits[g]++;
}

Console.WriteLine($"{"Goal",-40}{"GDD",10}{"Simulated",11}{"Turnstones",12}");
for (int g = 0; g < goals.Length; g++)
{
    double p = hits[g] / (double)turns;
    string expected = p > 0 ? (1 / p).ToString("N0") : "n/a";
    Console.WriteLine($"{goals[g].Name,-40}{goals[g].Gdd,10:P3}{p,11:P3}{expected,12}");
}

Console.WriteLine();

// --- Lane pacing ----------------------------------------------------------------------------------
Console.WriteLine("LANE: 10 minutes of auto-cast farming on the grey-box stage");
Console.WriteLine($"{"Weapon",-8}{"Attack",8}{"Mobs",8}{"Korstones",11}{"Deaths",8}{"Draughts used",15}{"Sorn earned",14}");
foreach (int level in new[] { 0, 3, 6, 9 })
{
    var session = new PlayerSession(new XorShiftRandom(seed + (ulong)level));
    session.Weapon.UpgradeLevel = level;
    session.Lane.SetHero(HeroFactory.FromWeapon(session.Weapon));
    for (int s = 0; s < session.Lane.AutoCast.Length; s++) session.Lane.AutoCast[s] = true;

    long sornStart = session.Inventory.Sorn;
    int draughtsUsed = 0;
    for (int t = 0; t < 10 * 60 * LaneSim.TicksPerSecond; t++)
    {
        session.Lane.Tick();
        foreach (LaneEvent e in session.Lane.DrainEvents())
            if (e.Kind == LaneEventKind.HeroHealed) draughtsUsed++;
    }

    Console.WriteLine($"+{level,-7}{HeroFactory.FromWeapon(session.Weapon).Attack,8}{session.Lane.MobsKilled,8}{session.Lane.KorstonesDestroyed,11}{session.Lane.Deaths,8}{draughtsUsed,15}{session.Inventory.Sorn - sornStart,14:N0}");
}

// --------------------------------------------------------------------------------------------------

ClimbStats Climb(ForgeMethod method, bool patience)
{
    var forge = new ForgeService(patience);
    var counts = new int[runs];
    double sorn = 0;
    for (int r = 0; r < runs; r++)
    {
        var item = new ItemState(60, Rarity.Rare);
        while (item.UpgradeLevel < ItemState.MaxUpgradeLevel)
        {
            sorn += ForgeRules.Cost(item.ItemLevel, item.UpgradeLevel);
            forge.Attempt(item, method, rng);
            counts[r]++;
        }
    }
    Array.Sort(counts);
    return new ClimbStats(counts.Average(), counts[runs / 2], counts[runs * 9 / 10], sorn / runs);
}

double RawGamble(int start, bool patience)
{
    var forge = new ForgeService(patience);
    int reached = 0;
    for (int r = 0; r < runs; r++)
    {
        var item = new ItemState(60, Rarity.Rare) { UpgradeLevel = start };
        while (!item.Destroyed && item.UpgradeLevel < ItemState.MaxUpgradeLevel)
            forge.Attempt(item, ForgeMethod.ForgeAlone, rng);
        if (!item.Destroyed) reached++;
    }
    return reached / (double)runs;
}

static int Tier(ItemState item, int entryId)
{
    foreach (Etching e in item.Etchings)
        if (e.EntryId == entryId) return e.Tier;
    return 0;
}

readonly record struct ClimbStats(double Mean, int Median, int P90, double MeanSorn);
