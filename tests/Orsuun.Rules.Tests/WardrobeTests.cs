using Orsuun.Rules;
using Orsuun.Rules.Combat;
using Xunit;

namespace Orsuun.Rules.Tests;

/// <summary>The wardrobe (owner, 25 Sep 2026): timed skins, mounts and companions with small stats, sold for Amber, dropped by bosses.</summary>
public class WardrobeTests
{
    private const long Now = 1_790_000_000;

    [Fact]
    public void The_caravan_sells_every_duration_and_longer_costs_less_a_day()
    {
        Assert.Equal(new[] { 1, 3, 5, 7, 14 }, Wardrobe.Days);
        WardrobeDef horse = Wardrobe.Find("ember-warhorse")!;
        Assert.Equal(new[] { 60, 150, 220, 280, 480 }, Wardrobe.Days.Select(d => Wardrobe.Price(horse, d)));
        Assert.Equal(-1, Wardrobe.Price(horse, 2));
        Assert.Equal(-1, Wardrobe.Price(Wardrobe.Find("mirage-veil")!, 7));      // a Commander's trophy only drops
        foreach (WardrobeDef def in Wardrobe.All.Where(d => d.Sold))
            for (int i = 1; i < Wardrobe.Days.Length; i++)
                Assert.True(Wardrobe.Price(def, Wardrobe.Days[i]) * Wardrobe.Days[i - 1] < Wardrobe.Price(def, Wardrobe.Days[i - 1]) * Wardrobe.Days[i]);
        Assert.Equal(3, Enum.GetValues(typeof(WardrobeKind)).Length);
        Assert.All(Enum.GetValues(typeof(WardrobeKind)).Cast<WardrobeKind>(), k => Assert.Contains(Wardrobe.All, d => d.Kind == k && d.Sold));
    }

    [Fact]
    public void A_piece_held_again_adds_its_time_and_one_run_out_starts_over()
    {
        var owned = new List<OwnedPiece>();
        Wardrobe.Grant(owned, "sky-falcon", 3, Now);
        Wardrobe.Grant(owned, "sky-falcon", 7, Now + 3600);
        Assert.Single(owned);
        Assert.Equal(Now + 10 * 86400L, owned[0].ExpiresUnix);
        Wardrobe.Grant(owned, "steppe-pony", 1, Now);
        Wardrobe.Grant(owned, "steppe-pony", 1, Now + 5 * 86400L);           // ran out four days ago: counted from now
        Assert.Equal(Now + 6 * 86400L, owned[1].ExpiresUnix);
        Assert.Equal(owned.Select(p => (p.Id, p.ExpiresUnix)), Wardrobe.Parse(Wardrobe.Format(owned)).Select(p => (p.Id, p.ExpiresUnix)));
        Wardrobe.Prune(owned, Now + 30 * 86400L);
        Assert.Empty(owned);
    }

    [Fact]
    public void A_skin_adds_hp_a_mount_attack_and_only_while_held()
    {
        var gear = new List<ItemState>();
        for (int s = 0; s < 8; s++) gear.Add(new ItemState(30, Rarity.Rare, (EquipSlot)s) { UpgradeLevel = 5 });
        HeroStats plain = HeroFactory.FromEquipment(gear, 30);
        var owned = new List<OwnedPiece>();
        Wardrobe.Grant(owned, "ember-warhorse", 1, Now);
        Wardrobe.Grant(owned, "frost-hunter-furs", 1, Now);
        Wardrobe.Grant(owned, "sky-falcon", 1, Now);
        List<WardrobeDef> worn = Wardrobe.Worn(new[] { "ember-warhorse", "frost-hunter-furs", "sky-falcon" }, owned, Now + 60);
        HeroStats dressed = HeroFactory.FromEquipment(gear, 30, HeroClass.Vanguard, worn);
        Assert.Equal(plain.Attack * 104 / 100, dressed.Attack);
        Assert.Equal(plain.MaxHp * 103 / 100, dressed.MaxHp);
        Assert.Equal(plain.Defense, dressed.Defense);
        Assert.Equal(5, Wardrobe.Bonus(worn, WardrobePerk.Xp));
        Assert.Empty(Wardrobe.Worn(new[] { "ember-warhorse" }, owned, Now + 2 * 86400L));   // ran out
        Assert.Empty(Wardrobe.Worn(new[] { "hollow-steed" }, owned, Now));                  // never held
    }

    [Fact]
    public void Bosses_from_gorak_pass_on_drop_short_pieces()
    {
        var rng = new XorShiftRandom(7);
        int[] counts = new int[15];
        for (int i = 0; i < 20000; i++) counts[Wardrobe.RollDropDays(rng)]++;
        Assert.InRange(counts[1], 11500, 12500);
        Assert.InRange(counts[3], 5500, 6500);
        Assert.True(counts[5] > 0 && counts[7] > 0 && counts[7] < counts[5]);
        Assert.Equal(0, counts[14]);

        int early = 0, late = 0;
        for (ulong seed = 1; seed <= 4000; seed++)
        {
            var a = new Inventory();
            HuntYield.LootBoss(Content.Stage(10), a, new XorShiftRandom(seed));
            early += a.WardrobeDrops.Count;
            var b = new Inventory();
            HuntYield.LootBoss(Content.Stage(20), b, new XorShiftRandom(seed));
            late += b.WardrobeDrops.Count;
        }
        Assert.Equal(0, early);
        Assert.InRange(late, 10, 45);                                        // 0.6% of 4000
    }

    [Fact]
    public void A_commander_drops_its_own_trophy_to_its_first_five()
    {
        BossDef queen = Content.Bosses.First(b => b.Name == "The Mirage Queen");
        int first = 0, sixth = 0;
        for (ulong seed = 1; seed <= 2000; seed++)
        {
            var a = new Inventory();
            HuntYield.LootCommander(queen, 1, a, new XorShiftRandom(seed));
            Assert.All(a.WardrobeDrops, d => Assert.StartsWith("mirage-veil:", d));
            first += a.WardrobeDrops.Count;
            var b = new Inventory();
            HuntYield.LootCommander(queen, 6, b, new XorShiftRandom(seed));
            sixth += b.WardrobeDrops.Count;
        }
        Assert.InRange(first, 140, 260);                                     // 10%
        Assert.Equal(0, sixth);
        Assert.All(Content.Bosses, b => Assert.NotNull(Wardrobe.FindByName(b.SkinName)));
    }

    [Fact]
    public void Amber_packs_pay_a_bonus_and_the_first_one_twice()
    {
        Assert.Equal(new[] { 60, 300, 650, 1400, 3800, 8000 }, Amber.Packs.Select(p => p.Amber));
        Assert.Equal("$0.99", Amber.Packs[0].PriceText);
        Assert.Equal("$99.99", Amber.Packs[5].PriceText);
        Assert.Equal(330, Amber.Paid(Amber.Pack(2)!, firstPurchase: false));
        Assert.Equal(630, Amber.Paid(Amber.Pack(2)!, firstPurchase: true));
        Assert.Null(Amber.Pack(7));
    }
}
