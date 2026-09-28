using Microsoft.EntityFrameworkCore;
using Orsuun.Rules;
using Orsuun.Server.Data;

namespace Orsuun.Server.Game;

// Rug Stalls (owner, 28 Sep 2026; Rules.Market.RugWares): Exchange listings laid on the seller's own rug. They sell
// through the Exchange's buy (the seller is paid by letter), but are browsed stall by stall instead of on its lists.
public sealed partial class GameService
{
    /// <summary>Why another listing does not fit (the Exchange's ten, or the rug's twelve), or null.</summary>
    private async Task<string?> RoomProblemAsync(Account account, bool rug, CancellationToken ct)
    {
        int active = await _db.MarketListings.CountAsync(l => l.SellerId == account.Id && l.Status == ListingStatus.Active && l.Rug == rug, ct);
        if (rug) return active >= Market.RugWares ? $"Your rug holds {Market.RugWares} wares." : null;
        return active >= Market.MaxListings ? $"You can have {Market.MaxListings} listings on the Exchange at once." : null;
    }

    /// <summary>The rugs laid out now, the newest first, and how full the hero's own is.</summary>
    public async Task<RugsDto> RugsAsync(Account account, CancellationToken ct)
    {
        await ExpireListingsAsync(account, ct);
        await SaveAsync(ct);
        DateTime now = DateTime.UtcNow;
        var stalls = await _db.MarketListings.AsNoTracking()
            .Where(l => l.Rug && l.Status == ListingStatus.Active && l.ExpiresUtc > now)
            .GroupBy(l => new { l.SellerId, l.SellerName, l.SellerBanner })
            .Select(g => new { g.Key.SellerId, g.Key.SellerName, g.Key.SellerBanner, Wares = g.Count(), Newest = g.Max(l => l.CreatedUtc) })
            .OrderByDescending(g => g.Newest).Take(60).ToListAsync(ct);
        int mine = stalls.FirstOrDefault(s => s.SellerId == account.Id)?.Wares ?? 0;
        return new RugsDto(stalls.Select(s => new RugStallDto(s.SellerId, s.SellerName, s.SellerBanner, s.Wares, s.SellerId == account.Id)).ToArray(),
            mine, Market.RugWares);
    }

    /// <summary>One rug's wares, the dearest first.</summary>
    public async Task<RugDto> RugAsync(Account account, Guid sellerId, CancellationToken ct)
    {
        DateTime now = DateTime.UtcNow;
        List<MarketListing> wares = await _db.MarketListings.AsNoTracking()
            .Where(l => l.Rug && l.SellerId == sellerId && l.Status == ListingStatus.Active && l.ExpiresUtc > now)
            .OrderByDescending(l => l.Price).ToListAsync(ct);
        var seller = await _db.Accounts.AsNoTracking().Where(a => a.Id == sellerId).Select(a => new { a.Id, a.Name, a.Banner }).FirstOrDefaultAsync(ct);
        var itemIds = wares.Select(l => l.ItemId).ToList();
        Dictionary<Guid, Item> items = await _db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        bool mine = sellerId == account.Id;
        ListingDto? Dto(MarketListing l) => l.GoodId >= 0
            ? new ListingDto(l.Id, null, l.Price, l.SellerName, l.SellerBanner, mine, MinutesLeft(l, now), l.Status, GoodId: l.GoodId, GoodCount: l.GoodCount, Rug: true)
            : l.BookId >= 0
            ? new ListingDto(l.Id, null, l.Price, l.SellerName, l.SellerBanner, mine, MinutesLeft(l, now), l.Status, l.BookId, l.BookCount, Rug: true)
            : items.TryGetValue(l.ItemId, out Item? item)
            ? new ListingDto(l.Id, ToDto(item), l.Price, l.SellerName, l.SellerBanner, mine, MinutesLeft(l, now), l.Status, Rug: true)
            : null;
        return new RugDto(sellerId, seller != null ? ShownName(seller.Id, seller.Name) : "", seller?.Banner ?? Banner.None,
            wares.Select(Dto).OfType<ListingDto>().ToArray(), mine, Market.TaxPercent);
    }
}
