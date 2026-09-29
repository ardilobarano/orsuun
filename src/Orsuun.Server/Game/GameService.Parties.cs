using Microsoft.EntityFrameworkCore;
using Orsuun.Rules;
using Orsuun.Server.Data;

namespace Orsuun.Server.Game;

// Hunting parties (owner, 29 Sep 2026; Rules.Parties): a party is its leader's id on each member's row. Membership changes
// run in a transaction that locks the leader's row; other heroes' fields change through single UPDATE statements.
public sealed partial class GameService
{
    /// <summary>The hero's party: its members (online, hunting alongside), the bonus now, and an invite waiting.</summary>
    public async Task<PartyDto> PartyAsync(Account account, CancellationToken ct)
    {
        DateTime now = DateTime.UtcNow;
        List<PartyMemberDto> members = new();
        int together = 0;
        if (account.PartyLeaderId is Guid leader)
        {
            DateTime since = now.AddSeconds(-Parties.PresentSeconds);
            var rows = await _db.Accounts.AsNoTracking().Where(a => a.PartyLeaderId == leader)
                .OrderBy(a => a.PartyJoinedUtc).Select(a => new { a.Id, a.Name, a.Class, a.Xp, a.LastHeartbeatUtc, a.ParkedStage, a.AtRiver })
                .ToListAsync(ct);
            foreach (var m in rows)
            {
                bool online = m.LastHeartbeatUtc > since;
                bool alongside = m.Id != account.Id && online && !m.AtRiver && Parties.Together(m.ParkedStage, account.ParkedStage);
                if (alongside) together++;
                members.Add(new PartyMemberDto(m.Id, ShownName(m.Id, m.Name), m.Class, Content.LevelFor(m.Xp), m.Id == account.Id || online,
                    alongside, m.Id == leader, Content.StageName(m.ParkedStage)));
            }
        }
        bool invited = account.PartyInviteFrom != null && account.PartyInviteUtc > now.AddMinutes(-Parties.InviteMinutes);
        return new PartyDto(account.PartyLeaderId ?? Guid.Empty, members.ToArray(), Parties.BonusBp(together) / 100,
            invited ? account.PartyInviteFrom!.Value : Guid.Empty, invited ? account.PartyInviteName ?? "" : "", Parties.MaxMembers);
    }

    /// <summary>The hunt's bonus now: partymates online and hunting the same place (heartbeat, online only).</summary>
    private async Task<int> PartyBonusBpAsync(Account account, DateTime now, CancellationToken ct)
    {
        if (account.PartyLeaderId is not Guid leader || account.AtRiver) return 0;
        DateTime since = now.AddSeconds(-Parties.PresentSeconds);
        List<int> stages = await _db.Accounts.AsNoTracking()
            .Where(a => a.PartyLeaderId == leader && a.Id != account.Id && a.LastHeartbeatUtc > since && !a.AtRiver)
            .Select(a => a.ParkedStage).ToListAsync(ct);
        return Parties.BonusBp(stages.Count(s => Parties.Together(s, account.ParkedStage)));
    }

    /// <summary>Asks a friend or guildmate to join (their invite waits ten minutes; a newer one replaces it).</summary>
    public async Task<PartyDto> PartyInviteAsync(Account account, PartyInviteRequest request, CancellationToken ct)
    {
        HeroRef other = await FindHeroAsync(request.AccountId, request.Name, ct);
        if (other.Id == account.Id) throw new GameException("self", "That is you.");
        if (other.LoginId == account.LoginId) throw new GameException("own_hero", "That is one of your own heroes.");
        if (Blocks(account.Blocked ?? "", other.Id) || Blocks(other.Blocked, account.Id)) throw new GameException("blocked", "You cannot invite them.");
        bool guildmate = account.GuildId != null && other.GuildId == account.GuildId;
        bool friend = await _db.Friendships.AnyAsync(f => f.Accepted && ((f.FromId == account.Id && f.ToId == other.Id) || (f.FromId == other.Id && f.ToId == account.Id)), ct);
        if (!guildmate && !friend) throw new GameException("not_friend", "Only friends and guildmates can join your party.");
        Guid? theirs = await _db.Accounts.Where(a => a.Id == other.Id).Select(a => a.PartyLeaderId).SingleAsync(ct);
        if (theirs != null) throw new GameException("in_party", other.Name + " is in a party already.");
        if (account.PartyLeaderId is Guid leader && await _db.Accounts.CountAsync(a => a.PartyLeaderId == leader, ct) >= Parties.MaxMembers)
            throw new GameException("party_full", $"A party holds {Parties.MaxMembers} heroes.");
        string me = NameOf(account);
        DateTime now = DateTime.UtcNow;
        await _db.Accounts.Where(a => a.Id == other.Id).ExecuteUpdateAsync(s => s
            .SetProperty(a => a.PartyInviteFrom, (Guid?)account.Id).SetProperty(a => a.PartyInviteName, me).SetProperty(a => a.PartyInviteUtc, (DateTime?)now), ct);
        return await PartyAsync(account, ct) with { Message = other.Name + " is asked to join your party." };
    }

    /// <summary>Joins the party that asked (or turns it down).</summary>
    public async Task<PartyDto> PartyAnswerAsync(Account account, PartyAnswerRequest request, CancellationToken ct)
    {
        DateTime now = DateTime.UtcNow;
        Guid? from = account.PartyInviteFrom;
        bool fresh = from != null && account.PartyInviteUtc > now.AddMinutes(-Parties.InviteMinutes);
        account.PartyInviteFrom = null;
        account.PartyInviteName = null;
        account.PartyInviteUtc = null;
        if (!request.Accept || !fresh)
        {
            await SaveAsync(ct);
            if (request.Accept) throw new GameException("invite_gone", "That invite has lapsed.");
            return await PartyAsync(account, ct);
        }
        if (account.PartyLeaderId != null) throw new GameException("in_party", "Leave your party first.");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var inviter = await _db.Accounts.AsNoTracking().Where(a => a.Id == from!.Value).Select(a => new { a.Id, a.PartyLeaderId }).SingleOrDefaultAsync(ct)
            ?? throw new GameException("no_hero", "That hero is gone.");
        Guid leader = inviter.PartyLeaderId ?? inviter.Id;
        await LockAccountRowAsync(leader, ct);
        // The inviter's party, read under the lock: it may have filled, or the inviter may have left it.
        Guid? now_ = await _db.Accounts.Where(a => a.Id == inviter.Id).Select(a => a.PartyLeaderId).SingleAsync(ct);
        leader = now_ ?? inviter.Id;
        int count = await _db.Accounts.CountAsync(a => a.PartyLeaderId == leader, ct);
        if (count == 0)
        {
            // A party begins: the inviter leads it.
            await _db.Accounts.Where(a => a.Id == inviter.Id).ExecuteUpdateAsync(s => s
                .SetProperty(a => a.PartyLeaderId, (Guid?)inviter.Id).SetProperty(a => a.PartyJoinedUtc, (DateTime?)now), ct);
            count = 1;
        }
        if (count >= Parties.MaxMembers) throw new GameException("party_full", $"That party holds {Parties.MaxMembers} heroes already.");
        account.PartyLeaderId = leader;
        account.PartyJoinedUtc = now;
        SystemLine(PartyChannel(leader), $"{NameOf(account)} joined the party.");
        await SaveAsync(ct);
        await tx.CommitAsync(ct);
        return await PartyAsync(account, ct) with { Message = "You joined the party." };
    }

    public async Task<PartyDto> PartyLeaveAsync(Account account, CancellationToken ct)
    {
        if (account.PartyLeaderId == null) throw new GameException("no_party", "You are not in a party.");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await LeavePartyCoreAsync(account, ct);
        await SaveAsync(ct);
        await tx.CommitAsync(ct);
        return await PartyAsync(account, ct) with { Message = "You left the party." };
    }

    /// <summary>The leader sends a member away.</summary>
    public async Task<PartyDto> PartyKickAsync(Account account, PartyKickRequest request, CancellationToken ct)
    {
        if (account.PartyLeaderId != account.Id) throw new GameException("not_leader", "Only the party's leader can do that.");
        if (request.AccountId == account.Id) throw new GameException("self", "Leave the party instead.");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await LockAccountRowAsync(account.Id, ct);
        var sent = await _db.Accounts.AsNoTracking().Where(a => a.Id == request.AccountId && a.PartyLeaderId == account.Id)
            .Select(a => new { a.Id, a.Name }).SingleOrDefaultAsync(ct) ?? throw new GameException("not_member", "They are not in your party.");
        await _db.Accounts.Where(a => a.Id == sent.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.PartyLeaderId, (Guid?)null).SetProperty(a => a.PartyJoinedUtc, (DateTime?)null), ct);
        SystemLine(PartyChannel(account.Id), $"{ShownName(sent.Id, sent.Name)} was sent away.");
        // A party of one is no party.
        if (await _db.Accounts.CountAsync(a => a.PartyLeaderId == account.Id, ct) <= 1)
        {
            account.PartyLeaderId = null;
            account.PartyJoinedUtc = null;
        }
        await SaveAsync(ct);
        await tx.CommitAsync(ct);
        return await PartyAsync(account, ct) with { Message = "They left the party." };
    }

    /// <summary>Takes the hero out of its party (in the caller's transaction): a leader hands the lead to the member who
    /// joined first, and a party left with one hero ends.</summary>
    private async Task LeavePartyCoreAsync(Account account, CancellationToken ct)
    {
        if (account.PartyLeaderId is not Guid leader) return;
        await LockAccountRowAsync(leader, ct);
        account.PartyLeaderId = null;
        account.PartyJoinedUtc = null;
        await SaveAsync(ct);
        var rest = await _db.Accounts.Where(a => a.PartyLeaderId == leader && a.Id != account.Id)
            .OrderBy(a => a.PartyJoinedUtc).Select(a => new { a.Id, a.Name }).ToListAsync(ct);
        if (rest.Count <= 1)
        {
            await _db.Accounts.Where(a => a.PartyLeaderId == leader)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.PartyLeaderId, (Guid?)null).SetProperty(a => a.PartyJoinedUtc, (DateTime?)null), ct);
            return;
        }
        if (leader == account.Id)
        {
            // The lead passes, and the party's chat with it (a channel is its leader's).
            Guid heir = rest[0].Id;
            await _db.Accounts.Where(a => a.PartyLeaderId == leader).ExecuteUpdateAsync(s => s.SetProperty(a => a.PartyLeaderId, (Guid?)heir), ct);
            SystemLine(PartyChannel(heir), $"{NameOf(account)} left the party; {ShownName(heir, rest[0].Name)} leads it now.");
        }
        else SystemLine(PartyChannel(leader), $"{NameOf(account)} left the party.");
    }

    /// <summary>A party's chat channel: its leader's id (Rules.Parties). Lines there are read from when each member joined.</summary>
    private static string PartyChannel(Guid leader) => "p:" + leader.ToString("N");
    private static bool IsPartyChannel(string channel) => channel.StartsWith("p:", StringComparison.Ordinal);

    /// <summary>A partymate hunting somewhere else now says so in the party's chat.</summary>
    private void PartyMoved(Account account, int fromStage)
    {
        if (account.PartyLeaderId is not Guid leader || Parties.Place(fromStage) == Parties.Place(account.ParkedStage)) return;
        string where = Dungeons.IsFloor(account.ParkedStage) ? "a dungeon" : Content.IsZone(account.ParkedStage) ? Content.StageName(account.ParkedStage)
            : Content.MapOfStage(account.ParkedStage).Name;
        SystemLine(PartyChannel(leader), $"{NameOf(account)} now hunts {where}.");
    }

    /// <summary>Locks one hero's row for the transaction (a party's leader: membership changes queue behind it).</summary>
    private async Task LockAccountRowAsync(Guid id, CancellationToken ct) =>
        await _db.Database.ExecuteSqlAsync($@"SELECT 1 FROM ""Accounts"" WHERE ""Id"" = {id} FOR UPDATE", ct);
}
