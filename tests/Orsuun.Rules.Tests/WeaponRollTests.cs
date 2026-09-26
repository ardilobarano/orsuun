using Orsuun.Rules;
using Orsuun.Rules.Combat;
using Xunit;

namespace Orsuun.Rules.Tests;

/// <summary>
/// Average damage and skill damage (owner, 26 Sep 2026: "after level 30 we need to add 'ortalama zarar +%...' like metin 2
/// to all weapons, it will be nearly impossible to have +%60, it will be from -%30 to +%60", and skill damage from -15 to
/// +30 the same way).
/// </summary>
public class WeaponRollTests
{
    [Fact]
    public void Only_weapons_of_item_level_30_and_up_roll()
    {
        var rng = new XorShiftRandom(7);
        var low = new ItemState(29, Rarity.Epic, EquipSlot.Weapon);
        var armour = new ItemState(90, Rarity.Epic, EquipSlot.Armor);
        WeaponRolls.Roll(low, rng);
        WeaponRolls.Roll(armour, rng);
        Assert.Equal(0, low.AverageDamagePercent + low.SkillDamagePercent);
        Assert.Equal(0, armour.AverageDamagePercent + armour.SkillDamagePercent);

        // Weapon drops from level 30 on carry both; below, none do.
        int rolled = 0;
        for (ulong seed = 1; seed <= 400; seed++)
        {
            ItemState drop = HuntYield.DropGear(Content.Stage(45), new Inventory(), new XorShiftRandom(seed), Rarity.Legendary);
            if (drop.Slot == EquipSlot.Weapon && (drop.AverageDamagePercent != 0 || drop.SkillDamagePercent != 0)) rolled++;
            ItemState early = HuntYield.DropGear(Content.Stage(15), new Inventory(), new XorShiftRandom(seed), Rarity.Legendary);
            Assert.Equal(0, early.AverageDamagePercent + early.SkillDamagePercent);
        }
        Assert.True(rolled > 30, $"{rolled} rolled weapons");
    }

    [Fact]
    public void The_top_of_each_range_is_nearly_impossible()
    {
        var rng = new XorShiftRandom(11);
        int n = 200_000, negative = 0, thirty = 0, fifty = 0, sixty = 0, skillTwenty = 0;
        long sum = 0;
        for (int i = 0; i < n; i++)
        {
            var w = new ItemState(60, Rarity.Rare, EquipSlot.Weapon);
            WeaponRolls.Roll(w, rng);
            Assert.InRange(w.AverageDamagePercent, WeaponRolls.AverageMin, WeaponRolls.AverageMax);
            Assert.InRange(w.SkillDamagePercent, WeaponRolls.SkillMin, WeaponRolls.SkillMax);
            sum += w.AverageDamagePercent;
            if (w.AverageDamagePercent < 0) negative++;
            if (w.AverageDamagePercent >= 30) thirty++;
            if (w.AverageDamagePercent >= 50) fifty++;
            if (w.AverageDamagePercent == 60) sixty++;
            if (w.SkillDamagePercent >= 20) skillTwenty++;
        }
        Assert.InRange(sum / n, 6, 10);                 // centred near +8%
        Assert.InRange(negative * 100 / n, 24, 32);     // about a quarter below zero
        Assert.InRange(thirty * 1000 / n, 50, 72);      // about 6% reach +30%
        Assert.True(fifty * 1000 / n <= 2, $"{fifty} of {n} at +50% or more");
        Assert.True(sixty <= 12, $"{sixty} of {n} at +60%");
        Assert.True(skillTwenty * 100 / n <= 2, $"{skillTwenty} of {n} with skill damage +20% or more");
        Assert.True(WeaponRolls.AveragePpm(60) < 20 && WeaponRolls.SkillPpm(30) < 40);
    }

    private static HeroStats Hero(int average, int skill)
    {
        var items = new List<ItemState>();
        for (int s = 0; s < 8; s++) items.Add(new ItemState(60, Rarity.Rare, (EquipSlot)s) { UpgradeLevel = 7 });
        items[0].AverageDamagePercent = average;
        items[0].SkillDamagePercent = skill;
        return HeroFactory.FromEquipment(items, 60);
    }

    private static long Dealt(HeroStats hero, bool cast)
    {
        long dealt = 0;
        for (ulong seed = 1; seed <= 6; seed++)
        {
            var lane = new LaneSim(Content.Stage(55), hero, SkillDef.For(HeroClass.Vanguard), new Inventory(), new XorShiftRandom(seed));
            for (int guard = 0; guard < 20000 && lane.Phase != LanePhase.Fighting; guard++) { lane.Tick(); lane.DrainEvents(); }
            if (cast) lane.TryCast(1);
            for (int t = 0; t < 3 * LaneSim.TicksPerSecond && lane.Phase == LanePhase.Fighting; t++)
            {
                lane.Tick();
                foreach (LaneEvent e in lane.DrainEvents())
                    if (e.Kind == LaneEventKind.EnemyDamaged) dealt += e.Amount;
            }
        }
        return dealt;
    }

    [Fact]
    public void Average_damage_moves_plain_attacks_and_skill_damage_the_skills()
    {
        HeroStats plain = Hero(0, 0);
        Assert.Equal(0, plain.AverageDamagePercent);
        Assert.Equal(40, Hero(40, 0).AverageDamagePercent);
        Assert.True(Dealt(Hero(40, 0), cast: false) > Dealt(plain, cast: false) * 115 / 100);
        Assert.True(Dealt(Hero(-30, 0), cast: false) < Dealt(plain, cast: false) * 85 / 100);
        // A cast of Iron Whirl with +30% skill damage lands harder than with -15%.
        Assert.True(Dealt(Hero(0, 30), cast: true) > Dealt(Hero(0, -15), cast: true));

        // A weapon under level 30 carries no rolls, whatever it says.
        var early = new ItemState(20, Rarity.Rare) { AverageDamagePercent = 50 };
        Assert.Equal(0, HeroFactory.FromEquipment(new[] { early }, 20).AverageDamagePercent);

        // Offline, a better weapon roll clears more packs; a mounted hero only gains from average damage.
        HuntSettlement bare = HuntYield.Settle(Content.Stage(55), plain, 3600, 3600, 10000, new Inventory(), new XorShiftRandom(1));
        HuntSettlement strong = HuntYield.Settle(Content.Stage(55), Hero(40, 20), 3600, 3600, 10000, new Inventory(), new XorShiftRandom(1));
        Assert.True(strong.Packs > bare.Packs);
    }
}
