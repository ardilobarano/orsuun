using System.ComponentModel.DataAnnotations;
using Orsuun.Rules;
using Orsuun.Rules.Combat;

namespace Orsuun.Server.Data;

/// <summary>
/// The player's account (owner, 25 Sep 2026: four characters per account, Metin2 style). Devices sign in to it; it holds
/// what the characters share: Amber (Rules.Amber), the Banner sworn once for all four, and the depot (items with
/// DepotLoginId), and signs in: the email and password, and the Google / Apple links (ExternalLogin.LoginId). Each
/// character is an Account row (the hero) with LoginId, a slot and a chosen name.
/// </summary>
public sealed class Login
{
    public Guid Id { get; set; }
    public DateTime CreatedUtc { get; set; }
    [MaxLength(64)] public string? CreatedIp { get; set; }
    /// <summary>Sign in (lower-case, unique) and the password hash ("pbkdf2-sha256$iterations$salt$hash"); null for guests.</summary>
    [MaxLength(254)] public string? Email { get; set; }
    [MaxLength(200)] public string? PasswordHash { get; set; }
    public long Amber { get; set; }
    public int AmberPurchases { get; set; }
    public Banner Banner { get; set; } = Banner.None;
    public DateTime? SwornUtc { get; set; }
}

public sealed class Account
{
    public Guid Id { get; set; }
    /// <summary>The login (player account) this character belongs to, its slot there (0-3) and its chosen name.</summary>
    public Guid LoginId { get; set; }
    public int Slot { get; set; }
    [MaxLength(24)] public string Name { get; set; } = "";
    /// <summary>Rules.Characters.NameKey(Name): unique among characters.</summary>
    [MaxLength(24)] public string NameKey { get; set; } = "";
    /// <summary>Opaque device token for the guest login. Real auth (Apple, Google) replaces this later.</summary>
    [MaxLength(128)] public string DeviceToken { get; set; } = "";
    [MaxLength(64)] public string? SessionToken { get; set; }
    public DateTime CreatedUtc { get; set; }
    /// <summary>Client IP at account creation, for the new-accounts-per-network cap. Guest login only; null for old rows.</summary>
    [MaxLength(64)] public string? CreatedIp { get; set; }
    public int WeaponsBroken { get; set; }
    public DateTime LastHeartbeatUtc { get; set; }

    public long Sorn { get; set; }
    public int Potions { get; set; }
    public int Materials { get; set; }
    public int ScrollsOfMercy { get; set; }
    public int KhansAlloys { get; set; }
    public int AnvilWards { get; set; }
    public int Turnstones { get; set; }
    public int EtchingNeedles { get; set; }
    public int SummoningMarkers { get; set; }
    public int HuntMarks { get; set; }
    public int PinningWax { get; set; }
    public long Xp { get; set; }
    /// <summary>Bounty counts and claims for the current day and week (Rules.BountyProgress.Serialize).</summary>
    [MaxLength(512)] public string Bounties { get; set; } = "";

    /// <summary>The Banner sworn to; None until the oath.</summary>
    public Banner Banner { get; set; } = Banner.None;
    public DateTime? SwornUtc { get; set; }
    /// <summary>The last fortress siege fight, for the cooldown.</summary>
    public DateTime? LastSiegeUtc { get; set; }
    /// <summary>Dungeon runs entered in the bounty day DungeonDay (Rules.Bounties.DayKey), against the free keys.</summary>
    [MaxLength(16)] public string DungeonDay { get; set; } = "";
    public int DungeonRuns { get; set; }
    /// <summary>The run waiting at the Chained Smith (DungeonRun.Id), 0 when none.</summary>
    public long DungeonRunAtSmith { get; set; }
    /// <summary>The Pits (Rules.Pits): rating, record, Laurels, fights in the bounty day PitDay, and the challengers' roll.</summary>
    public int PitRating { get; set; } = Rules.Pits.StartRating;
    public int PitWins { get; set; }
    public int PitLosses { get; set; }
    public int Laurels { get; set; }
    [MaxLength(16)] public string PitDay { get; set; } = "";
    public int PitFights { get; set; }
    public int PitRoll { get; set; }
    /// <summary>
    /// The wardrobe (Rules.Wardrobe.Format: "id:expiresUnix;...") and the piece worn in each slot ("" for none). Amber is
    /// on the Login since characters came (25 Sep 2026).
    /// </summary>
    [MaxLength(2048)] public string Wardrobe { get; set; } = "";
    [MaxLength(64)] public string WornSkin { get; set; } = "";
    [MaxLength(64)] public string WornMount { get; set; } = "";
    [MaxLength(64)] public string WornCompanion { get; set; } = "";
    /// <summary>The Campaign Trail this season (Rules.TrailProgress: "season|xp|pass|free|paid").</summary>
    [MaxLength(96)] public string Trail { get; set; } = "";

    /// <summary>The guild this account belongs to, its rank there and when it joined.</summary>
    public Guid? GuildId { get; set; }
    public GuildRank GuildRank { get; set; }
    public DateTime? GuildJoinedUtc { get; set; }
    /// <summary>Sorn donated to the current guild since joining (the member list shows it).</summary>
    public long GuildDonated { get; set; }
    /// <summary>Sorn donated in the bounty day GuildDonationDay (Rules.Bounties.DayKey), for the daily cap.</summary>
    public long GuildDonatedToday { get; set; }
    [MaxLength(16)] public string GuildDonationDay { get; set; } = "";
    /// <summary>Guild Tallies, spent in the guild shop. They stay with the player across guilds.</summary>
    public int Tallies { get; set; }

    /// <summary>Players whose chat lines this account does not see, semicolon separated account ids.</summary>
    [MaxLength(2000)] public string Blocked { get; set; } = "";
    public DateTime? LastChatUtc { get; set; }

    /// <summary>Moderation: no chat until this time; banned accounts cannot sign in at all.</summary>
    public DateTime? MutedUntilUtc { get; set; }
    public DateTime? BannedUtc { get; set; }
    [MaxLength(200)] public string? BanReason { get; set; }
    /// <summary>Korshards by rank as "n;n;n;n;n" (Trooper .. Guard of the Khan).</summary>
    [MaxLength(64)] public string Korshards { get; set; } = "0;0;0;0;0";
    /// <summary>Owned skins, semicolon separated.</summary>
    [MaxLength(2048)] public string Skins { get; set; } = "";

    public int HighestStageCleared { get; set; }
    /// <summary>Campaign stage number or zone id (100+) the farm lane is parked in.</summary>
    public int ParkedStage { get; set; } = 1;

    /// <summary>Seed of the online farm lane: loop n runs from ActivePlay.LoopSeed(LaneSeed, n). New on every park.</summary>
    public long LaneSeed { get; set; }
    /// <summary>Next loop number the server will accept a report for.</summary>
    public int LaneLoop { get; set; }

    /// <summary>The class being played; it picks the hero's stat shape and skill kit.</summary>
    public HeroClass Class { get; set; } = HeroClass.Vanguard;

    /// <summary>All items the account owns, equipped or in the loot list. Loaded with the account.</summary>
    public List<Item> Items { get; set; } = new();

    public Item Weapon => Items.Single(i => i.Equipped && i.Slot == EquipSlot.Weapon && !i.Destroyed);

    /// <summary>The equipped, intact item in a slot, or null.</summary>
    public Item? EquippedIn(EquipSlot slot) => Items.SingleOrDefault(i => i.Equipped && i.Slot == slot && !i.Destroyed);

    /// <summary>Mapped to PostgreSQL's xmin by IsRowVersion(); never set by hand.</summary>
    public uint Version { get; set; }
}

public sealed class Item
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public EquipSlot Slot { get; set; }
    public bool Equipped { get; set; }
    public int ItemLevel { get; set; }
    public Rarity Rarity { get; set; }
    public int UpgradeLevel { get; set; }
    public int PatienceBp { get; set; }
    public int LockedEtchingIndex { get; set; } = -1;
    /// <summary>Etchings as "entryId:tier:value" triples, in slot order.</summary>
    [MaxLength(256)] public string Etchings { get; set; } = "";
    /// <summary>Sockets as "-" (empty), "x" (Dead Shard) or "type:rank", semicolon separated, in socket order.</summary>
    [MaxLength(64)] public string Sockets { get; set; } = "";
    public bool Destroyed { get; set; }
    public DateTime CreatedUtc { get; set; }
    /// <summary>On the Salt Exchange: out of the bag, cannot be worn, forged or turned until sold, cancelled or expired.</summary>
    public bool Listed { get; set; }
    /// <summary>
    /// In the shared depot of this login: out of the bag like a listed piece. OwnerId stays the character that put it in
    /// until another takes it out.
    /// </summary>
    public Guid? DepotLoginId { get; set; }

    /// <summary>
    /// On the table of this live direct trade: out of the bag like a listed piece, so it cannot be forged, worn or trimmed
    /// away by a full bag while the other side looks at it. Cleared when the trade ends.
    /// </summary>
    public long? TradeId { get; set; }

    /// <summary>Out of the bag: on the Salt Exchange, in the depot or on a trade table (not worn, forged, turned or counted in the bag).</summary>
    public bool OutOfBag => Listed || DepotLoginId != null || TradeId != null;

    public ItemState ToState()
    {
        var state = new ItemState(ItemLevel, Rarity, Slot)
        {
            UpgradeLevel = UpgradeLevel,
            PatienceBp = PatienceBp,
            LockedEtchingIndex = LockedEtchingIndex,
            Destroyed = Destroyed,
        };
        foreach (string triple in Etchings.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = triple.Split(':');
            state.Etchings.Add(new Etching(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2])));
        }
        string[] sockets = Sockets.Split(';', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < sockets.Length && i < state.Sockets.Length; i++)
        {
            if (sockets[i] == "x") state.Sockets[i] = Socket.DeadShard;
            else if (sockets[i] != "-")
            {
                string[] parts = sockets[i].Split(':');
                state.Sockets[i] = new Socket((ShardType)int.Parse(parts[0]), int.Parse(parts[1]));
            }
        }
        return state;
    }

    public static Item From(ItemState state, Guid ownerId, bool equipped)
    {
        var item = new Item { Id = Guid.NewGuid(), OwnerId = ownerId, Slot = state.Slot, Equipped = equipped, ItemLevel = state.ItemLevel, Rarity = state.Rarity, CreatedUtc = DateTime.UtcNow };
        item.ApplyState(state);
        return item;
    }

    public void ApplyState(ItemState state)
    {
        UpgradeLevel = state.UpgradeLevel;
        PatienceBp = state.PatienceBp;
        LockedEtchingIndex = state.LockedEtchingIndex;
        Destroyed = state.Destroyed;
        Etchings = string.Join(';', state.Etchings.Select(e => $"{e.EntryId}:{e.Tier}:{e.Value}"));
        Sockets = string.Join(';', state.Sockets.Select(s => s.Dead ? "x" : s.Type == null ? "-" : $"{(int)s.Type.Value}:{s.Rank}"));
    }
}

/// <summary>
/// Server-wide spawn clock of one Commander. A boss is up from SpawnUtc for BossDef.WindowSeconds; each spawn has one
/// HP pool that every player's fight wears down (shared since 24 Sep 2026), sized when the spawn opens.
/// </summary>
public sealed class BossClock
{
    public int BossId { get; set; }
    public DateTime SpawnUtc { get; set; }
    /// <summary>The spawn the pool below belongs to; a new spawn refills it.</summary>
    public DateTime PoolSpawnUtc { get; set; }
    public long HpMax { get; set; }
    public long HpLeft { get; set; }
    public DateTime? SlainUtc { get; set; }
    public Banner SlainBanner { get; set; }
    [MaxLength(48)] public string? SlainBy { get; set; }
}

/// <summary>One player's fight against one Commander spawn: the shared pool's damage ranking reads these.</summary>
public sealed class BossHit
{
    public long Id { get; set; }
    public int BossId { get; set; }
    public DateTime SpawnUtc { get; set; }
    public Guid AccountId { get; set; }
    [MaxLength(48)] public string Name { get; set; } = "";
    public Banner Banner { get; set; }
    public long Damage { get; set; }
    public DateTime Utc { get; set; }
}

/// <summary>War of Banners points of one Banner in one season.</summary>
public sealed class BannerScore
{
    [MaxLength(16)] public string Season { get; set; } = "";
    public Banner Banner { get; set; }
    public long Points { get; set; }
}

/// <summary>A guild: name, tag and colour, the treasury, XP (level) and its skills. Members point at it from Account.</summary>
public sealed class Guild
{
    public Guid Id { get; set; }
    [MaxLength(20)] public string Name { get; set; } = "";
    /// <summary>Lower-case name, unique: two guilds never differ only by case.</summary>
    [MaxLength(20)] public string NameKey { get; set; } = "";
    [MaxLength(4)] public string Tag { get; set; } = "";
    [MaxLength(7)] public string Color { get; set; } = "#B0B0B0";
    /// <summary>Open guilds take anyone who taps JOIN; closed ones take nobody new.</summary>
    public bool Open { get; set; } = true;
    public long Treasury { get; set; }
    public long Xp { get; set; }
    public int Plunder { get; set; }
    public int Muster { get; set; }
    public DateTime CreatedUtc { get; set; }
    [MaxLength(160)] public string LastEvent { get; set; } = "";
    /// <summary>Guild war rating (Elo, Rules.GuildWars) and record.</summary>
    public int WarRating { get; set; } = Rules.GuildWars.StartRating;
    public int WarWins { get; set; }
    public int WarLosses { get; set; }
    public int WarDraws { get; set; }
}

/// <summary>A guild signed up by its leader for a war night (Rules.GuildWars.NightKey).</summary>
public sealed class GuildWarSignup
{
    [MaxLength(24)] public string Night { get; set; } = "";
    public Guid GuildId { get; set; }
    public DateTime Utc { get; set; }
}

/// <summary>A war night: when it runs, and whether its signed guilds were paired (the row is the pairing lock).</summary>
public sealed class GuildWarNight
{
    [MaxLength(24)] public string Night { get; set; } = "";
    public DateTime StartsUtc { get; set; }
    public DateTime EndsUtc { get; set; }
    public bool Paired { get; set; }
}

/// <summary>
/// One war between two guilds on a night: kills, the three lanes' fronts (+ toward B's camp, - toward A's), each side's
/// war flag (lane, -1 none) and the result once settled (0 running, 1 A won, 2 B won, 3 draw).
/// </summary>
public sealed class GuildWar
{
    public long Id { get; set; }
    [MaxLength(24)] public string Night { get; set; } = "";
    public Guid GuildA { get; set; }
    public Guid GuildB { get; set; }
    public DateTime StartsUtc { get; set; }
    public DateTime EndsUtc { get; set; }
    public int KillsA { get; set; }
    public int KillsB { get; set; }
    public int Front0 { get; set; }
    public int Front1 { get; set; }
    public int Front2 { get; set; }
    public int FlagA { get; set; } = -1;
    public int FlagB { get; set; } = -1;
    public int Result { get; set; }
    [MaxLength(160)] public string LastEvent { get; set; } = "";

    public int[] Fronts => new[] { Front0, Front1, Front2 };

    public void SetFront(int lane, int value)
    {
        if (lane == 0) Front0 = value;
        else if (lane == 1) Front1 = value;
        else Front2 = value;
    }
}

/// <summary>One member's duels in one war.</summary>
public sealed class GuildWarEntry
{
    public long WarId { get; set; }
    public Guid AccountId { get; set; }
    public Guid GuildId { get; set; }
    public int Fights { get; set; }
    public int Wins { get; set; }
    public DateTime LastUtc { get; set; }
}

/// <summary>
/// A dungeon run that stopped at the Chained Smith (State 0) waiting for the player's choice, or a finished one (1).
/// Floors up to the smith are fought and paid on entering; the rest after the smith.
/// </summary>
public sealed class DungeonRun
{
    public long Id { get; set; }
    public Guid AccountId { get; set; }
    public int DungeonId { get; set; }
    /// <summary>The campaign stage the run is scaled to (Rules.Dungeons.Level).</summary>
    public int Level { get; set; }
    public int FloorsCleared { get; set; }
    public int State { get; set; }
    public DateTime StartedUtc { get; set; }
}

/// <summary>A guild's bid on a fortress keep for a bounty week (one per guild a week), and its keep damage if it contends.</summary>
public sealed class FortressBid
{
    [MaxLength(16)] public string Week { get; set; } = "";
    public Guid GuildId { get; set; }
    public int FortressId { get; set; }
    public long Amount { get; set; }
    public DateTime Utc { get; set; }
    public bool Contender { get; set; }
    public bool Refunded { get; set; }
    public long Damage { get; set; }
}

/// <summary>A fortress: its holding Banner, the phase under siege and that phase's wall, and each Banner's siege damage.</summary>
public sealed class Fortress
{
    public int Id { get; set; }
    public Banner Holder { get; set; }
    public DateTime HeldSinceUtc { get; set; }
    public int Phase { get; set; }
    public long WallMax { get; set; }
    public long Wall { get; set; }
    public long SiegeEmber { get; set; }
    public long SiegeSky { get; set; }
    public long SiegeGold { get; set; }
    [MaxLength(160)] public string LastEvent { get; set; } = "";
    /// <summary>The guild holding the keep (won at the Sunday keep siege): its flag flies here too.</summary>
    public Guid? FlagGuildId { get; set; }
    /// <summary>The bounty week the keep's bids and siege belong to, and where they stand (Rules.FortressKeeps).</summary>
    [MaxLength(16)] public string KeepWeek { get; set; } = "";
    /// <summary>0 bids open, 1 the keep siege (contenders chosen), 2 settled.</summary>
    public int KeepState { get; set; }
    public DateTime KeepStartsUtc { get; set; }
    public DateTime KeepEndsUtc { get; set; }
    /// <summary>What the holding guild's members mended during this week's keep siege.</summary>
    public long KeepMended { get; set; }
}

/// <summary>One chat line: a player's, or a system line (AccountId empty) for guild and world events.</summary>
public sealed class ChatMessage
{
    public long Id { get; set; }
    /// <summary>"world" or "g:" + guild id (Rules.Chat).</summary>
    [MaxLength(40)] public string Channel { get; set; } = "";
    public Guid AccountId { get; set; }
    [MaxLength(56)] public string Name { get; set; } = "";
    public Banner Banner { get; set; }
    [MaxLength(200)] public string Text { get; set; } = "";
    public DateTime Utc { get; set; }
    public int Reports { get; set; }
    public bool Hidden { get; set; }
    /// <summary>A moderator has looked at this line's reports (it leaves the queue).</summary>
    public bool Reviewed { get; set; }
}

/// <summary>A Google or Apple identity linked to an account (sign in with it on any phone).</summary>
public sealed class ExternalLogin
{
    public long Id { get; set; }
    [MaxLength(16)] public string Provider { get; set; } = "";
    /// <summary>The provider's stable user id ("sub").</summary>
    [MaxLength(255)] public string Subject { get; set; } = "";
    /// <summary>The login it signs in to (was the account before characters, 25 Sep 2026).</summary>
    public Guid LoginId { get; set; }
    [MaxLength(254)] public string? Email { get; set; }
    public DateTime CreatedUtc { get; set; }
}

/// <summary>What a moderator did, for the moderation log.</summary>
public sealed class AdminAction
{
    public long Id { get; set; }
    public DateTime Utc { get; set; }
    [MaxLength(254)] public string Admin { get; set; } = "";
    [MaxLength(32)] public string Action { get; set; } = "";
    [MaxLength(64)] public string Target { get; set; } = "";
    [MaxLength(300)] public string Detail { get; set; } = "";
}

public sealed class ChatReport
{
    public long Id { get; set; }
    public long MessageId { get; set; }
    public Guid ReporterId { get; set; }
    public DateTime Utc { get; set; }
}

/// <summary>A request to join a guild whose gates are shut; the leader or an officer answers it.</summary>
public sealed class GuildRequest
{
    public long Id { get; set; }
    public Guid GuildId { get; set; }
    public Guid AccountId { get; set; }
    public DateTime Utc { get; set; }
}

/// <summary>A piece on the Salt Exchange. The item row keeps its owner (the seller) with Listed set until it closes.</summary>
public sealed class MarketListing
{
    public long Id { get; set; }
    public Guid ItemId { get; set; }
    public Guid SellerId { get; set; }
    [MaxLength(56)] public string SellerName { get; set; } = "";
    public Banner SellerBanner { get; set; }
    public long Price { get; set; }
    public ListingStatus Status { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime ExpiresUtc { get; set; }
    public DateTime? ClosedUtc { get; set; }
    public Guid? BuyerId { get; set; }
    // Copied from the item for browsing and sorting without loading it.
    public EquipSlot Slot { get; set; }
    public Rarity Rarity { get; set; }
    public int ItemLevel { get; set; }
    public int UpgradeLevel { get; set; }
}

/// <summary>
/// A device signed in to an account: its device token (guest login) and its own session, so one account can be
/// played on two phones. Accounts made before devices existed are found by Account.DeviceToken and get a row.
/// </summary>
public sealed class Device
{
    [MaxLength(128)] public string Token { get; set; } = "";
    /// <summary>The login signed in on this device, and the character chosen there (Guid.Empty: the character screen).</summary>
    public Guid LoginId { get; set; }
    public Guid AccountId { get; set; }
    [MaxLength(64)] public string? SessionToken { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime LastSeenUtc { get; set; }
}

/// <summary>Append-only record of every roll and every currency change. Support and rate audits read this.</summary>
/// <summary>An error the game client caught on a player's device (store readiness: crash reports without a third party).</summary>
public sealed class ClientLog
{
    public long Id { get; set; }
    public Guid AccountId { get; set; }
    public DateTime Utc { get; set; }
    [MaxLength(32)] public string Platform { get; set; } = "";
    [MaxLength(32)] public string Version { get; set; } = "";
    [MaxLength(512)] public string Message { get; set; } = "";
    [MaxLength(4000)] public string Stack { get; set; } = "";
}

public sealed class LedgerEntry
{
    public long Id { get; set; }
    public Guid AccountId { get; set; }
    public Guid? ItemId { get; set; }
    public DateTime Utc { get; set; }
    /// <summary>Idempotency key from the client, unique per account.</summary>
    [MaxLength(64)] public string RequestId { get; set; } = "";
    [MaxLength(32)] public string Kind { get; set; } = "";
    /// <summary>Human-readable inputs and result, for example "ScrollOfMercy +7->+8 chance=5000 roll=success".</summary>
    [MaxLength(512)] public string Detail { get; set; } = "";
    public long SornDelta { get; set; }
}

/// <summary>
/// A direct trade between two characters (Rules.DirectTrade): From asked To. Each side's offer is bag pieces (item ids)
/// and sorn, with its step (offering, locked, confirmed). Changed only with the row locked (FOR UPDATE).
/// </summary>
public sealed class TradeSession
{
    public long Id { get; set; }
    public Guid FromId { get; set; }
    public Guid ToId { get; set; }
    public TradeState State { get; set; }
    [MaxLength(400)] public string FromItems { get; set; } = "";
    public long FromSorn { get; set; }
    public TradeStep FromStep { get; set; }
    [MaxLength(400)] public string ToItems { get; set; } = "";
    public long ToSorn { get; set; }
    public TradeStep ToStep { get; set; }
    public DateTime CreatedUtc { get; set; }
    /// <summary>The last change to either offer: the buttons wait DirectTrade.LockSeconds after it.</summary>
    public DateTime ChangedUtc { get; set; }
    /// <summary>The last action of either side: an idle window closes after DirectTrade.IdleMinutes.</summary>
    public DateTime TouchedUtc { get; set; }
    public DateTime? ClosedUtc { get; set; }
    [MaxLength(64)] public string ClosedReason { get; set; } = "";
}
