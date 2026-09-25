using Orsuun.Rules;
using Orsuun.Rules.Combat;
using Xunit;

namespace Orsuun.Rules.Tests;

/// <summary>
/// Skill grades (owner, 26 Sep 2026): a book per skill of each class; Mastered steps need 1, 1, 2 .. 9 good reads at 70%,
/// 8 hours apart; Grand steps and Peerless an Oathstone at 60% and Honor, more each step.
/// </summary>
public class SkillGradeTests
{
    [Fact]
    public void Twelve_books_one_per_skill_of_each_class()
    {
        Assert.Equal(12, Books.Count);
        Assert.Equal(0, Books.Id(HeroClass.Vanguard, 0));
        Assert.Equal(4, Books.Id(HeroClass.Kestrel, 1));
        Assert.Equal(HeroClass.Drumcaller, Books.ClassOf(11));
        Assert.Equal(2, Books.SlotOf(11));
        Assert.Equal("Technique Scroll: Iron Whirl", Books.Name(1));
        Assert.Equal("Technique Scroll: Void Lance", Books.Name(Books.Id(HeroClass.Wraithsworn, 0)));
    }

    [Fact]
    public void Grades_run_from_Normal_through_Mastered_and_Grand_to_Peerless()
    {
        Assert.Equal(21, SkillGrades.Max);
        Assert.Equal("Normal", SkillGrades.Name(0));
        Assert.Equal("M10", SkillGrades.Name(10));
        Assert.Equal("G1", SkillGrades.Name(11));
        Assert.Equal("P", SkillGrades.Name(21));
        Assert.Equal(20, SkillGrades.BonusPercent(10));
        Assert.Equal(50, SkillGrades.BonusPercent(20));
        Assert.Equal(60, SkillGrades.BonusPercent(21));
        // Reads a step needs: Normal to M1 1, M1 to M2 1, M2 to M3 2 .. M9 to M10 9: 46 in all.
        Assert.Equal(new[] { 1, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, Enumerable.Range(0, 10).Select(SkillGrades.ReadsNeeded).ToArray());
        Assert.Equal(46, Enumerable.Range(0, 10).Sum(SkillGrades.ReadsNeeded));
        Assert.Equal(0, SkillGrades.ReadsNeeded(10));
        // Honor for an Oathstone try: G1 30 .. G10 300, Peerless 330; none for reads.
        Assert.Equal(0, SkillGrades.HonorCost(9));
        Assert.Equal(30, SkillGrades.HonorCost(10));
        Assert.Equal(300, SkillGrades.HonorCost(19));
        Assert.Equal(330, SkillGrades.HonorCost(20));
        Assert.Equal(7000, SkillGrades.ChanceBp(0));
        Assert.Equal(6000, SkillGrades.ChanceBp(15));
        int[] all = SkillGrades.Parse("3;x;99;0;0;0;0;0;0;0;0;4");
        Assert.Equal(new[] { 3, 0, 21 }, SkillGrades.ForClass(all, HeroClass.Vanguard));
        Assert.Equal(new[] { 0, 0, 4 }, SkillGrades.ForClass(all, HeroClass.Drumcaller));
    }

    [Fact]
    public void Training_needs_its_book_rest_an_Oathstone_and_Honor()
    {
        Assert.NotNull(SkillGrades.Problem(0, 0, 5, 999, 99_999));
        Assert.NotNull(SkillGrades.Problem(0, 1, 0, 0, 3600));
        Assert.Null(SkillGrades.Problem(0, 1, 0, 0, 8 * 3600));
        Assert.NotNull(SkillGrades.Problem(10, 5, 0, 999, 99_999));
        Assert.NotNull(SkillGrades.Problem(10, 0, 1, 29, 0));
        Assert.Null(SkillGrades.Problem(10, 0, 1, 30, 0));
        Assert.NotNull(SkillGrades.Problem(21, 9, 9, 9999, 99_999));

        // M3 needs three good reads: the grade rises on the third.
        var always = new XorShiftRandom(1);
        int grade = 3, reads = 0, good = 0;
        for (int i = 0; i < 200 && grade == 3; i++)
        {
            (grade, reads) = SkillGrades.Train(grade, reads, always, out bool ok);
            if (ok) good++;
        }
        Assert.Equal(4, grade);
        Assert.Equal(3, good);
        Assert.Equal(0, reads);

        int wins = 0;
        var rng = new XorShiftRandom(11);
        for (int i = 0; i < 2000; i++) { SkillGrades.Train(12, 0, rng, out bool ok); if (ok) wins++; }
        Assert.InRange(wins, 1100, 1300);
    }

    [Fact]
    public void A_graded_skill_hits_harder_on_both_sides_of_the_replay()
    {
        var gear = new[] { new ItemState(40, Rarity.Rare, EquipSlot.Weapon) { UpgradeLevel = 7 } };
        HeroStats plain = HeroFactory.FromEquipment(gear, 40);
        HeroStats graded = HeroFactory.FromEquipment(gear, 40, skillGrades: new[] { 21, 10, 0 });
        Assert.Equal(new[] { 60, 20, 0 }, graded.SkillGradeBonusPercent);
        Assert.Equal(plain.Attack, graded.Attack);
        long Damage(HeroStats hero)
        {
            var lane = new LaneSim(Content.Stage(40), hero, SkillDef.For(HeroClass.Vanguard), new Inventory { Potions = 5 }, new XorShiftRandom(3));
            for (int i = 0; i < lane.AutoCast.Length; i++) lane.AutoCast[i] = true;
            long dealt = 0;
            for (int t = 0; t < 600; t++)
            {
                lane.Tick();
                foreach (LaneEvent e in lane.DrainEvents()) if (e.Kind == LaneEventKind.EnemyDamaged) dealt += e.Amount;
            }
            return dealt;
        }
        Assert.True(Damage(graded) > Damage(plain));

        // The session holds all twelve grades and fights with its class's three.
        var session = new PlayerSession(new XorShiftRandom(7));
        var all = new int[Books.Count];
        all[Books.Id(HeroClass.Vanguard, 0)] = 5;
        all[Books.Id(HeroClass.Kestrel, 0)] = 10;
        session.SetSkillGrades(all);
        Assert.Equal(10, session.Hero.SkillGradeBonusPercent[0]);
        session.SetClass(HeroClass.Kestrel);
        Assert.Equal(20, session.Hero.SkillGradeBonusPercent[0]);
    }

    [Fact]
    public void Books_come_from_Wardens_and_the_shops()
    {
        var inv = new Inventory();
        Dungeons.WardenChest(inv, 40, new XorShiftRandom(5), Dungeons.Find(1));
        Assert.Equal(1, inv.Books.Sum());
        var shop = new Inventory { HuntMarks = 100 };
        HuntShop.Buy(shop, 6, 3, HeroClass.Kestrel, new XorShiftRandom(2));
        Assert.Equal(3, Enumerable.Range(0, 3).Sum(s => shop.Books[Books.Id(HeroClass.Kestrel, s)]));
        Assert.Equal(70, shop.HuntMarks);
    }
}
