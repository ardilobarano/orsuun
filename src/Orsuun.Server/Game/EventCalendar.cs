using Microsoft.EntityFrameworkCore;
using Orsuun.Rules;
using Orsuun.Server.Data;

namespace Orsuun.Server.Game;

/// <summary>
/// The world events on the server's calendar (Rules.WorldEvents), kept in memory: WorldClock reloads it every tick and
/// moderators' changes reload it at once, so a request never reads the table. Holds the last 30 days (a Commander clock
/// rolls forward through past rush nights) and the next 8.
/// </summary>
public sealed class EventCalendar
{
    public sealed record Entry(long Id, WorldEventKind Kind, DateTime StartsUtc, DateTime EndsUtc);

    private volatile Entry[] _events = Array.Empty<Entry>();

    public async Task ReloadAsync(GameDb db, CancellationToken ct)
    {
        DateTime now = DateTime.UtcNow, from = now.AddDays(-30), to = now.AddDays(8);
        _events = (await db.WorldEvents.AsNoTracking()
                .Where(e => !e.Cancelled && e.EndsUtc > from && e.StartsUtc < to)
                .OrderBy(e => e.StartsUtc).ToListAsync(ct))
            .Select(e => new Entry(e.Id, e.Kind, e.StartsUtc, e.EndsUtc)).ToArray();
    }

    public bool Active(WorldEventKind kind, DateTime utc)
    {
        foreach (Entry e in _events)
            if (e.Kind == kind && e.StartsUtc <= utc && utc < e.EndsUtc)
                return true;
        return false;
    }

    /// <summary>The event of a kind running now, or null.</summary>
    public Entry? Running(WorldEventKind kind, DateTime utc) => _events.FirstOrDefault(e => e.Kind == kind && e.StartsUtc <= utc && utc < e.EndsUtc);

    /// <summary>The next event of a kind to begin after now, and the last one that ended (for the fishing contest's board).</summary>
    public Entry? Next(WorldEventKind kind, DateTime utc) => _events.Where(e => e.Kind == kind && e.StartsUtc > utc).OrderBy(e => e.StartsUtc).FirstOrDefault();
    public Entry? Last(WorldEventKind kind, DateTime utc) => _events.Where(e => e.Kind == kind && e.EndsUtc <= utc).OrderByDescending(e => e.EndsUtc).FirstOrDefault();

    /// <summary>Extra Forge chance now (a lucky forge hour).</summary>
    public int ForgeLuckBp(DateTime utc) => Active(WorldEventKind.LuckyForge, utc) ? WorldEvents.ForgeLuckBp : 0;

    /// <summary>Hunting's extra sorn for [from, to): the share of it a double sorn event covers.</summary>
    public int SornBonusPercent(DateTime from, DateTime to) =>
        WorldEvents.SornBonus(from, to, _events.Where(e => e.Kind == WorldEventKind.DoubleSorn).Select(e => (e.StartsUtc, e.EndsUtc)));

    /// <summary>When a Commander that spawned at <paramref name="spawnUtc"/> spawns next (sooner in a Commander rush).</summary>
    public DateTime NextSpawn(DateTime spawnUtc, int respawnSeconds) =>
        WorldEvents.NextSpawn(spawnUtc, respawnSeconds, Active(WorldEventKind.CommanderRush, spawnUtc));

    /// <summary>What runs now and what comes next, for the HUD and the phone's notifications.</summary>
    public WorldEventDto[] Dto(DateTime now)
    {
        var list = new List<WorldEventDto>();
        foreach (Entry e in _events)
        {
            if (e.EndsUtc <= now || list.Count >= 6) continue;
            WorldEventDef? def = WorldEvents.Def(e.Kind);
            if (def == null) continue;
            bool running = e.StartsUtc <= now;
            list.Add(new WorldEventDto(e.Kind.ToString(), def.Name, def.Effect, running,
                running ? 0 : (long)(e.StartsUtc - now).TotalSeconds, (long)(e.EndsUtc - now).TotalSeconds));
        }
        return list.ToArray();
    }
}
