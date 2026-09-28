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

// --- New systems (28 Sep 2026): the Bannerkin, fish meals, tempering, the sixth etching, daily pay -----------------------
Console.WriteLine();
const int LaneRuns = 10;
Console.WriteLine("BANNERKIN: a Vanguard at his level's stage (Rare gear of the stage, weapon and armour +7), 10 minutes each");
Console.WriteLine($"{"Level",-7}{"Kin",-10}{"Score",7}{"Focus",8}{"Heal",6}{"Lane mobs",11}{"Korstones",11}{"Deaths",8}{"Estimate mobs",15}{"Estimate stones",17}");
// Stages 25, 55, 85: Korstone stages (a map's tenth stage ends with its boss).
foreach (int level in new[] { 25, 55, 85 })
{
    StageConfig stage = Content.Stage(level);
    var gear = new List<ItemState>();
    foreach (EquipSlot slot in (EquipSlot[])Enum.GetValues(typeof(EquipSlot)))
    {
        var piece = new ItemState(Math.Max(1, stage.GearItemLevel), Rarity.Rare, slot);
        if (slot == EquipSlot.Weapon || slot == EquipSlot.Armor) piece.UpgradeLevel = 7;
        gear.Add(piece);
    }
    var forgedKin = new List<ItemState>();
    foreach (EquipSlot slot in Bannerkin.Slots)
    {
        ItemState piece = Bannerkin.NewPiece(Math.Max(1, stage.GearItemLevel), Rarity.Legendary, slot);
        piece.UpgradeLevel = 9;
        forgedKin.Add(piece);
    }
    foreach ((string name, List<ItemState>? kin) in new (string, List<ItemState>?)[] { ("none", null), ("starter", Bannerkin.StarterSet(level)), ("+9 Leg.", forgedKin) })
    {
        HeroStats hero = HeroFactory.FromEquipment(gear, level, HeroClass.Vanguard, kin: kin);
        long mobs = 0, deaths = 0, stones = 0;
        for (int run = 0; run < LaneRuns; run++)
        {
            var lane = new LaneSim(stage, hero, SkillDef.For(HeroClass.Vanguard), new Inventory { Potions = 50 }, new XorShiftRandom(seed + (ulong)(level * 10 + run)));
            for (int s = 0; s < lane.AutoCast.Length; s++) lane.AutoCast[s] = true;
            for (int t = 0; t < 10 * 60 * LaneSim.TicksPerSecond; t++) { lane.Tick(); lane.DrainEvents(); }
            mobs += lane.MobsKilled;
            deaths += lane.Deaths;
            stones += lane.KorstonesDestroyed;
        }
        HuntSettlement settled = HuntYield.Settle(stage, hero, 600, 600, RandomExtensions.FullBp, new Inventory(), new XorShiftRandom(seed));
        long packs = settled.Packs;
        long estimate = packs * (stage.PackSizeMin + stage.PackSizeMax) / 2;
        KinStats? k = hero.Kin;
        Console.WriteLine($"{level,-7}{name,-10}{k?.Score ?? 0,7}{(k?.FocusBp ?? 0) / 100,7}%{k?.HealPercent ?? 0,5}%{mobs / LaneRuns,11}{stones / (double)LaneRuns,11:F1}{deaths / (double)LaneRuns,8:F1}{estimate,15}{settled.Korstones,17}");
    }
}

Console.WriteLine();
Console.WriteLine("FISH MEALS: every fish eaten at once, and how long an hour at the river keeps them all running");
{
    int xp = 0, sorn = 0;
    foreach (FishDef f in Fishing.Fish) { xp += f.XpPercent; sorn += f.SornPercent; }
    Console.WriteLine($"All five running: +{xp}% XP, +{sorn}% sorn (a double sorn weekend is +{WorldEvents.SornBonusPercent}%)");
    // The Tireless Rod lands a catch every AutoSeconds; each fish's share of catches is its weight among the catch.
    double catches = 3600.0 / Fishing.AutoSeconds;
    int totalWeight = 0;
    foreach (FishDef f in Fishing.Fish) totalWeight += f.Weight;
    double hours = double.MaxValue;
    foreach (FishDef f in Fishing.Fish)
    {
        double caught = catches * (10000 - Fishing.MusselBp) / 10000.0 * f.Weight / totalWeight;
        double mealHours = caught * f.Minutes / 60.0;
        hours = Math.Min(hours, mealHours);
        Console.WriteLine($"  {f.Name,-16} +{f.XpPercent,2}% XP +{f.SornPercent,2}% sorn  {f.Minutes,3} min  {caught,6:F1} an hour of rod  = {mealHours,5:F1} h of meal");
    }
    Console.WriteLine($"An hour of the Tireless Rod keeps all five running for {hours:F1} hours of hunting");
}

Console.WriteLine();
Console.WriteLine("TEMPER: attempts and sorn from +9 to Temper 10 (60% a step, a failure drops one step)");
foreach (int itemLevel in new[] { 60, 100 })
{
    var temperRng = new XorShiftRandom(seed + (ulong)itemLevel);
    long attempts = 0, sornSpent = 0;
    int temperRuns = Math.Min(runs, 20_000);
    for (int r = 0; r < temperRuns; r++)
    {
        int step = 0;
        while (step < Tempering.MaxSteps)
        {
            attempts++;
            sornSpent += Tempering.Cost(itemLevel, step);
            if (temperRng.RollBp(Tempering.ChanceBp)) step++; else step = Math.Max(0, step - 1);
        }
    }
    Console.WriteLine($"Item level {itemLevel,3}: {attempts / (double)temperRuns,6:F1} attempts, {sornSpent / temperRuns,14:N0} sorn for +{Tempering.MaxSteps}% base stats");
}
Console.WriteLine($"SIXTH ETCHING: {10000 / EtchingRules.AddChance(5)} Grandmaster's Needles on average ({Array.Find(Pits.Shop, i => i.Id == 10)!.Laurels * 10000 / EtchingRules.AddChance(5):N0} Laurels)");

Console.WriteLine();
Console.WriteLine("DAILY PAY against the hunt: minutes of live hunting each is worth, by stage");
Console.WriteLine($"{"Stage",-7}{"Sorn/hour hunting",18}{"4 errands",11}{"Login gift",12}{"Contest #1",12}");
foreach (int level in new[] { 10, 30, 60, 90 })
{
    StageConfig stage = Content.Stage(level);
    HeroStats hero = HeroFactory.FromEquipment(new[] { new ItemState(Math.Max(1, stage.GearItemLevel), Rarity.Rare, EquipSlot.Weapon) { UpgradeLevel = 7 } }, level);
    var inv = new Inventory();
    HuntYield.Settle(stage, hero, 3600, 3600, RandomExtensions.FullBp, inv, new XorShiftRandom(seed));
    double perMinute = Math.Max(1, inv.Sorn) / 60.0;
    long errands = Errands.Sorn(level) * Errands.Givers, login = DailyLogin.Reward(1, level).Sorn, contest = Fishing.ContestPrize(1, level).Sorn;
    Console.WriteLine($"{level,-7}{inv.Sorn,18:N0}{errands / perMinute,9:F1} m{login / perMinute,10:F1} m{contest / perMinute,10:F1} m");
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
