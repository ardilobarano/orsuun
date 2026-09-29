using Orsuun.Rules;
using Orsuun.Server.Data;

namespace Orsuun.Server.Game;

// Trail caches (owner, 29 Sep 2026; Rules.TrailCaches): a chest beside a big map's trail, rolled here when opened.
public sealed partial class GameService
{
    /// <summary>Today's caches opened and the last one's time (a new bounty day starts at none).</summary>
    private (int Opened, DateTime? Last) CachesOf(Account account) =>
        account.CacheDay == Bounties.DayKey(_bells.LocalNow) ? (account.CachesToday, account.CacheOpenedUtc) : (0, null);

    /// <summary>Seconds until the hero's next cache (0: one lies on the trail now), or -1 when today's are opened.</summary>
    private long CacheIn(Account account)
    {
        (int opened, DateTime? last) = CachesOf(account);
        return TrailCaches.SecondsToNext(opened, last, DateTime.UtcNow);
    }

    /// <summary>Opens the cache lying on the trail: the server rolls what it holds, pays it and counts it for the bounty.</summary>
    public async Task<CacheOpenDto> OpenCacheAsync(Account account, CacheOpenRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        if (account.AtRiver) throw new GameException("at_river", "No caches lie by the river.");
        (int opened, DateTime? last) = CachesOf(account);
        long wait = TrailCaches.SecondsToNext(opened, last, DateTime.UtcNow);
        if (wait < 0) throw new GameException("no_cache", "Today's caches are all opened: more come with the evening bell.");
        if (wait > 0) throw new GameException("no_cache", "The next cache is not on the trail yet.");
        DailyReward reward = TrailCaches.Roll(_rng, account.HighestStageCleared);
        Inventory inventory = Snapshot(account);
        reward.GrantTo(inventory);
        Apply(account, inventory);
        account.CacheDay = Bounties.DayKey(_bells.LocalNow);
        account.CachesToday = opened + 1;
        account.CacheOpenedUtc = DateTime.UtcNow;
        Count(account, BountyMetric.CachesOpened, 1);
        _db.Ledger.Add(Entry(account.Id, null, "cache", reward.Text, reward.Sorn, request.RequestId));
        await SaveAsync(ct);
        return new CacheOpenDto(ToState(account), reward.Text);
    }
}
