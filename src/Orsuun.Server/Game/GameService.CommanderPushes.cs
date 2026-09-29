using Microsoft.EntityFrameworkCore;
using Orsuun.Rules;
using Orsuun.Server.Data;

namespace Orsuun.Server.Game;

/// <summary>
/// Commander pushes (owner, 29 Sep 2026: "A phone notification when a Commander rises on the map you (or your party) hunt,
/// even while the game is closed; a switch in SETTINGS to turn it off."). The world clock notices each Commander's new
/// spawn once (BossClock.AnnouncedUtc, claimed by a single UPDATE) and pushes to the heroes hunting one of its places
/// (Content.CommanderPlaces) who are not in the game now but played in the last few days: one hero a login, at most one
/// Commander push a login every CommanderPushHours, never to a login that turned them off.
/// </summary>
public sealed partial class GameService
{
    public const int CommanderPushHours = 3, CommanderPushActiveDays = 3;

    private async Task AnnounceCommandersAsync(CancellationToken ct)
    {
        DateTime now = DateTime.UtcNow;
        List<int> ids = Content.Bosses.Select(b => b.Id).ToList();
        await _db.BossClocks.Where(c => ids.Contains(c.BossId)).LoadAsync(ct);
        var rising = new List<(BossDef boss, DateTime spawn)>();
        foreach (BossDef boss in Content.Bosses)
        {
            BossClock clock = await ClockAsync(boss, now, ct);
            bool up = now >= clock.SpawnUtc && now < clock.SpawnUtc.AddSeconds(BossDef.WindowSeconds - 60);
            if (up && (clock.AnnouncedUtc == null || clock.AnnouncedUtc < clock.SpawnUtc)) rising.Add((boss, clock.SpawnUtc));
        }
        await _db.SaveChangesAsync(ct);   // the clocks rolled forward
        foreach ((BossDef boss, DateTime spawn) in rising)
        {
            // Claimed once, whichever tick (or server) gets here first.
            int claimed = await _db.BossClocks.Where(c => c.BossId == boss.Id && (c.AnnouncedUtc == null || c.AnnouncedUtc < spawn))
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.AnnouncedUtc, (DateTime?)spawn), ct);
            if (claimed == 0) continue;
            await PushCommanderAsync(boss, now, ct);
        }
    }

    private async Task PushCommanderAsync(BossDef boss, DateTime now, CancellationToken ct)
    {
        DateTime active = now.AddDays(-CommanderPushActiveDays), away = now.AddSeconds(-Parties.PresentSeconds), rested = now.AddHours(-CommanderPushHours);
        foreach (int place in Content.CommanderPlaces(boss))
        {
            (int first, int last) = Parties.Stages(place);
            string where = Content.IsZone(first) ? Content.StageName(first) : Content.MapOfStage(first).Name;
            // The heroes hunting there, and the partymates of anyone hunting there (Rules.Parties).
            List<Guid> parties = await _db.Accounts.AsNoTracking()
                .Where(a => a.ParkedStage >= first && a.ParkedStage <= last && a.LastHeartbeatUtc > active && a.PartyLeaderId != null)
                .Select(a => a.PartyLeaderId!.Value).Distinct().ToListAsync(ct);
            var heroes = await (from a in _db.Accounts.AsNoTracking()
                                join l in _db.Logins.AsNoTracking() on a.LoginId equals l.Id
                                where (a.ParkedStage >= first && a.ParkedStage <= last || (a.PartyLeaderId != null && parties.Contains(a.PartyLeaderId.Value)))
                                      && a.LastHeartbeatUtc > active && a.LastHeartbeatUtc < away
                                      && a.BannedUtc == null && !a.AtRiver && !l.NoCommanderPushes && (l.CommanderPushUtc == null || l.CommanderPushUtc < rested)
                                select new { a.Id, a.LoginId, Here = a.ParkedStage >= first && a.ParkedStage <= last }).ToListAsync(ct);
            foreach (var login in heroes.GroupBy(h => h.LoginId))
            {
                // One a login, claimed by its own UPDATE (another Commander may be pushing to it this same tick).
                int claimed = await _db.Logins.Where(l => l.Id == login.Key && (l.CommanderPushUtc == null || l.CommanderPushUtc < rested))
                    .ExecuteUpdateAsync(s => s.SetProperty(l => l.CommanderPushUtc, (DateTime?)now), ct);
                if (claimed == 0) continue;
                var hero = login.OrderByDescending(h => h.Here).First();
                _push.Queue(new PushSender.Push(hero.Id, "commander", boss.Name + " has risen",
                    hero.Here ? $"{boss.Name} stands on {where} for ten minutes: every hero there is called to fight."
                              : $"{boss.Name} stands on {where}, where your party hunts, for ten minutes."));
            }
        }
    }

    /// <summary>SETTINGS: Commander pushes on or off (the login's, for all its characters).</summary>
    public async Task<StateDto> CommanderPushesAsync(Account account, CommanderPushRequest request, CancellationToken ct)
    {
        if (_login == null) throw new GameException("no_login", "Sign in again.");
        _login.NoCommanderPushes = !request.On;
        await SaveAsync(ct);
        return ToState(account);
    }
}
