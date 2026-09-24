using Orsuun.Rules;

namespace Orsuun.Server.Game;

/// <summary>Server-local time for Evening Bells. Configured by Bells:TimeZone; ORSUUN_FORCE_BELL overrides in Development.</summary>
public sealed class BellClock
{
    private readonly TimeZoneInfo _zone;
    private readonly Bell? _forced;

    public BellClock(IConfiguration config, IHostEnvironment env)
    {
        string id = config["Bells:TimeZone"] ?? "Europe/Istanbul";
        _zone = Find(id) ?? Find("Turkey Standard Time") ?? TimeZoneInfo.Utc;

        string? forced = env.IsDevelopment() ? Environment.GetEnvironmentVariable("ORSUUN_FORCE_BELL") : null;
        if (!string.IsNullOrEmpty(forced) && Enum.TryParse(forced, true, out Bell bell)) _forced = bell;
    }

    public DateTime LocalNow => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _zone);

    /// <summary>A server-local time (war nights, keep sieges) as UTC.</summary>
    public DateTime ToUtc(DateTime local) => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), _zone);

    public Bell Active => _forced ?? EveningBells.Active(LocalNow);

    public BellDto Dto()
    {
        DateTime now = LocalNow;
        Bell active = Active;
        Bell next = EveningBells.Next(now, out int minutes);
        return new BellDto(active, EveningBells.Name(active), next, minutes, now.ToString("HH:mm"));
    }

    private static TimeZoneInfo? Find(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException) { return null; }
        catch (InvalidTimeZoneException) { return null; }
    }
}
