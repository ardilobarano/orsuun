using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Orsuun.Rules;
using Orsuun.Server.Data;

namespace Orsuun.Server.Game;

/// <summary>
/// The Salt Exchange (owner, 24 Sep 2026): any bag piece listed for sorn, bought by anyone, the seller paid the price
/// less the 5% tax. A listed piece keeps its owner with Item.Listed set, so it is out of the bag until the listing
/// closes. The listing row is locked for a buy or a cancel; the seller's sorn is added with one UPDATE.
/// </summary>
public sealed partial class GameService
{
    /// <summary>Closes listings past their time and hands the pieces back (lazily, on market reads and logins).</summary>
    private async Task ExpireListingsAsync(Account? account, CancellationToken ct)
    {
        DateTime now = DateTime.UtcNow;
        var due = await _db.MarketListings.AsNoTracking().Where(l => l.Status == ListingStatus.Active && l.ExpiresUtc <= now)
            .Select(l => new { l.Id, l.ItemId }).Take(200).ToListAsync(ct);
        if (due.Count == 0) return;
        var ids = due.Select(d => d.Id).ToList();
        var items = due.Select(d => d.ItemId).ToList();
        // Only listings still active expire: a buy that got there first (it holds the row lock) has set them Sold.
        await _db.MarketListings.Where(l => ids.Contains(l.Id) && l.Status == ListingStatus.Active).ExecuteUpdateAsync(s => s
            .SetProperty(l => l.Status, ListingStatus.Expired).SetProperty(l => l.ClosedUtc, (DateTime?)now), ct);
        var expiredItems = await _db.MarketListings.AsNoTracking().Where(l => ids.Contains(l.Id) && l.Status == ListingStatus.Expired)
            .Select(l => l.ItemId).ToListAsync(ct);
        await _db.Items.Where(i => expiredItems.Contains(i.Id) && i.Listed).ExecuteUpdateAsync(s => s.SetProperty(i => i.Listed, false), ct);
        // The request's own account is already loaded: keep its copy in step.
        if (account != null)
            foreach (Item item in account.Items.Where(i => expiredItems.Contains(i.Id))) item.Listed = false;
    }

    private static int MinutesLeft(MarketListing l, DateTime now) => Math.Max(0, (int)(l.ExpiresUtc - now).TotalMinutes);

    public async Task<MarketDto> MarketAsync(Account account, EquipSlot? slot, string? sort, int page, CancellationToken ct) =>
        await MarketViewAsync(account, slot, sort, page, "", ct);

    private async Task<MarketDto> MarketViewAsync(Account account, EquipSlot? slot, string? sort, int page, string message, CancellationToken ct)
    {
        await ExpireListingsAsync(account, ct);
        DateTime now = DateTime.UtcNow;
        IQueryable<MarketListing> query = _db.MarketListings.AsNoTracking().Where(l => l.Status == ListingStatus.Active && l.ExpiresUtc > now);
        if (slot is EquipSlot s) query = query.Where(l => l.Slot == s);
        int total = await query.CountAsync(ct);
        int pages = Math.Max(1, (total + Market.PageSize - 1) / Market.PageSize);
        page = Math.Clamp(page, 0, pages - 1);
        query = sort switch
        {
            "newest" => query.OrderByDescending(l => l.CreatedUtc).ThenByDescending(l => l.Id),
            "level" => query.OrderByDescending(l => l.UpgradeLevel).ThenByDescending(l => l.ItemLevel).ThenBy(l => l.Price),
            _ => query.OrderBy(l => l.Price).ThenBy(l => l.Id),
        };
        List<MarketListing> rows = await query.Skip(page * Market.PageSize).Take(Market.PageSize).ToListAsync(ct);
        List<MarketListing> mine = await _db.MarketListings.AsNoTracking()
            .Where(l => l.SellerId == account.Id && (l.Status == ListingStatus.Active || l.ClosedUtc > now.AddDays(-3)))
            .OrderBy(l => l.Status).ThenByDescending(l => l.ClosedUtc ?? l.CreatedUtc).Take(Market.MaxListings + 10).ToListAsync(ct);

        var itemIds = rows.Concat(mine).Select(l => l.ItemId).Distinct().ToList();
        Dictionary<Guid, Item> items = await _db.Items.AsNoTracking().Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        ListingDto? Dto(MarketListing l) => items.TryGetValue(l.ItemId, out Item? item)
            ? new ListingDto(l.Id, ToDto(item), l.Price, l.SellerName, l.SellerBanner, l.SellerId == account.Id, MinutesLeft(l, now), l.Status)
            : null;
        return new MarketDto(ToState(account), rows.Select(Dto).OfType<ListingDto>().ToArray(), page, pages, total,
            mine.Select(Dto).OfType<ListingDto>().ToArray(), Market.TaxPercent, message);
    }

    public async Task<MarketDto> ListItemAsync(Account account, MarketListRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        Item item = account.Items.SingleOrDefault(i => i.Id == request.ItemId && !i.Destroyed && !i.OutOfBag)
            ?? throw new GameException("no_item", "You do not own that item.");
        if (item.Equipped) throw new GameException("worn", "Take the piece off before you sell it.");
        if (Market.PriceProblem(request.Price) is string problem) throw new GameException("bad_price", problem);
        if (await _db.MarketListings.CountAsync(l => l.SellerId == account.Id && l.Status == ListingStatus.Active, ct) >= Market.MaxListings)
            throw new GameException("listings_full", $"You can have {Market.MaxListings} pieces on the Exchange at once.");

        DateTime now = DateTime.UtcNow;
        item.Listed = true;
        _db.MarketListings.Add(new MarketListing
        {
            ItemId = item.Id, SellerId = account.Id, SellerName = DisplayName(account), SellerBanner = account.Banner, Price = request.Price,
            Status = ListingStatus.Active, CreatedUtc = now, ExpiresUtc = now.AddHours(Market.ListingHours),
            Slot = item.Slot, Rarity = item.Rarity, ItemLevel = item.ItemLevel, UpgradeLevel = item.UpgradeLevel,
        });
        _db.Ledger.Add(Entry(account.Id, item.Id, "market-list", $"price={request.Price}", 0, request.RequestId));
        await SaveAsync(ct);
        string name = item.ToState().DisplayName;
        return await MarketViewAsync(account, null, null, 0, $"{name} +{item.UpgradeLevel} is on the Exchange for {request.Price.ToString("N0", CultureInfo.InvariantCulture)} sorn.", ct);
    }

    private async Task<MarketListing> LockListingAsync(long id, CancellationToken ct) =>
        (await _db.MarketListings.FromSql($@"SELECT * FROM ""MarketListings"" WHERE ""Id"" = {id} FOR UPDATE").ToListAsync(ct)).FirstOrDefault()
        ?? throw new GameException("no_listing", "That listing is gone.");

    public async Task<MarketDto> BuyListingAsync(Account account, MarketBuyRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        MarketListing listing = await LockListingAsync(request.ListingId, ct);
        DateTime now = DateTime.UtcNow;
        if (listing.Status != ListingStatus.Active || listing.ExpiresUtc <= now) throw new GameException("sold", "Someone was quicker: that piece is gone.");
        if (listing.SellerId == account.Id) throw new GameException("own_listing", "That is your own listing.");
        if (account.Sorn < listing.Price) throw new GameException("sorn", "Not enough sorn.");
        if (account.Items.Count(i => !i.Equipped && !i.Destroyed && !i.OutOfBag) >= MaxLoot) throw new GameException("bag_full", "Your bag is full.");

        Item item = await _db.Items.FirstOrDefaultAsync(i => i.Id == listing.ItemId, ct) ?? throw new GameException("no_item", "That piece is gone.");
        long payout = Market.Payout(listing.Price);
        item.OwnerId = account.Id;
        item.Listed = false;
        item.Equipped = false;
        account.Items.Add(item);
        account.Sorn -= listing.Price;
        listing.Status = ListingStatus.Sold;
        listing.BuyerId = account.Id;
        listing.ClosedUtc = now;
        await _db.Accounts.Where(a => a.Id == listing.SellerId).ExecuteUpdateAsync(s => s.SetProperty(a => a.Sorn, a => a.Sorn + payout), ct);
        await PayKeepHoldersAsync(Market.Tax(listing.Price), ct);
        string name = item.ToState().DisplayName + " +" + item.UpgradeLevel;
        _db.Ledger.Add(Entry(account.Id, item.Id, "market-buy", $"listing={listing.Id} price={listing.Price} seller={listing.SellerId}", -listing.Price, request.RequestId));
        _db.Ledger.Add(Entry(listing.SellerId, item.Id, "market-sale", $"listing={listing.Id} price={listing.Price} tax={Market.Tax(listing.Price)} buyer={account.Id}",
            payout, "sale-" + listing.Id));
        await SaveAsync(ct);
        await tx.CommitAsync(ct);
        return await MarketViewAsync(account, null, null, 0, $"You bought {name} for {listing.Price.ToString("N0", CultureInfo.InvariantCulture)} sorn. It is in your bag.", ct);
    }

    public async Task<MarketDto> CancelListingAsync(Account account, MarketBuyRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        MarketListing listing = await LockListingAsync(request.ListingId, ct);
        if (listing.SellerId != account.Id) throw new GameException("not_yours", "That is not your listing.");
        if (listing.Status != ListingStatus.Active) throw new GameException("closed", "That listing has closed.");
        listing.Status = ListingStatus.Cancelled;
        listing.ClosedUtc = DateTime.UtcNow;
        Item? item = account.Items.SingleOrDefault(i => i.Id == listing.ItemId);
        if (item != null) item.Listed = false;
        _db.Ledger.Add(Entry(account.Id, listing.ItemId, "market-cancel", $"listing={listing.Id}", 0, request.RequestId));
        await SaveAsync(ct);
        await tx.CommitAsync(ct);
        return await MarketViewAsync(account, null, null, 0, "Taken off the Exchange: the piece is back in your bag.", ct);
    }
}
