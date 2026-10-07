using Orsuun.Rules;
using Orsuun.Rules.Combat;

namespace Orsuun.Server.Game;

/// <summary>Lobby: the client has a character screen (25 Sep 2026); a new device then starts without a character.</summary>
public sealed record GuestLoginRequest(string DeviceToken, bool Lobby = false);
/// <summary>AccountId is the character chosen on this device (Guid.Empty: the character screen, since 25 Sep 2026).</summary>
public sealed record GuestLoginResponse(Guid AccountId, string SessionToken, bool Created, Guid LoginId = default, int Characters = 0);

/// <summary>Slot picks the equipped item on the anvil; every item follows the weapon's rules. Omitted = weapon.</summary>
/// <summary>A tapped skill in a loop report: the lane tick (from the loop start) and the skill index.</summary>
public sealed record CastDto(int Tick, int Skill);
/// <summary>One finished lane loop, for the server to replay (Rules.Combat.ActivePlay).</summary>
public sealed record LoopReportDto(int Loop, int Ticks, int Potions, bool[]? AutoCast, CastDto[]? Casts, int[]? Elite = null);
/// <summary>Heartbeat body: the loops finished since the last one. Empty or missing = plain auto-cast pace.</summary>
public sealed record HeartbeatRequest(LoopReportDto[]? Loops = null);
/// <summary>The lane seed (decimal string, it is a ulong) and the next loop number the server expects.</summary>
public sealed record LaneDto(string Seed, int Loop);

/// <summary>ItemId picks any owned piece, worn or in the bag; without it the piece worn in Slot is used.</summary>
public sealed record ForgeRequest(string RequestId, ForgeMethod Method, EquipSlot Slot = EquipSlot.Weapon, Guid? ItemId = null, bool Pearl = false);
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
    EtchingDto[] Etchings, SocketDto[] Sockets, int AverageDamage = 0, int SkillDamage = 0, bool Kin = false, bool KinWorn = false, int Temper = 0);
public sealed record TemperRequest(string RequestId, Guid ItemId);
public sealed record TemperDto(StateDto State, bool Success, int Before, int After, string Message);

public sealed record SocketInsertRequest(string RequestId, Guid ItemId, int SocketIndex, ShardType Type, int Rank);
public sealed record SocketClearRequest(string RequestId, Guid ItemId, int SocketIndex);
public sealed record SocketResultDto(bool Success, int SocketIndex, string Text);

public sealed record EquipRequest(string RequestId, Guid ItemId);
/// <summary>Sells bag pieces to the merchant for sorn (Rules.Bag.SellPrice): ItemId, or the pieces picked in ItemIds.</summary>
public sealed record BagSellRequest(string RequestId, Guid ItemId, Guid[]? ItemIds = null);
public sealed record ParkRequest(int Stage);
/// <summary>An error the client caught; stored for the team, at most ClientLogsPerHour per account.</summary>
public sealed record ClientLogRequest(string Platform, string Version, string Message, string? Stack = null);

/// <summary>A first only the phone sees (GameService.Funnel): "tutorial-3", "tutorial-done", "tutorial-skipped".</summary>
public sealed record MilestoneRequest(string Name);
public sealed record AdminFunnelStepDto(string Name, string Label, int Count, double MedianMinutes);
public sealed record AdminFunnelDto(int Days, int Heroes, AdminFunnelStepDto[] WayIn, AdminFunnelStepDto[] Steps);
public sealed record AdminErrorDto(string Message, int Count, int Heroes, string Platforms, string Versions, DateTime FirstUtc, DateTime LastUtc, string Stack);
/// <summary>Switches the class being played (playtest: free and instant).</summary>
public sealed record ClassRequest(HeroClass HeroClass);
public sealed record PushRequest(string RequestId);

/// <summary>The server's verdict on a push plus the seed the client replays to show it.</summary>
/// <summary>Bell is the bell that was active when the run was scored; the replay must apply the same one.</summary>
public sealed record PushResultDto(int Stage, bool Cleared, ulong Seed, int Ticks, int NewHighestStageCleared, int PotionsAtStart, Bell Bell);

public sealed record HeroDto(long Attack, long Defense, long MaxHp, int CritChanceBp);

public sealed record InventoryDto(long Sorn, int Potions, int Materials, int ScrollsOfMercy, int KhansAlloys, int AnvilWards, int Turnstones,
    int EtchingNeedles, int SummoningMarkers, long Xp, int Level, int[] Korshards, string[] Skins, int HuntMarks = 0, int PinningWax = 0, int Tallies = 0,
    int MastersNeedles = 0, int Oathstones = 0, int[]? Books = null, int[]? Fish = null, int Mussels = 0, int[]? Pearls = null, int GrandmasterNeedles = 0);

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
/// <summary>Changes the account's Banner (once a season, Rules.Banners.ChangeOathstones from this hero).</summary>
public sealed record BannerChangeRequest(string RequestId, Banner Banner);
/// <summary>Oath Renewal (Rules.OathRenewal): the hero back to level 1 for a lasting bonus.</summary>
public sealed record RenewRequest(string RequestId);
/// <summary>One try at a skill's next grade (Rules.SkillGrades) for the class played: Slot 0 burst, 1 area, 2 haste.</summary>
public sealed record SkillTrainRequest(string RequestId, int Slot);
/// <summary>The hero after a try, and how it went.</summary>
public sealed record SkillTrainDto(StateDto State, int Slot, bool Success, int Grade, string Message);
public sealed record BannerStandingDto(Banner Banner, string Name, long Points, int Fortresses);
public sealed record FortressDto(int Id, string Name, string Region, Banner Holder, SiegePhase Phase, long Wall, long WallMax,
    long SiegeEmber, long SiegeSky, long SiegeGold, string LastEvent, string FlagGuild = "");
/// <summary>The War of Banners at a glance: this season's points, last season's winner and its bonus, the fortresses.</summary>
public sealed record WarDto(string Season, BannerStandingDto[] Standings, Banner LastWinner, int MySornBonusPercent, FortressDto[] Fortresses,
    int SiegeCooldownSeconds, KeepDto[]? Keeps = null, string Message = "");

/// <summary>
/// A fortress keep for the guilds (Rules.FortressKeeps): the holding guild, the week's bids, and the Sunday siege.
/// State 0 bids open (SecondsToSiege counts down), 1 the keep siege (SecondsLeft), 2 settled until the week turns.
/// </summary>
public sealed record KeepDto(int FortressId, string Name, string HolderTag, string HolderName, string HolderColor, int State, int SecondsToSiege,
    int SecondsLeft, KeepBidDto[] Bids, long MyBid, bool Contending, bool Holding, bool CanBid, bool CanFight, long Wall, long Mended, string LastEvent);
public sealed record KeepBidDto(string Tag, string Name, string Color, long Amount, bool Contender, long Damage, bool Mine);
public sealed record KeepBidRequest(string RequestId, int FortressId, long Amount);
public sealed record KeepFightRequest(string RequestId, int FortressId);

/// <summary>Guild war (Rules.GuildWars): the guild's record, the next night, tonight's war and the ladder.</summary>
public sealed record GuildWarDto(int Rating, int Wins, int Losses, int Draws, string NextNight, int SecondsToNext, bool SignedUp, int SignedGuilds,
    bool CanSignUp, bool CanFlag, bool AtWar, GuildWarFoeDto? Foe, int MyKills, int TheirKills, int MyScore, int TheirScore, GuildWarLaneDto[] Lanes,
    int SecondsLeft, int FightsLeft, int CooldownSeconds, string LastEvent, string LastResult, GuildWarLadderDto[] Ladder, string Message = "");
public sealed record GuildWarFoeDto(string Tag, string Name, string Color, int Level, int Rating);
/// <summary>A lane from this guild's side: Front runs from -5 (broken against us) to +5 (we broke it).</summary>
public sealed record GuildWarLaneDto(string Name, int Front, bool MyFlag, bool TheirFlag, bool Broken);
public sealed record GuildWarLadderDto(string Tag, string Name, string Color, int Rating, int Wins, int Losses, int Draws, bool Mine);
public sealed record GuildWarSignupRequest(bool Join);
public sealed record GuildWarFlagRequest(int Lane);
public sealed record GuildWarFightRequest(string RequestId, int Lane);
/// <summary>One duel: the replay is the attacker's hero against Champion (HP, attack) under Seed, with no draughts.</summary>
public sealed record DuelResultDto(int Lane, ulong Seed, string Champion, long ChampionHp, long ChampionAttack, bool Won, int WinChancePercent, string Text,
    HeroClass DefenderClass = HeroClass.Vanguard, int DefenderBand = 0, Figure DefenderFigure = Figure.Man);
public sealed record GuildWarFightDto(StateDto State, DuelResultDto Duel, GuildWarDto War);
public sealed record SiegeRequest(string RequestId, int FortressId);
public sealed record SiegeResultDto(int FortressId, int BossId, bool Defending, ulong Seed, long Damage, int PotionsAtStart, Bell Bell,
    SiegePhase Phase, long WallLeft, bool PhaseBroken, bool Captured, Banner Holder, string Text);
/// <summary>The account's guild at a glance (every state carries it; empty Tag = no guild).</summary>
public sealed record GuildBriefDto(string Tag, string Name, string Color, GuildRank Rank);
public sealed record GuildDto(Guid Id, string Name, string Tag, string Color, bool Open, int Level, long Xp, long NextLevelXp, long Treasury,
    int Plunder, int Muster, int Members, int MaxMembers, int SornBonusPercent, string[] Fortresses, string LastEvent);
public sealed record GuildMemberDto(Guid AccountId, string Name, Banner Banner, GuildRank Rank, int Level, long Donated, int LastSeenMinutes, bool Me);
public sealed record GuildListItemDto(Guid Id, string Name, string Tag, string Color, int Level, int Members, int MaxMembers, bool Open, bool Requested = false,
    string InvitedBy = "");
/// <summary>
/// The GUILD screen: the account's guild with its members, or (no guild) guilds to join. Every guild call returns it,
/// with the account's state inside.
/// </summary>
public sealed record GuildViewDto(StateDto State, GuildDto? Mine, GuildMemberDto[] Members, GuildListItemDto[] Browse, long DonatedToday,
    long DonationCap, string Message = "", GuildMemberDto[]? Requests = null, string[]? Log = null, GuildListItemDto[]? Invites = null,
    GuildMemberDto[]? Invited = null);
/// <summary>The leader or an officer invites a hero, by id (chat, friends) or by name.</summary>
public sealed record GuildInviteRequest(string RequestId, Guid AccountId, string? Name = null);
public sealed record GuildInviteAnswerRequest(string RequestId, Guid GuildId, bool Accept);

/// <summary>A hero on the friend list (or asking, or asked): MinutesAway since its last heartbeat (Rules.Friends.Seen).</summary>
public sealed record FriendDto(Guid AccountId, string Name, HeroClass Class, int Level, Banner Banner, string GuildTag, int MinutesAway);
public sealed record FriendsDto(FriendDto[] Friends, FriendDto[] Asking, FriendDto[] Asked, int Max, bool CanInvite, string Message = "");
/// <summary>Asks a hero, by id (chat) or by name, to be friends; asking one who already asked you makes you friends.</summary>
public sealed record FriendAddRequest(Guid AccountId, string? Name = null);
public sealed record FriendAnswerRequest(Guid AccountId, bool Accept);
/// <summary>Takes a friend off the list, takes back a request, or turns one down.</summary>
public sealed record FriendRemoveRequest(Guid AccountId);
public sealed record GuildAnswerRequest(string RequestId, Guid AccountId, bool Accept);

/// <summary>One chat line; System lines (guild and world events) have no speaker.</summary>
/// <summary>A chat line; on the Bazaar Call it may carry the piece it links (Item, as it is now), or LinkGone when the
/// sender no longer has it.</summary>
public sealed record ChatLineDto(long Id, Guid AccountId, string Name, Banner Banner, string Text, DateTime Utc, bool System, bool Mine, string? Title = null,
    ItemDto? Item = null, bool LinkGone = false);
/// <summary>A channel's lines after the id the client asked from (at most Rules.Chat.PageSize), and the newest id.</summary>
public sealed record ChatDto(string Channel, ChatLineDto[] Lines, long LatestId, int Blocked);
/// <summary>A line to say; on the Bazaar Call ItemId links one of the sender's pieces.</summary>
public sealed record ChatSayRequest(string Channel, string Text, long After = 0, Guid? ItemId = null);
public sealed record ChatReportRequest(long MessageId, string Channel = "world");
public sealed record ChatBlockRequest(Guid AccountId, bool Block, string Channel = "world");

/// <summary>A piece on the Salt Exchange with its full details, its price and its seller.</summary>
/// <summary>
/// A listing: a piece (Item), or with BookId a stack of BookCount Technique Scrolls, or with GoodId a stack of GoodCount
/// goods (Rules.TradeGoods; Item null for both).
/// </summary>
public sealed record ListingDto(long Id, ItemDto? Item, long Price, string SellerName, Banner SellerBanner, bool Mine, int MinutesLeft,
    ListingStatus Status = ListingStatus.Active, int BookId = -1, int BookCount = 0, int GoodId = -1, int GoodCount = 0, bool Rug = false);
public sealed record MarketDto(StateDto State, ListingDto[] Listings, int Page, int Pages, int Total, ListingDto[] Mine, int TaxPercent,
    string Message = "");
public sealed record MarketListRequest(string RequestId, Guid ItemId, long Price, int BookId = -1, int BookCount = 0, int GoodId = -1, int GoodCount = 0, bool Rug = false);
/// <summary>Rug Stalls: the rugs laid out (newest first), and one rug's wares.</summary>
public sealed record RugStallDto(Guid SellerId, string Name, Banner Banner, int Wares, bool Mine);
public sealed record RugsDto(RugStallDto[] Stalls, int MyWares, int MaxWares, string Message = "");
public sealed record RugDto(Guid SellerId, string Name, Banner Banner, ListingDto[] Wares, bool Mine, int TaxPercent);
/// <summary>
/// What a kind of thing sold for over Market.HistoryDays: sales, the average, lowest and highest price for one (a stack's
/// price over its count), the last sale and how long ago, and up to ten recent prices for one, newest first.
/// </summary>
public sealed record PriceHistoryDto(string What, int Sales, long Average, long Low, long High, long Last, int LastMinutesAgo, int Days, long[] Recent);
public sealed record MarketBuyRequest(string RequestId, long ListingId);

/// <summary>Sign up saves an email and password to the account being played; sign in moves this device to an account.</summary>
public sealed record RegisterRequest(string Email, string Password);
/// <summary>Password reset and email verification by emailed codes (GameService.Recovery).</summary>
public sealed record ForgotRequest(string Email);
public sealed record ResetRequest(string Email, string Code, string Password, string DeviceToken);
public sealed record VerifyRequest(string Code);
public sealed record MessageDto(string Message, bool EmailVerified = false);
public sealed record LoginRequest(string Email, string Password, string DeviceToken);
public sealed record AccountDto(string? Email, bool Registered);
/// <summary>Sign in with Google or Apple: begin gives the page to open; the app redeems the ticket it is sent back with.</summary>
public sealed record ExternalBeginRequest(string Provider);
public sealed record ExternalBeginDto(string Url);
public sealed record ExternalTicketRequest(string Ticket, string DeviceToken);
/// <summary>A native button's ID token (the iOS Apple button later), for the account being played.</summary>
public sealed record ExternalTokenRequest(string Provider, string IdToken, string? Nonce = null);
public sealed record ExternalLoginResultDto(Guid AccountId, string SessionToken, bool Switched, bool Linked, string Provider);
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

/// <summary>A settled stretch of hunting; LeftBehind counts the drops a full bag could not take.</summary>
public sealed record SettlementDto(long CountedSeconds, long Packs, long Korstones, long SornEarned, bool Offline, int ActiveBp = 10000, int LoopsVerified = 0,
    int LeftBehind = 0, long ElitePacks = 0, string? Elite = null);

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
    GuildBriefDto? Guild = null,
    string? Email = null,
    string[]? Logins = null,
    int DungeonRunsLeft = 0,
    long DungeonRunAtSmith = 0,
    WardrobeDto? Wardrobe = null,
    TrailDto? Trail = null,
    TradeBriefDto? Trade = null,
    int DungeonPausedId = 0,
    int FriendAsks = 0,
    int GuildInvites = 0,
    int Renewals = 0,
    int[]? SkillGrades = null,
    int[]? SkillProgress = null,
    long[]? SkillReadySeconds = null,
    long Honor = 0,
    int Whispers = 0,
    Figure Figure = Figure.Man,
    DailyDto? Daily = null,
    int Mail = 0,
    WorldEventDto[]? Events = null,
    int AchievementsReady = 0,
    string? Title = null,
    bool EmailVerified = false,
    GoalCountsDto? GoalCounts = null,
    RiverDto? River = null,
    KinDto? Kin = null,
    ErrandsDto? Errands = null,
    long CacheIn = -1,
    Guid PartyLeader = default,
    string? PartyInvite = null,
    int EliteCamp = -1,
    long EliteLeft = 0,
    long PartyLookLeft = 0,
    bool CommanderPushes = true,
    MapQuestDto? Quest = null,
    RainDto? Rain = null,
    BondBriefDto? Bond = null);
public sealed record CommanderPushRequest(bool On);

/// <summary>A hunting party (Rules.Parties): its members, the hunt's bonus now (percent), an invite waiting.</summary>
public sealed record PartyMemberDto(Guid Id, string Name, HeroClass Class, int Level, bool Online, bool Together, bool Leader, string Hunting);
public sealed record PartyDto(Guid LeaderId, PartyMemberDto[] Members, int BonusPercent, Guid InviteFrom, string InviteName, int MaxMembers,
    string? Message = null,
    Guid OpenDungeonId = default, int OpenDungeon = 0, long OpenDungeonLeft = 0, string OpenedBy = "", bool OpenJoined = false, string[]? OpenJoinedNames = null);
public sealed record PartyInviteRequest(Guid AccountId, string? Name = null);
/// <summary>The party board of the hero's place: heroes looking for a party, and how long the hero's own listing lasts.</summary>
public sealed record PartyBoardEntryDto(Guid Id, string Name, HeroClass Class, int Level, string? Title, int MinutesListed);
public sealed record PartyBoardDto(string Place, PartyBoardEntryDto[] Heroes, long LookingLeft, string? Message = null);
public sealed record PartyLookRequest(bool Look);
public sealed record PartyAnswerRequest(bool Accept);
public sealed record PartyKickRequest(Guid AccountId);

/// <summary>A trail cache opened (Rules.TrailCaches): the hero after, and what it held.</summary>
public sealed record CacheOpenRequest(string RequestId);
public sealed record CacheOpenDto(StateDto State, string Found);

/// <summary>A map's quest chain (Rules.MapQuests) for a hero: the step being worked on (Step = Steps: the chain is done), the
/// giver's words, what it asks, how far along, where on the map (a camp of its layout), the step's pay, and the campaign stage
/// to clear before the map (and its chain) opens.</summary>
public sealed record MapQuestDto(int Map, string MapName, string Giver, string Title, int Step, int Steps, string Ask, string Task, long Progress,
    long Target, string Camp, bool Ready, bool Open, long Sorn, long Xp, int Materials, bool Piece, string Ending, int OpensAfter);
public sealed record QuestsDto(MapQuestDto[] Maps, int Hunting);
public sealed record QuestClaimRequest(string RequestId, int Map);

/// <summary>The Endless Tower (Rules.Tower): the hero's week (best floor, climbs left, the next chest), the week's ladder and
/// the hero's place on it, when the week ends, and the title the hero won last week.</summary>
public sealed record TowerRowDto(int Rank, Guid Id, string Name, HeroClass Class, int Level, int Best, string Title);
public sealed record TowerDto(int Best, int ClimbsLeft, int ClimbsPerDay, int NextChest, string NextChestHolds, TowerRowDto[] Ladder, int Rank,
    long WeekEndsIn, string? Title, int BestEver, string? Message = null);
public sealed record TowerClimbRequest(string RequestId);
/// <summary>The Giant Korstone of Korstone Rain (Rules.KorstoneRain): its map and camp, its shared health, how long it
/// stands, the hero's strikes left, and whether (and by whom) it broke.</summary>
public sealed record RainDto(long Id, int Map, string MapName, int Camp, long HpLeft, long HpMax, long SecondsLeft, long SecondsSinceFall,
    int StrikesLeft, long MyDamage, bool Broken, string BrokenBy);
public sealed record RainStrikeRequest(string RequestId);
/// <summary>A sworn bond (Rules.Bonds): the partner and whether they hunt beside the hero now, the time together and the ring,
/// the XP bonus, an ask waiting, and whether the hero may ask (the wait after a bond ended).</summary>
public sealed record BondDto(Guid PartnerId, string PartnerName, HeroClass PartnerClass, int PartnerLevel, bool PartnerOnline, bool Together,
    DateTime? SinceUtc, long SecondsTogether, int Ring, string RingName, long NextRingSeconds, int XpBonusBp, Guid AskFrom, string AskName,
    long WaitSeconds, string? Message = null);
/// <summary>The bond as the state carries it: the partner and the ring, and an ask waiting.</summary>
public sealed record BondBriefDto(Guid Partner, string PartnerName, int Ring, string AskName);
public sealed record BondAskRequest(Guid AccountId, string? Name = null);
public sealed record BondAnswerRequest(bool Accept);
/// <summary>A strike: its seed and the draughts at its start (the lane replays it), the damage, the stone after, the line.</summary>
public sealed record RainStrikeDto(StateDto State, int Map, ulong Seed, int PotionsAtStart, long Damage, long HpLeft, long HpMax, bool Broke, string Text);
/// <summary>A climb: the last floors to replay (each with its seed and the draughts at its start), the floor reached, where the
/// hero fell, a new best, the chests opened on the way, and the line to show.</summary>
public sealed record TowerClimbDto(StateDto State, DungeonFloorDto[] Floors, int Reached, int FellOn, bool NewBest, string Chests, string Text, TowerDto Tower);

/// <summary>The townsfolk's errands today (Rules.Errands): each townsman's, how far along, whether paid, and the pay.</summary>
public sealed record ErrandDto(int Giver, int Id, string Text, long Progress, long Target, bool Paid);
public sealed record ErrandsDto(ErrandDto[] List, long Sorn, int Materials, long SecondsToReset);
public sealed record ErrandRequest(string RequestId, int Giver);

/// <summary>The Bannerkin (Rules.Bannerkin): joined or not, what it wears and what that makes of its two casts.</summary>
public sealed record KinDto(bool Joined, ItemDto[] Worn, int Score, int FocusBp, int FocusSeconds, int FocusCooldownSeconds, int HealPercent, int HealCooldownSeconds);
public sealed record KinWearRequest(string RequestId, Guid ItemId);

/// <summary>Fishing (Rules.Fishing): at the river or not, each fish's boost seconds left (by id), the Tireless Rod's seconds
/// left, and what the rod brought in since the last state (a heartbeat's auto catches).</summary>
public sealed record RiverDto(bool AtRiver, long[] MealSeconds, long RodSecondsLeft, int[]? AutoFish = null, int AutoMussels = 0);
public sealed record CastBiteDto(int BiteMs, int WindowMs);
public sealed record ReelDto(StateDto State, string Kind, int Fish, string Message, int Grams = 0);
/// <summary>The fishing contest's board: the one running (or the last one, when none runs), when it ends or the next
/// begins, the hero's best and rank in it, and its top ten.</summary>
public sealed record ContestDto(bool Running, bool Any, long EndsInSeconds, long NextInSeconds, int MyGrams, int MyFish, int MyRank,
    ContestRowDto[] Top, string[] Prizes);
public sealed record ContestRowDto(string Name, Banner Banner, int Fish, int Grams, bool Mine);
public sealed record LandRequest(bool Landed);
public sealed record OpenMusselsRequest(string RequestId, int Count);
public sealed record OpenMusselsDto(StateDto State, int Opened, int[] Pearls, string Message);
public sealed record EatRequest(string RequestId, int Fish);
public sealed record AutoRodRequest(string RequestId, int Days);

/// <summary>What the goal line asks of a hero's own history (Rules.GoalWorld; lifetime counters since 27 Sep 2026).</summary>
public sealed record GoalCountsDto(long Commanders, long Dungeons, long Bounties, int PitWins);

/// <summary>The ACHIEVEMENTS screen (Rules.Achievements): every achievement with its progress, and the title worn.</summary>
public sealed record AchievementsDto(StateDto State, AchievementDto[] List, int TitleId, string Title, string Message);
public sealed record AchievementDto(int Id, string Name, string Text, long Progress, long Target, int Honor, long Sorn, string? Title, bool Done, bool Claimed);
public sealed record AchievementClaimRequest(string RequestId, int Id);
public sealed record TitleRequest(int Id);

/// <summary>The guild's raid this week (Rules.GuildRaids): its boss, the pool, the hero's fights left today, the top ten.</summary>
public sealed record GuildRaidDto(string Boss, int Map, string MapName, string Mechanic, long HpMax, long HpLeft, long SecondsLeft, int FightsLeft,
    long MyDamage, RaidHitDto[] Top, bool Slain, string SlainBy, string Message);
public sealed record RaidHitDto(string Name, long Damage);
public sealed record RaidFightRequest(string RequestId);
public sealed record GuildRaidFightDto(GuildRaidDto Raid, ulong Seed, long Damage, bool Killed, int PotionsAtStart, StateDto State);

/// <summary>A world event on the server's calendar (Rules.WorldEvents) that runs now or comes within the week.</summary>
public sealed record WorldEventDto(string Kind, string Name, string Effect, bool Running, long StartsInSeconds, long EndsInSeconds);

/// <summary>
/// The login calendar: the day a claim now takes (1..7), whether today's is still to claim, the seven gifts for this
/// hero, and the seconds until the next bounty day.
/// </summary>
public sealed record DailyDto(int Day, bool Claimable, string[] Gifts, long SecondsToNext);
public sealed record DailyClaimRequest(string RequestId);

/// <summary>
/// The mailbox (Rules.Mail): the newest letters first, with what each still holds (Taken once it was taken), and the
/// hero's state after a take.
/// </summary>
public sealed record MailDto(StateDto State, LetterDto[] Letters, int Unread, string Message);
public sealed record LetterDto(long Id, string Kind, string From, string Title, string Body, DateTime Utc, bool Read, bool Taken,
    long Sorn, int GoodId, int GoodCount, int BookId, int BookCount, ItemDto? Item);
/// <summary>Takes what letter LetterId holds; 0 takes every letter's (a piece stays when the bag is full).</summary>
public sealed record MailTakeRequest(string RequestId, long LetterId);
/// <summary>Deletes letter LetterId once nothing is left in it; 0 deletes every such letter.</summary>
public sealed record MailDeleteRequest(long LetterId);

/// <summary>Private messages (Rules.Whispers): one conversation in the list.</summary>
public sealed record WhisperConversationDto(Guid AccountId, string Name, string Class, int Level, int MinutesAway, string LastText, DateTime LastUtc, bool LastMine, int Unread);
public sealed record WhispersDto(WhisperConversationDto[] Conversations, int Unread, string Message);
public sealed record WhisperLineDto(long Id, bool Mine, string Text, DateTime Utc);
/// <summary>A conversation's page: new lines after After, or older ones before Before; Blocked when you blocked them.</summary>
public sealed record WhisperThreadDto(Guid AccountId, string Name, string Class, int Level, int MinutesAway, WhisperLineDto[] Lines, long Latest, bool HasOlder, bool Blocked, string Message);
public sealed record WhisperSendRequest(Guid AccountId, string? Name, string Text, long After);
public sealed record WhisperReportRequest(long MessageId);

/// <summary>Amber and the wardrobe (Rules.Wardrobe): pieces held with the seconds they have left, and the one worn per slot.</summary>
public sealed record WardrobeDto(long Amber, WardrobePieceDto[] Pieces, string Skin, string Mount, string Companion, bool FirstPurchase);
public sealed record WardrobePieceDto(string Id, long SecondsLeft);
public sealed record CaravanBuyRequest(string RequestId, string PieceId, int Days);
/// <summary>Wears PieceId (a held piece with time left); an empty PieceId takes off the piece worn in Kind ("Skin", "Mount", "Companion").</summary>
public sealed record WearRequest(string RequestId, string PieceId, string Kind);
public sealed record AmberPackRequest(string RequestId, int PackId);
/// <summary>A store purchase to credit: the store ("apple", "google"; "test" on a Development server), the pack's store
/// product id and what the store gave the phone (Apple: the transaction id; Google: the purchase token).</summary>
public sealed record AmberPurchaseRequest(string Store, string ProductId, string Receipt);
/// <summary>Added false: that store transaction was credited before (the phone may finish it with the store all the same).</summary>
public sealed record AmberPurchaseDto(StateDto State, string Message, bool Added, long Amber);

/// <summary>
/// The Campaign Trail (Rules.CampaignTrail): the season, its XP and tier, the pass bought (0 none, 1 Trail, 2 Plus) and the
/// claimed tiers as bits (bit t-1 for tier t); Owed counts last season's rewards left unclaimed (the next claim hands
/// them over). The client draws the reward table from the shared rules.
/// </summary>
public sealed record TrailDto(int Season, string Name, long SecondsLeft, long Xp, int Tier, int XpIntoTier, int Pass, long FreeClaimed, long PaidClaimed,
    int Owed);
/// <summary>Claims tier Tier's ready rewards on both tracks; Tier 0 claims every reward ready.</summary>
public sealed record TrailClaimRequest(string RequestId, int Tier);
/// <summary>Buys the paid track, or Plus (the paid track and ten tiers; from the Trail it costs the difference).</summary>
public sealed record TrailBuyRequest(string RequestId, bool Plus);

/// <summary>A live direct trade in brief (on /me and the heartbeat): an invitation to answer, or a window to go back to.</summary>
public sealed record TradeBriefDto(long Id, TradeState State, bool Incoming, string OtherName);
/// <summary>
/// The trade window (Rules.DirectTrade). Steps: 0 offering, 1 locked, 2 confirmed. LockLeft: seconds the buttons wait
/// after the last change. Hero is the hero's state once the trade went through on this request.
/// </summary>
public sealed record TradeDto(long Id, TradeState State, bool Incoming, string OtherName, ItemDto[] MyItems, long MySorn, TradeStep MyStep,
    ItemDto[] TheirItems, long TheirSorn, TradeStep TheirStep, int LockLeft, int TaxPercent, bool RulesRelaxed, string Message, StateDto? Hero = null,
    BookOfferDto[]? MyBooks = null, BookOfferDto[]? TheirBooks = null);
/// <summary>Asks a hero to trade, by name, or by id (chat, friends).</summary>
public sealed record TradeInviteRequest(string RequestId, string Name, Guid? AccountId = null);
public sealed record TradeRequest(string RequestId, long TradeId);
/// <summary>A stack of Technique Scrolls on a trade's table.</summary>
public sealed record BookOfferDto(int BookId, int Count);
public sealed record TradeOfferRequest(string RequestId, long TradeId, Guid[] ItemIds, long Sorn, BookOfferDto[]? Books = null);

/// <summary>The Pits (Rules.Pits): the record, the three challengers, the board, the shop's currency.</summary>
/// <summary>
/// The Pits: rating and league, the lifetime record, Laurels, tickets; the season (Rules.Pits seasons): its record, the
/// seconds to its end, the title held and the last season's end for this hero (rank 0: not ranked), and its champions.
/// </summary>
public sealed record PitsDto(int Rating, string League, int Wins, int Losses, int Laurels, int TicketsLeft, PitChallengerDto[] Challengers,
    PitBoardDto[] Board, string Message = "", int SeasonWins = 0, int SeasonLosses = 0, long SeasonSecondsLeft = 0, string Title = "",
    int LastRank = 0, int LastRating = 0, int LastLaurels = 0, string LastChampions = "");
/// <summary>A challenger: Id is an account id, or "shade:-1|0|1" for a Pit shade cut from the attacker's own gear.</summary>
public sealed record PitChallengerDto(string Id, string Name, string Tag, int Rating, string League, HeroClass Class, string Weapon, int WinChancePercent, bool Shade);
public sealed record PitBoardDto(int Rank, string Name, string Tag, int Rating, string League, int Wins, int Losses, string Weapon, bool Me, string Title = "", Guid Id = default);
public sealed record PitFightRequest(string RequestId, string OpponentId);
public sealed record PitShopRequest(string RequestId, int ItemId);
public sealed record PitFightDto(StateDto State, DuelResultDto Duel, PitsDto Pits, int RatingBefore, int RatingAfter, int LaurelsGained);

/// <summary>Dungeons (Rules.Dungeons): enter a run, and answer the Chained Smith.</summary>
/// <summary>PartyDungeonId joins a dungeon a partymate opened (the empty Guid: none; Unity's JSON cannot leave it out).</summary>
public sealed record DungeonEnterRequest(string RequestId, int DungeonId, Guid? PartyDungeonId = null);
/// <summary>ItemId empty walks past the smith; otherwise that piece is forged with ForgeMethod.ChainedSmith (a string: Unity's
/// JSON writes a missing id as "").</summary>
public sealed record DungeonSmithRequest(string RequestId, long RunId, string? ItemId, string? Rune = null);
/// <summary>One fought floor: the client replays Dungeons.Floor(dungeon, floor, level) with the hero under Seed.</summary>
public sealed record DungeonFloorDto(int Floor, ulong Seed, int PotionsAtStart, bool Cleared);
/// <summary>
/// A part of a run: the floors fought now, and whether it stopped at the smith (AtSmith), ended in a fall (FellOn, the
/// floor) or cleared the dungeon (Cleared, with the Warden's Chest). Smith is the smith's forge when there was one.
/// </summary>
/// At a rune lock (Pause "RuneLock") Riddle and Runes are its riddle; the answer comes back as DungeonSmithRequest.Rune.
public sealed record DungeonResultDto(StateDto State, long RunId, int DungeonId, int Level, DungeonFloorDto[] Floors, bool AtSmith, bool Cleared,
    int FellOn, string Chest, ForgeResultDto? Smith, string SmithItem, string Text, string Pause = "", string Riddle = "", string[]? Runes = null,
    TownHeroDto[]? Mates = null);

public sealed record ForgeResultDto(ForgeOutcome Outcome, int ChanceBp, int LevelBefore, int LevelAfter);

public sealed record ErrorDto(string Code, string Message);

/// <summary>Moderation (the /admin page).</summary>
public sealed record AdminLoginRequest(string Email, string Password);
public sealed record AdminLoginDto(string Token, string Email, int Minutes);
public sealed record AdminOverviewDto(int Players, int Active24h, int New24h, int WithEmail, int Chat24h, int OpenReports, int Guilds,
    int ActiveListings, int Muted, int Banned);
public sealed record AdminLineDto(long Id, string Channel, Guid AccountId, string Name, string Text, DateTime Utc, int Reports, bool Hidden, bool Reviewed);
public sealed record AdminLineRequest(string Action);
public sealed record AdminPlayerDto(Guid Id, string Name, string Email, Banner Banner, string Guild, int Level, DateTime CreatedUtc, DateTime LastSeenUtc,
    DateTime? MutedUntilUtc, DateTime? BannedUtc, string? BanReason, int ReportedLines);
public sealed record AdminMuteRequest(int Minutes, string? Reason = null);
public sealed record AdminBanRequest(string Reason, bool HideLines = true);
public sealed record AdminGuildDto(Guid Id, string Name, string Tag, int Level, int Members, string Leader, DateTime CreatedUtc, bool Open);
public sealed record AdminRenameRequest(string Name, string Tag);
/// <summary>A name to report: Kind "hero" reports the hero's name, "guild" the name of the guild that hero is in.</summary>
public sealed record NameReportRequest(string Kind, Guid AccountId);
/// <summary>A leaderboard row: a hero (Tag its guild's) or a guild (Name, Tag, Level; Class empty).</summary>
public sealed record LeaderRowDto(int Rank, Guid Id, string Name, string Title, string Class, int Level, Banner Banner, long Value, string Tag);
public sealed record LeaderboardDto(string Board, string Period, LeaderRowDto[] Rows, LeaderRowDto? Mine, string Note);
/// <summary>Another hero as anyone may see them (INSPECT): who they are and what they wear.</summary>
public sealed record InviteDto(string Code, int Invited, int Rewarded, int MaxInvited, int RewardLevel, long Sorn, int Scrolls, string InvitedBy,
    bool MineRewarded, bool CanEnter, string Message);
public sealed record InviteRequest(string Code);
/// <summary>A hero standing in the town square: what the square draws (bands come from the item levels) and names.</summary>
public sealed record TownHeroDto(Guid Id, string Name, string Title, HeroClass Class, Figure Figure, int Level, Banner Banner, string Skin,
    int ArmorLevel, int ArmorPlus, int WeaponLevel, int WeaponPlus, bool Party = false);
public sealed record TownDto(TownHeroDto[] Heroes);
/// <summary>"I am in town" (every so often while the square shows), or Leaving when it closes.</summary>
public sealed record TownVisitRequest(bool Leaving = false);
public sealed record InspectDto(Guid Id, string Name, string Title, HeroClass Class, Figure Figure, int Level, Banner Banner, string GuildName, string GuildTag,
    int HighestStage, int PitRating, int PitWins, string Skin, ItemDto[] Worn, bool Banned);
/// <summary>A phone's push token ("ios": APNs, "android": Firebase), sent after the player allows notifications.</summary>
public sealed record PushTokenRequest(string Platform, string Token);
public sealed record AdminNameDto(string Kind, Guid TargetId, string Name, string Tag, int Reports, DateTime FirstUtc, DateTime LastUtc, bool Banned);
public sealed record AdminNameKeepRequest(string Kind, Guid TargetId);
public sealed record AdminHeroRenameRequest(string Name);
public sealed record AdminActionDto(DateTime Utc, string Admin, string Action, string Target, string Detail);
/// <summary>A world event on the moderation page's calendar, with its times in server time.</summary>
public sealed record AdminEventDto(long Id, string Kind, string Name, DateTime StartsUtc, DateTime EndsUtc, string StartsLocal, string EndsLocal,
    bool Weekly, bool Cancelled, bool Announced, string By);
public sealed record AdminEventRequest(string Kind, string StartsLocal, int Hours);
public sealed record DevEventRequest(string Kind, int Minutes = 60);

/// <summary>The character screen (25 Sep 2026): the login's characters, its Banner and Amber.</summary>
public sealed record LobbyDto(Guid LoginId, CharacterSlotDto[] Characters, int MaxSlots, Banner Banner, long Amber, string? Email, string Message = "", int Links = 0,
    bool EmailVerified = false);
/// <summary>One character: enough to stand its model on the stage (class, armour and weapon bands, worn skin look).</summary>
public sealed record CharacterSlotDto(Guid Id, int Slot, string Name, HeroClass Class, int Level, int ArmorBand, int WeaponBand, int WeaponUpgrade,
    string Skin, int HighestStageCleared, DateTime LastPlayedUtc, string GuildTag, bool Banned, Figure Figure = Figure.Man);
/// <summary>HeroClass as its name ("Vanguard"); Slot -1 takes the first free one; Figure "Man" or "Woman", empty for the class's first look.</summary>
public sealed record CreateCharacterRequest(string Name, string HeroClass, int Slot = -1, string? Figure = null);
/// <summary>Select or delete; delete needs the character's name typed as Name.</summary>
public sealed record CharacterRequest(Guid CharacterId, string Name = "");
public sealed record DepotDto(StateDto State, ItemDto[] Items, int Capacity, string Message = "");
public sealed record DepotRequest(string RequestId, Guid ItemId);

