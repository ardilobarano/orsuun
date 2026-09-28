using Orsuun.Rules;
using Orsuun.Rules.Combat;
using Xunit;

namespace Orsuun.Rules.Tests;

/// <summary>Fishing (owner, 28 Sep 2026; GDD: a cast per 30 s, a mussel's pearls 6% / 2% / 0.5%) and invite codes.</summary>
public class FishingTests
{
    [Fact]
    public void Mussels_hold_pearls_at_the_gdds_odds()
    {
        Assert.Equal(new[] { 600, 200, 50 }, Fishing.PearlBp);
        var rng = new XorShiftRandom(7);
        var found = new int[3];
        const int opened = 200_000;
        for (int i = 0; i < opened; i++)
            if (Fishing.Open(rng) is Pearl p) found[(int)p]++;
        Assert.InRange(found[(int)Pearl.Moon] / (double)opened, 0.055, 0.065);
        Assert.InRange(found[(int)Pearl.Tide] / (double)opened, 0.017, 0.023);
        Assert.InRange(found[(int)Pearl.Heart] / (double)opened, 0.004, 0.006);
    }

    [Fact]
    public void A_pearl_pays_only_the_last_three_attempts()
    {
        Assert.Null(Fishing.PearlFor(6));
        Assert.Equal(Pearl.Moon, Fishing.PearlFor(7));
        Assert.Equal(Pearl.Tide, Fishing.PearlFor(8));
        Assert.Equal(Pearl.Heart, Fishing.PearlFor(9));
    }

    [Fact]
    public void The_tireless_rod_lands_one_catch_every_thirty_seconds_up_to_twelve_hours()
    {
        var from = new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(0, Fishing.AutoCatches(from, from.AddSeconds(29)));
        Assert.Equal(2, Fishing.AutoCatches(from, from.AddSeconds(61)));
        Assert.Equal(12 * 120, Fishing.AutoCatches(from, from.AddDays(3)));
    }

    [Fact]
    public void A_reel_counts_around_the_bite_and_not_long_after()
    {
        Assert.False(Fishing.InTime(1000, 3000));
        Assert.True(Fishing.InTime(2800, 3000));
        Assert.True(Fishing.InTime(3000 + Fishing.WindowMs, 3000));
        Assert.False(Fishing.InTime(3000 + Fishing.WindowMs + Fishing.LateMs + 1, 3000));
    }

    [Fact]
    public void A_meal_counts_for_the_part_of_the_interval_it_lasted()
    {
        var from = new DateTime(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(0, Fishing.MealShareBp(from, from.AddMinutes(10), null));
        Assert.Equal(10000, Fishing.MealShareBp(from, from.AddMinutes(10), from.AddHours(1)));
        Assert.Equal(5000, Fishing.MealShareBp(from, from.AddMinutes(10), from.AddMinutes(5)));
        Assert.Equal(0, Fishing.MealShareBp(from, from.AddMinutes(10), from.AddMinutes(-1)));
    }

    [Fact]
    public void Catches_are_mostly_fish_and_the_taimen_is_rare()
    {
        var rng = new XorShiftRandom(11);
        int mussels = 0, taimen = 0;
        const int casts = 100_000;
        for (int i = 0; i < casts; i++)
        {
            (CatchKind kind, int fish) = Fishing.Land(rng);
            if (kind == CatchKind.Mussel) mussels++;
            else if (fish == 4) taimen++;
        }
        Assert.InRange(mussels / (double)casts, 0.18, 0.22);
        Assert.InRange(taimen / (double)casts, 0.04, 0.06);
    }

    [Fact]
    public void Pearls_and_fish_trade_after_the_korshards_without_renumbering()
    {
        Assert.Equal(TradeGoods.FirstPearl, TradeGoods.FirstKorshard + Content.KorshardRanks.Length);
        Assert.Equal(TradeGoods.FirstFish, TradeGoods.FirstPearl + 3);
        Assert.Equal("Scroll of Mercy", TradeGoods.Name(TradeGoods.ScrollOfMercy));
        Assert.Equal("Heart Pearl", TradeGoods.Name(TradeGoods.FirstPearl + 2));
        Assert.Equal("Golden Taimen", TradeGoods.Name(TradeGoods.FirstFish + 4));
        var inventory = new Inventory();
        TradeGoods.Add(inventory, TradeGoods.FirstPearl + 1, 2);
        TradeGoods.Add(inventory, TradeGoods.FirstFish, 3);
        Assert.Equal(2, inventory.Pearls[(int)Pearl.Tide]);
        Assert.Equal(3, TradeGoods.Held(inventory, TradeGoods.FirstFish));
    }

    [Fact]
    public void A_forge_paid_with_a_pearl_keeps_the_materials()
    {
        var session = new PlayerSession(new XorShiftRandom(1));
        session.Inventory.Sorn = 100_000_000;
        session.Inventory.Materials = 0;
        ItemState weapon = session.OnAnvil;
        weapon.UpgradeLevel = 6;
        Assert.NotNull(session.ForgeBlocker(ForgeMethod.ForgeAlone));
        Assert.Equal("No Moon Pearl", session.ForgeBlocker(ForgeMethod.ForgeAlone, pearl: true));
        session.Inventory.Pearls[(int)Pearl.Moon] = 1;
        Assert.Null(session.ForgeBlocker(ForgeMethod.ForgeAlone, pearl: true));
        session.Forge(ForgeMethod.ForgeAlone, pearl: true);
        Assert.Equal(0, session.Inventory.Pearls[(int)Pearl.Moon]);
        Assert.Equal(0, session.Inventory.Materials);
    }

    [Fact]
    public void Invite_codes_avoid_look_alike_letters()
    {
        var rng = new XorShiftRandom(3);
        for (int i = 0; i < 200; i++)
        {
            string code = Invites.NewCode(rng);
            Assert.True(Invites.Valid(code));
            Assert.DoesNotContain('O', code);
            Assert.DoesNotContain('0', code);
            Assert.DoesNotContain('I', code);
            Assert.DoesNotContain('1', code);
        }
        Assert.Equal("ABC234", Invites.Clean(" abc-234 "));
        Assert.False(Invites.Valid("ABCD"));
    }
}
