using Orsuun.Rules;
using Orsuun.Server.Data;

namespace Orsuun.Server.Game;

/// <summary>
/// The Caravan and the wardrobe (owner, 25 Sep 2026; Rules.Wardrobe, Rules.Amber): Amber bought with real money only,
/// skins, mounts and companions held for 1-14 days, one worn per slot. Until the stores are connected, Amber packs are
/// granted free on a Development server (the playtest) and refused elsewhere.
/// </summary>
public sealed partial class GameService
{
    private static long NowUnix => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>The worn pieces with time left: they count in Hero(account), and the companion in hunting gains.</summary>
    private static List<WardrobeDef> WornPieces(Account a) =>
        Wardrobe.Worn(new[] { a.WornSkin, a.WornMount, a.WornCompanion }, Wardrobe.Parse(a.Wardrobe), NowUnix);

    private WardrobeDto WardrobeOf(Account a)
    {
        long now = NowUnix;
        List<OwnedPiece> owned = Wardrobe.Parse(a.Wardrobe);
        WardrobePieceDto[] pieces = owned.Where(p => p.Active(now) && Wardrobe.Find(p.Id) != null)
            .OrderBy(p => p.ExpiresUnix).Select(p => new WardrobePieceDto(p.Id, p.ExpiresUnix - now)).ToArray();
        List<WardrobeDef> worn = WornPieces(a);
        string Slot(WardrobeKind kind) => worn.FirstOrDefault(d => d.Kind == kind)?.Id ?? "";
        return new WardrobeDto(_login?.Amber ?? 0, pieces, Slot(WardrobeKind.Skin), Slot(WardrobeKind.Mount), Slot(WardrobeKind.Companion),
            (_login?.AmberPurchases ?? 1) == 0);
    }

    private static void SetWorn(Account a, WardrobeKind kind, string id)
    {
        switch (kind)
        {
            case WardrobeKind.Skin: a.WornSkin = id; break;
            case WardrobeKind.Mount: a.WornMount = id; break;
            default: a.WornCompanion = id; break;
        }
    }

    private static string WornIn(Account a, WardrobeKind kind) =>
        kind == WardrobeKind.Skin ? a.WornSkin : kind == WardrobeKind.Mount ? a.WornMount : a.WornCompanion;

    /// <summary>Gives <paramref name="days"/> of a piece; worn at once when its slot is empty or its piece ran out.</summary>
    private static void Hold(Account a, WardrobeDef def, int days)
    {
        long now = NowUnix;
        List<OwnedPiece> owned = Wardrobe.Parse(a.Wardrobe);
        Wardrobe.Prune(owned, now);
        Wardrobe.Grant(owned, def.Id, days, now);
        a.Wardrobe = Wardrobe.Format(owned);
        string worn = WornIn(a, def.Kind);
        if (worn.Length == 0 || !owned.Any(p => p.Id == worn && p.Active(now))) SetWorn(a, def.Kind, def.Id);
    }

    /// <summary>Dropped pieces ("id:days") become held ones.</summary>
    private void HoldDrops(Account a, List<string> drops)
    {
        foreach (string drop in drops)
        {
            int colon = drop.LastIndexOf(':');
            WardrobeDef? def = colon > 0 ? Wardrobe.Find(drop[..colon]) : null;
            if (def == null || !int.TryParse(drop[(colon + 1)..], out int days) || days <= 0) continue;
            Hold(a, def, days);
            _db.Ledger.Add(Entry(a.Id, null, "wardrobe-drop", $"{def.Id} {days}d", 0, Guid.NewGuid().ToString("N")));
        }
        drops.Clear();
    }

    public async Task<StateDto> CaravanBuyAsync(Account account, CaravanBuyRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        WardrobeDef def = Wardrobe.Find(request.PieceId) ?? throw new GameException("no_piece", "The Caravan has no such thing.");
        int price = Wardrobe.Price(def, request.Days);
        if (price < 0) throw new GameException("not_sold", "The Caravan does not sell that for so long.");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        Login login = await LockLoginAsync(ct);
        if (login.Amber < price) throw new GameException("no_amber", "Not enough Amber.");
        login.Amber -= price;           // Amber is the account's: any of its characters spends it (25 Sep 2026)
        Hold(account, def, request.Days);
        _db.Ledger.Add(Entry(account.Id, null, "caravan", $"{def.Id} {request.Days}d for {price} Amber", 0, request.RequestId));
        await SaveAsync(ct);
        await tx.CommitAsync(ct);
        return ToState(account);
    }

    public async Task<StateDto> WearAsync(Account account, WearRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        // A piece changes the hero (skin HP, mount attack, and a mounted hero casts no skills): the time on the old
        // hero is settled first and the lane gets a fresh seed, as with a class change.
        DateTime now = DateTime.UtcNow;
        Settle(account, now);
        account.LastHeartbeatUtc = now;
        NewLane(account);
        if (string.IsNullOrEmpty(request.PieceId))
        {
            if (!Enum.TryParse(request.Kind, out WardrobeKind kind)) throw new GameException("no_slot", "No such wardrobe slot.");
            SetWorn(account, kind, "");
        }
        else
        {
            WardrobeDef def = Wardrobe.Find(request.PieceId) ?? throw new GameException("no_piece", "No such piece.");
            if (!Wardrobe.Parse(account.Wardrobe).Any(p => p.Id == def.Id && p.Active(NowUnix)))
                throw new GameException("not_held", "That piece is not yours, or its time has run out.");
            SetWorn(account, def.Kind, def.Id);
        }
        await SaveAsync(ct);
        return ToState(account);
    }

    /// <summary>
    /// An Amber pack. Real purchases need the stores (receipt checks come with the App Store and Google Play release);
    /// the playtest server (<paramref name="testStore"/>) grants packs free so the Caravan can be tried.
    /// </summary>
    public async Task<StateDto> AmberPackAsync(Account account, AmberPackRequest request, bool testStore, CancellationToken ct)
    {
        if (!testStore) throw new GameException("store_closed", "Amber goes on sale with the App Store and Google Play release.");
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        AmberPack pack = Amber.Pack(request.PackId) ?? throw new GameException("no_pack", "No such pack.");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        Login login = await LockLoginAsync(ct);
        int paid = Amber.Paid(pack, login.AmberPurchases == 0);
        login.Amber += paid;
        login.AmberPurchases++;
        _db.Ledger.Add(Entry(account.Id, null, "amber-test", $"pack {pack.Id} ({pack.PriceText}) paid {paid} Amber, free on the playtest", 0, request.RequestId));
        await SaveAsync(ct);
        await tx.CommitAsync(ct);
        return ToState(account);
    }
}
