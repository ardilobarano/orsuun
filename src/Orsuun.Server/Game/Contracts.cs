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
    int EtchingNeedles, int SummoningMarkers, long Xp, int Level, int[] Korshards, string[] Skins, int HuntMarks = 0, int PinningWax = 0, int Tallies = 0);

/// <summary>One bounty with this account's count toward it (the server counts; the client only shows).</summary>
public sealed record BountyDto(int Id, string Title, BountyPeriod Period, long Count, int Target, int Marks, bool Claimed);
public sealed record BountyBoardDto(BountyDto[] Items, int DailyResetSeconds, int WeeklyResetSeconds);
public sealed record ShopItemDto(int Id, string Name, int Marks, string Detail);
public sealed record ClaimBountyRequest(string RequestId, int BountyId);
public sealed record ShopBuyRequest(string RequestId, int ShopItemId, int Count = 1);
/// <summary>ETCH: an Etching Needle on an owned piece. PIN: Pinning Wax on one of its etchings (again on it: unpin).</summary>
public sealed record EtchRequest(string RequestId, Guid ItemId);
public sealed record PinRequest(string RequestId, Guid ItemId, int Index);
public sealed record EtchResultDto(bool Took, int ChanceBp, string Text);

/// <summary>The oath: once, to one of the three Banners.</summary>
public sealed record BannerRequest(Banner Banner);
public sealed record BannerStandingDto(Banner Banner, string Name, long Points, int Fortresses);
public sealed record FortressDto(int Id, string Name, string Region, Banner Holder, SiegePhase Phase, long Wall, long WallMax,
    long SiegeEmber, long SiegeSky, long SiegeGold, string LastEvent, string FlagGuild = "");
/// <summary>The War of Banners at a glance: this season's points, last season's winner and its bonus, the fortresses.</summary>
public sealed record WarDto(string Season, BannerStandingDto[] Standings, Banner LastWinner, int MySornBonusPercent, FortressDto[] Fortresses,
    int SiegeCooldownSeconds);
public sealed record SiegeRequest(string RequestId, int FortressId);
public sealed record SiegeResultDto(int FortressId, int BossId, bool Defending, ulong Seed, long Damage, int PotionsAtStart, Bell Bell,
    SiegePhase Phase, long WallLeft, bool PhaseBroken, bool Captured, Banner Holder, string Text);
/// <summary>The account's guild at a glance (every state carries it; empty Tag = no guild).</summary>
public sealed record GuildBriefDto(string Tag, string Name, string Color, GuildRank Rank);
public sealed record GuildDto(Guid Id, string Name, string Tag, string Color, bool Open, int Level, long Xp, long NextLevelXp, long Treasury,
    int Plunder, int Muster, int Members, int MaxMembers, int SornBonusPercent, string[] Fortresses, string LastEvent);
public sealed record GuildMemberDto(Guid AccountId, string Name, Banner Banner, GuildRank Rank, int Level, long Donated, int LastSeenMinutes, bool Me);
public sealed record GuildListItemDto(Guid Id, string Name, string Tag, string Color, int Level, int Members, int MaxMembers, bool Open);
/// <summary>
/// The GUILD screen: the account's guild with its members, or (no guild) guilds to join. Every guild call returns it,
/// with the account's state inside.
/// </summary>
public sealed record GuildViewDto(StateDto State, GuildDto? Mine, GuildMemberDto[] Members, GuildListItemDto[] Browse, long DonatedToday,
    long DonationCap, string Message = "");
public sealed record GuildCreateRequest(string RequestId, string Name, string Tag, string Color);
public sealed record GuildJoinRequest(string RequestId, Guid GuildId);
public sealed record GuildLeaveRequest(string RequestId);
public sealed record GuildMemberRequest(string RequestId, Guid AccountId, GuildRank Rank = GuildRank.Member);
public sealed record GuildDonateRequest(string RequestId, long Sorn);
public sealed record GuildSkillRequest(string RequestId, GuildSkill Skill);
public sealed record GuildShopRequest(string RequestId, int ItemId);
public sealed record GuildSettingsRequest(string RequestId, bool Open, string Color);

/// <summary>A fighter on a Commander spawn's damage board.</summary>
public sealed record BossHitDto(string Name, Banner Banner, long Damage);

public sealed record BossFightRequest(string RequestId, int BossId);

/// <summary>One Commander's state for the panel: up now with seconds left, or next spawn in N seconds.</summary>
/// <summary>Since 24 Sep 2026 each spawn has one HP pool for the whole server: HpLeft/HpMax, who slew it, the top fighters.</summary>
public sealed record BossStatusDto(int BossId, string Name, string Mechanic, bool Up, long SecondsLeft, bool FoughtThisSpawn,
    long HpLeft = 0, long HpMax = 0, bool Slain = false, string? SlainBy = null, Banner SlainBanner = Banner.None, BossHitDto[]? Top = null);

/// <summary>PoolLeft: the shared pool after this fight; Slew: this fight took the last of it.</summary>
public sealed record BossFightResultDto(int BossId, ulong Seed, long Damage, bool Killed, int Rank, string Chest, int PotionsAtStart, Bell Bell,
    long PoolLeft = 0, bool Slew = false);

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
    HeroClass HeroClass = HeroClass.Vanguard,
    BountyBoardDto? Bounties = null,
    Banner Banner = Banner.None,
    string Name = "",
    SiegeResultDto? LastSiege = null,
    EtchResultDto? LastEtch = null,
    GuildBriefDto? Guild = null);

public sealed record ForgeResultDto(ForgeOutcome Outcome, int ChanceBp, int LevelBefore, int LevelAfter);

public sealed record ErrorDto(string Code, string Message);
