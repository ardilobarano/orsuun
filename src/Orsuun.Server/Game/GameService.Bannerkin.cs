using Orsuun.Rules;
using Orsuun.Server.Data;

namespace Orsuun.Server.Game;

// The Bannerkin (owner, 28 Sep 2026; Rules.Bannerkin): joins at level 25 with a plain set, wears pieces of its own
// (Item.Kin, worn ones Item.KinWorn: out of the bag, still forgeable), and fights beside the hero (Hero(account)).
public sealed partial class GameService
{
    /// <summary>What the Bannerkin wears, for the hero's stats (null before it joins).</summary>
    private static IEnumerable<ItemState>? KinPieces(Account a) =>
        a.KinJoined ? a.Items.Where(i => i.KinWorn && i.Kin && !i.Destroyed).Select(i => i.ToState()).ToList() : null;

    private static KinDto KinOf(Account a)
    {
        KinStats? stats = Bannerkin.Stats(KinPieces(a));
        ItemDto[] worn = a.KinJoined ? a.Items.Where(i => i.KinWorn && !i.Destroyed).OrderBy(i => i.Slot).Select(ToDto).ToArray() : Array.Empty<ItemDto>();
        return new KinDto(a.KinJoined, worn, stats?.Score ?? 0, stats?.FocusBp ?? 0, Bannerkin.FocusSeconds, Bannerkin.FocusCooldownSeconds,
            stats?.HealPercent ?? 0, Bannerkin.HealCooldownSeconds);
    }

    /// <summary>A piece broken at the Forge: the hero's worn one leaves a starter, the Bannerkin's a plain kin piece; one
    /// from the bag is simply gone.</summary>
    private void BreakPiece(Account account, Item item)
    {
        bool worn = item.Equipped, kinWorn = item.KinWorn;
        item.Equipped = false;
        item.KinWorn = false;
        if (item.Slot == EquipSlot.Weapon && !item.Kin) account.WeaponsBroken++;
        if (worn) account.Items.Add(Item.From(NewStarter(item.Slot), account.Id, equipped: true));
        if (kinWorn)
        {
            Item plain = Item.From(Bannerkin.NewPiece(PlayerSession.StarterItemLevel, Rarity.Rare, item.Slot), account.Id, equipped: false);
            plain.KinWorn = true;
            account.Items.Add(plain);
        }
    }

    /// <summary>The Bannerkin joins (level 25): it comes with six Rare pieces of the hero's level band, worn.</summary>
    public async Task<StateDto> JoinKinAsync(Account account, CancellationToken ct)
    {
        if (account.KinJoined) return ToState(account);
        if (Content.LevelFor(account.Xp) < Bannerkin.JoinLevel)
            throw new GameException("kin_level", $"The Bannerkin joins heroes of level {Bannerkin.JoinLevel}.");
        // The hunt so far is paid with the hero as he was; the lane starts again with the Bannerkin behind him.
        DateTime now = DateTime.UtcNow;
        SettlementDto settlement = Settle(account, now);
        account.LastHeartbeatUtc = now;
        foreach (ItemState piece in Bannerkin.StarterSet(Content.LevelFor(account.Xp)))
        {
            Item item = Item.From(piece, account.Id, equipped: false);
            item.KinWorn = true;
            account.Items.Add(item);
        }
        account.KinJoined = true;
        NewLane(account);
        _db.Ledger.Add(Entry(account.Id, null, "kin-join", "", 0, Guid.NewGuid().ToString("N")));
        await SaveAsync(ct);
        return ToState(account, settlement: settlement);
    }

    /// <summary>Gives the Bannerkin a piece from the bag; what it wore in that slot goes back to the bag. A change of the
    /// kin changes the hero, so the time so far is settled and the lane gets a fresh seed (as with the wardrobe).</summary>
    public async Task<StateDto> WearKinAsync(Account account, KinWearRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        if (!account.KinJoined) throw new GameException("no_kin", "The Bannerkin has not joined you yet.");
        Item item = account.Items.SingleOrDefault(i => i.Id == request.ItemId && !i.Destroyed && !i.OutOfBag && !i.Equipped)
                    ?? throw new GameException("no_item", "That piece is not in your bag.");
        if (!item.Kin || !Bannerkin.Wears(item.Slot)) throw new GameException("not_kin", "Only Bannerkin pieces fit the Bannerkin.");
        DateTime now = DateTime.UtcNow;
        SettlementDto settlement = Settle(account, now);
        account.LastHeartbeatUtc = now;
        foreach (Item worn in account.Items.Where(i => i.KinWorn && i.Slot == item.Slot)) worn.KinWorn = false;
        item.KinWorn = true;
        NewLane(account);
        _db.Ledger.Add(Entry(account.Id, item.Id, "kin-wear", item.Slot.ToString(), 0, request.RequestId));
        await SaveAsync(ct);
        return ToState(account, settlement: settlement);
    }
}
