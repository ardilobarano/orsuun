using System.ComponentModel.DataAnnotations;
using Orsuun.Rules;
using Orsuun.Rules.Combat;

namespace Orsuun.Server.Data;

public sealed class Account
{
    public Guid Id { get; set; }
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

    /// <summary>Sign in (lower-case, unique) and the password hash ("pbkdf2-sha256$iterations$salt$hash"); null for guests.</summary>
    [MaxLength(254)] public string? Email { get; set; }
    [MaxLength(200)] public string? PasswordHash { get; set; }
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
    /// <summary>The guild whose member broke the Hall for the holding Banner: its flag flies here too.</summary>
    public Guid? FlagGuildId { get; set; }
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
