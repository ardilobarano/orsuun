using Orsuun.Rules;

namespace Orsuun.Server.Game;

public sealed record GuestLoginRequest(string DeviceToken);
public sealed record GuestLoginResponse(Guid AccountId, string SessionToken, bool Created);

public sealed record ForgeRequest(string RequestId, ForgeMethod Method);
public sealed record TurnRequest(string RequestId);

public sealed record EtchingDto(int EntryId, string Name, int Tier, int Value);

public sealed record WeaponDto(
    Guid Id, int ItemLevel, Rarity Rarity, int UpgradeLevel, int PatienceBp, int LockedEtchingIndex, EtchingDto[] Etchings);

public sealed record InventoryDto(long Sorn, int Potions, int Materials, int ScrollsOfMercy, int KhansAlloys, int AnvilWards, int Turnstones);

public sealed record ForgePreviewDto(long Cost, int Materials, int ChanceAloneBp, int ChanceAlloyBp, bool OathbreakPossible);

public sealed record SettlementDto(long CountedSeconds, long Packs, long Korstones, long SornEarned, bool Offline);

/// <summary>Everything the client needs to draw the HUD and the Forge. Returned by every mutating call.</summary>
public sealed record StateDto(
    Guid AccountId,
    InventoryDto Inventory,
    WeaponDto Weapon,
    ForgePreviewDto Forge,
    int WeaponsBroken,
    DateTime ServerUtc,
    SettlementDto? Settlement,
    ForgeResultDto? LastForge);

public sealed record ForgeResultDto(ForgeOutcome Outcome, int ChanceBp, int LevelBefore, int LevelAfter);

public sealed record ErrorDto(string Code, string Message);
