namespace Orsuun.Server.Game;

/// <summary>
/// The server's own clock for the shared world, every 30 seconds: pairs the guilds signed up for a war night once it
/// begins, settles wars whose hour is up, and moves the fortress keeps through their week (bids close Sunday 20:00,
/// the keep siege settles an hour later). Status reads never do this work; every step takes its own row locks.
/// </summary>
public sealed class WorldClock : BackgroundService
{
    private static readonly TimeSpan Every = TimeSpan.FromSeconds(30);
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<WorldClock> _log;

    public WorldClock(IServiceScopeFactory scopes, ILogger<WorldClock> log)
    {
        _scopes = scopes;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        using var timer = new PeriodicTimer(Every);
        do
        {
            try
            {
                using IServiceScope scope = _scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<GameService>().TickWorldAsync(stop);
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "World clock tick failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stop));
    }
}
