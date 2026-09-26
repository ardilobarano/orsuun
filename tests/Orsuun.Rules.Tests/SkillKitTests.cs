using Orsuun.Rules;
using Orsuun.Rules.Combat;
using Xunit;

namespace Orsuun.Rules.Tests;

/// <summary>
/// Five skills a class (owner, 26 Sep 2026): the world bible's fourth and fifth of each branch unlock at levels 30 and 60,
/// and each new kind does what its card says. Each check runs the same seeds with and without the cast.
/// </summary>
public class SkillKitTests
{
    private static readonly HeroClass[] Classes = { HeroClass.Vanguard, HeroClass.Kestrel, HeroClass.Wraithsworn, HeroClass.Drumcaller };

    private static HeroStats Hero(HeroClass cls, int level, int upgrade = 7)
    {
        var items = new List<ItemState>();
        for (int s = 0; s < 8; s++) items.Add(new ItemState(level, Rarity.Rare, (EquipSlot)s) { UpgradeLevel = upgrade });
        return HeroFactory.FromEquipment(items, level, cls);
    }

    /// <summary>Runs to the first fight (or the boss with boss: true), casts the skill (-1: none) and runs on; the events after it.</summary>
    private static (LaneSim Lane, List<LaneEvent> Events, bool Cast) Run(HeroClass cls, int level, int skill, int ticks, ulong seed = 3, int stage = 35, bool boss = false)
    {
        var lane = new LaneSim(Content.Stage(stage), Hero(cls, level), SkillDef.For(cls), new Inventory(), new XorShiftRandom(seed));
        while (lane.Phase != LanePhase.Fighting || (boss && !lane.IsBossEncounter))
        {
            lane.Tick();
            lane.DrainEvents();
        }
        bool cast = skill >= 0 && lane.TryCast(skill);
        var events = new List<LaneEvent>(lane.DrainEvents());
        for (int t = 0; t < ticks && lane.Phase == LanePhase.Fighting; t++)
        {
            lane.Tick();
            events.AddRange(lane.DrainEvents());
        }
        return (lane, events, cast);
    }

    private static long Dealt(IEnumerable<LaneEvent> events) => events.Where(e => e.Kind == LaneEventKind.EnemyDamaged).Sum(e => e.Amount);

    private static long Taken(IEnumerable<LaneEvent> events) => events.Where(e => e.Kind == LaneEventKind.HeroDamaged).Sum(e => e.Amount);

    /// <summary>
    /// Sum of a measure over seeds 1..8, with the skill cast and without: on a pack (stage 35), or on the Lantern Widow
    /// (boss: true), whose fight outlasts every buff so damage is not capped by dying mobs.
    /// </summary>
    private static (long With, long Without) Compare(HeroClass cls, int skill, int ticks, Func<List<LaneEvent>, long> measure, bool boss = false)
    {
        long with = 0, without = 0;
        int stage = boss ? BossStage : 35;
        for (ulong seed = 1; seed <= 8; seed++)
        {
            with += measure(Run(cls, 65, skill, ticks, seed, stage, boss).Events);
            without += measure(Run(cls, 65, -1, ticks, seed, stage, boss).Events);
        }
        return (with, without);
    }

    private const int BossStage = 60;

    [Fact]
    public void Every_class_has_five_skills_the_last_two_at_levels_30_and_60()
    {
        foreach (HeroClass cls in Classes)
        {
            SkillDef[] kit = SkillDef.For(cls);
            Assert.Equal(SkillGrades.Slots, kit.Length);
            Assert.All(kit.Take(3), k => Assert.Equal(1, k.UnlockLevel));
            Assert.Equal(30, kit[3].UnlockLevel);
            Assert.Equal(60, kit[4].UnlockLevel);
        }
        Assert.Equal(new[] { "Honed Edge", "Bull Rush" }, SkillDef.For(HeroClass.Vanguard).Skip(3).Select(k => k.Name));
        Assert.Equal(new[] { "Venom Cloud", "Shadow Stoop" }, SkillDef.For(HeroClass.Kestrel).Skip(3).Select(k => k.Name));
        Assert.Equal(new[] { "Grave Chains", "Shroud of Night" }, SkillDef.For(HeroClass.Wraithsworn).Skip(3).Select(k => k.Name));
        Assert.Equal(new[] { "Hunter's Blessing", "Mirror Ward" }, SkillDef.For(HeroClass.Drumcaller).Skip(3).Select(k => k.Name));
    }

    [Fact]
    public void The_fourth_and_fifth_skills_wait_for_their_levels()
    {
        foreach (HeroClass cls in Classes)
        {
            Assert.False(Run(cls, 29, 3, 0).Cast);
            Assert.True(Run(cls, 30, 3, 0).Cast);
            Assert.False(Run(cls, 59, 4, 0).Cast);
            Assert.True(Run(cls, 60, 4, 0).Cast);
        }
    }

    [Fact]
    public void Honed_Edge_and_Hunters_Blessing_hit_harder_for_a_while()
    {
        (long with, long without) = Compare(HeroClass.Vanguard, 3, 8 * LaneSim.TicksPerSecond, Dealt, boss: true);
        Assert.True(with > without * 11 / 10, $"Honed Edge {with} vs {without}");

        long Crits(List<LaneEvent> ev) => ev.Count(e => e.Kind == LaneEventKind.EnemyDamaged && e.Crit);
        (long crits, long plain) = Compare(HeroClass.Drumcaller, 3, 8 * LaneSim.TicksPerSecond, Crits, boss: true);
        Assert.True(crits > plain * 13 / 10, $"Hunter's Blessing crits {crits} vs {plain}");
    }

    [Fact]
    public void Bull_Rush_stuns_a_pack_but_not_a_boss()
    {
        // The pack's first blows are due within two seconds; stunned, none lands.
        (long with, long without) = Compare(HeroClass.Vanguard, 4, 2 * LaneSim.TicksPerSecond - 2, Taken);
        Assert.Equal(0, with);
        Assert.True(without > 0);

        // Nine-Winters keeps swinging.
        (LaneSim _, List<LaneEvent> events, bool cast) = Run(HeroClass.Vanguard, 65, 4, 2 * LaneSim.TicksPerSecond, stage: 40, boss: true);
        Assert.True(cast);
        Assert.True(Taken(events) > 0);
    }

    [Fact]
    public void Venom_Cloud_poisons_the_pack_and_Shadow_Stoop_hits_hard()
    {
        (long with, long without) = Compare(HeroClass.Kestrel, 3, 6 * LaneSim.TicksPerSecond, Dealt);
        Assert.True(with > without * 12 / 10, $"Venom Cloud {with} vs {without}");

        // Aimed at the Widow: five times the attack at least (a finisher's double comes below 30% health).
        (LaneSim _, List<LaneEvent> events, bool cast) = Run(HeroClass.Kestrel, 65, 4, 0, stage: BossStage, boss: true);
        Assert.True(cast);
        long attack = Hero(HeroClass.Kestrel, 65).Attack;
        Assert.Contains(events, e => e.Kind == LaneEventKind.EnemyDamaged && e.Amount >= attack * 4);
    }

    [Fact]
    public void Grave_Chains_and_the_veil_spare_the_hero()
    {
        (long chained, long free) = Compare(HeroClass.Wraithsworn, 3, 6 * LaneSim.TicksPerSecond, Taken);
        Assert.True(chained < free, $"Grave Chains {chained} vs {free}");

        (long veiled, long bare) = Compare(HeroClass.Wraithsworn, 4, 6 * LaneSim.TicksPerSecond, Taken);
        Assert.True(veiled < bare * 7 / 10, $"Shroud of Night {veiled} vs {bare}");
        Assert.Contains(Run(HeroClass.Wraithsworn, 65, 4, 6 * LaneSim.TicksPerSecond).Events, e => e.Kind == LaneEventKind.HeroDamaged && e.Text == "veiled");
    }

    [Fact]
    public void Mirror_Ward_softens_blows_and_returns_them()
    {
        (long taken, long bare) = Compare(HeroClass.Drumcaller, 4, 8 * LaneSim.TicksPerSecond, Taken, boss: true);
        Assert.True(taken < bare, $"Mirror Ward taken {taken} vs {bare}");
        (long dealt, long plain) = Compare(HeroClass.Drumcaller, 4, 8 * LaneSim.TicksPerSecond, Dealt, boss: true);
        Assert.True(dealt > plain, $"Mirror Ward dealt {dealt} vs {plain}");
    }

    [Fact]
    public void Grades_raise_the_new_skills_too()
    {
        // A Peerless Honed Edge (+60% of its power) against a Normal one, on the Widow over its 8 seconds.
        long Honed(int grade)
        {
            long dealt = 0;
            for (ulong seed = 1; seed <= 6; seed++)
            {
                var items = new List<ItemState>();
                for (int s = 0; s < 8; s++) items.Add(new ItemState(65, Rarity.Rare, (EquipSlot)s) { UpgradeLevel = 7 });
                HeroStats hero = HeroFactory.FromEquipment(items, 65, HeroClass.Vanguard, skillGrades: new[] { 0, 0, 0, grade, 0 });
                var lane = new LaneSim(Content.Stage(BossStage), hero, SkillDef.For(HeroClass.Vanguard), new Inventory(), new XorShiftRandom(seed));
                for (int guard = 0; guard < 20000 && (lane.Phase != LanePhase.Fighting || !lane.IsBossEncounter); guard++) { lane.Tick(); lane.DrainEvents(); }
                Assert.True(lane.TryCast(3));
                for (int t = 0; t < 8 * LaneSim.TicksPerSecond && lane.Phase == LanePhase.Fighting; t++)
                {
                    lane.Tick();
                    dealt += Dealt(lane.DrainEvents());
                }
            }
            return dealt;
        }
        Assert.True(Honed(SkillGrades.Max) > Honed(0) * 105 / 100);
    }

    [Fact]
    public void A_mounted_hero_only_attacks_and_hunts_slower_offline()
    {
        // Owner, 26 Sep 2026: "at mount make char only autoattacking, off the mount it can use skills".
        var items = new List<ItemState>();
        for (int s = 0; s < 8; s++) items.Add(new ItemState(65, Rarity.Rare, (EquipSlot)s) { UpgradeLevel = 7 });
        WardrobeDef mount = Wardrobe.Find("ember-warhorse")!;
        HeroStats riding = HeroFactory.FromEquipment(items, 65, HeroClass.Vanguard, new[] { mount });
        HeroStats walking = HeroFactory.FromEquipment(items, 65, HeroClass.Vanguard);
        Assert.True(riding.Mounted);
        Assert.False(walking.Mounted);

        var lane = new LaneSim(Content.Stage(35), riding, SkillDef.For(HeroClass.Vanguard), new Inventory(), new XorShiftRandom(4));
        for (int i = 0; i < lane.AutoCast.Length; i++) lane.AutoCast[i] = true;
        var events = new List<LaneEvent>();
        for (int t = 0; t < 30 * LaneSim.TicksPerSecond; t++)
        {
            if (lane.Phase == LanePhase.Fighting) Assert.False(lane.TryCast(0));
            lane.Tick();
            events.AddRange(lane.DrainEvents());
        }
        Assert.DoesNotContain(events, e => e.Kind == LaneEventKind.SkillCast);
        Assert.Contains(events, e => e.Kind == LaneEventKind.EnemyDamaged);

        HuntSettlement onFoot = HuntYield.Settle(Content.Stage(35), walking, 3600, 3600, 10000, new Inventory(), new XorShiftRandom(1));
        HuntSettlement mounted = HuntYield.Settle(Content.Stage(35), riding, 3600, 3600, 10000, new Inventory(), new XorShiftRandom(1));
        Assert.True(mounted.Packs < onFoot.Packs, $"{mounted.Packs} vs {onFoot.Packs}");
    }
}
