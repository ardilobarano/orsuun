using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Orsuun.Rules;
using Orsuun.Server.Data;

namespace Orsuun.Server.Game;

/// <summary>
/// The Salt Exchange (owner, 24 Sep 2026): any bag piece listed for sorn, bought by anyone, the seller paid the price
/// less the 5% tax. A listed piece keeps its owner with Item.Listed set, so it is out of the bag until the listing
/// closes. The listing row is locked for a buy or a cancel; the seller's sorn is added with one UPDATE. Technique
/// Scrolls (owner, 26 Sep 2026: books trade) list as stacks: the count leaves the seller's stack into the listing and
/// goes to the buyer, or back to the seller when the listing is cancelled or runs out.
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
        var expired = await _db.MarketListings.AsNoTracking().Where(l => ids.Contains(l.Id) && l.Status == ListingStatus.Expired)
            .Select(l => new { l.ItemId, l.SellerId, l.BookId, l.BookCount }).ToListAsync(ct);
        var expiredItems = expired.Where(l => l.BookId < 0).Select(l => l.ItemId).ToList();
        // Book stacks go back to their sellers (this request's own hero on its tracked row).
        foreach (var b in expired.Where(l => l.BookId >= 0))
        {
            if (account != null && b.SellerId == account.Id) AddBooks(account, b.BookId, b.BookCount);
            else await AddBooksElsewhereAsync(b.SellerId, b.BookId, b.BookCount, ct);
        }
        await _db.Items.Where(i => expiredItems.Contains(i.Id) && i.Listed).ExecuteUpdateAsync(s => s.SetProperty(i => i.Listed, false), ct);
        // The request's own account is already loaded: keep its copy in step.
        if (account != null)
            foreach (Item item in account.Items.Where(i => expiredItems.Contains(i.Id))) item.Listed = false;
    }

    private static int MinutesLeft(MarketListing l, DateTime now) => Math.Max(0, (int)(l.ExpiresUtc - now).TotalMinutes);

    public async Task<MarketDto> MarketAsync(Account account, EquipSlot? slot, string? sort, int page, CancellationToken ct, bool books = false) =>
        await MarketViewAsync(account, slot, sort, page, "", ct, books);

    private async Task<MarketDto> MarketViewAsync(Account account, EquipSlot? slot, string? sort, int page, string message, CancellationToken ct, bool books = false)
    {
        await ExpireListingsAsync(account, ct);
        await SaveAsync(ct);
        DateTime now = DateTime.UtcNow;
        IQueryable<MarketListing> query = _db.MarketListings.AsNoTracking().Where(l => l.Status == ListingStatus.Active && l.ExpiresUtc > now);
        if (books) query = query.Where(l => l.BookId >= 0);
        else if (slot is EquipSlot s) query = query.Where(l => l.Slot == s && l.BookId < 0);
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
        ListingDto? Dto(MarketListing l) => l.BookId >= 0
            ? new ListingDto(l.Id, null, l.Price, l.SellerName, l.SellerBanner, l.SellerId == account.Id, MinutesLeft(l, now), l.Status, l.BookId, l.BookCount)
            : items.TryGetValue(l.ItemId, out Item? item)
            ? new ListingDto(l.Id, ToDto(item), l.Price, l.SellerName, l.SellerBanner, l.SellerId == account.Id, MinutesLeft(l, now), l.Status)
            : null;
        return new MarketDto(ToState(account), rows.Select(Dto).OfType<ListingDto>().ToArray(), page, pages, total,
            mine.Select(Dto).OfType<ListingDto>().ToArray(), Market.TaxPercent, message);
    }

    public async Task<MarketDto> ListItemAsync(Account account, MarketListRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        if (request.BookId >= 0) return await ListBooksAsync(account, request, ct);
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
        string name = Content.ItemName(item.ToState(), account.Class);
        return await MarketViewAsync(account, null, null, 0, $"{name} +{item.UpgradeLevel} is on the Exchange for {request.Price.ToString("N0", CultureInfo.InvariantCulture)} sorn.", ct);
    }

    /// <summary>A stack of Technique Scrolls on the Exchange: the count leaves the hero's stack into the listing.</summary>
    private async Task<MarketDto> ListBooksAsync(Account account, MarketListRequest request, CancellationToken ct)
    {
        if (!Books.Valid(request.BookId)) throw new GameException("no_book", "No such book.");
        int held = BookCounts(account)[request.BookId];
        if (request.BookCount < 1 || request.BookCount > held) throw new GameException("no_books", $"You hold {held} of that book.");
        if (Market.PriceProblem(request.Price) is string problem) throw new GameException("bad_price", problem);
        if (await _db.MarketListings.CountAsync(l => l.SellerId == account.Id && l.Status == ListingStatus.Active, ct) >= Market.MaxListings)
            throw new GameException("listings_full", $"You can have {Market.MaxListings} listings on the Exchange at once.");
        DateTime now = DateTime.UtcNow;
        SetBooks(account, request.BookId, held - request.BookCount);
        _db.MarketListings.Add(new MarketListing
        {
            ItemId = Guid.Empty, SellerId = account.Id, SellerName = DisplayName(account), SellerBanner = account.Banner, Price = request.Price,
            Status = ListingStatus.Active, CreatedUtc = now, ExpiresUtc = now.AddHours(Market.ListingHours), BookId = request.BookId, BookCount = request.BookCount,
        });
        _db.Ledger.Add(Entry(account.Id, null, "market-list", $"book={request.BookId} count={request.BookCount} price={request.Price}", 0, request.RequestId));
        await SaveAsync(ct);
        return await MarketViewAsync(account, null, null, 0,
            $"{request.BookCount} × {Books.Name(request.BookId)} on the Exchange for {request.Price.ToString("N0", CultureInfo.InvariantCulture)} sorn.", ct, books: true);
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
        if (listing.BookId >= 0)
        {
            long bookPayout = Market.Payout(listing.Price);
            AddBooks(account, listing.BookId, listing.BookCount);
            account.Sorn -= listing.Price;
            listing.Status = ListingStatus.Sold;
            listing.BuyerId = account.Id;
            listing.ClosedUtc = now;
            await _db.Accounts.Where(a => a.Id == listing.SellerId).ExecuteUpdateAsync(s => s.SetProperty(a => a.Sorn, a => a.Sorn + bookPayout), ct);
            await PayKeepHoldersAsync(Market.Tax(listing.Price), ct);
            _db.Ledger.Add(Entry(account.Id, null, "market-buy", $"listing={listing.Id} book={listing.BookId} count={listing.BookCount} price={listing.Price} seller={listing.SellerId}",
                -listing.Price, request.RequestId));
            _db.Ledger.Add(Entry(listing.SellerId, null, "market-sale", $"listing={listing.Id} book={listing.BookId} count={listing.BookCount} price={listing.Price} tax={Market.Tax(listing.Price)} buyer={account.Id}",
                bookPayout, "sale-" + listing.Id));
            await SaveAsync(ct);
            await tx.CommitAsync(ct);
            return await MarketViewAsync(account, null, null, 0,
                $"You bought {listing.BookCount} × {Books.Name(listing.BookId)} for {listing.Price.ToString("N0", CultureInfo.InvariantCulture)} sorn.", ct, books: true);
        }
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
        string name = Content.ItemName(item.ToState(), account.Class) + " +" + item.UpgradeLevel;
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
        if (listing.BookId >= 0) AddBooks(account, listing.BookId, listing.BookCount);
        Item? item = listing.BookId >= 0 ? null : account.Items.SingleOrDefault(i => i.Id == listing.ItemId);
        if (item != null) item.Listed = false;
        _db.Ledger.Add(Entry(account.Id, listing.ItemId, "market-cancel", $"listing={listing.Id}", 0, request.RequestId));
        await SaveAsync(ct);
        await tx.CommitAsync(ct);
        return await MarketViewAsync(account, null, null, 0, listing.BookId >= 0 ? "Taken off the Exchange: the books are back with you."
            : "Taken off the Exchange: the piece is back in your bag.", ct);
    }
}
