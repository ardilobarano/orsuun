using System.Collections.Generic;
using System.Linq;
using Orsuun.Rules;
using Orsuun.Rules.Combat;
using Xunit;

/// <summary>The client's online farm loop (PlayerSession) must produce reports the server's replay accepts.</summary>
public class SessionLoopTests
{
    private const ulong Seed = 0xC0FFEE1234UL;

    /// <summary>
    /// Ticks the farm lane like GameRoot does, tapping an aimed Burst whenever a Korstone is up. Lane XP levels the
    /// hero between loops, so each loop is paired with the hero it started with (the server judges with its own).
    /// </summary>
    private static List<(LoopReport report, HeroStats hero)> Play(PlayerSession session, int loops, bool tap)
    {
        var reports = new List<(LoopReport, HeroStats)>();
        var heroes = new Dictionary<int, HeroStats> { [session.LaneLoop] = session.Hero };
        for (int guard = 0; guard < loops * ActivePlay.MaxLoopTicks && reports.Count < loops; guard++)
        {
            if (tap && session.Lane.Phase == LanePhase.Fighting && session.Lane.IsKorstoneEncounter) session.Cast(0);
            session.Lane.Tick();
            session.Lane.DrainEvents();
            if (!session.CloseLoopIfDone()) continue;
            heroes[session.LaneLoop] = session.Hero;
            foreach (LoopReport r in session.TakeReports()) reports.Add((r, heroes[r.Loop]));
        }
        return reports;
    }

    private static LoopVerdict Judge(PlayerSession session, LoopReport r, HeroStats hero) =>
        ActivePlay.Verify(EveningBells.Apply(Content.Stage(session.ParkedStage), Bell.None), hero, SkillDef.VanguardWrath(),
            Seed, r.Loop, r.AutoCast, r.Casts, r.Potions, r.Ticks);

    [Fact]
    public void Every_reported_loop_replays_on_the_server()
    {
        var session = new PlayerSession(new XorShiftRandom(3));
        session.Lane.AutoCast[1] = true;
        session.SetLaneSeed(Seed, 0);

        var reports = Play(session, 3, tap: true);

        Assert.Equal(new[] { 0, 1, 2 }, reports.Select(p => p.report.Loop).ToArray());
        Assert.Contains(reports, p => p.report.Casts.Count > 0);
        foreach ((LoopReport r, HeroStats hero) in reports)
        {
            LoopVerdict v = Judge(session, r, hero);
            Assert.True(v.Accepted, v.Reason + " on loop " + r.Loop);
            Assert.Equal(r.Ticks, v.Ticks);
        }
    }

    [Fact]
    public void A_mid_loop_auto_cast_toggle_skips_that_loop_only()
    {
        var session = new PlayerSession(new XorShiftRandom(3));
        session.SetLaneSeed(Seed, 5);
        for (int i = 0; i < 40; i++) session.Lane.Tick();
        session.ToggleAutoCast(1);

        var reports = Play(session, 1, tap: false);

        Assert.Single(reports);
        Assert.Equal(6, reports[0].report.Loop);    // loop 5 was toggled mid-way and is not reported
        Assert.True(Judge(session, reports[0].report, reports[0].hero).Accepted);
    }

    [Fact]
    public void The_same_seed_again_keeps_the_loop_count()
    {
        var session = new PlayerSession(new XorShiftRandom(3));
        session.SetLaneSeed(Seed, 0);
        Play(session, 1, tap: false);
        session.SetLaneSeed(Seed, 0);
        Assert.Equal(1, session.LaneLoop);
        session.SetLaneSeed(Seed + 1, 0);
        Assert.Equal(0, session.LaneLoop);
    }
}
