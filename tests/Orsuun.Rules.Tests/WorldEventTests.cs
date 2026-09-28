using System;
using System.Linq;
using Orsuun.Rules;
using Xunit;

namespace Orsuun.Rules.Tests;

/// <summary>Weekend events (owner, 27 Sep 2026).</summary>
public class WorldEventTests
{
    [Fact]
    public void A_week_holds_a_double_sorn_weekend_two_lucky_hours_two_rush_nights_and_a_fishing_contest()
    {
        // Mon 28 Sep 2026 00:00 server time: the week to Mon 5 Oct.
        var week = WorldEvents.Weekly(new DateTime(2026, 9, 28), 7);
        var sorn = Assert.Single(week, e => e.Kind == WorldEventKind.DoubleSorn);
        Assert.Equal(new DateTime(2026, 10, 3, 0, 0, 0), sorn.Start);
        Assert.Equal(new DateTime(2026, 10, 5, 0, 0, 0), sorn.End);
        Assert.Equal(new[] { new DateTime(2026, 10, 3, 20, 0, 0), new DateTime(2026, 10, 4, 20, 0, 0) },
            week.Where(e => e.Kind == WorldEventKind.LuckyForge).Select(e => e.Start));
        Assert.All(week.Where(e => e.Kind == WorldEventKind.LuckyForge), e => Assert.Equal(TimeSpan.FromHours(1), e.End - e.Start));
        Assert.Equal(new[] { new DateTime(2026, 10, 2, 20, 0, 0), new DateTime(2026, 10, 3, 20, 0, 0) },
            week.Where(e => e.Kind == WorldEventKind.CommanderRush).Select(e => e.Start));
        var contest = Assert.Single(week, e => e.Kind == WorldEventKind.FishingContest);
        Assert.Equal(new DateTime(2026, 10, 3, 12, 0, 0), contest.Start);
        Assert.Equal(new DateTime(2026, 10, 4, 20, 0, 0), contest.End);
        Assert.Equal(6, week.Count);
    }

    [Fact]
    public void The_calendar_starts_from_the_hour_asked_and_not_before()
    {
        // Saturday 20:30: the lucky hour began at 20:00, so the next one is Sunday's.
        var rest = WorldEvents.Weekly(new DateTime(2026, 10, 3, 20, 30, 0), 1);
        Assert.Equal(new DateTime(2026, 10, 4, 20, 0, 0), Assert.Single(rest, e => e.Kind == WorldEventKind.LuckyForge).Start);
        Assert.DoesNotContain(rest, e => e.Kind == WorldEventKind.DoubleSorn);
    }

    [Fact]
    public void Double_sorn_pays_for_the_share_of_the_time_it_covers()
    {
        var weekend = new[] { (new DateTime(2026, 10, 3, 0, 0, 0), new DateTime(2026, 10, 5, 0, 0, 0)) };
        DateTime friday22 = new DateTime(2026, 10, 2, 22, 0, 0);
        Assert.Equal(0, WorldEvents.SornBonus(friday22, friday22.AddHours(2), weekend));
        Assert.Equal(50, WorldEvents.SornBonus(friday22, friday22.AddHours(4), weekend));
        Assert.Equal(100, WorldEvents.SornBonus(friday22.AddHours(3), friday22.AddHours(3).AddSeconds(30), weekend));
        Assert.Equal(0, WorldEvents.SornBonus(friday22, friday22, weekend));
    }

    [Fact]
    public void A_commander_rush_brings_commanders_back_every_15_minutes()
    {
        DateTime spawn = new DateTime(2026, 10, 2, 20, 30, 0);
        Assert.Equal(spawn.AddMinutes(45), WorldEvents.NextSpawn(spawn, 45 * 60, rushAtSpawn: false));
        Assert.Equal(spawn.AddMinutes(15), WorldEvents.NextSpawn(spawn, 45 * 60, rushAtSpawn: true));
        Assert.True(WorldEvents.RushRespawnSeconds > BossDef.WindowSeconds, "a spawn's window closes before the next one opens");
    }

    [Fact]
    public void The_lucky_hour_adds_five_percent_to_a_forge()
    {
        var item = new ItemState(30, Rarity.Rare, EquipSlot.Weapon) { UpgradeLevel = 3 };
        var plain = new ForgeService();
        var lucky = new ForgeService { LuckBp = WorldEvents.ForgeLuckBp };
        Assert.Equal(plain.ChanceBp(item, ForgeMethod.ForgeAlone) + 500, lucky.ChanceBp(item, ForgeMethod.ForgeAlone));
        Assert.Equal(RandomExtensions.FullBp, new ForgeService { LuckBp = 20_000 }.ChanceBp(item, ForgeMethod.ForgeAlone));
    }

    [Fact]
    public void Every_kind_has_its_name_and_effect()
    {
        foreach (WorldEventKind kind in Enum.GetValues(typeof(WorldEventKind)))
        {
            if (kind == WorldEventKind.None) continue;
            WorldEventDef? def = WorldEvents.Def(kind);
            Assert.NotNull(def);
            Assert.False(string.IsNullOrEmpty(def!.Name));
            Assert.False(string.IsNullOrEmpty(def.Effect));
        }
    }
}
