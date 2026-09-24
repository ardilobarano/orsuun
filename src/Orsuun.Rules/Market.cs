#nullable enable

namespace Orsuun.Rules
{
    public enum ListingStatus
    {
        Active = 0,
        Sold = 1,
        Cancelled = 2,
        Expired = 3,
    }

    /// <summary>
    /// The Salt Exchange (owner, 24 Sep 2026: "a global trading screen that all players can list their items for gold
    /// or buying from them"; GDD: one exchange, 5% tax, the Salt Peace keeps trade neutral between Banners). Any piece
    /// in the bag can be listed for sorn; the buyer pays the price, the seller receives it less the tax (a sorn sink).
    /// A listing lasts 48 hours, then the piece goes back to its seller.
    /// </summary>
    public static class Market
    {
        public const int TaxPercent = 5;
        public const long MinPrice = 1_000;
        public const long MaxPrice = 1_000_000_000;
        public const int ListingHours = 48;
        public const int MaxListings = 10;
        public const int PageSize = 8;

        public static long Tax(long price) => price * TaxPercent / 100;
        public static long Payout(long price) => price - Tax(price);

        public static string? PriceProblem(long price)
        {
            if (price < MinPrice) return $"The lowest price is {MinPrice:N0} sorn.";
            if (price > MaxPrice) return $"The highest price is {MaxPrice:N0} sorn.";
            return null;
        }
    }
}
