using Microsoft.EntityFrameworkCore;
using Orsuun.Rules;
using Orsuun.Server.Data;

namespace Orsuun.Server.Game;

/// <summary>
/// The Campaign Trail (Rules.CampaignTrail; GDD section 9): each character's 8 week season of 50 tiers, climbed with the
/// XP bounties pay, a free track and a paid track bought with the account's Amber.
/// </summary>
public sealed partial class GameService
{
    /// <summary>This character's Trail as it stands (read only: a Trail from a past season shows as a fresh one, with its unclaimed rewards counted).</summary>
    private TrailDto TrailOf(Account a)
    {
        DateTime local = _bells.LocalNow;
        TrailSeason season = CampaignTrail.Season(local);
        TrailProgress p = TrailProgress.Parse(a.Trail);
        int owed = p.Roll(season.Number, out _).Count;
        return new TrailDto(season.Number, season.Name, CampaignTrail.SecondsLeft(local), p.Xp, p.Tier, p.XpIntoTier, (int)p.Pass,
            p.FreeClaimed, p.PaidClaimed, owed);
    }

    /// <summary>
    /// This character's Trail moved to the current season. A season left behind hands over the rewards it had ready and
    /// unclaimed rather than let them lapse. Writes account.Trail; the caller saves.
    /// </summary>
    private TrailProgress RollTrail(Account a, out int handedOver)
    {
        DateTime local = _bells.LocalNow;
        TrailProgress p = TrailProgress.Parse(a.Trail);
        List<(int Tier, bool Paid)> owed = p.Roll(CampaignTrail.Season(local).Number, out int old);
        handedOver = owed.Count;
        if (owed.Count > 0) GrantTrail(a, owed, CampaignTrail.Season(old), local, "trail-season-end", Guid.NewGuid().ToString("N"));
        a.Trail = p.Serialize();
        return p;
    }

    private void GrantTrail(Account a, List<(int Tier, bool Paid)> claimed, TrailSeason season, DateTime local, string kind, string requestId)
    {
        var inventory = Snapshot(a);
        int days = CampaignTrail.PieceDays(season, local);
        foreach (var (tier, paid) in claimed) CampaignTrail.Reward(tier, paid, season).GrantTo(inventory, days);
        Apply(a, inventory);
        _db.Ledger.Add(Entry(a.Id, null, kind, $"season={season.Number} free {Ranges(claimed, false)} paid {Ranges(claimed, true)}", 0, requestId));
    }

    /// <summary>A track's claimed tiers as ranges ("1-12,15"), short enough for the ledger however many are claimed at once.</summary>
    private static string Ranges(List<(int Tier, bool Paid)> claimed, bool paid)
    {
        var tiers = claimed.Where(c => c.Paid == paid).Select(c => c.Tier).OrderBy(t => t).ToList();
        var parts = new List<string>();
        for (int i = 0; i < tiers.Count; i++)
        {
            int start = tiers[i];
            while (i + 1 < tiers.Count && tiers[i + 1] == tiers[i] + 1) i++;
            parts.Add(start == tiers[i] ? start.ToString() : start + "-" + tiers[i]);
        }
        return parts.Count == 0 ? "-" : string.Join(",", parts);
    }

    public async Task<StateDto> TrailClaimAsync(Account account, TrailClaimRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        if (request.Tier < 0 || request.Tier > CampaignTrail.Tiers) throw new GameException("no_tier", "No such tier.");
        TrailProgress p = RollTrail(account, out int handedOver);
        List<(int Tier, bool Paid)> claimed = p.ClaimReady(request.Tier);
        if (claimed.Count == 0 && handedOver == 0) throw new GameException("nothing_ready", "Nothing to claim there yet.");
        account.Trail = p.Serialize();
        if (claimed.Count > 0) GrantTrail(account, claimed, CampaignTrail.Season(p.Season), _bells.LocalNow, "trail-claim", request.RequestId);
        await SaveAsync(ct);
        return ToState(account);
    }

    /// <summary>The paid track (650 Amber) or Trail Plus (1,400, ten tiers more; 750 from the Trail), for this character.</summary>
    public async Task<StateDto> TrailBuyAsync(Account account, TrailBuyRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        TrailProgress p = RollTrail(account, out _);
        TrailPass to = request.Plus ? TrailPass.Plus : TrailPass.Trail;
        int price = CampaignTrail.Price(p.Pass, to);
        if (price < 0) throw new GameException("bought", p.Pass == TrailPass.Plus ? "This hero has Trail Plus already." : "This hero has the Trail already.");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        Login login = await LockLoginAsync(ct);
        if (login.Amber < price) throw new GameException("no_amber", "Not enough Amber.");
        login.Amber -= price;
        p.Buy(to);
        account.Trail = p.Serialize();
        _db.Ledger.Add(Entry(account.Id, null, "trail-buy", $"season={p.Season} {to} for {price} Amber", 0, request.RequestId));
        await SaveAsync(ct);
        await tx.CommitAsync(ct);
        return ToState(account);
    }

    /// <summary>
    /// Development only: adds Trail XP, and with <paramref name="lastSeason"/> moves this Trail back a season so the next
    /// claim hands its ready rewards over.
    /// </summary>
    public async Task<StateDto> DevTrailAsync(Account account, int xp, bool lastSeason, CancellationToken ct)
    {
        TrailProgress p = RollTrail(account, out _);
        p.AddXp(xp);
        string stored = p.Serialize();
        if (lastSeason) stored = (p.Season - 1) + stored[stored.IndexOf('|')..];
        account.Trail = stored;
        await SaveAsync(ct);
        return ToState(account);
    }

    /// <summary>
    /// The login row, locked for this transaction (FOR UPDATE, then reloaded): Amber is shared by the login's characters,
    /// so two of them spending at once queue instead of both spending the same Amber.
    /// </summary>
    private async Task<Login> LockLoginAsync(CancellationToken ct)
    {
        Guid id = _login?.Id ?? throw new GameException("no_login", "Sign in again.");
        Login login = (await _db.Logins.FromSql($@"SELECT * FROM ""Logins"" WHERE ""Id"" = {id} FOR UPDATE").ToListAsync(ct)).FirstOrDefault()
            ?? throw new GameException("no_login", "Sign in again.");
        await _db.Entry(login).ReloadAsync(ct);
        _login = login;
        return login;
    }
}
