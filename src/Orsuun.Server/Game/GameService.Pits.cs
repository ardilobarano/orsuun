using System.Globalization;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Orsuun.Rules;
using Orsuun.Rules.Combat;
using Orsuun.Server.Data;

namespace Orsuun.Server.Game;

/// <summary>
/// The Pits (owner, 25 Sep 2026: "the Pits"; Rules.Pits): three challengers at a time (players near the attacker's rating,
/// Pit shades where there are too few), five tickets a bounty day, a duel scored here and replayed by the client, Elo with
/// leagues, Laurels for the Pit shop, and a board of the best with their weapons on show.
/// </summary>
public sealed partial class GameService
{
    private sealed record Challenger(string Id, string Name, string Tag, int Rating, HeroClass Class, List<ItemState> Gear, int Level, Account? Account);

    private int PitTicketsLeft(Account account) =>
        Math.Max(0, Pits.TicketsPerDay - (account.PitDay == Rules.Bounties.DayKey(_bells.LocalNow) ? account.PitFights : 0));

    private static List<ItemState> Worn(Account a) => a.Items.Where(i => i.Equipped && !i.Destroyed).Select(i => i.ToState()).ToList();

    private static string WeaponLine(List<ItemState> gear)
    {
        ItemState? weapon = gear.FirstOrDefault(i => i.Slot == EquipSlot.Weapon);
        return weapon == null ? "no weapon" : $"{weapon.DisplayName} +{weapon.UpgradeLevel}";
    }

    /// <summary>
    /// The challengers on offer: the same three until a fight or a refresh (PitRoll). Players within the match window who
    /// were about in the last two weeks come first; Pit shades fill the rest, one forge level weaker, even and stronger.
    /// </summary>
    private async Task<List<Challenger>> ChallengersAsync(Account account, CancellationToken ct)
    {
        int rating = account.PitRating;
        DateTime since = DateTime.UtcNow.AddDays(-14);
        List<Account> near = await _db.Accounts.AsNoTracking()
            .Where(a => a.Id != account.Id && a.BannedUtc == null && a.LastHeartbeatUtc > since
                        && a.PitRating >= rating - Pits.MatchWindow && a.PitRating <= rating + Pits.MatchWindow)
            .OrderBy(a => a.Id).Take(30).ToListAsync(ct);
        var guildIds = near.Where(a => a.GuildId != null).Select(a => a.GuildId!.Value).Distinct().ToList();
        var tags = await _db.Guilds.AsNoTracking().Where(g => guildIds.Contains(g.Id)).Select(g => new { g.Id, g.Tag }).ToListAsync(ct);

        byte[] id = account.Id.ToByteArray();
        ulong seed = BitConverter.ToUInt64(id, 0) ^ ((ulong)(uint)account.PitRoll << 32) ^ (ulong)account.PitRoll;
        var rng = new XorShiftRandom(seed);
        var list = new List<Challenger>();
        while (list.Count < Pits.Challengers && near.Count > 0)
        {
            Account a = near[rng.NextInt(near.Count)];
            near.Remove(a);
            if (!a.Items.Any(i => i.Equipped && !i.Destroyed)) continue;
            string tag = tags.Where(t => t.Id == a.GuildId).Select(t => t.Tag).FirstOrDefault() ?? "";
            list.Add(new Challenger(a.Id.ToString(), NameOf(a), tag, a.PitRating, a.Class, Worn(a), Content.LevelFor(a.Xp), a));
        }
        int[] steps = { -1, 0, 1 };
        foreach (int step in steps.Skip(list.Count))
        {
            // A shade's name is fixed by the account and the roll, so the list does not reshuffle between views.
            var name = new Guid(BitConverter.GetBytes(seed + (ulong)(step + 7) * 0x9E3779B97F4A7C15UL).Concat(id.Take(8)).ToArray());
            list.Add(new Challenger("shade:" + step.ToString(CultureInfo.InvariantCulture), "Shade of " + Banners.GeneratedName(name), "",
                rating + step * 60, (HeroClass)rng.NextInt(4), Pits.ShadeGear(Worn(account), step), Content.LevelFor(account.Xp), null));
        }
        return list;
    }

    private double Edge(Account account, Challenger foe) =>
        Duels.Edge(Duels.Neutral(Worn(account), Content.LevelFor(account.Xp)), Duels.Neutral(foe.Gear, foe.Level)) + Pits.AttackerEdge;

    public async Task<PitsDto> PitsAsync(Account account, string message, CancellationToken ct)
    {
        List<Challenger> challengers = await ChallengersAsync(account, ct);
        PitChallengerDto[] offered = challengers.Select(c => new PitChallengerDto(c.Id, c.Name, c.Tag, c.Rating, Pits.League(c.Rating), c.Class,
            WeaponLine(c.Gear), (int)Math.Round(Duels.WinChance(Edge(account, c)) * 100), c.Account == null)).ToArray();

        List<Account> top = await _db.Accounts.AsNoTracking().Where(a => a.PitWins + a.PitLosses > 0 && a.BannedUtc == null)
            .OrderByDescending(a => a.PitRating).ThenByDescending(a => a.PitWins).Take(20).ToListAsync(ct);
        var guildIds = top.Where(a => a.GuildId != null).Select(a => a.GuildId!.Value).Distinct().ToList();
        var tags = await _db.Guilds.AsNoTracking().Where(g => guildIds.Contains(g.Id)).Select(g => new { g.Id, g.Tag }).ToListAsync(ct);
        PitBoardDto[] board = top.Select((a, i) => new PitBoardDto(i + 1, NameOf(a),
            tags.Where(t => t.Id == a.GuildId).Select(t => t.Tag).FirstOrDefault() ?? "", a.PitRating, Pits.League(a.PitRating), a.PitWins, a.PitLosses,
            WeaponLine(Worn(a)), a.Id == account.Id)).ToArray();

        return new PitsDto(account.PitRating, Pits.League(account.PitRating), account.PitWins, account.PitLosses, account.Laurels, PitTicketsLeft(account),
            offered, board, message);
    }

    /// <summary>New challengers (free).</summary>
    public async Task<PitsDto> PitRefreshAsync(Account account, CancellationToken ct)
    {
        account.PitRoll++;
        await SaveAsync(ct);
        return await PitsAsync(account, "New challengers step into the pit.", ct);
    }

    public async Task<PitFightDto> PitFightAsync(Account account, PitFightRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        if (PitTicketsLeft(account) <= 0) throw new GameException("no_tickets", "Today's Pit tickets are spent. New ones come at 20:00.");
        List<Challenger> challengers = await ChallengersAsync(account, ct);
        Challenger foe = challengers.FirstOrDefault(c => c.Id == request.OpponentId)
            ?? throw new GameException("no_challenger", "That challenger has left the pit. Look again.");

        ulong seed = BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(8));
        var rng = new XorShiftRandom(seed);
        double edge = Edge(account, foe);
        bool won = Duels.Roll(edge, rng);
        string foeName = (foe.Tag.Length > 0 ? $"[{foe.Tag}] " : "") + foe.Name;
        BossDef champion = Duels.Stage(foeName, Hero(account), won, seed);

        int before = account.PitRating;
        (int mine, int theirs) = Pits.Rate(account.PitRating, foe.Rating, won, foe.Account != null);
        account.PitRating = mine;
        if (won) account.PitWins++; else account.PitLosses++;
        int laurels = won ? Pits.WinLaurels : Pits.LossLaurels;
        account.Laurels += laurels;
        string day = Rules.Bounties.DayKey(_bells.LocalNow);
        account.PitFights = (account.PitDay == day ? account.PitFights : 0) + 1;
        account.PitDay = day;
        account.PitRoll++;
        // A real defender's rating moves too: one UPDATE, like other accounts' guild fields.
        if (foe.Account != null && theirs != foe.Rating)
        {
            int shift = theirs - foe.Rating;
            await _db.Accounts.Where(a => a.Id == foe.Account.Id).ExecuteUpdateAsync(s => s.SetProperty(a => a.PitRating, a => a.PitRating + shift), ct);
        }
        _db.Ledger.Add(Entry(account.Id, null, "pit",
            $"foe={foe.Id} seed={seed} edge={edge.ToString("0.000", CultureInfo.InvariantCulture)} won={won} rating={before}->{mine} laurels={laurels}", 0, request.RequestId));
        await SaveAsync(ct);

        ItemState? armor = foe.Gear.FirstOrDefault(i => i.Slot == EquipSlot.Armor);
        string text = won ? $"You beat {foeName} in the pit: rating {before} → {mine}, +{laurels} Laurels."
            : $"{foeName} threw you down: rating {before} → {mine}, +{laurels} Laurels.";
        var duel = new DuelResultDto(0, seed, champion.Name, champion.Hp, champion.Attack, won, (int)Math.Round(Duels.WinChance(edge) * 100), text,
            foe.Class, armor != null ? ItemLooks.Tier(armor.ItemLevel) : 0);
        return new PitFightDto(ToState(account), duel, await PitsAsync(account, "", ct), before, mine, laurels);
    }

    public async Task<PitsDto> PitShopAsync(Account account, PitShopRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        PitShopItem item = Pits.ShopItem(request.ItemId) ?? throw new GameException("no_item", "The Pit shop has no such thing.");
        if (account.Laurels < item.Laurels) throw new GameException("no_laurels", "Not enough Laurels.");
        account.Laurels -= item.Laurels;
        int[] shards = ParseShards(account.Korshards);
        shards[item.KorshardRank]++;
        account.Korshards = string.Join(';', shards);
        _db.Ledger.Add(Entry(account.Id, null, "pit-shop", $"{item.Name} for {item.Laurels} Laurels", 0, request.RequestId));
        await SaveAsync(ct);
        return await PitsAsync(account, $"A {item.Name} for {item.Laurels} Laurels.", ct);
    }
}
