using Microsoft.EntityFrameworkCore;
using Orsuun.Rules;
using Orsuun.Server.Data;

namespace Orsuun.Server.Game;

// The town square's life (owner, 28 Sep 2026: picked "Town life": "Other heroes who are in town right now stand around
// the square in their own looks and names"). A hero in town says so every so often (/v1/town); the answer is who else
// is there now, as the square draws them: class, figure, the armour and weapon bands with their shine, a worn skin.
public sealed partial class GameService
{
    /// <summary>Heroes shown in a square at most, and how long a visit counts after the last word from it.</summary>
    public const int TownShown = 6, TownPresentSeconds = 75;

    public async Task<TownDto> TownAsync(Account account, TownVisitRequest request, CancellationToken ct)
    {
        DateTime now = DateTime.UtcNow;
        account.InTownUtc = request.Leaving ? null : now;
        await SaveAsync(ct);
        if (request.Leaving) return new TownDto(Array.Empty<TownHeroDto>());
        DateTime since = now.AddSeconds(-TownPresentSeconds);
        List<Guid> blocked = BlockedList(account);
        List<Account> heroes = (await _db.Accounts.AsNoTracking()
                .Where(a => a.InTownUtc > since && a.Id != account.Id && a.BannedUtc == null && !a.AtRiver)
                .OrderByDescending(a => a.InTownUtc).Take(TownShown + blocked.Count).ToListAsync(ct))
            .Where(a => !blocked.Contains(a.Id)).Take(TownShown).ToList();
        return new TownDto(await DrawnAsync(heroes, ct));
    }

    /// <summary>Other heroes shown hunting beside the road at most, and how fresh their last heartbeat must be.</summary>
    public const int FieldShown = 4, FieldPresentSeconds = 180;

    /// <summary>
    /// The field's other hunters (owner, 28 Sep 2026: the Metin2-style field, "C + Other players"): heroes hunting the
    /// same place now (a campaign map, else the same zone; nobody on a dungeon floor), by their last heartbeat, not at
    /// the river, not blocked or banned. The field draws them beside the road fighting monsters of their own.
    /// </summary>
    public async Task<TownDto> FieldAsync(Account account, CancellationToken ct)
    {
        int stage = account.ParkedStage;
        if (Dungeons.IsFloor(stage) || account.AtRiver) return new TownDto(Array.Empty<TownHeroDto>());
        int first = stage, last = stage;
        if (!Content.IsZone(stage))
        {
            first = (Content.MapOfStage(stage).Id - 1) * MapDef.StagesPerMap + 1;
            last = first + MapDef.StagesPerMap - 1;
        }
        DateTime since = DateTime.UtcNow.AddSeconds(-FieldPresentSeconds);
        List<Guid> blocked = BlockedList(account);
        List<Account> heroes = (await _db.Accounts.AsNoTracking()
                .Where(a => a.LastHeartbeatUtc > since && a.Id != account.Id && a.BannedUtc == null && !a.AtRiver
                            && a.ParkedStage >= first && a.ParkedStage <= last)
                .OrderByDescending(a => a.LastHeartbeatUtc).Take(FieldShown + blocked.Count).ToListAsync(ct))
            .Where(a => !blocked.Contains(a.Id)).Take(FieldShown).ToList();
        return new TownDto(await DrawnAsync(heroes, ct));
    }

    /// <summary>Heroes as a square or a field draws them: class, figure, the armour and weapon bands with their shine, a skin.</summary>
    private async Task<TownHeroDto[]> DrawnAsync(List<Account> heroes, CancellationToken ct)
    {
        List<Guid> ids = heroes.Select(h => h.Id).ToList();
        List<Item> gear = ids.Count == 0 ? new List<Item>() : await _db.Items.AsNoTracking()
            .Where(i => ids.Contains(i.OwnerId) && i.Equipped && !i.Destroyed && (i.Slot == EquipSlot.Weapon || i.Slot == EquipSlot.Armor))
            .ToListAsync(ct);
        return heroes.Select(h =>
        {
            Item? armor = gear.FirstOrDefault(i => i.OwnerId == h.Id && i.Slot == EquipSlot.Armor);
            Item? weapon = gear.FirstOrDefault(i => i.OwnerId == h.Id && i.Slot == EquipSlot.Weapon);
            string skin = WornPieces(h).FirstOrDefault(p => p.Kind == WardrobeKind.Skin)?.Look ?? "";
            return new TownHeroDto(h.Id, NameOf(h), TitleOf(h) ?? "", h.Class, h.Figure, Content.LevelFor(h.Xp), h.Banner, skin,
                armor?.ItemLevel ?? 1, armor?.UpgradeLevel ?? 0, weapon?.ItemLevel ?? 1, weapon?.UpgradeLevel ?? 0);
        }).ToArray();
    }
}
