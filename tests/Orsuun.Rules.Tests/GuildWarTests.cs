using Orsuun.Rules;
using Orsuun.Rules.Combat;
using Xunit;

namespace Orsuun.Rules.Tests;

/// <summary>Guild war nights, duels decided by gear, and the fortress keeps' bids (GDD section 7).</summary>
public class GuildWarTests
{
    private static List<ItemState> Set(int upgrade, int itemLevel)
    {
        var items = new List<ItemState>();
        for (int s = 0; s < 8; s++) items.Add(new ItemState(itemLevel, Rarity.Rare, (EquipSlot)s) { UpgradeLevel = upgrade });
        return items;
    }

    [Fact]
    public void War_nights_are_wednesday_and_saturday_at_21_for_an_hour()
    {
        // Thursday 24 Sep 2026, noon: the next night is Saturday 26 Sep.
        Assert.Equal(new DateTime(2026, 9, 26, 21, 0, 0), GuildWars.StartOf(new DateTime(2026, 9, 24, 12, 0, 0)));
        // Wednesday 30 Sep, 21:30: that night is still running.
        DateTime start = GuildWars.StartOf(new DateTime(2026, 9, 30, 21, 30, 0));
        Assert.Equal(new DateTime(2026, 9, 30, 21, 0, 0), start);
        Assert.True(GuildWars.Running(new DateTime(2026, 9, 30, 21, 30, 0), start));
        // At 22:00 it is over and the next is Saturday.
        Assert.Equal(new DateTime(2026, 10, 3, 21, 0, 0), GuildWars.StartOf(new DateTime(2026, 9, 30, 22, 0, 0)));
        Assert.Equal("N2026-10-03", GuildWars.NightKey(new DateTime(2026, 10, 3, 21, 0, 0)));
    }

    [Fact]
    public void Guilds_pair_by_rating_and_an_odd_one_sits_out()
    {
        List<(int A, int B)> pairs = GuildWars.Pair(new[] { 1000, 1100, 980, 1050, 1000 }, out int bye);
        Assert.Equal(new[] { (1, 3), (0, 4) }, pairs);
        Assert.Equal(2, bye);
        Assert.Empty(GuildWars.Pair(new[] { 1000 }, out bye));
        Assert.Equal(0, bye);
    }

    [Fact]
    public void Fronts_break_at_five_steps_and_score_ten()
    {
        int front = 0;
        front = GuildWars.Push(front, sideA: true, flag: true);
        front = GuildWars.Push(front, sideA: true, flag: true);
        Assert.Equal(4, front);
        front = GuildWars.Push(front, sideA: true, flag: true);
        Assert.Equal(5, front);                                   // clamped at the break
        Assert.Equal(5, GuildWars.Push(front, sideA: false, flag: false));   // a broken lane stays broken
        int[] fronts = { 5, -1, -5 };
        Assert.Equal(3 + 10, GuildWars.Score(3, fronts, sideA: true));
        Assert.Equal(4 + 10, GuildWars.Score(4, fronts, sideA: false));
        Assert.Equal(2, GuildWars.Result(13, 14));
        Assert.Equal(3, GuildWars.Result(14, 14));
    }

    [Fact]
    public void Ratings_move_by_elo()
    {
        Assert.Equal((1016, 984), GuildWars.Rate(1000, 1000, 1));
        Assert.Equal((1000, 1000), GuildWars.Rate(1000, 1000, 3));
        (int a, int b) = GuildWars.Rate(1200, 1000, 2);
        Assert.True(a < 1200 - 16 && b > 1000 + 16);             // an upset moves more
    }

    [Fact]
    public void Gear_decides_not_class()
    {
        // The neutral frame ignores the class: equal gear is an even duel for every matchup.
        HeroStats a = Duels.Neutral(Set(6, 50), 50), d = Duels.Neutral(Set(6, 50), 50);
        Duels.Compress(a, d);
        Assert.Equal(0.0, Duels.Edge(a, d), 6);

        // GDD: a full +9 set beats a full +7 set about 80% of the time (compressed as in guild war).
        foreach (int level in new[] { 30, 70 })
        {
            HeroStats nine = Duels.Neutral(Set(9, level), level), seven = Duels.Neutral(Set(7, level), level);
            Duels.Compress(nine, seven);
            double edge = Duels.Edge(nine, seven);
            var rng = new XorShiftRandom(5);
            int wins = 0;
            for (int i = 0; i < 4000; i++) if (Duels.Roll(edge, rng)) wins++;
            Assert.InRange(wins / 40.0, 76.0, 84.0);
        }
    }

    [Fact]
    public void Compression_keeps_a_third_less_of_what_is_above_the_median()
    {
        var a = new HeroStats { Attack = 1000, Defense = 100, MaxHp = 10_000 };
        var b = new HeroStats { Attack = 600, Defense = 100, MaxHp = 6_000 };
        Duels.Compress(a, b);
        Assert.Equal(800 + 140, a.Attack);
        Assert.Equal(600, b.Attack);
        Assert.Equal(100, a.Defense);
        Assert.Equal(8_000 + 1_400, a.MaxHp);
    }

    [Fact]
    public void The_replay_ends_the_way_the_roll_said()
    {
        foreach (HeroClass cls in new[] { HeroClass.Vanguard, HeroClass.Kestrel, HeroClass.Wraithsworn, HeroClass.Drumcaller })
            foreach (bool win in new[] { true, false })
            {
                HeroStats hero = HeroFactory.FromEquipment(Set(4, 30), 30, cls);
                BossDef champion = Duels.Stage("[TAG] Rival", hero, win, 42);
                Assert.Equal(Duels.ChampionId, champion.Id);
                Assert.Equal(win, BossRun.Simulate(champion, hero, new Inventory(), 42).Killed);
            }
    }

    [Fact]
    public void Keep_bids_pick_four_contenders_and_the_wall_decides()
    {
        var t = new DateTime(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);
        var bids = new[] { (100_000L, t), (300_000L, t), (100_000L, t.AddMinutes(-5)), (50_000L, t), (200_000L, t) };
        Assert.Equal(new[] { 1, 4, 2, 0 }, FortressKeeps.PickContenders(bids));
        Assert.Equal(-1, FortressKeeps.Winner(new long[] { 140_000, 90_000 }, 0));            // under the wall
        Assert.Equal(0, FortressKeeps.Winner(new long[] { 160_000, 90_000 }, 0));
        Assert.Equal(-1, FortressKeeps.Winner(new long[] { 160_000, 90_000 }, 20_000));       // the holders mended it
        Assert.Equal(-1, FortressKeeps.Winner(Array.Empty<long>(), 0));
        // The keep siege is the bounty week's last evening: Sunday 20:00.
        Assert.Equal(new DateTime(2026, 9, 27, 20, 0, 0), FortressKeeps.SiegeStart(new DateTime(2026, 9, 24, 12, 0, 0)));
        Assert.Equal(new DateTime(2026, 10, 4, 20, 0, 0), FortressKeeps.SiegeStart(new DateTime(2026, 9, 28, 20, 30, 0)));
    }
}
