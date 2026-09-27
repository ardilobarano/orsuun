using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Orsuun.Rules;
using Orsuun.Server.Data;

namespace Orsuun.Server.Game;

/// <summary>
/// The mailbox (owner, 27 Sep 2026: "Mailbox"; Rules.Mail). Letters are written with one insert, so any request may
/// write to any hero without touching its row: the Exchange's sale pays the seller by letter and a listing that runs out
/// comes home by letter (its piece held with Item.InMail, out of the bag). Taking locks the hero's letters.
/// </summary>
public sealed partial class GameService
{
    /// <summary>Writes a letter to a hero (saved with the request's other changes) and, once saved, pushes it to their phones
    /// (an Exchange sale, a listing come home, a raid's pay: PushSender).</summary>
    private void SendLetter(Guid to, string kind, string from, string title, string body, long sorn = 0, int goodId = -1, int goodCount = 0,
        int bookId = -1, int bookCount = 0, Guid? itemId = null)
    {
        _db.Letters.Add(new Letter
        {
            AccountId = to, Kind = kind, From = from, Title = Clip(title, Mail.TitleMax), Body = Clip(body, Mail.BodyMax), Sorn = sorn,
            GoodId = goodId, GoodCount = goodCount, BookId = bookId, BookCount = bookCount, ItemId = itemId, Utc = DateTime.UtcNow,
        });
        _pushes.Add(new PushSender.Push(to, "letter-" + kind, Clip(title, Mail.TitleMax), "A letter has come from " + from + ". Open the mailbox."));
    }

    /// <summary>A phone's push token: kept for the login it plays (a token moves with a sign-in to another login).</summary>
    public async Task<MessageDto> PushTokenAsync(Account account, PushTokenRequest request, CancellationToken ct)
    {
        string platform = request.Platform == "ios" ? "ios" : request.Platform == "android" ? "android" : throw new GameException("bad_platform", "Unknown platform.");
        string token = (request.Token ?? "").Trim();
        if (token.Length < 16 || token.Length > 256) throw new GameException("bad_token", "That push token could not be read.");
        PushToken? row = await _db.PushTokens.FirstOrDefaultAsync(t => t.Token == token, ct);
        if (row == null) _db.PushTokens.Add(row = new PushToken { Token = token });
        row.LoginId = account.LoginId;
        row.Platform = platform;
        row.Utc = DateTime.UtcNow;
        await SaveAsync(ct);
        return new MessageDto("Notifications on.");
    }

    private static string Clip(string text, int max) => text.Length <= max ? text : text[..max];

    private static string SornText(long amount) => amount.ToString("N0", CultureInfo.InvariantCulture);

    public async Task<MailDto> MailAsync(Account account, CancellationToken ct) => await MailViewAsync(account, "", ct);

    /// <summary>The letters, newest first; opening the mailbox reads them all (the count on MENU clears).</summary>
    private async Task<MailDto> MailViewAsync(Account account, string message, CancellationToken ct)
    {
        await ExpireListingsAsync(account, ct);
        await SaveAsync(ct);
        DateTime old = DateTime.UtcNow.AddDays(-Mail.KeepDays);
        await _db.Letters.Where(l => l.AccountId == account.Id && l.Utc < old
                                     && (l.TakenUtc != null || l.Sorn == 0 && l.GoodId < 0 && l.BookId < 0 && l.ItemId == null))
            .ExecuteDeleteAsync(ct);
        List<Letter> letters = await _db.Letters.AsNoTracking().Where(l => l.AccountId == account.Id)
            .OrderByDescending(l => l.Id).Take(Mail.MaxShown).ToListAsync(ct);
        await _db.Letters.Where(l => l.AccountId == account.Id && !l.Read).ExecuteUpdateAsync(s => s.SetProperty(l => l.Read, true), ct);
        Dictionary<Guid, Item> pieces = account.Items.Where(i => i.InMail).ToDictionary(i => i.Id);
        LetterDto Dto(Letter l) => new(l.Id, l.Kind, l.From, l.Title, l.Body, l.Utc, l.Read, l.TakenUtc != null, l.Sorn, l.GoodId, l.GoodCount,
            l.BookId, l.BookCount, l.ItemId is Guid id && l.TakenUtc == null && pieces.TryGetValue(id, out Item? item) ? ToDto(item) : null);
        return new MailDto(ToState(account), letters.Select(Dto).ToArray(), 0, message);
    }

    /// <summary>Takes what one letter holds, or every letter's (LetterId 0). A piece needs room in the bag and waits otherwise.</summary>
    public async Task<MailDto> TakeMailAsync(Account account, MailTakeRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        List<Letter> letters = request.LetterId > 0
            ? await _db.Letters.FromSql($@"SELECT * FROM ""Letters"" WHERE ""Id"" = {request.LetterId} AND ""AccountId"" = {account.Id} FOR UPDATE").ToListAsync(ct)
            : await _db.Letters.FromSql($@"SELECT * FROM ""Letters"" WHERE ""AccountId"" = {account.Id} AND ""TakenUtc"" IS NULL ORDER BY ""Id"" FOR UPDATE").ToListAsync(ct);
        if (request.LetterId > 0 && letters.Count == 0) throw new GameException("no_letter", "That letter is gone.");
        DateTime now = DateTime.UtcNow;
        int bag = account.Items.Count(i => !i.Equipped && !i.Destroyed && !i.OutOfBag);
        long sorn = 0;
        int taken = 0, waiting = 0;
        var ids = new List<long>();
        foreach (Letter letter in letters.Where(l => l.TakenUtc == null && l.HoldsSomething))
        {
            if (letter.ItemId is Guid id)
            {
                Item? piece = account.Items.SingleOrDefault(i => i.Id == id && i.InMail && !i.Destroyed);
                if (piece != null)
                {
                    if (bag >= MaxLoot) { waiting++; continue; }
                    piece.InMail = false;
                    bag++;
                }
            }
            if (letter.Sorn > 0)
            {
                account.Sorn += letter.Sorn;
                sorn += letter.Sorn;
            }
            if (letter.GoodId >= 0) AddGood(account, letter.GoodId, letter.GoodCount);
            if (letter.BookId >= 0) AddBooks(account, letter.BookId, letter.BookCount);
            letter.TakenUtc = now;
            letter.Read = true;
            taken++;
            ids.Add(letter.Id);
        }
        if (taken > 0)
            _db.Ledger.Add(Entry(account.Id, null, "mail-take", $"letters={string.Join(",", ids)}", sorn, request.RequestId));
        await SaveAsync(ct);
        await tx.CommitAsync(ct);
        string message = taken == 0 && waiting == 0 ? "Nothing to take."
            : (taken == 1 ? "Taken." : taken > 1 ? $"Taken from {taken} letters." : "")
              + (sorn > 0 ? $" +{SornText(sorn)} sorn." : "")
              + (waiting > 0 ? $" {(waiting == 1 ? "A piece waits" : $"{waiting} pieces wait")}: your bag is full." : "");
        return await MailViewAsync(account, message.Trim(), ct);
    }

    /// <summary>Deletes a letter with nothing left in it, or every such letter (LetterId 0).</summary>
    public async Task<MailDto> DeleteMailAsync(Account account, MailDeleteRequest request, CancellationToken ct)
    {
        IQueryable<Letter> done = _db.Letters.Where(l => l.AccountId == account.Id
                                                         && (l.TakenUtc != null || l.Sorn == 0 && l.GoodId < 0 && l.BookId < 0 && l.ItemId == null));
        int gone = await (request.LetterId > 0 ? done.Where(l => l.Id == request.LetterId) : done).ExecuteDeleteAsync(ct);
        if (request.LetterId > 0 && gone == 0) throw new GameException("take_first", "Take what the letter holds first.");
        return await MailViewAsync(account, request.LetterId > 0 ? "Letter thrown away." : gone == 0 ? "Nothing to throw away." : $"{gone} letters thrown away.", ct);
    }
}
