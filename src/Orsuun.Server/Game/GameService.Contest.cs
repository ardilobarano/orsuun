using Microsoft.EntityFrameworkCore;
using Orsuun.Rules;
using Orsuun.Server.Data;

namespace Orsuun.Server.Game;

// Fishing contests (owner, 28 Sep 2026: picked "Fishing contests"; Rules.Fishing, WorldEventKind.FishingContest): the
// board the river shows, and the prizes WorldClock pays by letter when a contest ends.
public sealed partial class GameService
{
    public async Task<ContestDto> ContestAsync(Account account, CancellationToken ct)
    {
        DateTime now = DateTime.UtcNow;
        EventCalendar.Entry? running = _events.Running(WorldEventKind.FishingContest, now);
        EventCalendar.Entry? shown = running ?? _events.Last(WorldEventKind.FishingContest, now);
        EventCalendar.Entry? next = _events.Next(WorldEventKind.FishingContest, now);
        string[] prizes = Enumerable.Range(1, 4).Select(rank =>
        {
            (long sorn, int good, int count) = Fishing.ContestPrize(rank, account.HighestStageCleared);
            return $"{(rank < 4 ? "#" + rank : "#4-" + Fishing.ContestPaid)}: {sorn:N0} sorn, {count} {TradeGoods.Name(good)}";
        }).ToArray();
        if (shown == null)
            return new ContestDto(false, false, 0, next == null ? 0 : (long)(next.StartsUtc - now).TotalSeconds, 0, -1, 0, Array.Empty<ContestRowDto>(), prizes);
        DateTime key = shown.StartsUtc;
        List<Account> top = await _db.Accounts.AsNoTracking().Where(a => a.ContestStartUtc == key && a.ContestGrams > 0)
            .OrderByDescending(a => a.ContestGrams).Take(Fishing.ContestPaid).ToListAsync(ct);
        bool mine = account.ContestStartUtc == key && account.ContestGrams > 0;
        int rank = mine ? 1 + await _db.Accounts.CountAsync(a => a.ContestStartUtc == key && a.ContestGrams > account.ContestGrams, ct) : 0;
        return new ContestDto(running != null, true, running == null ? 0 : (long)(running.EndsUtc - now).TotalSeconds,
            next == null ? 0 : (long)(next.StartsUtc - now).TotalSeconds, mine ? account.ContestGrams : 0, mine ? account.ContestFish : -1, rank,
            top.Select(a => new ContestRowDto(NameOf(a), a.Banner, a.ContestFish, a.ContestGrams, a.Id == account.Id)).ToArray(), prizes);
    }

    /// <summary>
    /// WorldClock, every tick: a fishing contest that has ended is claimed once (WorldEvent.Settled) and pays its first
    /// Fishing.ContestPaid by letter; its heaviest wears the Angler of the Week title for a week and is named in world chat.
    /// </summary>
    private async Task SettleContestsAsync(CancellationToken ct)
    {
        DateTime now = DateTime.UtcNow;
        List<WorldEvent> ended = await _db.WorldEvents.AsNoTracking()
            .Where(e => e.Kind == WorldEventKind.FishingContest && !e.Cancelled && !e.Settled && e.EndsUtc <= now).ToListAsync(ct);
        foreach (WorldEvent e in ended)
        {
            if (await _db.WorldEvents.Where(w => w.Id == e.Id && !w.Settled).ExecuteUpdateAsync(s => s.SetProperty(w => w.Settled, true), ct) != 1)
                continue;
            List<Account> winners = await _db.Accounts.AsNoTracking().Where(a => a.ContestStartUtc == e.StartsUtc && a.ContestGrams > 0)
                .OrderByDescending(a => a.ContestGrams).Take(Fishing.ContestPaid).ToListAsync(ct);
            for (int i = 0; i < winners.Count; i++)
            {
                Account w = winners[i];
                int rank = i + 1;
                (long sorn, int good, int count) = Fishing.ContestPrize(rank, w.HighestStageCleared);
                string fish = Fishing.FishById(w.ContestFish)?.Name ?? "fish";
                SendLetter(w.Id, "contest", "Old Nergui", "The Fishing Contest",
                    $"Your {Fishing.Kilos(w.ContestGrams)} {fish} came in #{rank} of the contest. Here is your share of the prizes."
                    + (rank == 1 ? $" You wear the title {Fishing.AnglerTitle} for a week." : ""), sorn, good, count);
            }
            if (winners.Count > 0)
            {
                Account best = winners[0];
                DateTime until = e.EndsUtc.AddDays(Fishing.AnglerDays);
                await _db.Accounts.Where(a => a.Id == best.Id).ExecuteUpdateAsync(s => s.SetProperty(a => a.AnglerUntilUtc, until), ct);
                SystemLine(Chat.World, $"{NameOf(best)} won the Fishing Contest with a {Fishing.Kilos(best.ContestGrams)} {Fishing.FishById(best.ContestFish)?.Name ?? "fish"}!");
            }
            await SaveAsync(ct);
        }
    }
}
