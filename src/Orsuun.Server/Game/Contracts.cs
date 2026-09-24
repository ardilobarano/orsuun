using Orsuun.Rules;
using Orsuun.Rules.Combat;

namespace Orsuun.Server.Game;

public sealed record GuestLoginRequest(string DeviceToken);
public sealed record GuestLoginResponse(Guid AccountId, string SessionToken, bool Created);

/// <summary>Slot picks the equipped item on the anvil; every item follows the weapon's rules. Omitted = weapon.</summary>
/// <summary>A tapped skill in a loop report: the lane tick (from the loop start) and the skill index.</summary>
public sealed record CastDto(int Tick, int Skill);
/// <summary>One finished lane loop, for the server to replay (Rules.Combat.ActivePlay).</summary>
public sealed record LoopReportDto(int Loop, int Ticks, int Potions, bool[]? AutoCast, CastDto[]? Casts);
/// <summary>Heartbeat body: the loops finished since the last one. Empty or missing = plain auto-cast pace.</summary>
public sealed record HeartbeatRequest(LoopReportDto[]? Loops = null);
/// <summary>The lane seed (decimal string, it is a ulong) and the next loop number the server expects.</summary>
public sealed record LaneDto(string Seed, int Loop);

/// <summary>ItemId picks any owned piece, worn or in the bag; without it the piece worn in Slot is used.</summary>
public sealed record ForgeRequest(string RequestId, ForgeMethod Method, EquipSlot Slot = EquipSlot.Weapon, Guid? ItemId = null);
/// <summary>
/// Count 1..50 (10 without Hearthfire Blessing). Targets (the turning helper, up to five etchings with tiers) stop the
/// batch once all are on the item; StopEntryId/MinTier is the older one-etching stop rule, used when Targets is empty.
/// </summary>
public sealed record TurnRequest(string RequestId, int Count = 1, int? StopEntryId = null, int MinTier = 1, EquipSlot Slot = EquipSlot.Weapon, Guid? ItemId = null,
    TurnTargetDto[]? Targets = null);

public sealed record TurnTargetDto(int EntryId, int MinTier);
public sealed record TurnResultDto(int Turns, int TurnstonesSpent, bool Stopped);

/// <summary>Active Evening Bell and the next one, in server-local time.</summary>
public sealed record BellDto(Bell Active, string ActiveName, Bell Next, int MinutesUntilNext, string ServerLocalTime);

public sealed record EtchingDto(int EntryId, string Name, int Tier, int Value);

public sealed record SocketDto(bool Dead, string? Type, int Rank, string Text);

public sealed record ItemDto(
    Guid Id, EquipSlot Slot, bool Equipped, string Name, int ItemLevel, Rarity Rarity, int UpgradeLevel, int PatienceBp, int LockedEtchingIndex,
    EtchingDto[] Etchings, SocketDto[] Sockets);

public sealed record SocketInsertRequest(string RequestId, Guid ItemId, int SocketIndex, ShardType Type, int Rank);
public sealed record SocketClearRequest(string RequestId, Guid ItemId, int SocketIndex);
public sealed record SocketResultDto(bool Success, int SocketIndex, string Text);

public sealed record EquipRequest(string RequestId, Guid ItemId);
public sealed record ParkRequest(int Stage);
/// <summary>An error the client caught; stored for the team, at most ClientLogsPerHour per account.</summary>
public sealed record ClientLogRequest(string Platform, string Version, string Message, string? Stack = null);
/// <summary>Switches the class being played (playtest: free and instant).</summary>
public sealed record ClassRequest(HeroClass HeroClass);
public sealed record PushRequest(string RequestId);

/// <summary>The server's verdict on a push plus the seed the client replays to show it.</summary>
/// <summary>Bell is the bell that was active when the run was scored; the replay must apply the same one.</summary>
public sealed record PushResultDto(int Stage, bool Cleared, ulong Seed, int Ticks, int NewHighestStageCleared, int PotionsAtStart, Bell Bell);

public sealed record HeroDto(long Attack, long Defense, long MaxHp, int CritChanceBp);

public sealed record InventoryDto(long Sorn, int Potions, int Materials, int ScrollsOfMercy, int KhansAlloys, int AnvilWards, int Turnstones,
    int EtchingNeedles, int SummoningMarkers, long Xp, int Level, int[] Korshards, string[] Skins);

public sealed record BossFightRequest(string RequestId, int BossId);

/// <summary>One Commander's state for the panel: up now with seconds left, or next spawn in N seconds.</summary>
public sealed record BossStatusDto(int BossId, string Name, string Mechanic, bool Up, long SecondsLeft, bool FoughtThisSpawn);

public sealed record BossFightResultDto(int BossId, ulong Seed, long Damage, bool Killed, int Rank, string Chest, int PotionsAtStart, Bell Bell);

public sealed record ForgePreviewDto(long Cost, int Materials, int ChanceAloneBp, int ChanceAlloyBp, bool OathbreakPossible);

public sealed record SettlementDto(long CountedSeconds, long Packs, long Korstones, long SornEarned, bool Offline, int ActiveBp = 10000, int LoopsVerified = 0);

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
    BossStatusDto[] Bosses,
    BellDto Bell,
    DateTime ServerUtc,
    SettlementDto? Settlement,
    ForgeResultDto? LastForge,
    PushResultDto? LastPush,
    BossFightResultDto? LastBossFight,
    SocketResultDto? LastSocket,
    TurnResultDto? LastTurn,
    LaneDto? Lane = null,
    HeroClass HeroClass = HeroClass.Vanguard);

public sealed record ForgeResultDto(ForgeOutcome Outcome, int ChanceBp, int LevelBefore, int LevelAfter);

public sealed record ErrorDto(string Code, string Message);
