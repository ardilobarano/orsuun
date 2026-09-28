using Orsuun.Rules;
using Orsuun.Server.Data;

namespace Orsuun.Server.Game;

// The townsfolk's daily errands (owner, 28 Sep 2026; Rules.Errands): counted by Feat, handed in in the town square.
public sealed partial class GameService
{
    /// <summary>The hero's errand day, moved to today (a new bounty day starts empty).</summary>
    private ErrandProgress ErrandsOf(Account account) => ErrandProgress.Parse(account.Errands, Bounties.DayKey(_bells.LocalNow));

    private ErrandsDto ErrandsDtoOf(Account account)
    {
        ErrandProgress p = ErrandsOf(account);
        int level = Content.LevelFor(account.Xp);
        ErrandDto[] list = Enumerable.Range(0, Errands.Givers).Select(g =>
        {
            ErrandDef e = Errands.For(g, p.Day, level);
            return new ErrandDto(g, e.Id, e.Text, Math.Min(p.Count(e.Metric), e.Target), e.Target, p.Paid(g));
        }).ToArray();
        return new ErrandsDto(list, Errands.Sorn(account.HighestStageCleared), Errands.Materials, Bounties.SecondsToDailyReset(_bells.LocalNow));
    }

    /// <summary>Hands in a townsman's errand: done today and not yet paid, it pays its sorn and materials.</summary>
    public async Task<StateDto> HandInErrandAsync(Account account, ErrandRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        if (request.Giver < 0 || request.Giver >= Errands.Givers) throw new GameException("no_errand", "Nobody here gave that errand.");
        ErrandProgress p = ErrandsOf(account);
        ErrandDef errand = Errands.For(request.Giver, p.Day, Content.LevelFor(account.Xp));
        if (p.Paid(request.Giver)) throw new GameException("errand_paid", "That errand is done for today: a new one comes with the evening bell.");
        if (!p.Done(errand)) throw new GameException("errand_open", "Not done yet: " + errand.Text + ".");
        long sorn = Errands.Sorn(account.HighestStageCleared);
        account.Sorn += sorn;
        account.Materials += Errands.Materials;
        p.Pay(request.Giver);
        account.Errands = p.Serialize();
        _db.Ledger.Add(Entry(account.Id, null, "errand", $"giver={request.Giver} id={errand.Id} {errand.Text} materials={Errands.Materials}", sorn, request.RequestId));
        await SaveAsync(ct);
        return ToState(account);
    }
}
