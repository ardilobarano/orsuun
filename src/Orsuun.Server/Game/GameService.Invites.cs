using Microsoft.EntityFrameworkCore;
using Orsuun.Rules;
using Orsuun.Server.Data;

namespace Orsuun.Server.Game;

// Invite a friend (owner, 28 Sep 2026; Rules.Invites): each hero's code, entered by a new hero of another login before
// level 10; when the invited hero reaches level 10 both are paid by letter (sorn and a Scroll of Mercy, never Amber).
public sealed partial class GameService
{
    /// <summary>Heroes that reached the invite's level in this request: paid just before the save (PayInvitesAsync).</summary>
    private readonly List<Account> _invitesDue = new();

    /// <summary>Called wherever XP is written back (Apply): an invited hero at level 10 is paid with the next save.</summary>
    private void CheckInvite(Account a)
    {
        if (a.InvitedById != null && !a.InviteRewarded && Content.LevelFor(a.Xp) >= Invites.RewardLevel && !_invitesDue.Contains(a))
            _invitesDue.Add(a);
    }

    private async Task PayInvitesAsync(CancellationToken ct)
    {
        if (_invitesDue.Count == 0) return;
        List<Account> due = _invitesDue.ToList();
        _invitesDue.Clear();
        foreach (Account a in due)
        {
            if (a.InviteRewarded || a.InvitedById is not Guid inviterId) continue;
            a.InviteRewarded = true;
            var inviter = await _db.Accounts.AsNoTracking().Where(x => x.Id == inviterId)
                .Select(x => new { x.Id, x.Name, x.HighestStageCleared, x.BannedUtc }).FirstOrDefaultAsync(ct);
            SendLetter(a.Id, "invite", "The Caravan", "A friend's welcome",
                $"You reached level {Invites.RewardLevel} on a friend's invitation"
                + (inviter != null ? $" ({ShownName(inviter.Id, inviter.Name)})" : "") + ". Here is your share.",
                sorn: Invites.Sorn(a.HighestStageCleared), goodId: TradeGoods.ScrollOfMercy, goodCount: Invites.ScrollsOfMercy);
            if (inviter == null || inviter.BannedUtc != null) continue;
            SendLetter(inviter.Id, "invite", "The Caravan", "Your friend reached level " + Invites.RewardLevel,
                $"{NameOf(a)} came on your invitation and has reached level {Invites.RewardLevel}. Here is your share.",
                sorn: Invites.Sorn(inviter.HighestStageCleared), goodId: TradeGoods.ScrollOfMercy, goodCount: Invites.ScrollsOfMercy);
        }
    }

    /// <summary>The invite screen: this hero's code (made on first ask), who it brought in, and the code this hero entered.</summary>
    public async Task<InviteDto> InviteAsync(Account account, CancellationToken ct) => await InviteViewAsync(account, "", ct);

    private async Task<InviteDto> InviteViewAsync(Account account, string message, CancellationToken ct)
    {
        if (account.InviteCode.Length == 0)
        {
            for (int attempt = 0; ; attempt++)
            {
                string code = Invites.NewCode(_rng);
                if (await _db.Accounts.AnyAsync(a => a.InviteCode == code, ct))
                {
                    if (attempt > 20) throw new GameException("busy", "Try again in a moment.");
                    continue;
                }
                account.InviteCode = code;
                try { await SaveAsync(ct); break; }
                catch (DbUpdateException) when (attempt < 20)
                {
                    // Another hero took the same code between the check and the save.
                    account.InviteCode = "";
                    _db.ChangeTracker.Clear();
                    account = await _db.Accounts.FirstAsync(a => a.Id == account.Id, ct);
                }
            }
        }
        var brought = await _db.Accounts.AsNoTracking().Where(a => a.InvitedById == account.Id)
            .Select(a => new { a.InviteRewarded }).ToListAsync(ct);
        string invitedBy = "";
        if (account.InvitedById is Guid by)
        {
            var inviter = await _db.Accounts.AsNoTracking().Where(a => a.Id == by).Select(a => new { a.Id, a.Name }).FirstOrDefaultAsync(ct);
            invitedBy = inviter != null ? ShownName(inviter.Id, inviter.Name) : "a hero who has gone";
        }
        bool canEnter = account.InvitedById == null && Content.LevelFor(account.Xp) < Invites.RewardLevel;
        return new InviteDto(account.InviteCode, brought.Count, brought.Count(b => b.InviteRewarded), Invites.MaxInvited, Invites.RewardLevel,
            Invites.Sorn(account.HighestStageCleared), Invites.ScrollsOfMercy, invitedBy, account.InviteRewarded, canEnter, message);
    }

    /// <summary>Enters a friend's code: a hero below level 10, once, from another login, while the code has room.</summary>
    public async Task<InviteDto> EnterInviteAsync(Account account, InviteRequest request, CancellationToken ct)
    {
        string code = Invites.Clean(request.Code);
        if (account.InvitedById != null) throw new GameException("invite_taken", "This hero has already entered a friend's code.");
        if (Content.LevelFor(account.Xp) >= Invites.RewardLevel)
            throw new GameException("invite_late", $"A friend's code is entered before level {Invites.RewardLevel}.");
        if (!Invites.Valid(code)) throw new GameException("invite_code", "That code could not be read: it has six letters and numbers.");
        Account? inviter = await _db.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.InviteCode == code, ct);
        if (inviter == null || inviter.BannedUtc != null) throw new GameException("invite_code", "No hero has that code.");
        if (inviter.Id == account.Id || inviter.LoginId == account.LoginId)
            throw new GameException("invite_self", "That code is your own account's. Share it with a friend.");
        if (await _db.Accounts.CountAsync(a => a.InvitedById == inviter.Id, ct) >= Invites.MaxInvited)
            throw new GameException("invite_full", "That code has brought in all the heroes it can.");
        account.InvitedById = inviter.Id;
        await SaveAsync(ct);
        return await InviteViewAsync(account, $"Code taken: reach level {Invites.RewardLevel} and you and {NameOf(inviter)} are both paid.", ct);
    }
}
