using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Orsuun.Rules;
using Orsuun.Server.Data;

namespace Orsuun.Server.Game;

/// <summary>
/// Direct trade (Rules.DirectTrade; GDD section 8): one hero asks another by name, both put bag pieces and sorn on the
/// table, both lock, both confirm. Every change goes through the locked trade row; the exchange itself also locks the
/// offered pieces and moves the other side's sorn in one guarded UPDATE, so nothing can change under it.
/// </summary>
public sealed partial class GameService
{
    private static bool IsLive(TradeState s) => s == TradeState.Invited || s == TradeState.Open;

    /// <summary>This character's live trade (asked, or open), closing it first if it has gone stale.</summary>
    private async Task<TradeSession?> LiveTradeAsync(Guid accountId, CancellationToken ct)
    {
        TradeSession? t = await _db.Trades.AsNoTracking()
            .Where(x => (x.FromId == accountId || x.ToId == accountId) && (x.State == TradeState.Invited || x.State == TradeState.Open))
            .OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        if (t == null) return null;
        DateTime now = DateTime.UtcNow;
        bool stale = t.State == TradeState.Invited ? now - t.CreatedUtc > TimeSpan.FromMinutes(DirectTrade.InviteMinutes)
            : now - t.TouchedUtc > TimeSpan.FromMinutes(DirectTrade.IdleMinutes);
        if (!stale) return t;
        int closed = await _db.Trades.Where(x => x.Id == t.Id && (x.State == TradeState.Invited || x.State == TradeState.Open))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.State, TradeState.Cancelled).SetProperty(x => x.ClosedUtc, now)
                .SetProperty(x => x.ClosedReason, t.State == TradeState.Invited ? "unanswered" : "idle"), ct);
        if (closed == 1) await ReleasePiecesAsync(t.Id, ct);
        return null;
    }

    /// <summary>Puts a closed trade's pieces back in their owners' bags (both sides; tracked ones too).</summary>
    private async Task ReleasePiecesAsync(long tradeId, CancellationToken ct)
    {
        await _db.Items.Where(i => i.TradeId == tradeId).ExecuteUpdateAsync(s => s.SetProperty(i => i.TradeId, (long?)null), ct);
        foreach (EntityEntry<Item> e in _db.ChangeTracker.Entries<Item>().Where(e => e.Entity.TradeId == tradeId).ToList())
            e.Entity.TradeId = null;
    }

    private async Task<TradeSession> LockTradeAsync(long id, Guid accountId, CancellationToken ct)
    {
        TradeSession t = (await _db.Trades.FromSql($@"SELECT * FROM ""Trades"" WHERE ""Id"" = {id} FOR UPDATE").ToListAsync(ct)).FirstOrDefault()
            ?? throw new GameException("no_trade", "That trade is gone.");
        await _db.Entry(t).ReloadAsync(ct);
        if (t.FromId != accountId && t.ToId != accountId) throw new GameException("no_trade", "That trade is not yours.");
        return t;
    }

    /// <summary>Why this hero may not trade (level 30, a 72 hour old account; the playtest server lifts both), or null.</summary>
    private async Task<string?> TradeProblemAsync(Guid accountId, bool relaxed, CancellationToken ct)
    {
        if (relaxed) return null;
        var who = await _db.Accounts.AsNoTracking().Where(a => a.Id == accountId).Select(a => new { a.Xp, a.LoginId }).FirstOrDefaultAsync(ct);
        if (who == null) return "That hero is gone.";
        DateTime created = await _db.Logins.AsNoTracking().Where(l => l.Id == who.LoginId).Select(l => l.CreatedUtc).FirstOrDefaultAsync(ct);
        return DirectTrade.Problem(Content.LevelFor(who.Xp), (DateTime.UtcNow - created).TotalHours);
    }

    public async Task<TradeBriefDto?> TradeBriefAsync(Account account, CancellationToken ct)
    {
        TradeSession? t = await LiveTradeAsync(account.Id, ct);
        if (t == null) return null;
        bool incoming = t.ToId == account.Id;
        return new TradeBriefDto(t.Id, t.State, incoming, await NameOfAsync(incoming ? t.FromId : t.ToId, ct));
    }

    private async Task<TradeDto> TradeViewAsync(Account account, TradeSession? t, bool relaxed, string message, CancellationToken ct, StateDto? state = null)
    {
        if (t == null)
            return new TradeDto(0, TradeState.Cancelled, false, "", Array.Empty<ItemDto>(), 0, TradeStep.Offering, Array.Empty<ItemDto>(), 0,
                TradeStep.Offering, 0, DirectTrade.TaxPercent, relaxed, message, state);
        bool from = t.FromId == account.Id;
        List<Guid> mine = DirectTrade.ParseIds(from ? t.FromItems : t.ToItems), theirs = DirectTrade.ParseIds(from ? t.ToItems : t.FromItems);
        List<Guid> all = mine.Concat(theirs).ToList();
        Dictionary<Guid, Item> items = all.Count == 0 ? new Dictionary<Guid, Item>()
            : await _db.Items.AsNoTracking().Where(i => all.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        ItemDto[] Dtos(List<Guid> ids) => ids.Where(items.ContainsKey).Select(id => ToDto(items[id])).ToArray();
        return new TradeDto(t.Id, t.State, !from, await NameOfAsync(from ? t.ToId : t.FromId, ct), Dtos(mine), from ? t.FromSorn : t.ToSorn,
            from ? t.FromStep : t.ToStep, Dtos(theirs), from ? t.ToSorn : t.FromSorn, from ? t.ToStep : t.FromStep,
            DirectTrade.LockLeft(t.ChangedUtc, DateTime.UtcNow), DirectTrade.TaxPercent, relaxed, message, state);
    }

    public async Task<TradeDto> TradeAsync(Account account, bool relaxed, CancellationToken ct) =>
        await TradeViewAsync(account, await LiveTradeAsync(account.Id, ct), relaxed, "", ct);

    public async Task<TradeDto> TradeInviteAsync(Account account, TradeInviteRequest request, bool relaxed, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        if (await TradeProblemAsync(account.Id, relaxed, ct) is string mine) throw new GameException("trade_rules", mine);
        string key = Characters.NameKey(request.Name ?? "");
        var other = await _db.Accounts.AsNoTracking().Where(a => a.NameKey == key && key != "").Select(a => new { a.Id, a.LoginId }).FirstOrDefaultAsync(ct)
            ?? throw new GameException("no_hero", "No hero goes by that name.");
        if (other.Id == account.Id) throw new GameException("self", "You cannot trade with yourself.");
        if (other.LoginId == account.LoginId) throw new GameException("own_hero", "Your own heroes share the depot: use DEPOT in GEAR.");
        if (await TradeProblemAsync(other.Id, relaxed, ct) is string theirs)
            throw new GameException("trade_rules", "They cannot trade yet: " + char.ToLowerInvariant(theirs[0]) + theirs[1..]);
        if (await LiveTradeAsync(account.Id, ct) != null) throw new GameException("busy", "Finish or cancel your open trade first.");
        if (await LiveTradeAsync(other.Id, ct) != null) throw new GameException("busy", "They are in another trade. Try again in a moment.");
        DateTime now = DateTime.UtcNow;
        var t = new TradeSession { FromId = account.Id, ToId = other.Id, State = TradeState.Invited, CreatedUtc = now, ChangedUtc = now, TouchedUtc = now };
        _db.Trades.Add(t);
        _db.Ledger.Add(Entry(account.Id, null, "trade-invite", $"to={other.Id}", 0, request.RequestId));
        await SaveAsync(ct);
        return await TradeViewAsync(account, t, relaxed, "Asked. They have " + DirectTrade.InviteMinutes + " minutes to answer.", ct);
    }

    public async Task<TradeDto> TradeAcceptAsync(Account account, TradeRequest request, bool relaxed, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        TradeSession t = await LockTradeAsync(request.TradeId, account.Id, ct);
        if (t.State != TradeState.Invited || t.ToId != account.Id) throw new GameException("no_invite", "That invitation is gone.");
        if (DateTime.UtcNow - t.CreatedUtc > TimeSpan.FromMinutes(DirectTrade.InviteMinutes)) throw new GameException("no_invite", "That invitation ran out.");
        if (await TradeProblemAsync(account.Id, relaxed, ct) is string problem) throw new GameException("trade_rules", problem);
        t.State = TradeState.Open;
        t.ChangedUtc = t.TouchedUtc = DateTime.UtcNow;
        _db.Ledger.Add(Entry(account.Id, null, "trade-accept", $"trade={t.Id} from={t.FromId}", 0, request.RequestId));
        await SaveAsync(ct);
        await tx.CommitAsync(ct);
        return await TradeViewAsync(account, t, relaxed, "The window is open.", ct);
    }

    public async Task<TradeDto> TradeCancelAsync(Account account, TradeRequest request, bool relaxed, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        TradeSession t = await LockTradeAsync(request.TradeId, account.Id, ct);
        if (IsLive(t.State))
        {
            t.State = TradeState.Cancelled;
            t.ClosedUtc = DateTime.UtcNow;
            t.ClosedReason = t.FromId == account.Id ? "from cancelled" : "to cancelled";
            _db.Ledger.Add(Entry(account.Id, null, "trade-cancel", $"trade={t.Id}", 0, request.RequestId));
            await ReleasePiecesAsync(t.Id, ct);
            await SaveAsync(ct);
        }
        await tx.CommitAsync(ct);
        return await TradeViewAsync(account, null, relaxed, "The trade is off.", ct);
    }

    /// <summary>Puts this side's offer on the table (the whole offer each time); both sides drop back to offering.</summary>
    public async Task<TradeDto> TradeOfferAsync(Account account, TradeOfferRequest request, bool relaxed, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        List<Guid> ids = (request.ItemIds ?? Array.Empty<Guid>()).Distinct().ToList();
        if (DirectTrade.OfferProblem(ids.Count, request.Sorn, account.Sorn) is string problem) throw new GameException("offer", problem);
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        TradeSession t = await LockTradeAsync(request.TradeId, account.Id, ct);
        if (t.State != TradeState.Open) throw new GameException("not_open", "The window is not open.");
        foreach (Guid id in ids)
        {
            Item? item = account.Items.FirstOrDefault(i => i.Id == id);
            if (item == null || item.Destroyed) throw new GameException("not_yours", "That piece is not in your bag.");
            if (item.Equipped) throw new GameException("worn", "Take the piece off before you trade it.");
            if (item.TradeId != t.Id && item.OutOfBag) throw new GameException("out_of_bag", "That piece is on the Exchange, in the depot or on another table.");
        }
        // Pieces on the table leave the bag until the trade ends; pieces taken back return to it.
        foreach (Item item in account.Items.Where(i => i.TradeId == t.Id && !ids.Contains(i.Id))) item.TradeId = null;
        foreach (Item item in account.Items.Where(i => ids.Contains(i.Id))) item.TradeId = t.Id;
        DateTime now = DateTime.UtcNow;
        if (t.FromId == account.Id) { t.FromItems = DirectTrade.FormatIds(ids); t.FromSorn = request.Sorn; }
        else { t.ToItems = DirectTrade.FormatIds(ids); t.ToSorn = request.Sorn; }
        t.FromStep = t.ToStep = TradeStep.Offering;
        t.ChangedUtc = t.TouchedUtc = now;
        _db.Ledger.Add(Entry(account.Id, null, "trade-offer", $"trade={t.Id} pieces={ids.Count} sorn={request.Sorn}", 0, request.RequestId));
        await SaveAsync(ct);
        await tx.CommitAsync(ct);
        return await TradeViewAsync(account, t, relaxed, "", ct);
    }

    /// <summary>The window's button: lock this side's offer, then (once both are locked) confirm; two confirms trade.</summary>
    public async Task<TradeDto> TradePressAsync(Account account, TradeRequest request, bool relaxed, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        TradeSession t = await LockTradeAsync(request.TradeId, account.Id, ct);
        if (t.State != TradeState.Open) throw new GameException("not_open", "The window is not open.");
        bool from = t.FromId == account.Id;
        DateTime now = DateTime.UtcNow;
        TradeStep? next = DirectTrade.Advance(from ? t.FromStep : t.ToStep, from ? t.ToStep : t.FromStep, DirectTrade.LockLeft(t.ChangedUtc, now));
        if (next == null) throw new GameException("wait", "Not yet: the offers just changed, or they have not locked theirs.");
        if (from) t.FromStep = next.Value; else t.ToStep = next.Value;
        t.TouchedUtc = now;
        _db.Ledger.Add(Entry(account.Id, null, "trade-" + next.Value.ToString().ToLowerInvariant(), $"trade={t.Id}", 0, request.RequestId));
        if (!DirectTrade.Complete(t.FromStep, t.ToStep))
        {
            await SaveAsync(ct);
            await tx.CommitAsync(ct);
            return await TradeViewAsync(account, t, relaxed, next == TradeStep.Locked ? "Locked. When both are locked, confirm." : "Confirmed. Waiting for them.", ct);
        }

        string? failed = await ExchangeAsync(account, t, now, ct);
        if (failed != null)
        {
            // Something moved under the offer: both sides look again.
            t.FromStep = t.ToStep = TradeStep.Offering;
            t.ChangedUtc = now;
            await SaveAsync(ct);
            await tx.CommitAsync(ct);
            return await TradeViewAsync(account, t, relaxed, failed, ct);
        }
        await SaveAsync(ct);
        await tx.CommitAsync(ct);
        return await TradeViewAsync(account, t, relaxed, "Traded.", ct, ToState(account));
    }

    /// <summary>
    /// Moves both offers (inside the caller's transaction, the trade row locked): the pieces locked and checked, this
    /// side's sorn on its tracked row, the other side's in one guarded UPDATE. Returns why it could not, or null.
    /// </summary>
    private async Task<string?> ExchangeAsync(Account account, TradeSession t, DateTime now, CancellationToken ct)
    {
        bool from = t.FromId == account.Id;
        Guid otherId = from ? t.ToId : t.FromId;
        List<Guid> mine = DirectTrade.ParseIds(from ? t.FromItems : t.ToItems), theirs = DirectTrade.ParseIds(from ? t.ToItems : t.FromItems);
        long mySorn = from ? t.FromSorn : t.ToSorn, theirSorn = from ? t.ToSorn : t.FromSorn;

        Guid[] all = mine.Concat(theirs).ToArray();
        List<Item> locked = all.Length == 0 ? new List<Item>()
            : await _db.Items.FromSql($@"SELECT * FROM ""Items"" WHERE ""Id"" = ANY({all}) FOR UPDATE").ToListAsync(ct);
        foreach (Item i in locked) await _db.Entry(i).ReloadAsync(ct);
        bool Tradable(Item? i, Guid owner) => i != null && i.OwnerId == owner && !i.Destroyed && !i.Equipped && i.TradeId == t.Id
            && !i.Listed && i.DepotLoginId == null;
        foreach (Guid id in mine)
            if (!Tradable(locked.FirstOrDefault(i => i.Id == id), account.Id)) return "One of your pieces changed (worn, listed or gone). Check the offers again.";
        foreach (Guid id in theirs)
            if (!Tradable(locked.FirstOrDefault(i => i.Id == id), otherId)) return "One of their pieces changed. Check the offers again.";
        if (account.Sorn < mySorn) return "You no longer have that sorn.";

        // Pieces on the table are out of both bags already.
        int myBag = account.Items.Count(i => !i.Equipped && !i.Destroyed && !i.OutOfBag);
        int theirBag = await _db.Items.CountAsync(i => i.OwnerId == otherId && !i.Equipped && !i.Destroyed && !i.Listed && i.DepotLoginId == null
            && i.TradeId == null, ct);
        if (myBag + theirs.Count > MaxLoot) return "Your bag has no room for their pieces.";
        if (theirBag + mine.Count > MaxLoot) return "Their bag has no room for your pieces.";

        // The other side: its sorn moves in one guarded statement (it may be playing at this moment).
        long theirDelta = DirectTrade.Received(mySorn) - theirSorn;
        int paid = await _db.Accounts.Where(a => a.Id == otherId && a.Sorn >= theirSorn)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Sorn, a => a.Sorn + theirDelta), ct);
        if (paid != 1) return "They no longer have that sorn.";
        account.Sorn += DirectTrade.Received(theirSorn) - mySorn;

        // This side's pieces leave its tracked collection (detached first, so nothing reads them as orphans) and change
        // owner in one statement; theirs join it as on the Exchange.
        foreach (Guid id in mine)
        {
            Item item = locked.First(i => i.Id == id);
            _db.Entry(item).State = EntityState.Detached;
            account.Items.Remove(item);
        }
        if (mine.Count > 0)
            await _db.Items.Where(i => mine.Contains(i.Id) && i.OwnerId == account.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.OwnerId, otherId).SetProperty(i => i.TradeId, (long?)null), ct);
        foreach (Guid id in theirs)
        {
            Item item = locked.First(i => i.Id == id);
            item.OwnerId = account.Id;
            item.TradeId = null;
            account.Items.Add(item);
        }

        t.State = TradeState.Done;
        t.ClosedUtc = now;
        t.ClosedReason = "traded";
        string Pieces(List<Guid> ids) => ids.Count == 0 ? "none" : string.Join(",", ids.Select(id => id.ToString("N")[..8]));
        long tax = DirectTrade.Tax(mySorn) + DirectTrade.Tax(theirSorn);
        _db.Ledger.Add(Entry(account.Id, null, "trade-done",
            $"trade={t.Id} with={otherId} gave={Pieces(mine)} sorn={mySorn} got={Pieces(theirs)} sorn={theirSorn} tax={tax}",
            DirectTrade.Received(theirSorn) - mySorn, "trade-" + t.Id.ToString(CultureInfo.InvariantCulture) + "-" + account.Id.ToString("N")[..8]));
        _db.Ledger.Add(Entry(otherId, null, "trade-done",
            $"trade={t.Id} with={account.Id} gave={Pieces(theirs)} sorn={theirSorn} got={Pieces(mine)} sorn={mySorn} tax={tax}",
            theirDelta, "trade-" + t.Id.ToString(CultureInfo.InvariantCulture) + "-" + otherId.ToString("N")[..8]));
        return null;
    }
}
