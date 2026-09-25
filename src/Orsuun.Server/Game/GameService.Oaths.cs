using Microsoft.EntityFrameworkCore;
using Orsuun.Rules;
using Orsuun.Server.Data;

namespace Orsuun.Server.Game;

/// <summary>
/// Changing Banners and Oath Renewal (owner, 25 Sep 2026: "Banner change and Oath Renewal"). The Banner is the
/// account's: a change is paid in Oathstones by the hero who swears anew, once a War season, under the login row lock
/// (four heroes share it); every hero of the account follows. Oath Renewal (GDD section 12) sends a level 105 hero back
/// to level 1 for a lasting +3% attack and HP; its time is settled first and the lane gets a fresh seed, as with a
/// change of class, so no loop of the old hero is judged against the new one.
/// </summary>
public sealed partial class GameService
{
    public async Task<StateDto> ChangeBannerAsync(Account account, BannerChangeRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        if (!Enum.IsDefined(request.Banner)) throw new GameException("bad_banner", "Choose one of the three Banners.");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        Login login = await LockLoginAsync(ct);
        string season = Season();
        Banner old = login.Banner != Banner.None ? login.Banner : account.Banner;
        if (Banners.ChangeProblem(old, request.Banner, login.BannerChangedSeason, season, account.Oathstones) is string problem)
            throw new GameException("banner_change", problem);
        DateTime now = DateTime.UtcNow;
        account.Oathstones -= Banners.ChangeOathstones;
        login.Banner = request.Banner;
        login.SwornUtc = now;
        login.BannerChangedSeason = season;
        account.Banner = request.Banner;
        account.SwornUtc = now;
        await _db.Accounts.Where(a => a.LoginId == login.Id && a.Id != account.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Banner, request.Banner).SetProperty(a => a.SwornUtc, now), ct);
        SystemLine(Chat.World, $"{NameOf(account)} left the {Banners.Def(old).Name} and rides under the {Banners.Def(request.Banner).Name} now.");
        _db.Ledger.Add(Entry(account.Id, null, "banner-change", $"{old}->{request.Banner} season={season} oathstones={Banners.ChangeOathstones}", 0, request.RequestId));
        await SaveAsync(ct);
        await tx.CommitAsync(ct);
        return ToState(account);
    }

    public async Task<StateDto> RenewAsync(Account account, RenewRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        if (OathRenewal.Problem(Content.LevelFor(account.Xp), account.Renewals) is string problem) throw new GameException("renewal", problem);
        DateTime now = DateTime.UtcNow;
        SettlementDto settlement = Settle(account, now);
        account.LastHeartbeatUtc = now;
        account.Xp = 0;
        account.Renewals += 1;
        NewLane(account);
        SystemLine(Chat.World, $"{NameOf(account)} renewed the oath ({account.Renewals}/{OathRenewal.MaxRenewals}) and rides again from level 1.");
        _db.Ledger.Add(Entry(account.Id, null, "oath-renewal", $"renewals={account.Renewals}", 0, request.RequestId));
        await SaveAsync(ct);
        return ToState(account, settlement: settlement);
    }

    /// <summary>Playtest only: sets the hero's level (for Oath Renewal).</summary>
    public async Task<StateDto> DevLevelAsync(Account account, int level, CancellationToken ct)
    {
        int l = Math.Max(1, Math.Min(Content.MaxLevel, level));
        account.Xp = Content.XpPerLevelSquare * l * l;
        await SaveAsync(ct);
        return ToState(account);
    }
}
