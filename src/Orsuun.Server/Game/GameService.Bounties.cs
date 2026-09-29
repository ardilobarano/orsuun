using Orsuun.Rules;
using Orsuun.Server.Data;

namespace Orsuun.Server.Game;

/// <summary>Hunt Marks bounties, the Hunt Marks shop, and the etching tools it sells (ETCH, PIN).</summary>
public sealed partial class GameService
{
    /// <summary>This account's bounty counts, moved to the current day and week (server-local, reset at 20:00).</summary>
    private BountyProgress Progress(Account account)
    {
        BountyProgress p = BountyProgress.Parse(account.Bounties);
        DateTime local = _bells.LocalNow;
        p.Roll(Rules.Bounties.DayKey(local), Rules.Bounties.WeekKey(local));
        return p;
    }

    /// <summary>Counts toward the bounties; every caller passes a number the server decided itself.</summary>
    private void Count(Account account, BountyMetric metric, long amount)
    {
        if (amount <= 0) return;
        BountyProgress p = Progress(account);
        p.Add(metric, amount);
        account.Bounties = p.Serialize();
        Feat(account, Bounties.FeatOf(metric), amount);   // the first FeatMetrics follow BountyMetric, later ones are mapped
    }

    private BountyBoardDto Board(Account account)
    {
        BountyProgress p = Progress(account);
        DateTime local = _bells.LocalNow;
        BountyDto[] items = Rules.Bounties.All
            .Select(b => new BountyDto(b.Id, b.Title, b.Period, Math.Min(p.Count(b), b.Target), b.Target, b.Marks, p.Claimed(b)))
            .ToArray();
        return new BountyBoardDto(items, Rules.Bounties.SecondsToDailyReset(local), Rules.Bounties.SecondsToWeeklyReset(local));
    }

    public async Task<StateDto> ClaimBountyAsync(Account account, ClaimBountyRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        BountyDef bounty = Rules.Bounties.Find(request.BountyId) ?? throw new GameException("no_bounty", "Unknown bounty.");
        BountyProgress p = Progress(account);
        if (p.Claimed(bounty)) throw new GameException("claimed", "Already claimed.");
        if (!p.Claimable(bounty)) throw new GameException("not_done", "Not finished yet.");
        int marks = p.Claim(bounty);
        account.HuntMarks += marks;
        Feat(account, FeatMetric.BountiesClaimed, 1);
        account.Bounties = p.Serialize();
        // The day's missions pay Campaign Trail XP (GDD section 3).
        TrailProgress trail = RollTrail(account, out _);
        int trailXp = CampaignTrail.BountyXp(bounty);
        trail.AddXp(trailXp);
        account.Trail = trail.Serialize();
        _db.Ledger.Add(Entry(account.Id, null, "bounty", $"id={bounty.Id} {bounty.Title} marks={marks} trailXp={trailXp}", 0, request.RequestId));
        await SaveAsync(ct);
        return ToState(account);
    }

    public async Task<StateDto> BuyAsync(Account account, ShopBuyRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        ShopItem item = HuntShop.Find(request.ShopItemId) ?? throw new GameException("no_item", "Unknown shop item.");
        var inventory = Snapshot(account);
        try { HuntShop.Buy(inventory, item.Id, request.Count, account.Class, _rng); }
        catch (InvalidOperationException ex) { throw new GameException("shop", ex.Message); }
        Apply(account, inventory);
        _db.Ledger.Add(Entry(account.Id, null, "shop", $"{request.Count}x {item.Name} marks={item.Marks * request.Count}", 0, request.RequestId));
        await SaveAsync(ct);
        return ToState(account);
    }

    public async Task<StateDto> EtchAsync(Account account, EtchRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        Item item = AnvilItem(account, request.ItemId, EquipSlot.Weapon);
        ItemState state = item.ToState();
        var inventory = Snapshot(account);
        if (EtchingActions.EtchBlocker(state, inventory) is string blocker) throw new GameException("cannot_etch", blocker);
        int chance = EtchingActions.EtchChanceBp(state);
        bool took = EtchingActions.Etch(state, inventory, _etchings, _rng);
        item.ApplyState(state);
        Apply(account, inventory);
        string text;
        if (took)
        {
            Etching e = state.Etchings[^1];
            text = $"The needle took: T{e.Tier} {EtchingPool.For(state.Slot).Entries[e.EntryId].Name} +{e.Value}";
        }
        else text = "The needle slipped; the piece is unharmed.";
        _db.Ledger.Add(Entry(account.Id, item.Id, "etch", $"chance={chance} took={took} etchings={item.Etchings}", 0, request.RequestId));
        await SaveAsync(ct);
        return ToState(account, etch: new EtchResultDto(took, chance, text));
    }

    public async Task<StateDto> PinAsync(Account account, PinRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        Item item = AnvilItem(account, request.ItemId, EquipSlot.Weapon);
        ItemState state = item.ToState();
        var inventory = Snapshot(account);
        if (EtchingActions.PinBlocker(state, request.Index, inventory) is string blocker) throw new GameException("cannot_pin", blocker);
        EtchingActions.Pin(state, request.Index, inventory);
        item.ApplyState(state);
        Apply(account, inventory);
        _db.Ledger.Add(Entry(account.Id, item.Id, "pin", $"index={request.Index} locked={state.LockedEtchingIndex}", 0, request.RequestId));
        await SaveAsync(ct);
        return ToState(account);
    }
}
