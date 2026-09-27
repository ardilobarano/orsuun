using Orsuun.Rules;
using Orsuun.Server.Data;

namespace Orsuun.Server.Game;

/// <summary>
/// The daily login calendar (owner, 27 Sep 2026: "Daily login rewards"; Rules.DailyLogin): one claim a bounty day per
/// login, shared by its characters like Amber, so the claim locks the login row; the gift goes to the character
/// claiming it.
/// </summary>
public sealed partial class GameService
{
    private DailyDto? DailyOf(Account account)
    {
        if (_login == null) return null;
        DateTime local = _bells.LocalNow;
        bool claimable = _login.DailyClaimedOn != Bounties.DayKey(local);
        int day = claimable ? DailyLogin.NextDay(_login.DailyDay) : Math.Max(1, _login.DailyDay);
        string[] gifts = Enumerable.Range(1, DailyLogin.Days).Select(d => DailyLogin.Reward(d, account.HighestStageCleared).Text).ToArray();
        long toNext = (long)Math.Max(0, (Bounties.DayStart(local).AddDays(1) - local).TotalSeconds);
        return new DailyDto(day, claimable, gifts, toNext);
    }

    public async Task<StateDto> ClaimDailyAsync(Account account, DailyClaimRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        Login login = await LockLoginAsync(ct);
        string today = Bounties.DayKey(_bells.LocalNow);
        if (login.DailyClaimedOn == today) throw new GameException("claimed", "Today's gift is taken: the next comes with the evening bell.");
        int day = DailyLogin.NextDay(login.DailyDay);
        DailyReward reward = DailyLogin.Reward(day, account.HighestStageCleared);
        Inventory inventory = Snapshot(account);
        reward.GrantTo(inventory);
        Apply(account, inventory);
        login.DailyDay = day;
        login.DailyClaimedOn = today;
        Feat(account, FeatMetric.GiftsClaimed, 1);
        _db.Ledger.Add(Entry(account.Id, null, "daily", $"day={day} {reward.Text}", reward.Sorn, request.RequestId));
        await SaveAsync(ct);
        await tx.CommitAsync(ct);
        return ToState(account);
    }
}
