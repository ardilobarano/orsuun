#nullable enable
using System;
using System.Collections.Generic;

namespace Orsuun.Rules
{
    /// <summary>Where a direct trade stands: asked, open (both at the window), or over.</summary>
    public enum TradeState { Invited = 0, Open = 1, Done = 2, Cancelled = 3 }

    /// <summary>
    /// One side's step in the window: offering, locked (the first confirm: "this is my offer") or confirmed (the second:
    /// "I take theirs"). The trade goes through when both sides have confirmed.
    /// </summary>
    public enum TradeStep { Offering = 0, Locked = 1, Confirmed = 2 }

    /// <summary>
    /// Direct trade (GDD section 8, the trade window; owner, 25 Sep 2026: "Player-to-player trade"). Two heroes put bag
    /// pieces and sorn on the table. Two-step confirm: both lock their offer, then both confirm; any change to either
    /// offer drops both back to offering and holds the buttons for LockSeconds, which kills the last-second swap. Needs
    /// level 30 and a 72 hour old account (GDD, against throwaway accounts); the sorn pays the GDD's 2% trade tax.
    /// </summary>
    public static class DirectTrade
    {
        public const int MinLevel = 30;
        public const int MinAccountAgeHours = 72;
        public const int TaxPercent = 2;
        public const int LockSeconds = 5;
        public const int MaxPieces = 8;
        public const long MaxSorn = 1_000_000_000;
        /// <summary>An invitation nobody answers, or a window nobody touches, closes after this long.</summary>
        public const int InviteMinutes = 3;
        public const int IdleMinutes = 10;

        /// <summary>Sorn the other side receives for <paramref name="sorn"/> offered: the offer less the 2% tax.</summary>
        public static long Received(long sorn) => sorn - Tax(sorn);

        public static long Tax(long sorn) => sorn * TaxPercent / 100;

        /// <summary>Why a hero may not trade yet, or null.</summary>
        public static string? Problem(int level, double accountAgeHours, bool checkAge = true)
        {
            if (level < MinLevel) return $"Direct trade opens at level {MinLevel}.";
            if (checkAge && accountAgeHours < MinAccountAgeHours) return $"An account can trade {MinAccountAgeHours} hours after it was made.";
            return null;
        }

        /// <summary>Why an offer is not allowed, or null.</summary>
        public static string? OfferProblem(int pieces, long sorn, long sornHeld)
        {
            if (pieces < 0 || pieces > MaxPieces) return $"At most {MaxPieces} pieces a trade.";
            if (sorn < 0 || sorn > MaxSorn) return "That much sorn cannot change hands at once.";
            if (sorn > sornHeld) return "Not enough sorn.";
            return null;
        }

        /// <summary>Seconds until the buttons wake after the last change (0: they are awake).</summary>
        public static int LockLeft(DateTime changedUtc, DateTime nowUtc) =>
            Math.Max(0, (int)Math.Ceiling((changedUtc.AddSeconds(LockSeconds) - nowUtc).TotalSeconds));

        /// <summary>
        /// The next step for a side that presses the window's button: offering to locked (any time the hold is over),
        /// locked to confirmed (only once the other side has locked too). Null when the press does nothing.
        /// </summary>
        public static TradeStep? Advance(TradeStep mine, TradeStep theirs, int lockLeft)
        {
            if (lockLeft > 0) return null;
            if (mine == TradeStep.Offering) return TradeStep.Locked;
            if (mine == TradeStep.Locked && theirs != TradeStep.Offering) return TradeStep.Confirmed;
            return null;
        }

        /// <summary>True when the trade goes through: both sides confirmed.</summary>
        public static bool Complete(TradeStep a, TradeStep b) => a == TradeStep.Confirmed && b == TradeStep.Confirmed;

        public static string FormatIds(IEnumerable<Guid> ids) => string.Join(",", ids);

        public static List<Guid> ParseIds(string? text)
        {
            var list = new List<Guid>();
            if (string.IsNullOrEmpty(text)) return list;
            foreach (string part in text!.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                if (Guid.TryParse(part, out Guid id) && !list.Contains(id)) list.Add(id);
            return list;
        }

        /// <summary>Technique Scrolls on a table, "bookId:count,..": valid books, positive counts, one entry a book.</summary>
        public static string FormatBooks(IEnumerable<KeyValuePair<int, int>> books)
        {
            var parts = new List<string>();
            foreach (var b in Normalise(books)) parts.Add(b.Key + ":" + b.Value);
            return string.Join(",", parts);
        }

        public static List<KeyValuePair<int, int>> ParseBooks(string? text)
        {
            var list = new List<KeyValuePair<int, int>>();
            if (string.IsNullOrEmpty(text)) return list;
            foreach (string part in text!.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] kv = part.Split(':');
                if (kv.Length == 2 && int.TryParse(kv[0], out int id) && int.TryParse(kv[1], out int n)) list.Add(new KeyValuePair<int, int>(id, n));
            }
            return Normalise(list);
        }

        /// <summary>Sums repeated books, drops invalid ones and counts outside 1..Books.MaxStack.</summary>
        public static List<KeyValuePair<int, int>> Normalise(IEnumerable<KeyValuePair<int, int>> books)
        {
            var sums = new SortedDictionary<int, int>();
            foreach (var b in books)
            {
                if (!Books.Valid(b.Key) || b.Value <= 0) continue;
                sums.TryGetValue(b.Key, out int n);
                sums[b.Key] = Math.Min(Books.MaxStack, n + b.Value);
            }
            return new List<KeyValuePair<int, int>>(sums);
        }
    }
}
