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
    EtchingDto[] Etchings, SocketDto[] Sockets, int AverageDamage = 0, int SkillDamage = 0);

public sealed record SocketInsertRequest(string RequestId, Guid ItemId, int SocketIndex, ShardType Type, int Rank);
public sealed record SocketClearRequest(string RequestId, Guid ItemId, int SocketIndex);
public sealed record SocketResultDto(bool Success, int SocketIndex, string Text);

public sealed record EquipRequest(string RequestId, Guid ItemId);
/// <summary>Sells bag pieces to the merchant for sorn (Rules.Bag.SellPrice): ItemId, or the pieces picked in ItemIds.</summary>
public sealed record BagSellRequest(string RequestId, Guid ItemId, Guid[]? ItemIds = null);
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
    int EtchingNeedles, int SummoningMarkers, long Xp, int Level, int[] Korshards, string[] Skins, int HuntMarks = 0, int PinningWax = 0, int Tallies = 0,
    int MastersNeedles = 0, int Oathstones = 0, int[]? Books = null);

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
public sealed record ChatLineDto(long Id, Guid AccountId, string Name, Banner Banner, string Text, DateTime Utc, bool System, bool Mine);
/// <summary>A channel's lines after the id the client asked from (at most Rules.Chat.PageSize), and the newest id.</summary>
public sealed record ChatDto(string Channel, ChatLineDto[] Lines, long LatestId, int Blocked);
public sealed record ChatSayRequest(string Channel, string Text, long After = 0);
public sealed record ChatReportRequest(long MessageId, string Channel = "world");
public sealed record ChatBlockRequest(Guid AccountId, bool Block, string Channel = "world");

/// <summary>A piece on the Salt Exchange with its full details, its price and its seller.</summary>
/// <summary>
/// A listing: a piece (Item), or with BookId a stack of BookCount Technique Scrolls, or with GoodId a stack of GoodCount
/// goods (Rules.TradeGoods; Item null for both).
/// </summary>
public sealed record ListingDto(long Id, ItemDto? Item, long Price, string SellerName, Banner SellerBanner, bool Mine, int MinutesLeft,
    ListingStatus Status = ListingStatus.Active, int BookId = -1, int BookCount = 0, int GoodId = -1, int GoodCount = 0);
public sealed record MarketDto(StateDto State, ListingDto[] Listings, int Page, int Pages, int Total, ListingDto[] Mine, int TaxPercent,
    string Message = "");
public sealed record MarketListRequest(string RequestId, Guid ItemId, long Price, int BookId = -1, int BookCount = 0, int GoodId = -1, int GoodCount = 0);
/// <summary>
/// What a kind of thing sold for over Market.HistoryDays: sales, the average, lowest and highest price for one (a stack's
/// price over its count), the last sale and how long ago, and up to ten recent prices for one, newest first.
/// </summary>
public sealed record PriceHistoryDto(string What, int Sales, long Average, long Low, long High, long Last, int LastMinutesAgo, int Days, long[] Recent);
public sealed record MarketBuyRequest(string RequestId, long ListingId);

/// <summary>Sign up saves an email and password to the account being played; sign in moves this device to an account.</summary>
public sealed record RegisterRequest(string Email, string Password);
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
    int LeftBehind = 0);

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
    WorldEventDto[]? Events = null);

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
public sealed record PitBoardDto(int Rank, string Name, string Tag, int Rating, string League, int Wins, int Losses, string Weapon, bool Me, string Title = "");
public sealed record PitFightRequest(string RequestId, string OpponentId);
public sealed record PitShopRequest(string RequestId, int ItemId);
public sealed record PitFightDto(StateDto State, DuelResultDto Duel, PitsDto Pits, int RatingBefore, int RatingAfter, int LaurelsGained);

/// <summary>Dungeons (Rules.Dungeons): enter a run, and answer the Chained Smith.</summary>
public sealed record DungeonEnterRequest(string RequestId, int DungeonId);
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
    int FellOn, string Chest, ForgeResultDto? Smith, string SmithItem, string Text, string Pause = "", string Riddle = "", string[]? Runes = null);

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
public sealed record AdminActionDto(DateTime Utc, string Admin, string Action, string Target, string Detail);
/// <summary>A world event on the moderation page's calendar, with its times in server time.</summary>
public sealed record AdminEventDto(long Id, string Kind, string Name, DateTime StartsUtc, DateTime EndsUtc, string StartsLocal, string EndsLocal,
    bool Weekly, bool Cancelled, bool Announced, string By);
public sealed record AdminEventRequest(string Kind, string StartsLocal, int Hours);
public sealed record DevEventRequest(string Kind, int Minutes = 60);

/// <summary>The character screen (25 Sep 2026): the login's characters, its Banner and Amber.</summary>
public sealed record LobbyDto(Guid LoginId, CharacterSlotDto[] Characters, int MaxSlots, Banner Banner, long Amber, string? Email, string Message = "", int Links = 0);
/// <summary>One character: enough to stand its model on the stage (class, armour and weapon bands, worn skin look).</summary>
public sealed record CharacterSlotDto(Guid Id, int Slot, string Name, HeroClass Class, int Level, int ArmorBand, int WeaponBand, int WeaponUpgrade,
    string Skin, int HighestStageCleared, DateTime LastPlayedUtc, string GuildTag, bool Banned, Figure Figure = Figure.Man);
/// <summary>HeroClass as its name ("Vanguard"); Slot -1 takes the first free one; Figure "Man" or "Woman", empty for the class's first look.</summary>
public sealed record CreateCharacterRequest(string Name, string HeroClass, int Slot = -1, string? Figure = null);
/// <summary>Select or delete; delete needs the character's name typed as Name.</summary>
public sealed record CharacterRequest(Guid CharacterId, string Name = "");
public sealed record DepotDto(StateDto State, ItemDto[] Items, int Capacity, string Message = "");
public sealed record DepotRequest(string RequestId, Guid ItemId);

