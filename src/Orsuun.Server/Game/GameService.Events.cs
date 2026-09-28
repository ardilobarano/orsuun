using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Orsuun.Rules;
using Orsuun.Server.Data;

namespace Orsuun.Server.Game;

// Weekend events (Rules.WorldEvents, owner 27 Sep 2026): the server's calendar of timed world events.
public sealed partial class GameService
{
    /// <summary>
    /// WorldClock, every tick: writes the weekly calendar's events a week ahead (a row that exists, called off or not,
    /// is left alone), says each event in world chat once it begins, and reloads the calendar the requests read.
    /// </summary>
    public async Task TickEventsAsync(CancellationToken ct)
    {
        DateTime today = _bells.LocalNow.Date;
        foreach ((WorldEventKind kind, DateTime start, DateTime end) in WorldEvents.Weekly(today.AddDays(-2), 10))
        {
            DateTime startsUtc = _bells.ToUtc(start), endsUtc = _bells.ToUtc(end);
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $@"INSERT INTO ""WorldEvents"" (""Kind"", ""StartsUtc"", ""EndsUtc"", ""Weekly"", ""Cancelled"", ""Announced"", ""By"")
                   VALUES ({(int)kind}, {startsUtc}, {endsUtc}, TRUE, FALSE, FALSE, 'calendar') ON CONFLICT (""Kind"", ""StartsUtc"") DO NOTHING", ct);
        }

        DateTime now = DateTime.UtcNow;
        List<WorldEvent> begun = await _db.WorldEvents.AsNoTracking()
            .Where(e => !e.Cancelled && !e.Announced && e.StartsUtc <= now && e.EndsUtc > now).ToListAsync(ct);
        foreach (WorldEvent e in begun)
        {
            // One announcement per event, whichever tick claims it.
            if (await _db.WorldEvents.Where(w => w.Id == e.Id && !w.Announced).ExecuteUpdateAsync(s => s.SetProperty(w => w.Announced, true), ct) != 1)
                continue;
            WorldEventDef? def = WorldEvents.Def(e.Kind);
            if (def == null) continue;
            SystemLine(Chat.World, $"{def.Name} has begun: {def.Effect} until {LocalText(e.EndsUtc)}.");
            await SaveAsync(ct);
        }
        await _events.ReloadAsync(_db, ct);
        await SettleContestsAsync(ct);
    }

    /// <summary>A time in server-local words, "Monday 00:00" (invariant: the phone translates the day's name).</summary>
    private string LocalText(DateTime utc) =>
        _bells.ToLocal(utc).ToString("dddd HH:mm", CultureInfo.InvariantCulture);

    // ---- Moderation: the calendar ----

    public async Task<AdminEventDto[]> AdminEventsAsync(CancellationToken ct)
    {
        DateTime from = DateTime.UtcNow.AddDays(-2);
        return (await _db.WorldEvents.AsNoTracking().Where(e => e.EndsUtc > from).OrderBy(e => e.StartsUtc).Take(100).ToListAsync(ct))
            .Select(e => new AdminEventDto(e.Id, e.Kind.ToString(), WorldEvents.Def(e.Kind)?.Name ?? e.Kind.ToString(), e.StartsUtc, e.EndsUtc,
                _bells.ToLocal(e.StartsUtc).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                _bells.ToLocal(e.EndsUtc).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), e.Weekly, e.Cancelled, e.Announced, e.By))
            .ToArray();
    }

    /// <summary>A moderator puts an event on the calendar: its kind, its start in server time ("2026-10-10 20:00") and hours.</summary>
    public async Task AdminAddEventAsync(string admin, AdminEventRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse(request.Kind, out WorldEventKind kind) || WorldEvents.Def(kind) == null)
            throw new GameException("bad_event", "Choose an event.");
        if (!DateTime.TryParseExact(request.StartsLocal?.Replace('T', ' '), "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime local))
            throw new GameException("bad_event", "Give the start as 2026-10-10 20:00 (server time).");
        if (request.Hours < 1 || request.Hours > WorldEvents.MaxHours)
            throw new GameException("bad_event", $"An event lasts 1 to {WorldEvents.MaxHours} hours.");
        DateTime startsUtc = _bells.ToUtc(local), endsUtc = startsUtc.AddHours(request.Hours);
        if (endsUtc <= DateTime.UtcNow) throw new GameException("bad_event", "That time has passed.");
        if (await _db.WorldEvents.AnyAsync(e => e.Kind == kind && e.StartsUtc == startsUtc, ct))
            throw new GameException("bad_event", "That event already starts then.");
        _db.WorldEvents.Add(new WorldEvent { Kind = kind, StartsUtc = startsUtc, EndsUtc = endsUtc, By = Clip(admin, 40) });
        _db.AdminActions.Add(new AdminAction { Utc = DateTime.UtcNow, Admin = admin, Action = "event", Target = kind.ToString(), Detail = $"{local:yyyy-MM-dd HH:mm} for {request.Hours} h" });
        await SaveAsync(ct);
        await _events.ReloadAsync(_db, ct);
    }

    /// <summary>Calls an event off, or back on.</summary>
    public async Task AdminCancelEventAsync(string admin, long id, bool cancelled, CancellationToken ct)
    {
        WorldEvent e = await _db.WorldEvents.FindAsync(new object[] { id }, ct) ?? throw new GameException("bad_event", "No such event.");
        e.Cancelled = cancelled;
        _db.AdminActions.Add(new AdminAction { Utc = DateTime.UtcNow, Admin = admin, Action = cancelled ? "event-off" : "event-on", Target = e.Kind.ToString(),
            Detail = _bells.ToLocal(e.StartsUtc).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) });
        await SaveAsync(ct);
        await _events.ReloadAsync(_db, ct);
    }

    /// <summary>Development: an event of this kind starts now for <paramref name="minutes"/> (smoke test, screenshots).</summary>
    public async Task<StateDto> DevEventAsync(Account account, string kindName, int minutes, CancellationToken ct)
    {
        if (!Enum.TryParse(kindName, out WorldEventKind kind) || WorldEvents.Def(kind) == null)
            throw new GameException("bad_event", "Choose an event.");
        DateTime now = DateTime.UtcNow;
        _db.WorldEvents.Add(new WorldEvent { Kind = kind, StartsUtc = now, EndsUtc = now.AddMinutes(Math.Clamp(minutes, 1, 24 * 60)), By = "dev" });
        await SaveAsync(ct);
        await _events.ReloadAsync(_db, ct);
        _forge.LuckBp = _events.ForgeLuckBp(now);
        return ToState(account);
    }
}
