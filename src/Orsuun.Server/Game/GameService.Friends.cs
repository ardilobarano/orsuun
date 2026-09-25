using Microsoft.EntityFrameworkCore;
using Orsuun.Rules;
using Orsuun.Rules.Combat;
using Orsuun.Server.Data;

namespace Orsuun.Server.Game;

/// <summary>
/// Friends (owner, 25 Sep 2026: "adding friends and friend list"; Rules.Friends): each hero's own list. A request waits
/// for the other hero to take it; asking a hero who already asked you makes you friends at once. Neither side may have
/// blocked the other, and blocking a friend ends the friendship. From chat or the list a friend can be asked to trade
/// or invited to a guild.
/// </summary>
public sealed partial class GameService
{
    /// <summary>Another hero, found by id (chat, lists) or, when the id is empty, by name.</summary>
    private sealed record HeroRef(Guid Id, Guid LoginId, Guid? GuildId, string Blocked, string Name);

    private async Task<HeroRef> FindHeroAsync(Guid id, string? name, CancellationToken ct)
    {
        IQueryable<Account> query = _db.Accounts.AsNoTracking();
        if (id != Guid.Empty) query = query.Where(a => a.Id == id);
        else
        {
            string key = Characters.NameKey(name ?? "");
            if (key.Length == 0) throw new GameException("no_hero", "Type a hero's name.");
            query = query.Where(a => a.NameKey == key);
        }
        var hero = await query.Select(a => new { a.Id, a.LoginId, a.GuildId, a.Blocked, a.Name }).FirstOrDefaultAsync(ct)
            ?? throw new GameException("no_hero", "No hero goes by that name.");
        return new HeroRef(hero.Id, hero.LoginId, hero.GuildId, hero.Blocked ?? "", ShownName(hero.Id, hero.Name));
    }

    private static bool Blocks(string blocked, Guid who) => blocked.Contains(who.ToString(), StringComparison.OrdinalIgnoreCase);

    public async Task<FriendsDto> FriendsAsync(Account account, string message, CancellationToken ct)
    {
        List<Friendship> rows = await _db.Friendships.AsNoTracking().Where(f => f.FromId == account.Id || f.ToId == account.Id).ToListAsync(ct);
        var ids = rows.Select(f => f.FromId == account.Id ? f.ToId : f.FromId).Distinct().ToList();
        var heroes = await _db.Accounts.AsNoTracking().Where(a => ids.Contains(a.Id))
            .Select(a => new { a.Id, a.Name, a.Class, a.Xp, a.Banner, a.GuildId, a.LastHeartbeatUtc }).ToListAsync(ct);
        var guildIds = heroes.Where(h => h.GuildId != null).Select(h => h.GuildId!.Value).Distinct().ToList();
        var tags = await _db.Guilds.AsNoTracking().Where(g => guildIds.Contains(g.Id)).Select(g => new { g.Id, g.Tag }).ToListAsync(ct);
        DateTime now = DateTime.UtcNow;

        FriendDto[] List(IEnumerable<Guid> who) => who
            .Select(id => heroes.FirstOrDefault(h => h.Id == id))
            .Where(h => h != null)
            .Select(h => new FriendDto(h!.Id, ShownName(h.Id, h.Name), h.Class, Content.LevelFor(h.Xp), h.Banner,
                tags.Where(t => t.Id == h.GuildId).Select(t => t.Tag).FirstOrDefault() ?? "", (int)Math.Max(0, (now - h.LastHeartbeatUtc).TotalMinutes)))
            .OrderBy(f => f.MinutesAway >= Friends.OnlineMinutes).ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        FriendDto[] friends = List(rows.Where(f => f.Accepted).Select(f => f.FromId == account.Id ? f.ToId : f.FromId).Distinct());
        FriendDto[] asking = List(rows.Where(f => !f.Accepted && f.ToId == account.Id).Select(f => f.FromId));
        FriendDto[] asked = List(rows.Where(f => !f.Accepted && f.FromId == account.Id).Select(f => f.ToId));
        bool canInvite = account.GuildId != null && Guilds.CanManage(account.GuildRank);
        return new FriendsDto(friends, asking, asked, Friends.MaxFriends, canInvite, message);
    }

    private Task<int> FriendCountAsync(Guid id, CancellationToken ct) =>
        _db.Friendships.CountAsync(f => f.Accepted && (f.FromId == id || f.ToId == id), ct);

    public async Task<FriendsDto> AddFriendAsync(Account account, FriendAddRequest request, CancellationToken ct)
    {
        HeroRef other = await FindHeroAsync(request.AccountId, request.Name, ct);
        if (other.Id == account.Id) throw new GameException("self", "That is you.");
        if (other.LoginId == account.LoginId) throw new GameException("own_hero", "That is one of your own heroes.");
        if (Blocks(account.Blocked ?? "", other.Id)) throw new GameException("blocked", "You blocked them. Unblock them in CHAT first.");
        if (Blocks(other.Blocked, account.Id)) throw new GameException("not_taking", "They are not taking friend requests.");

        List<Friendship> between = await _db.Friendships
            .Where(f => (f.FromId == account.Id && f.ToId == other.Id) || (f.FromId == other.Id && f.ToId == account.Id)).ToListAsync(ct);
        if (between.Any(f => f.Accepted)) throw new GameException("friends", other.Name + " is on your list already.");
        if (between.Any(f => f.FromId == account.Id)) throw new GameException("asked", "You asked " + other.Name + " already.");
        if (await FriendCountAsync(account.Id, ct) >= Friends.MaxFriends) throw new GameException("list_full", $"Your list holds {Friends.MaxFriends} friends.");

        Friendship? theirs = between.FirstOrDefault(f => f.FromId == other.Id);
        if (theirs != null)
        {
            // They asked first: asking back is saying yes.
            if (await FriendCountAsync(other.Id, ct) >= Friends.MaxFriends) throw new GameException("their_list_full", "Their friend list is full.");
            theirs.Accepted = true;
            theirs.Utc = DateTime.UtcNow;
            await SaveAsync(ct);
            return await FriendsAsync(account, $"You and {other.Name} are friends.", ct);
        }
        if (await _db.Friendships.CountAsync(f => f.FromId == account.Id && !f.Accepted, ct) >= Friends.MaxAsked)
            throw new GameException("asked_full", $"You have {Friends.MaxAsked} requests waiting. Take some back first.");
        _db.Friendships.Add(new Friendship { FromId = account.Id, ToId = other.Id, Utc = DateTime.UtcNow });
        try { await SaveAsync(ct); }
        catch (DbUpdateException) { throw new GameException("asked", "You asked " + other.Name + " already."); }
        return await FriendsAsync(account, $"Asked {other.Name}. They will find it on their friend list.", ct);
    }

    public async Task<FriendsDto> AnswerFriendAsync(Account account, FriendAnswerRequest request, CancellationToken ct)
    {
        Friendship ask = await _db.Friendships.FirstOrDefaultAsync(f => f.FromId == request.AccountId && f.ToId == account.Id && !f.Accepted, ct)
            ?? throw new GameException("no_request", "That request is gone.");
        string them = await NameOfAsync(request.AccountId, ct);
        if (!request.Accept)
        {
            _db.Friendships.Remove(ask);
            await SaveAsync(ct);
            return await FriendsAsync(account, $"You turned {them} down.", ct);
        }
        if (await FriendCountAsync(account.Id, ct) >= Friends.MaxFriends) throw new GameException("list_full", $"Your list holds {Friends.MaxFriends} friends.");
        if (await FriendCountAsync(request.AccountId, ct) >= Friends.MaxFriends) throw new GameException("their_list_full", "Their friend list is full.");
        ask.Accepted = true;
        ask.Utc = DateTime.UtcNow;
        // Both asked at the same moment: one row is enough.
        await _db.Friendships.Where(f => f.FromId == account.Id && f.ToId == request.AccountId).ExecuteDeleteAsync(ct);
        await SaveAsync(ct);
        return await FriendsAsync(account, $"You and {them} are friends.", ct);
    }

    public async Task<FriendsDto> RemoveFriendAsync(Account account, FriendRemoveRequest request, CancellationToken ct)
    {
        int gone = await _db.Friendships
            .Where(f => (f.FromId == account.Id && f.ToId == request.AccountId) || (f.FromId == request.AccountId && f.ToId == account.Id))
            .ExecuteDeleteAsync(ct);
        string them = await NameOfAsync(request.AccountId, ct);
        return await FriendsAsync(account, gone > 0 ? them + " is off your list." : "", ct);
    }

    /// <summary>Waiting friend requests and guild invites, for the HUD (on /me and the heartbeat).</summary>
    private async Task<(int friendAsks, int guildInvites)> SocialCountsAsync(Account account, CancellationToken ct)
    {
        int asks = await _db.Friendships.CountAsync(f => f.ToId == account.Id && !f.Accepted, ct);
        if (account.GuildId != null) return (asks, 0);
        DateTime lapsed = DateTime.UtcNow.AddDays(-Guilds.InviteDays);
        int invites = await _db.GuildInvites.CountAsync(i => i.AccountId == account.Id && i.Utc > lapsed, ct);
        return (asks, invites);
    }
}
