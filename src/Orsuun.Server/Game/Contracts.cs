using Orsuun.Rules;

namespace Orsuun.Server.Game;

public sealed record GuestLoginRequest(string DeviceToken);
public sealed record GuestLoginResponse(Guid AccountId, string SessionToken, bool Created);

public sealed record ForgeRequest(string RequestId, ForgeMethod Method);
public sealed record TurnRequest(string RequestId);

public sealed record EtchingDto(int EntryId, string Name, int Tier, int Value);

public sealed record ItemDto(
    Guid Id, EquipSlot Slot, bool Equipped, string Name, int ItemLevel, Rarity Rarity, int UpgradeLevel, int PatienceBp, int LockedEtchingIndex, EtchingDto[] Etchings);

public sealed record EquipRequest(string RequestId, Guid ItemId);
public sealed record ParkRequest(int Stage);
public sealed record PushRequest(string RequestId);

/// <summary>The server's verdict on a push plus the seed the client replays to show it.</summary>
public sealed record PushResultDto(int Stage, bool Cleared, ulong Seed, int Ticks, int NewHighestStageCleared, int PotionsAtStart);

public sealed record HeroDto(long Attack, long Defense, long MaxHp, int CritChanceBp);

public sealed record InventoryDto(long Sorn, int Potions, int Materials, int ScrollsOfMercy, int KhansAlloys, int AnvilWards, int Turnstones);

public sealed record ForgePreviewDto(long Cost, int Materials, int ChanceAloneBp, int ChanceAlloyBp, bool OathbreakPossible);

public sealed record SettlementDto(long CountedSeconds, long Packs, long Korstones, long SornEarned, bool Offline);

/// <summary>Everything the client needs to draw the HUD and the Forge. Returned by every mutating call.</summary>
public sealed record StateDto(
    Guid AccountId,
    InventoryDto Inventory,
    ItemDto Weapon,
    ItemDto[] Items,
    HeroDto Hero,
    ForgePreviewDto Forge,
    int WeaponsBroken,
    int HighestStageCleared,
    int ParkedStage,
    DateTime ServerUtc,
    SettlementDto? Settlement,
    ForgeResultDto? LastForge,
    PushResultDto? LastPush);

public sealed record ForgeResultDto(ForgeOutcome Outcome, int ChanceBp, int LevelBefore, int LevelAfter);

public sealed record ErrorDto(string Code, string Message);
