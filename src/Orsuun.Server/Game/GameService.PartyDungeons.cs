using Microsoft.EntityFrameworkCore;
using Orsuun.Rules;
using Orsuun.Server.Data;

namespace Orsuun.Server.Game;

/// <summary>
/// Party dungeons (owner, 30 Sep 2026; Rules.PartyDungeons): a partymate opens a dungeon for the party and goes in at once;
/// the others join for three minutes, each through the dungeon's own door (a key, the same checks, a run of their own). The
/// replays show the online partymates beside the hero. When the run closes (and no member waits at a pause), the world clock
/// claims it once and shares the party's chest among those who cleared, by letter.
/// </summary>
public sealed partial class GameService
{
    /// <summary>Opens a dungeon for the hero's party and enters it (the opener's own run).</summary>
    public async Task<DungeonResultDto> OpenPartyDungeonAsync(Account account, DungeonEnterRequest request, CancellationToken ct)
    {
        if (account.PartyLeaderId is not Guid leader) throw new GameException("no_party", "You are not in a party.");
        DungeonDef dungeon = Dungeons.Find(request.DungeonId) ?? throw new GameException("no_dungeon", "Unknown dungeon.");
        // The dungeon's own doors, checked before the party's run is opened.
        if (account.HighestStageCleared < dungeon.UnlockStage)
            throw new GameException("stage_locked", $"Clear {Content.StageName(dungeon.UnlockStage)} first.");
        if (account.DungeonRunAtSmith != 0) throw new GameException("run_open", "Your last run is still waiting on its pause floor.");
        if (DungeonRunsLeft(account) <= 0) throw new GameException("no_keys", "Today's dungeon keys are used. New ones come at 20:00.");
        DateTime now = DateTime.UtcNow;
        if (await _db.PartyDungeons.AnyAsync(p => p.PartyLeaderId == leader && p.SettledUtc == null && p.ClosesUtc > now, ct))
            throw new GameException("party_run_open", "Your party has a dungeon open already: join it.");
        var run = new PartyDungeon
        {
            Id = Guid.NewGuid(), PartyLeaderId = leader, DungeonId = dungeon.Id, OpenedById = account.Id, OpenedByName = NameOf(account),
            OpenedUtc = now, ClosesUtc = now.AddSeconds(PartyDungeons.JoinSeconds),
        };
        _db.PartyDungeons.Add(run);
        SystemLine(PartyChannel(leader), $"{NameOf(account)} opened {dungeon.Name} for the party: join within three minutes.");
        await SaveAsync(ct);
        return await EnterDungeonAsync(account, request with { PartyDungeonId = run.Id }, ct);
    }

    /// <summary>The party run a hero asks to join: open, this party's, this dungeon, not joined yet.</summary>
    private async Task<PartyDungeon?> PartyRunToJoinAsync(Account account, DungeonDef dungeon, Guid? id, CancellationToken ct)
    {
        if (id is not Guid runId || runId == Guid.Empty) return null;
        PartyDungeon run = await _db.PartyDungeons.AsNoTracking().FirstOrDefaultAsync(p => p.Id == runId, ct)
            ?? throw new GameException("no_party_run", "That party run is gone.");
        if (run.SettledUtc != null || run.ClosesUtc <= DateTime.UtcNow) throw new GameException("party_run_closed", "That party run has closed.");
        if (account.PartyLeaderId != run.PartyLeaderId) throw new GameException("not_member", "That run is another party's.");
        if (run.DungeonId != dungeon.Id) throw new GameException("bad_dungeon", "That party run is for another dungeon.");
        if (await _db.DungeonRuns.AnyAsync(r => r.PartyDungeonId == run.Id && r.AccountId == account.Id, ct))
            throw new GameException("already_joined", "You are in that party run already.");
        return run;
    }

    /// <summary>A party run's end in the party's chat, and the partymates to show beside the hero (online ones).</summary>
    private async Task<TownHeroDto[]?> PartyRunEndAsync(Account account, DungeonRun run, DungeonDef dungeon, int fellOn, bool cleared, CancellationToken ct)
    {
        if (run.PartyDungeonId == null || account.PartyLeaderId is not Guid leader) return null;
        if (cleared) SystemLine(PartyChannel(leader), $"{NameOf(account)} cleared {dungeon.Name}.");
        else if (fellOn > 0) SystemLine(PartyChannel(leader), $"{NameOf(account)} fell on floor {fellOn} of {dungeon.Name}.");
        DateTime present = DateTime.UtcNow.AddSeconds(-Parties.PresentSeconds);
        List<Account> mates = await _db.Accounts.AsNoTracking()
            .Where(a => a.PartyLeaderId == leader && a.Id != account.Id && a.LastHeartbeatUtc > present).Take(Parties.MaxMembers).ToListAsync(ct);
        return (await DrawnAsync(mates, ct)).Select(h => h with { Party = true }).ToArray();
    }

    /// <summary>The party's open run for the party card: which dungeon, how long it stays open, who opened and joined it.</summary>
    private async Task<PartyDto> WithOpenRunAsync(Account account, PartyDto party, CancellationToken ct)
    {
        if (account.PartyLeaderId is not Guid leader) return party;
        DateTime now = DateTime.UtcNow;
        PartyDungeon? run = await _db.PartyDungeons.AsNoTracking()
            .Where(p => p.PartyLeaderId == leader && p.SettledUtc == null && p.ClosesUtc > now).OrderByDescending(p => p.OpenedUtc).FirstOrDefaultAsync(ct);
        if (run == null) return party;
        var joined = await (from r in _db.DungeonRuns.AsNoTracking()
                            join a in _db.Accounts.AsNoTracking() on r.AccountId equals a.Id
                            where r.PartyDungeonId == run.Id
                            select new { a.Id, a.Name }).ToListAsync(ct);
        return party with
        {
            OpenDungeonId = run.Id, OpenDungeon = run.DungeonId, OpenDungeonLeft = (long)Math.Ceiling((run.ClosesUtc - now).TotalSeconds),
            OpenedBy = run.OpenedByName, OpenJoined = joined.Any(j => j.Id == account.Id), OpenJoinedNames = joined.Select(j => ShownName(j.Id, j.Name)).ToArray(),
        };
    }

    /// <summary>From the world clock: party runs past their door whose members are done (or that waited half an hour) are
    /// claimed once and their chest shared.</summary>
    private async Task SettlePartyDungeonsAsync(CancellationToken ct)
    {
        DateTime now = DateTime.UtcNow, stale = now.AddMinutes(-PartyDungeons.SettleAfterMinutes);
        var due = await _db.PartyDungeons.AsNoTracking().Where(p => p.SettledUtc == null && p.ClosesUtc < now)
            .Select(p => new { p.Id, p.OpenedUtc }).ToListAsync(ct);
        foreach (var run in due)
        {
            bool waiting = await _db.DungeonRuns.AnyAsync(r => r.PartyDungeonId == run.Id && r.State == 0, ct);
            if (waiting && run.OpenedUtc > stale) continue;   // someone still stands at the smith or the rune lock
            int claimed = await _db.PartyDungeons.Where(p => p.Id == run.Id && p.SettledUtc == null)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.SettledUtc, (DateTime?)now), ct);
            if (claimed == 1) await ShareChestAsync(run.Id, ct);
        }
    }

    /// <summary>The party's chest (Rules.PartyDungeons.Pool): one Warden's chest for each member who cleared, pooled and dealt
    /// out evenly, sent by letter (a good and a Technique Scroll stack a letter).</summary>
    private async Task ShareChestAsync(Guid id, CancellationToken ct)
    {
        PartyDungeon party = await _db.PartyDungeons.AsNoTracking().SingleAsync(p => p.Id == id, ct);
        DungeonDef? dungeon = Dungeons.Find(party.DungeonId);
        if (dungeon == null) return;
        var runs = await _db.DungeonRuns.AsNoTracking().Where(r => r.PartyDungeonId == id)
            .Select(r => new { r.AccountId, r.Level, r.FloorsCleared, r.State }).ToListAsync(ct);
        var clearers = runs.Where(r => r.State == 1 && r.FloorsCleared >= dungeon.Floors).ToList();
        string channel = PartyChannel(party.PartyLeaderId);
        if (runs.Count < PartyDungeons.MinJoined || clearers.Count == 0)
        {
            SystemLine(channel, runs.Count < PartyDungeons.MinJoined
                ? $"The party's run of {dungeon.Name} closed with one hero in it: no shared chest."
                : $"The party's run of {dungeon.Name} closed with nobody clearing it: no shared chest.");
            await SaveAsync(ct);
            return;
        }
        PartyDungeons.Share[] shares = PartyDungeons.Pool(dungeon, clearers.Min(c => c.Level), clearers.Count, _rng);
        string title = "The party's chest: " + dungeon.Name;
        string body = $"Shared among the {clearers.Count} heroes who cleared {dungeon.Name} together.";
        for (int i = 0; i < clearers.Count; i++)
        {
            var goods = shares[i].Goods.ToList();
            var books = shares[i].Books.ToList();
            for (int k = 0; k < Math.Max(goods.Count, books.Count); k++)
                SendLetter(clearers[i].AccountId, "party", "The party", title, body,
                    goodId: k < goods.Count ? goods[k].Key : -1, goodCount: k < goods.Count ? goods[k].Value : 0,
                    bookId: k < books.Count ? books[k].Key : -1, bookCount: k < books.Count ? books[k].Value : 0);
        }
        SystemLine(channel, $"The party's chest from {dungeon.Name} is shared among {clearers.Count} heroes: see your mailbox.");
        _db.Ledger.Add(Entry(party.OpenedById, null, "party-chest", $"party={id} dungeon={dungeon.Id} joined={runs.Count} cleared={clearers.Count}", 0, "party-chest:" + id.ToString("N")));
        await SaveAsync(ct);
    }

    /// <summary>Development: closes the hero's party's open run now and settles what can be settled (the smoke test).</summary>
    public async Task<PartyDto> DevClosePartyDungeonAsync(Account account, CancellationToken ct)
    {
        if (account.PartyLeaderId is Guid leader)
            await _db.PartyDungeons.Where(p => p.PartyLeaderId == leader && p.SettledUtc == null)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.ClosesUtc, DateTime.UtcNow.AddSeconds(-1)), ct);
        await SettlePartyDungeonsAsync(ct);
        return await PartyAsync(account, ct);
    }
}
