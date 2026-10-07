namespace Orsuun.Server.Game;

/// <summary>
/// The tester cap (owner, 7 Oct 2026: "Load test before more testers": "... and set a safe tester cap for the playtest
/// box"). At most Playtest:MaxOnline heroes (env Playtest__MaxOnline; 0 or unset: no cap) hunt at once, counted by a
/// heartbeat within GameService.OnlineGrace since they were made. A hero coming online past it (choosing a character,
/// or a phone's first heartbeat after more than the grace away, or a new hero's first) is refused with "server_full";
/// heroes already hunting go on, and moderators (Admin:Emails) always pass. tools/loadtest.sh measured the number
/// (HANDOFF.md, "Load test and tester cap").
/// </summary>
public sealed class OnlineCap
{
    public OnlineCap(IConfiguration config) => Max = Math.Max(0, config.GetValue("Playtest:MaxOnline", 0));

    public int Max { get; }
}
