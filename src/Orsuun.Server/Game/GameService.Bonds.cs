using Microsoft.EntityFrameworkCore;
using Orsuun.Rules;
using Orsuun.Rules.Combat;
using Orsuun.Server.Data;

namespace Orsuun.Server.Game;

/// <summary>
/// Sworn bonds (owner, 7 Oct 2026; Rules.Bonds). A bond is the partner's id on both heroes' rows. The hero's own row is
/// tracked; the other hero's changes in single conditional UPDATEs (so two heroes swearing at once cannot both win), the
/// pair's chat line is channel "b:" + both ids in order, and the bonus reaches only the live hunt (the heartbeat's XP).
/// </summary>
public sealed partial class GameService
{
    /// <summary>The pair's chat line: "b:" and the first half of each id in order (a channel holds 40 characters).</summary>
    private static string BondChannel(Guid a, Guid b) =>
        "b:" + (a.CompareTo(b) < 0 ? a.ToString("N")[..16] + b.ToString("N")[..16] : b.ToString("N")[..16] + a.ToString("N")[..16]);

    private static bool IsBondChannel(string channel) => channel.StartsWith("b:", StringComparison.Ordinal);

    private static bool BondAskWaiting(Account account, DateTime now) =>
        account.BondAskFrom != null && account.BondAskUtc > now.AddMinutes(-Bonds.AskMinutes);

    /// <summary>Seconds before the hero may swear another bond (after one ended), or 0.</summary>
    private static long BondWait(Account account, DateTime now) =>
        account.BondEndedUtc is DateTime ended ? Math.Max(0, (long)(ended.AddDays(Bonds.RebondDays) - now).TotalSeconds) : 0;

    private static BondBriefDto? BondBriefOf(Account account)
    {
        bool asked = BondAskWaiting(account, DateTime.UtcNow);
        if (account.BondPartnerId == null && !asked) return null;
        return new BondBriefDto(account.BondPartnerId ?? Guid.Empty, account.BondPartnerName, Bonds.RingLevel(account.BondSeconds),
            asked ? account.BondAskName ?? "" : "");
    }

    /// <summary>The hunt's XP bonus while the partner hunts beside the hero (basis points), and whether they do.</summary>
    private async Task<(int Bp, bool Together)> BondBonusAsync(Account account, DateTime now, CancellationToken ct)
    {
        if (account.BondPartnerId is not Guid partner || account.AtRiver) return (0, false);
        DateTime since = now.AddSeconds(-Parties.PresentSeconds);
        var them = await _db.Accounts.AsNoTracking().Where(a => a.Id == partner)
            .Select(a => new { a.PartyLeaderId, a.ParkedStage, a.LastHeartbeatUtc, a.AtRiver }).SingleOrDefaultAsync(ct);
        if (them == null) return (0, false);
        bool together = Bonds.Together(account.PartyLeaderId, account.ParkedStage, them.PartyLeaderId, them.ParkedStage,
            them.LastHeartbeatUtc > since && !them.AtRiver);
        return together ? (Bonds.XpBonusBp(Bonds.RingLevel(account.BondSeconds)), true) : (0, false);
    }

    /// <summary>The hero's bond card.</summary>
    public async Task<BondDto> BondAsync(Account account, CancellationToken ct, string? message = null)
    {
        DateTime now = DateTime.UtcNow;
        bool asked = BondAskWaiting(account, now);
        Guid askFrom = asked ? account.BondAskFrom!.Value : Guid.Empty;
        string askName = asked ? account.BondAskName ?? "" : "";
        int ring = Bonds.RingLevel(account.BondSeconds);
        if (account.BondPartnerId is not Guid partner)
            return new BondDto(Guid.Empty, "", HeroClass.Vanguard, 0, false, false, null, 0, 0, "", -1, 0, askFrom, askName, BondWait(account, now), message);
        var them = await _db.Accounts.AsNoTracking().Where(a => a.Id == partner)
            .Select(a => new { a.Name, a.Class, a.Xp, a.LastHeartbeatUtc }).SingleOrDefaultAsync(ct);
        (int bp, bool together) = await BondBonusAsync(account, now, ct);
        bool online = them != null && them.LastHeartbeatUtc > now - OnlineGrace;
        return new BondDto(partner, them == null ? account.BondPartnerName : ShownName(partner, them.Name), them?.Class ?? HeroClass.Vanguard,
            them == null ? 0 : Content.LevelFor(them.Xp), online, together, account.BondSinceUtc, account.BondSeconds, ring, Bonds.RingName(ring),
            Bonds.NextRingSeconds(ring), Bonds.XpBonusBp(ring), askFrom, askName, 0, message);
    }

    /// <summary>Asks a friend to swear a bond: both of the level, neither bonded, not one of the hero's own, not blocked.</summary>
    public async Task<BondDto> BondAskAsync(Account account, BondAskRequest request, CancellationToken ct)
    {
        DateTime now = DateTime.UtcNow;
        if (account.BondPartnerId != null) throw new GameException("bonded", "You are sworn to a bond already.");
        if (Content.LevelFor(account.Xp) < Bonds.MinLevel) throw new GameException("low_level", $"Bonds are sworn from level {Bonds.MinLevel}.");
        if (BondWait(account, now) > 0) throw new GameException("bond_wait", $"After a bond ends, {Bonds.RebondDays} days pass before another.");
        HeroRef other = await FindHeroAsync(request.AccountId, request.Name, ct);
        if (other.Id == account.Id) throw new GameException("self", "That is you.");
        if (other.LoginId == account.LoginId) throw new GameException("own_hero", "That is one of your own heroes.");
        if (Blocks(account.Blocked ?? "", other.Id) || Blocks(other.Blocked, account.Id)) throw new GameException("blocked", "You cannot ask them.");
        bool friend = await _db.Friendships.AnyAsync(f => f.Accepted && ((f.FromId == account.Id && f.ToId == other.Id) || (f.FromId == other.Id && f.ToId == account.Id)), ct);
        if (!friend) throw new GameException("not_friend", "Only a friend can be asked to swear a bond.");
        var them = await _db.Accounts.AsNoTracking().Where(a => a.Id == other.Id).Select(a => new { a.Xp, a.BondPartnerId, a.BondEndedUtc }).SingleAsync(ct);
        if (Content.LevelFor(them.Xp) < Bonds.MinLevel) throw new GameException("their_level", $"{other.Name} is below level {Bonds.MinLevel}.");
        if (them.BondPartnerId != null) throw new GameException("their_bond", $"{other.Name} is sworn to a bond already.");
        string name = NameOf(account);
        int asked = await _db.Accounts.Where(a => a.Id == other.Id && a.BondPartnerId == null).ExecuteUpdateAsync(s => s
            .SetProperty(a => a.BondAskFrom, account.Id).SetProperty(a => a.BondAskName, name).SetProperty(a => a.BondAskUtc, now), ct);
        if (asked == 0) throw new GameException("their_bond", $"{other.Name} is sworn to a bond already.");
        SendLetter(other.Id, "bond", name, $"{name} asks you to swear a bond",
            $"{name} asks you to swear a bond: hunt together in a party for more XP, and a Bond Ring that grows with every hour. Answer on BOND in FRIENDS within {Bonds.AskMinutes} minutes.");
        await SaveAsync(ct);
        return await BondAsync(account, ct, $"You asked {other.Name} to swear a bond.");
    }

    /// <summary>Answers a bond's ask: yes swears it on both rows (the asker's only if still free), no lets it go.</summary>
    public async Task<BondDto> BondAnswerAsync(Account account, BondAnswerRequest request, CancellationToken ct)
    {
        DateTime now = DateTime.UtcNow;
        if (!BondAskWaiting(account, now)) throw new GameException("no_ask", "No one asks you to swear a bond now.");
        Guid asker = account.BondAskFrom!.Value;
        string askerName = account.BondAskName ?? "";
        account.BondAskFrom = null;
        account.BondAskName = null;
        account.BondAskUtc = null;
        if (!request.Accept)
        {
            SendLetter(asker, "bond", NameOf(account), $"{NameOf(account)} declined your bond", $"{NameOf(account)} did not swear the bond you asked for.");
            await SaveAsync(ct);
            return await BondAsync(account, ct, "You let the ask go.");
        }
        if (account.BondPartnerId != null) throw new GameException("bonded", "You are sworn to a bond already.");
        if (Content.LevelFor(account.Xp) < Bonds.MinLevel) throw new GameException("low_level", $"Bonds are sworn from level {Bonds.MinLevel}.");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        string mine = NameOf(account);
        int sworn = await _db.Accounts.Where(a => a.Id == asker && a.BondPartnerId == null).ExecuteUpdateAsync(s => s
            .SetProperty(a => a.BondPartnerId, account.Id).SetProperty(a => a.BondPartnerName, mine).SetProperty(a => a.BondSinceUtc, now)
            .SetProperty(a => a.BondSeconds, 0L), ct);
        if (sworn == 0)
        {
            await SaveAsync(ct);
            await tx.CommitAsync(ct);
            return await BondAsync(account, ct, $"{askerName} is sworn to another bond now.");
        }
        account.BondPartnerId = asker;
        account.BondPartnerName = askerName;
        account.BondSinceUtc = now;
        account.BondSeconds = 0;
        SystemLine(BondChannel(account.Id, asker), $"{askerName} and {mine} swore a bond.");
        SystemLine(Chat.World, $"{askerName} and {mine} have sworn a bond under the Eternal Sky.");
        SendLetter(asker, "bond", mine, $"{mine} swore your bond", $"{mine} and you are sworn now. Hunt together in a party for more XP, and your Bond Ring grows with every hour together.");
        _db.Ledger.Add(Entry(account.Id, null, "bond", $"sworn={asker}", 0, "bond:" + Guid.NewGuid().ToString("N")));
        await SaveAsync(ct);
        await tx.CommitAsync(ct);
        return await BondAsync(account, ct, $"You and {askerName} are sworn.");
    }

    /// <summary>Breaks the hero's bond on both rows; each waits RebondDays before another.</summary>
    public async Task<BondDto> BondBreakAsync(Account account, CancellationToken ct)
    {
        if (account.BondPartnerId is not Guid partner) throw new GameException("no_bond", "You are not sworn to a bond.");
        await BreakBondCoreAsync(account, partner, ct);
        await SaveAsync(ct);
        return await BondAsync(account, ct, "The bond is broken.");
    }

    private async Task BreakBondCoreAsync(Account account, Guid partner, CancellationToken ct)
    {
        DateTime now = DateTime.UtcNow;
        await _db.Accounts.Where(a => a.Id == partner && a.BondPartnerId == account.Id).ExecuteUpdateAsync(s => s
            .SetProperty(a => a.BondPartnerId, (Guid?)null).SetProperty(a => a.BondPartnerName, "").SetProperty(a => a.BondSinceUtc, (DateTime?)null)
            .SetProperty(a => a.BondSeconds, 0L).SetProperty(a => a.BondEndedUtc, now), ct);
        string mine = NameOf(account);
        SendLetter(partner, "bond", mine, $"{mine} broke your bond", $"{mine} broke the bond between you. After {Bonds.RebondDays} days you may swear another.");
        account.BondPartnerId = null;
        account.BondPartnerName = "";
        account.BondSinceUtc = null;
        account.BondSeconds = 0;
        account.BondEndedUtc = now;
    }
}
