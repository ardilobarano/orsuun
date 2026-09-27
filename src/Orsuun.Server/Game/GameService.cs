using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Orsuun.Rules;
using Orsuun.Rules.Combat;
using Orsuun.Server.Data;

namespace Orsuun.Server.Game;

public sealed class GameException : Exception
{
    public GameException(string code, string message) : base(message) => Code = code;
    public string Code { get; }
}

/// <summary>
/// All game mutations. Each public method is one database transaction: load the account, apply the
/// rules from Orsuun.Rules, write the ledger, save. A concurrency clash aborts the whole thing.
/// </summary>
public sealed partial class GameService
{
    /// <summary>Gaps longer than this count as offline time at the offline rate.</summary>
    public static readonly TimeSpan OnlineGrace = TimeSpan.FromMinutes(3);
    /// <summary>Loot list cap; the oldest common pieces are dropped past it.</summary>
    public const int MaxLoot = Bag.Size;

    private readonly GameDb _db;
    private readonly IRandom _rng;
    private readonly BellClock _bells;
    private readonly EventCalendar _events;
    private readonly ForgeService _forge;
    private readonly EtchingService _etchings = new();
    private readonly SocketService _sockets = new();

    public GameService(GameDb db, IRandom rng, BellClock bells, EventCalendar events)
    {
        _db = db;
        _rng = rng;
        _bells = bells;
        _events = events;
        // A lucky forge hour (Rules.WorldEvents) adds its chance to every attempt this request makes.
        _forge = new ForgeService { LuckBp = events.ForgeLuckBp(DateTime.UtcNow) };
    }

    /// <summary>
    /// Device tokens are minted by the client, so a script could farm starter kits and boss-bracket entries.
    /// Until platform attestation (App Attest, Play Integrity) arrives, cap new accounts per network per day.
    /// Loopback is exempt so local dev and the smoke test keep working.
    /// </summary>
    public const int MaxNewAccountsPerIpPerDay = 10;

    /// <summary>
    /// A device signs in by its token. Since characters came (25 Sep 2026) a device belongs to a Login and has a chosen
    /// character (or none: the character screen). A new device gets a new Login; with <paramref name="lobby"/> (the current
    /// client) it starts without a character and the player makes one, otherwise (older clients, smoke tests) a first
    /// character with a generated name is made and chosen, as before.
    /// </summary>
    public async Task<GuestLoginResponse> GuestLoginAsync(string deviceToken, string? clientIp, CancellationToken ct, bool lobby = false)
    {
        if (string.IsNullOrWhiteSpace(deviceToken) || deviceToken.Length > 128)
            throw new GameException("bad_device_token", "Device token missing or too long.");

        // A device signed in before (Devices) wins; older accounts are still found by their own device token.
        Device? device = await _db.Devices.AsNoTracking().FirstOrDefaultAsync(d => d.Token == deviceToken, ct);
        Account? account = null;
        Login? login = null;
        if (device != null)
        {
            login = await _db.Logins.SingleOrDefaultAsync(l => l.Id == device.LoginId, ct);
            if (device.AccountId != Guid.Empty) account = await _db.Accounts.SingleOrDefaultAsync(a => a.Id == device.AccountId && a.LoginId == device.LoginId, ct);
        }
        else
        {
            account = await _db.Accounts.SingleOrDefaultAsync(a => a.DeviceToken == deviceToken, ct);
            if (account != null) login = await _db.Logins.SingleOrDefaultAsync(l => l.Id == account.LoginId, ct);
        }
        bool created = login == null;
        if (login == null)
        {
            DateTime now = DateTime.UtcNow;
            bool loopback = clientIp == null || System.Net.IPAddress.TryParse(clientIp, out var ip) && System.Net.IPAddress.IsLoopback(ip);
            if (!loopback)
            {
                DateTime since = now.AddDays(-1);
                int recent = await _db.Logins.CountAsync(l => l.CreatedIp == clientIp && l.CreatedUtc > since, ct);
                if (recent >= MaxNewAccountsPerIpPerDay)
                    throw new GameException("too_many_accounts", "Too many new accounts from this network today. Try again tomorrow.");
            }
            login = new Login { Id = Guid.NewGuid(), CreatedUtc = now, CreatedIp = clientIp };
            _db.Logins.Add(login);
            account = null;
            if (!lobby)
            {
                // Older clients have no character screen: their first character is made here with a generated name.
                bool tokenTaken = await _db.Accounts.AnyAsync(a => a.DeviceToken == deviceToken, ct);
                Guid id = Guid.NewGuid();
                account = NewCharacter(login, Banners.GeneratedName(id), HeroClass.Vanguard, 0, id);
                if (!tokenTaken) account.DeviceToken = deviceToken;
                account.CreatedIp = clientIp;
            }
        }

        if (account != null)
        {
            ThrowIfBanned(account);
            if (account.LaneSeed == 0) NewLane(account);
        }
        else if (await _db.Accounts.AnyAsync(a => a.LoginId == login.Id && a.BannedUtc != null, ct))
            throw new GameException("banned", "This account is banned.");
        string session = await BindDeviceAsync(deviceToken, login.Id, account?.Id ?? Guid.Empty, ct);
        await _db.SaveChangesAsync(ct);
        int characters = await _db.Accounts.CountAsync(a => a.LoginId == login.Id, ct);
        return new GuestLoginResponse(account?.Id ?? Guid.Empty, session, created, login.Id, characters);
    }

    /// <summary>A new character with the starter kit, the login's Banner, in the given slot.</summary>
    private Account NewCharacter(Login login, string name, HeroClass cls, int slot, Guid? id = null)
    {
        DateTime now = DateTime.UtcNow;
        var account = new Account
        {
            Id = id ?? Guid.NewGuid(),
            LoginId = login.Id,
            Slot = slot,
            Name = name,
            NameKey = Characters.NameKey(name),
            Class = cls,
            Figure = ItemLooks.NativeFigure(cls),
            Banner = login.Banner,
            SwornUtc = login.SwornUtc,
            CreatedUtc = now,
            LastHeartbeatUtc = now,
            Sorn = 20_000,
            Potions = 30,
            ScrollsOfMercy = 2,
            Turnstones = 5,
        };
        account.DeviceToken = "char-" + account.Id.ToString("N");
        account.Items.Add(Item.From(NewStarter(EquipSlot.Weapon), account.Id, equipped: true));
        NewLane(account);
        _db.Accounts.Add(account);
        _db.Ledger.Add(Entry(account.Id, null, "account-created", "starter kit, " + name, account.Sorn, Guid.NewGuid().ToString("N")));
        return account;
    }

    /// <summary>
    /// The character chosen on the session's device, with its login loaded (Amber, Banner). Null when the session is
    /// unknown; throws "no_character" when the device is on the character screen.
    /// </summary>
    public async Task<Account?> AuthenticateAsync(string? sessionToken, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(sessionToken)) return null;
        // Sessions live on devices; sessions handed out before devices existed are still on the account.
        Device? device = await _db.Devices.AsNoTracking().FirstOrDefaultAsync(d => d.SessionToken == sessionToken, ct);
        if (device != null && device.AccountId == Guid.Empty) throw new GameException("no_character", "Choose a character first.");
        Account? account = device != null
            ? await _db.Accounts.SingleOrDefaultAsync(a => a.Id == device.AccountId, ct)
            : await _db.Accounts.SingleOrDefaultAsync(a => a.SessionToken == sessionToken, ct);
        if (account != null) ThrowIfBanned(account);
        if (account?.GuildId is Guid guildId) _guild = await _db.Guilds.FindAsync(new object[] { guildId }, ct);
        if (account != null)
        {
            _login = await _db.Logins.SingleOrDefaultAsync(l => l.Id == account.LoginId, ct);
            await LoadLoginsAsync(account, ct);
        }
        return account;
    }

    /// <summary>The login behind a session, for the character screen (no character needed).</summary>
    public async Task<(Login login, Device device)?> AuthenticateLoginAsync(string? sessionToken, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(sessionToken)) return null;
        Device? device = await _db.Devices.FirstOrDefaultAsync(d => d.SessionToken == sessionToken, ct);
        if (device == null) return null;
        Login? login = await _db.Logins.SingleOrDefaultAsync(l => l.Id == device.LoginId, ct);
        if (login == null) return null;
        _login = login;
        return (login, device);
    }

    private static void ThrowIfBanned(Account account)
    {
        if (account.BannedUtc != null)
            throw new GameException("banned", "This account is banned" + (string.IsNullOrEmpty(account.BanReason) ? "." : ": " + account.BanReason));
    }

    /// <summary>At most this many loop reports are replayed per heartbeat.</summary>
    public const int MaxLoopsPerHeartbeat = 20;

    /// <summary>
    /// Credits hunting time since the last heartbeat and moves the heartbeat forward. Online, the loops the client
    /// reports are replayed (ActivePlay.Verify) and their pace is paid for the time they cover; the rest of the
    /// interval pays the plain auto-cast rate. Offline time never earns the active bonus.
    /// </summary>
    public async Task<StateDto> HeartbeatAsync(Account account, HeartbeatRequest? request, CancellationToken ct)
    {
        DateTime now = DateTime.UtcNow;
        bool online = now - account.LastHeartbeatUtc <= OnlineGrace;
        (int activeBp, int verified) = online ? VerifyLoops(account, request?.Loops, now) : (RandomExtensions.FullBp, 0);
        int bonus = await SornBonusPercentAsync(account.Banner, ct) + await GuildBonusPercentAsync(account, ct)
            + _events.SornBonusPercent(account.LastHeartbeatUtc, now);
        SettlementDto settlement = Settle(account, now, activeBp, verified, bonus) with { ActiveBp = activeBp, LoopsVerified = verified };
        account.LastHeartbeatUtc = now;
        Count(account, BountyMetric.Korstones, settlement.Korstones);
        if (!settlement.Offline) Count(account, BountyMetric.HuntSeconds, settlement.CountedSeconds);
        await SaveAsync(ct);
        await AddPointsAsync(account.Banner, settlement.Korstones * Banners.PointsPerKorstone, ct);
        return ToState(account, settlement: settlement);
    }

    /// <summary>
    /// Replays the reported loops in order and returns the interval's hunting efficiency. A report must be for the
    /// next expected loop or a later one (skipped loops, e.g. ones where the player changed gear, earn auto pace);
    /// replays stop once the reported ticks exceed the wall-clock interval by more than one loop. The efficiency is
    /// time-weighted: verified ticks, capped at the interval, pay their pace; the rest pays FullBp.
    /// </summary>
    private (int bp, int verified) VerifyLoops(Account account, LoopReportDto[]? loops, DateTime now)
    {
        if (loops == null || loops.Length == 0) return (RandomExtensions.FullBp, 0);
        long elapsedTicks = Math.Max(1L, (long)((now - account.LastHeartbeatUtc).TotalSeconds * LaneSim.TicksPerSecond));
        StageConfig stage = EveningBells.Apply(Content.Stage(account.ParkedStage), _bells.Active);
        HeroStats hero = Hero(account);
        SkillDef[] skills = SkillDef.For(hero.Class);
        ulong seed = unchecked((ulong)account.LaneSeed);

        long reported = 0, coveredTicks = 0, weighted = 0;
        int verified = 0;
        foreach (LoopReportDto loop in loops.Take(MaxLoopsPerHeartbeat))
        {
            if (loop.Loop < account.LaneLoop) continue;                       // already judged
            if (loop.Ticks <= 0 || loop.Ticks > ActivePlay.MaxLoopTicks) break;
            reported += loop.Ticks;
            if (reported > elapsedTicks + ActivePlay.MaxLoopTicks) break;     // more lane than wall clock
            account.LaneLoop = loop.Loop + 1;

            var casts = (loop.Casts ?? Array.Empty<CastDto>()).Select(c => new CastInput(c.Tick, c.Skill)).ToList();
            bool[] auto = loop.AutoCast ?? new bool[skills.Length];
            int potions = Math.Max(0, Math.Min(loop.Potions, account.Potions));
            LoopVerdict verdict = ActivePlay.Verify(stage, hero, skills, seed, loop.Loop, auto, casts, potions, loop.Ticks);
            if (!verdict.Accepted) continue;
            verified++;
            coveredTicks += verdict.Ticks;
            weighted += (long)verdict.Ticks * verdict.EfficiencyBp;
        }
        if (coveredTicks == 0) return (RandomExtensions.FullBp, verified);
        long average = weighted / coveredTicks;
        long covered = Math.Min(coveredTicks, elapsedTicks);
        long bp = (covered * average + (elapsedTicks - covered) * RandomExtensions.FullBp) / elapsedTicks;
        return ((int)Math.Max(RandomExtensions.FullBp, Math.Min(ActivePlay.MaxEfficiencyBp, bp)), verified);
    }

    /// <summary>A fresh lane seed and loop counter: on the first login and on every park.</summary>
    private static void NewLane(Account account)
    {
        long seed;
        do seed = BitConverter.ToInt64(RandomNumberGenerator.GetBytes(8)); while (seed == 0);
        account.LaneSeed = seed;
        account.LaneLoop = 0;
    }

    public StateDto GetState(Account account) => ToState(account);

    public async Task<StateDto> ForgeAsync(Account account, ForgeRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        if (request.Method == ForgeMethod.ChainedSmith || request.Method == ForgeMethod.AnvilWard && account.AnvilWards <= 0)
            throw new GameException("method_unavailable", "That method is not available here.");

        Item item = AnvilItem(account, request.ItemId, request.Slot);
        ItemState state = item.ToState();
        if (state.UpgradeLevel >= ItemState.MaxUpgradeLevel) throw new GameException("already_max", "That item is already +9.");

        long cost = ForgeRules.Cost(state.ItemLevel, state.UpgradeLevel);
        int materials = ForgeRules.MaterialsNeeded(state.UpgradeLevel + 1);
        if (account.Sorn < cost) throw new GameException("no_sorn", "Not enough sorn.");
        if (account.Materials < materials) throw new GameException("no_materials", "Not enough Wolf Sinew.");
        switch (request.Method)
        {
            case ForgeMethod.ScrollOfMercy when account.ScrollsOfMercy <= 0: throw new GameException("no_scroll", "No Scroll of Mercy.");
            case ForgeMethod.KhansAlloy when account.KhansAlloys <= 0: throw new GameException("no_alloy", "No Khan's Alloy.");
        }

        account.Sorn -= cost;
        account.Materials -= materials;
        if (request.Method == ForgeMethod.ScrollOfMercy) account.ScrollsOfMercy--;
        if (request.Method == ForgeMethod.KhansAlloy) account.KhansAlloys--;
        if (request.Method == ForgeMethod.AnvilWard) account.AnvilWards--;

        ForgeResult result = _forge.Attempt(state, request.Method, _rng);
        item.ApplyState(state);
        if (result.Outcome == ForgeOutcome.Oathbreak)
        {
            // A worn piece is replaced by a starter so the slot is never bare; a piece from the bag is simply gone.
            bool worn = item.Equipped;
            item.Equipped = false;
            if (item.Slot == EquipSlot.Weapon) account.WeaponsBroken++;
            if (worn) account.Items.Add(Item.From(NewStarter(item.Slot), account.Id, equipped: true));
        }

        Count(account, BountyMetric.ForgeAttempts, 1);
        if (result.LevelAfter > result.LevelBefore && result.LevelAfter >= 8)
            SystemLine(Chat.World, $"{DisplayName(account)} forged {Content.ItemName(state, account.Class)} to +{result.LevelAfter}!");
        _db.Ledger.Add(Entry(account.Id, item.Id, "forge",
            $"{request.Method} +{result.LevelBefore}->+{result.LevelAfter} chance={result.ChanceBp} outcome={result.Outcome}", -cost, request.RequestId));
        await SaveAsync(ct);
        return ToState(account, forge: new ForgeResultDto(result.Outcome, result.ChanceBp, result.LevelBefore, result.LevelAfter));
    }

    /// <summary>One turn, or a Bulk Turn with a stop rule. The free cap is 10; 50 needs Hearthfire Blessing (not yet modelled, so 50 is open).</summary>
    public async Task<StateDto> TurnAsync(Account account, TurnRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        Item item = AnvilItem(account, request.ItemId, request.Slot);
        ItemState state = item.ToState();
        if (state.Etchings.Count == 0) throw new GameException("no_etchings", "That item has no etchings to turn yet.");
        int cost = state.LockedEtchingIndex >= 0 ? 2 : 1;
        if (account.Turnstones < cost) throw new GameException("no_turnstones", "Not enough Turnstones.");
        if (request.Count < 1 || request.Count > EtchingService.BulkTurnMax) throw new GameException("bad_count", "Count must be 1 to 50.");
        if (request.StopEntryId.HasValue && (request.StopEntryId < 0 || request.StopEntryId >= EtchingPool.For(state.Slot).Entries.Count))
            throw new GameException("bad_stop_rule", "Unknown etching in the stop rule.");

        EtchingPool pool = EtchingPool.For(state.Slot);
        TurnTarget[] goal = request.Targets is { Length: > 0 }
            ? request.Targets.Select(t => new TurnTarget(t.EntryId, t.MinTier)).ToArray()
            : request.StopEntryId is int stopEntry ? new[] { new TurnTarget(stopEntry, Math.Clamp(request.MinTier, 1, 5)) } : Array.Empty<TurnTarget>();
        if (EtchingService.TargetProblem(state, pool, goal) is string problem) throw new GameException("bad_goal", problem);

        var inventory = Snapshot(account);
        int spent = _etchings.TurnUntil(state, pool, inventory, _rng, request.Count, goal, out int turns, out bool stopped);
        Apply(account, inventory);
        item.ApplyState(state);
        Count(account, BountyMetric.Turns, turns);
        string goalText = string.Join(",", goal.Select(t => $"{t.EntryId}:T{t.MinTier}"));
        _db.Ledger.Add(Entry(account.Id, item.Id, "turn", $"turns={turns} spent={spent} stopped={stopped} goal={goalText} etchings={item.Etchings}", 0, request.RequestId));
        await SaveAsync(ct);
        return ToState(account, turn: new TurnResultDto(turns, spent, stopped));
    }

    /// <summary>
    /// Deletes one character and everything tied to it: items, the ledger, client error reports and its Commander fight
    /// records, chat lines and reports, guild requests, Exchange listings, devices on it and its email and password
    /// (Apple requires in-app account deletion: DeleteAccountAsync runs this for every character of the login). War of
    /// Banners points stay with the Banner. A guild leader's guild passes on (or disbands if empty).
    /// </summary>
    private async Task DeleteCharacterCoreAsync(Account account, CancellationToken ct)
    {
        if (account.GuildId != null)
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            await LeaveCoreAsync(account, ct);
            await SaveAsync(ct);
            await tx.CommitAsync(ct);
        }
        await _db.Devices.Where(d => d.AccountId == account.Id).ExecuteDeleteAsync(ct);
        await _db.ChatMessages.Where(m => m.AccountId == account.Id).ExecuteDeleteAsync(ct);
        await _db.ChatReports.Where(r => r.ReporterId == account.Id).ExecuteDeleteAsync(ct);
        await _db.GuildRequests.Where(r => r.AccountId == account.Id).ExecuteDeleteAsync(ct);
        await _db.GuildInvites.Where(r => r.AccountId == account.Id).ExecuteDeleteAsync(ct);
        await _db.Friendships.Where(f => f.FromId == account.Id || f.ToId == account.Id).ExecuteDeleteAsync(ct);
        await _db.PrivateMessages.Where(m => m.FromId == account.Id || m.ToId == account.Id).ExecuteDeleteAsync(ct);
        await _db.Letters.Where(l => l.AccountId == account.Id).ExecuteDeleteAsync(ct);
        await _db.MarketListings.Where(l => l.SellerId == account.Id).ExecuteDeleteAsync(ct);
        await _db.MarketListings.Where(l => l.BuyerId == account.Id).ExecuteUpdateAsync(s => s.SetProperty(l => l.BuyerId, (Guid?)null), ct);
        await _db.Ledger.Where(l => l.AccountId == account.Id).ExecuteDeleteAsync(ct);
        await _db.ClientLogs.Where(l => l.AccountId == account.Id).ExecuteDeleteAsync(ct);
        await _db.BossHits.Where(h => h.AccountId == account.Id).ExecuteDeleteAsync(ct);
        await _db.Items.Where(i => i.OwnerId == account.Id).ExecuteDeleteAsync(ct);
        await _db.Accounts.Where(a => a.Id == account.Id).ExecuteDeleteAsync(ct);
    }

    public const int ClientLogsPerHour = 20;

    /// <summary>Stores an error report from the client, trimmed, at most ClientLogsPerHour per account.</summary>
    public async Task LogClientErrorAsync(Account account, ClientLogRequest request, CancellationToken ct)
    {
        DateTime since = DateTime.UtcNow.AddHours(-1);
        if (await _db.ClientLogs.CountAsync(l => l.AccountId == account.Id && l.Utc > since, ct) >= ClientLogsPerHour) return;
        static string Cut(string? s, int n) => s == null ? "" : s.Length <= n ? s : s.Substring(0, n);
        _db.ClientLogs.Add(new ClientLog
        {
            AccountId = account.Id, Utc = DateTime.UtcNow, Platform = Cut(request.Platform, 32), Version = Cut(request.Version, 32),
            Message = Cut(request.Message, 512), Stack = Cut(request.Stack, 4000),
        });
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>The piece a Forge or Turn acts on: any owned item by id (worn or in the bag), else the one worn in the slot.</summary>
    private static Item AnvilItem(Account account, Guid? itemId, EquipSlot slot)
    {
        if (itemId is Guid id)
            return account.Items.SingleOrDefault(i => i.Id == id && !i.Destroyed && !i.OutOfBag)
                ?? throw new GameException("no_item", "You do not own that item.");
        return account.EquippedIn(slot) ?? throw new GameException("no_item", "Nothing is equipped in that slot.");
    }

    /// <summary>Sets a Korshard on an owned item: the shard is spent, 70% it takes, 30% a Dead Shard blocks the socket.</summary>
    public async Task<StateDto> SocketInsertAsync(Account account, SocketInsertRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        Item item = account.Items.SingleOrDefault(i => i.Id == request.ItemId && !i.Destroyed && !i.OutOfBag)
            ?? throw new GameException("no_item", "You do not own that item.");
        ItemState state = item.ToState();
        var inventory = Snapshot(account);

        string? blocker = _sockets.InsertBlocker(state, request.SocketIndex, request.Type, request.Rank, inventory);
        if (blocker != null) throw new GameException("socket_blocked", blocker);

        bool ok = _sockets.TryInsert(state, request.SocketIndex, request.Type, request.Rank, inventory, _rng);
        item.ApplyState(state);
        Apply(account, inventory);

        string text = ok
            ? SocketRules.Name(request.Type) + " " + Content.KorshardRanks[request.Rank] + " shard set: " + SocketRules.Describe(request.Type, request.Rank)
            : "The shard shattered. A Dead Shard blocks socket " + (request.SocketIndex + 1) + ".";
        _db.Ledger.Add(Entry(account.Id, item.Id, "socket", $"insert {request.Type} rank={request.Rank} socket={request.SocketIndex} ok={ok}", 0, request.RequestId));
        await SaveAsync(ct);
        return ToState(account, socket: new SocketResultDto(ok, request.SocketIndex, text));
    }

    public async Task<StateDto> SocketClearAsync(Account account, SocketClearRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        Item item = account.Items.SingleOrDefault(i => i.Id == request.ItemId && !i.Destroyed && !i.OutOfBag)
            ?? throw new GameException("no_item", "You do not own that item.");
        ItemState state = item.ToState();
        var inventory = Snapshot(account);

        string? blocker = _sockets.ClearBlocker(state, request.SocketIndex, inventory);
        if (blocker != null) throw new GameException("socket_blocked", blocker);

        long cost = SocketRules.ClearCost(state);
        _sockets.Clear(state, request.SocketIndex, inventory);
        item.ApplyState(state);
        Apply(account, inventory);
        _db.Ledger.Add(Entry(account.Id, item.Id, "socket", $"clear socket={request.SocketIndex}", -cost, request.RequestId));
        await SaveAsync(ct);
        return ToState(account, socket: new SocketResultDto(true, request.SocketIndex, "Dead Shard removed for " + cost + " sorn."));
    }

    /// <summary>Equips an owned, unequipped piece; the previous piece in that slot goes back to the loot list.</summary>
    public async Task<StateDto> EquipAsync(Account account, EquipRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        Item item = account.Items.SingleOrDefault(i => i.Id == request.ItemId && !i.Destroyed && !i.OutOfBag)
            ?? throw new GameException("no_item", "You do not own that item.");
        if (item.Equipped) throw new GameException("already_equipped", "That piece is already equipped.");

        foreach (Item worn in account.Items.Where(i => i.Equipped && i.Slot == item.Slot)) worn.Equipped = false;
        item.Equipped = true;
        _db.Ledger.Add(Entry(account.Id, item.Id, "equip", item.Slot.ToString(), 0, request.RequestId));
        await SaveAsync(ct);
        return ToState(account);
    }

    /// <summary>
    /// Sells bag pieces to the merchant (owner, 26 Sep 2026: "selling mechanic ... for sorns but not automatically", then
    /// "we need a bulk sell but user can select items then press sell all"): by hand, one piece or the pieces picked, for
    /// Rules.Bag.SellPrice each; worn, listed, traded or depot pieces cannot be sold. All or nothing.
    /// </summary>
    public async Task<StateDto> SellPieceAsync(Account account, BagSellRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        Guid[] ids = (request.ItemIds is { Length: > 0 } picked ? picked : new[] { request.ItemId }).Distinct().ToArray();
        if (ids.Length > Bag.Size) throw new GameException("too_many", $"At most {Bag.Size} pieces at once.");
        var items = new List<Item>();
        foreach (Guid id in ids)
        {
            Item item = account.Items.SingleOrDefault(i => i.Id == id && !i.Destroyed && !i.OutOfBag)
                ?? throw new GameException("no_item", "You do not own that piece.");
            if (item.Equipped) throw new GameException("worn", "Take the piece off before you sell it.");
            items.Add(item);
        }
        long total = 0;
        for (int n = 0; n < items.Count; n++)
        {
            Item item = items[n];
            ItemState state = item.ToState();
            long price = Bag.SellPrice(state);
            total += price;
            _db.Items.Remove(item);
            account.Items.Remove(item);
            // One ledger row a piece; the request id is unique per account, so the others carry their number.
            string rid = n == 0 ? request.RequestId : request.RequestId[..Math.Min(request.RequestId.Length, 56)] + "#" + n;
            _db.Ledger.Add(Entry(account.Id, item.Id, "bag-sell",
                $"slot={state.Slot} rarity={state.Rarity} itemLevel={state.ItemLevel} plus={state.UpgradeLevel}", price, rid));
        }
        account.Sorn += total;
        await SaveAsync(ct);
        return ToState(account);
    }

    public async Task<StateDto> ParkAsync(Account account, ParkRequest request, CancellationToken ct)
    {
        if (!Content.IsUnlocked(request.Stage, account.HighestStageCleared))
            throw new GameException("stage_locked", "That stage or zone is not unlocked.");
        // Settle the time spent on the old stage before the rate changes.
        DateTime now = DateTime.UtcNow;
        SettlementDto settlement = Settle(account, now);
        account.LastHeartbeatUtc = now;
        account.ParkedStage = request.Stage;
        NewLane(account);
        await SaveAsync(ct);
        return ToState(account, settlement: settlement);
    }

    /// <summary>
    /// Switches class. The time on the old class is settled first, and the lane gets a fresh seed so loop reports
    /// from the old kit can never be judged against the new one.
    /// </summary>
    public async Task<StateDto> SetClassAsync(Account account, ClassRequest request, CancellationToken ct)
    {
        if (!Enum.IsDefined(request.HeroClass)) throw new GameException("bad_class", "Unknown class.");
        if (request.HeroClass == account.Class) return ToState(account);
        DateTime now = DateTime.UtcNow;
        SettlementDto settlement = Settle(account, now);
        account.LastHeartbeatUtc = now;
        account.Class = request.HeroClass;
        NewLane(account);
        _db.Ledger.Add(Entry(account.Id, null, "class", request.HeroClass.ToString(), 0, Guid.NewGuid().ToString("N")));
        await SaveAsync(ct);
        return ToState(account, settlement: settlement);
    }

    /// <summary>
    /// Decides the next stage with a fresh seed. The client replays the same seed with the same hero, so the
    /// fight it shows is the fight that was scored here.
    /// </summary>
    public async Task<StateDto> PushAsync(Account account, PushRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        int target = Math.Min(Content.TotalStages, account.HighestStageCleared + 1);
        if (account.HighestStageCleared >= Content.TotalStages) throw new GameException("no_more_stages", "Every stage is cleared.");

        ulong seed = BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(8));
        int potionsAtStart = account.Potions;
        var inventory = Snapshot(account);
        StageRunResult run = StageRun.Simulate(EveningBells.Apply(Content.Stage(target), _bells.Active), Hero(account), inventory, seed);
        Apply(account, inventory, hunt: true);
        if (run.Cleared) account.HighestStageCleared = target;
        Count(account, BountyMetric.Pushes, 1);

        _db.Ledger.Add(Entry(account.Id, null, "push", $"stage={target} seed={seed} cleared={run.Cleared} ticks={run.Ticks}", 0, request.RequestId));
        await SaveAsync(ct);
        if (run.Cleared) await AddPointsAsync(account.Banner, Banners.PointsPushCleared, ct);
        return ToState(account, push: new PushResultDto(target, run.Cleared, seed, run.Ticks, account.HighestStageCleared, potionsAtStart, _bells.Active));
    }

    /// <summary>
    /// One Commander fight. The boss must be up (server-wide clock) and this account may fight it once per spawn.
    /// Since 24 Sep 2026 every spawn has one HP pool for the whole server: the fight is scored here with a seed the
    /// client replays, its damage comes off the pool, and the fight that takes the last of it slays the Commander for
    /// its Banner. The chest follows the damage rank among this spawn's real fighters (simulated rivals fill the
    /// bracket to 20). The boss row is locked for the fight so two fights never spend the same HP.
    /// </summary>
    public async Task<StateDto> FightBossAsync(Account account, BossFightRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        BossDef boss = Content.Boss(request.BossId) ?? throw new GameException("no_boss", "Unknown Commander.");
        if (!Content.IsUnlocked(boss.ZoneId, account.HighestStageCleared))
            throw new GameException("stage_locked", "That Commander Ground is not unlocked.");

        DateTime now = DateTime.UtcNow;
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        BossClock clock = await LockClockAsync(boss, now, ct);
        if (now >= clock.SpawnUtc.AddSeconds(BossDef.WindowSeconds) || now < clock.SpawnUtc)
            throw new GameException("boss_down", boss.Name + " is not up.");
        if (clock.SlainUtc != null)
            throw new GameException("boss_slain", boss.Name + (clock.SlainBanner == Banner.None ? " has already fallen." : " has fallen to the " + Banners.Def(clock.SlainBanner).Name + "."));

        string spawnKey = "boss:" + boss.Id + ":" + clock.SpawnUtc.Ticks;
        if (await _db.Ledger.AnyAsync(l => l.AccountId == account.Id && l.Kind == spawnKey, ct))
            throw new GameException("already_fought", "You already fought " + boss.Name + " this spawn.");

        ulong seed = BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(8));
        int potionsAtStart = account.Potions;
        Bell bell = _bells.Active;
        var inventory = Snapshot(account);
        BossRunResult run = BossRun.Simulate(boss, Hero(account), inventory, seed, bell);
        List<long> others = await _db.BossHits.Where(h => h.BossId == boss.Id && h.SpawnUtc == clock.SpawnUtc).Select(h => h.Damage).ToListAsync(ct);
        int rank = BossRun.RankShared(run.Damage, others, boss, _rng);
        string chest = HuntYield.LootCommander(boss, rank, inventory, _rng);

        string name = DisplayName(account);
        clock.HpLeft = Math.Max(0, clock.HpLeft - run.Damage);
        bool slew = clock.HpLeft == 0;
        if (slew)
        {
            clock.SlainUtc = now;
            clock.SlainBanner = account.Banner;
            clock.SlainBy = name;
            chest += ", and you struck the last blow";
            SystemLine(Chat.World, account.Banner == Banner.None ? $"{boss.Name} has fallen; {name} struck the last blow."
                : $"{boss.Name} has fallen to the {Banners.Def(account.Banner).Name}; {name} struck the last blow.");
            if (await RewardTopGuildAsync(boss, clock, account, run.Damage, ct)) chest += $", and your guild's {Guilds.CommanderTopTallies} Guild Tallies for rank 1";
        }
        _db.BossHits.Add(new BossHit { BossId = boss.Id, SpawnUtc = clock.SpawnUtc, AccountId = account.Id, Name = name, Banner = account.Banner, Damage = run.Damage, Utc = now });
        Apply(account, inventory, hunt: true);
        Count(account, BountyMetric.CommanderFights, 1);

        _db.Ledger.Add(Entry(account.Id, null, spawnKey, $"seed={seed} damage={run.Damage} killed={run.Killed} rank={rank} pool={clock.HpLeft}/{clock.HpMax} slew={slew} bell={bell} chest={chest}", 0, request.RequestId));
        await SaveAsync(ct);
        long points = run.Damage / Banners.CommanderDamagePerPoint + (slew ? Banners.PointsCommanderSlain : 0);
        await AddPointsAsync(account.Banner, points, ct);
        await tx.CommitAsync(ct);
        return ToState(account, bossFight: new BossFightResultDto(boss.Id, seed, run.Damage, run.Killed, rank, chest, potionsAtStart, bell, clock.HpLeft, slew));
    }

    /// <summary>
    /// GDD: the guild of a Commander's rank 1 gets 50 Guild Tallies. When a spawn falls, the top damage dealer of the
    /// spawn (this fighter included) is paid them if in a guild, and the guild gains XP. True when that is this fighter.
    /// </summary>
    private async Task<bool> RewardTopGuildAsync(BossDef boss, BossClock clock, Account account, long damage, CancellationToken ct)
    {
        var top = await _db.BossHits.AsNoTracking().Where(h => h.BossId == boss.Id && h.SpawnUtc == clock.SpawnUtc)
            .OrderByDescending(h => h.Damage).Select(h => new { h.AccountId, h.Damage }).FirstOrDefaultAsync(ct);
        if (top == null || damage >= top.Damage)
        {
            if (account.GuildId == null) return false;
            account.Tallies += Guilds.CommanderTopTallies;
            await AddGuildXpAsync(account.GuildId, Guilds.CommanderTopXp, ct);
            return true;
        }
        Guid? guild = await _db.Accounts.AsNoTracking().Where(a => a.Id == top.AccountId).Select(a => a.GuildId).FirstOrDefaultAsync(ct);
        if (guild == null) return false;
        await _db.Accounts.Where(a => a.Id == top.AccountId).ExecuteUpdateAsync(s => s.SetProperty(a => a.Tallies, a => a.Tallies + Guilds.CommanderTopTallies), ct);
        await AddGuildXpAsync(guild, Guilds.CommanderTopXp, ct);
        return false;
    }

    /// <summary>Playtest only; disabled outside Development.</summary>
    public async Task<StateDto> DevGrantAsync(Account account, CancellationToken ct)
    {
        account.Sorn += 500_000;
        account.Materials += 10;
        account.ScrollsOfMercy += 5;
        account.KhansAlloys += 1;
        account.Turnstones += 20;
        account.HuntMarks += 20;
        account.EtchingNeedles += 5;
        account.MastersNeedles += 1;
        account.Oathstones += Banners.ChangeOathstones;
        for (int slot = 0; slot < Rules.SkillGrades.Slots; slot++) AddBooks(account, Books.Id(account.Class, slot), 5);
        account.Honor += 500;
        account.PinningWax += 2;
        account.Tallies += 100;
        account.Laurels += 100;
        int[] shards = ParseShards(account.Korshards);
        for (int i = 0; i < shards.Length; i++) shards[i] += 3;
        account.Korshards = string.Join(';', shards);
        _db.Ledger.Add(Entry(account.Id, null, "dev-grant", "playtest grant", 500_000, Guid.NewGuid().ToString("N")));
        await SaveAsync(ct);
        return ToState(account);
    }

    /// <summary>Playtest only: every Commander spawns now, so testers need not wait 45 minutes.</summary>
    public async Task<StateDto> DevBossesUpAsync(Account account, CancellationToken ct)
    {
        DateTime now = DateTime.UtcNow;
        foreach (BossDef boss in Content.Bosses)
        {
            BossClock clock = await ClockAsync(boss, now, ct);
            clock.SpawnUtc = now;
        }
        await SaveAsync(ct);
        return ToState(account);
    }

    /// <summary>Loads or creates a boss clock and rolls it forward to the current spawn cycle.</summary>
    private async Task<BossClock> ClockAsync(BossDef boss, DateTime now, CancellationToken ct)
    {
        BossClock? clock = await _db.BossClocks.FindAsync(new object[] { boss.Id }, ct);
        if (clock == null)
        {
            // A fresh server: the first spawn is on the next respawn boundary from a fixed epoch.
            clock = new BossClock { BossId = boss.Id, SpawnUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
            _db.BossClocks.Add(clock);
        }
        RollClock(boss, clock, now);
        return clock;
    }

    /// <summary>Moves a Commander's clock to its current spawn; spawns come faster during a Commander rush.</summary>
    private void RollClock(BossDef boss, BossClock clock, DateTime now)
    {
        for (DateTime next = _events.NextSpawn(clock.SpawnUtc, boss.RespawnSeconds); now >= next; next = _events.NextSpawn(next, boss.RespawnSeconds))
            clock.SpawnUtc = next;
    }

    private BossStatusDto BossStatus(BossDef boss, BossClock clock, bool fought, long freshPool, BossHitDto[] top, DateTime now)
    {
        DateTime windowEnd = clock.SpawnUtc.AddSeconds(BossDef.WindowSeconds);
        bool slain = clock.PoolSpawnUtc == clock.SpawnUtc && clock.SlainUtc != null;
        bool up = now >= clock.SpawnUtc && now < windowEnd && !slain;
        long secondsLeft = up ? (long)(windowEnd - now).TotalSeconds : (long)(_events.NextSpawn(clock.SpawnUtc, boss.RespawnSeconds) - now).TotalSeconds;
        // A spawn nobody has fought yet shows the pool it will open with.
        bool current = clock.PoolSpawnUtc == clock.SpawnUtc;
        return new BossStatusDto(boss.Id, boss.Name, boss.Mechanic.ToString(), up, Math.Max(0, secondsLeft), fought,
            current ? clock.HpLeft : freshPool, current ? clock.HpMax : freshPool, slain, slain ? clock.SlainBy : null, slain ? clock.SlainBanner : Banner.None, top);
    }

    /// <summary>New drops this request could not fit in a full bag (Apply counts them).</summary>
    private int LeftBehind { get; set; }

    private SettlementDto Settle(Account account, DateTime now, int onlineEfficiencyBp = RandomExtensions.FullBp, int loopsVerified = 0, int sornBonusPercent = 0)
    {
        int leftBefore = LeftBehind;
        TimeSpan gap = now - account.LastHeartbeatUtc;
        bool offline = gap > OnlineGrace;
        long seconds = (long)gap.TotalSeconds;
        long cap = offline ? OfflineRewards.FreeCapSeconds : (long)OnlineGrace.TotalSeconds;
        int efficiency = offline ? OfflineRewards.OfflineEfficiencyBp : onlineEfficiencyBp;

        StageConfig stage = Content.Stage(account.ParkedStage);
        ZoneDef? zone = Content.Zone(account.ParkedStage);
        // Fields IV-V and Commander Grounds need presence: nothing settles for them offline.
        if (offline && zone != null && !zone.OfflineAllowed) seconds = 0;
        if (stage.GearRarityCap > Rarity.Rare && offline) stage.GearRarityCap = Rarity.Rare;
        // Bells reward presence: they apply to live settlement only.
        if (!offline) EveningBells.Apply(stage, _bells.Active);
        var inventory = Snapshot(account);
        HuntSettlement s = HuntYield.Settle(stage, Hero(account), seconds, cap, efficiency, inventory, _rng);
        // Honor (Rules.SkillGrades): every Korstone broken, online or off.
        account.Honor += s.Korstones * Rules.SkillGrades.HonorPerKorstone;
        // The War of Banners bonus: last season's winning Banner and each fortress a Banner holds add sorn.
        long bonusSorn = s.SornEarned * sornBonusPercent / 100;
        inventory.Sorn += bonusSorn;
        Apply(account, inventory, hunt: true);

        if (s.CountedSeconds > 0)
            _db.Ledger.Add(Entry(account.Id, null, offline ? "settle-offline" : "settle-online",
                $"stage={account.ParkedStage} seconds={s.CountedSeconds} packs={s.Packs} korstones={s.Korstones} efficiencyBp={efficiency} loopsVerified={loopsVerified} bannerBonus={sornBonusPercent}%", s.SornEarned + bonusSorn, Guid.NewGuid().ToString("N")));

        return new SettlementDto(s.CountedSeconds, s.Packs, s.Korstones, s.SornEarned + bonusSorn, offline, LeftBehind: LeftBehind - leftBefore);
    }

    private async Task EnsureFreshRequestAsync(Account account, string requestId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(requestId) || requestId.Length > 64)
            throw new GameException("bad_request_id", "RequestId missing or too long.");
        if (await _db.Ledger.AnyAsync(l => l.AccountId == account.Id && l.RequestId == requestId, ct))
            throw new GameException("duplicate_request", "This request was already processed.");
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            string entries = string.Join(", ", ex.Entries.Select(e => e.Metadata.ClrType.Name + ":" + e.State));
            throw new GameException("conflict", "Another request changed this account. Refresh and retry. [" + entries + "]");
        }
    }

    /// <summary>A fresh Rare piece for the slot with 5 etchings: the starter kit, and what an Oathbreak leaves behind.</summary>
    private ItemState NewStarter(EquipSlot slot)
    {
        var state = new ItemState(PlayerSession.StarterItemLevel, Rarity.Rare, slot);
        EtchingPool pool = EtchingPool.For(slot);
        while (state.Etchings.Count < ItemState.MaxEtchings)
        {
            NeedleKind needle = state.Etchings.Count == ItemState.MaxEtchings - 1 ? NeedleKind.MastersNeedle : NeedleKind.EtchingNeedle;
            _etchings.TryAdd(state, pool, needle, _rng);
        }
        return state;
    }

    private static HeroStats Hero(Account a) =>
        HeroFactory.FromEquipment(a.Items.Where(i => i.Equipped && !i.Destroyed).Select(i => i.ToState()), Content.LevelFor(a.Xp), a.Class, WornPieces(a), a.Renewals,
            Rules.SkillGrades.ForClass(Rules.SkillGrades.Parse(a.SkillGrades), a.Class));

    private static int[] ParseShards(string s) => s.Split(';').Select(int.Parse).ToArray();
    private static string[] ParseSkins(string s) => s.Split(';', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// Boss statuses need the clocks, the shared pools, the top fighters and this account's fights this spawn; loaded
    /// per request by the endpoints. Nothing here refills a pool: only a fight (holding the row lock) opens a spawn's
    /// pool, so a status read can never undo a fight's damage.
    /// </summary>
    public async Task<StateDto> WithBossesAsync(Account account, StateDto state, CancellationToken ct)
    {
        await ExpireListingsAsync(account, ct);
        DateTime now = DateTime.UtcNow;
        var list = new List<BossStatusDto>();
        long freshPool = -1;
        foreach (BossDef boss in Content.Bosses)
        {
            BossClock clock = await ClockAsync(boss, now, ct);
            string spawnKey = "boss:" + boss.Id + ":" + clock.SpawnUtc.Ticks;
            bool fought = await _db.Ledger.AnyAsync(l => l.AccountId == account.Id && l.Kind == spawnKey, ct);
            BossHitDto[] top = await _db.BossHits.Where(h => h.BossId == boss.Id && h.SpawnUtc == clock.SpawnUtc)
                .OrderByDescending(h => h.Damage).Take(3).Select(h => new BossHitDto(h.Name, h.Banner, h.Damage)).ToArrayAsync(ct);
            if (freshPool < 0) freshPool = await PoolFightersAsync(now, ct);
            list.Add(BossStatus(boss, clock, fought, boss.Hp * freshPool, top, now));
        }
        await _db.SaveChangesAsync(ct);
        var (friendAsks, guildInvites, whispers) = await SocialCountsAsync(account, ct);
        int mail = await _db.Letters.CountAsync(l => l.AccountId == account.Id && !l.Read, ct);
        return state with { Bosses = list.ToArray(), Trade = await TradeBriefAsync(account, ct), FriendAsks = friendAsks, GuildInvites = guildInvites, Whispers = whispers,
            Mail = mail };
    }

    private StateDto ToState(Account account, SettlementDto? settlement = null, ForgeResultDto? forge = null, PushResultDto? push = null, BossFightResultDto? bossFight = null, SocketResultDto? socket = null, TurnResultDto? turn = null,
        SiegeResultDto? siege = null, EtchResultDto? etch = null)
    {
        Item weapon = account.Weapon;
        ItemState state = weapon.ToState();
        ForgeService forgeService = _forge;
        bool maxed = state.UpgradeLevel >= ItemState.MaxUpgradeLevel;
        HeroStats hero = Hero(account);

        return new StateDto(
            account.Id,
            new InventoryDto(account.Sorn, account.Potions, account.Materials, account.ScrollsOfMercy, account.KhansAlloys, account.AnvilWards, account.Turnstones,
                account.EtchingNeedles, account.SummoningMarkers, account.Xp, Content.LevelFor(account.Xp), ParseShards(account.Korshards), ParseSkins(account.Skins),
                account.HuntMarks, account.PinningWax, account.Tallies, account.MastersNeedles, account.Oathstones, BookCounts(account)),
            ToDto(weapon),
            account.Items.Where(i => !i.Destroyed && !i.OutOfBag).OrderByDescending(i => i.Equipped).ThenByDescending(i => i.CreatedUtc).Select(ToDto).ToArray(),
            new HeroDto(hero.Attack, hero.Defense, hero.MaxHp, hero.CritChanceBp),
            new ForgePreviewDto(
                maxed ? 0 : ForgeRules.Cost(state.ItemLevel, state.UpgradeLevel),
                maxed ? 0 : ForgeRules.MaterialsNeeded(state.UpgradeLevel + 1),
                maxed ? 0 : forgeService.ChanceBp(state, ForgeMethod.ForgeAlone),
                maxed ? 0 : forgeService.ChanceBp(state, ForgeMethod.KhansAlloy),
                !maxed && state.UpgradeLevel + 1 >= ForgeRules.FirstOathbreakTarget),
            account.WeaponsBroken,
            account.HighestStageCleared,
            account.ParkedStage,
            Array.Empty<BossStatusDto>(),
            _bells.Dto(),
            DateTime.UtcNow,
            settlement,
            forge,
            push,
            bossFight,
            socket,
            turn,
            new LaneDto(unchecked((ulong)account.LaneSeed).ToString(), account.LaneLoop),
            account.Class,
            Board(account),
            account.Banner,
            NameOf(account),
            siege,
            etch,
            Brief(account),
            _login?.Email,
            LoginsOf(account),
            DungeonRunsLeft(account),
            account.DungeonRunAtSmith,
            WardrobeOf(account),
            TrailOf(account),
            null,
            account.DungeonRunAtSmith != 0 ? account.DungeonPausedId : 0,
            Renewals: account.Renewals,
            SkillGrades: Rules.SkillGrades.Parse(account.SkillGrades),
            SkillProgress: Rules.SkillGrades.Parse(account.SkillProgress, 99),
            SkillReadySeconds: SkillReadySeconds(account),
            Honor: account.Honor,
            Figure: account.Figure,
            Daily: DailyOf(account),
            Events: _events.Dto(DateTime.UtcNow));
    }

    private static ItemDto ToDto(Item item)
    {
        ItemState s = item.ToState();
        EtchingPool pool = EtchingPool.For(s.Slot);
        return new ItemDto(item.Id, s.Slot, item.Equipped, s.DisplayName, s.ItemLevel, s.Rarity, s.UpgradeLevel, s.PatienceBp, s.LockedEtchingIndex,
            s.Etchings.Select(e => new EtchingDto(e.EntryId, pool.Entries[e.EntryId].Name, e.Tier, e.Value)).ToArray(),
            s.Sockets.Select(k => new SocketDto(k.Dead, k.Type?.ToString(), k.Rank,
                k.Dead ? "Dead Shard" : k.Type == null ? "empty" : SocketRules.Name(k.Type.Value) + " " + Content.KorshardRanks[k.Rank] + ": " + SocketRules.Describe(k.Type.Value, k.Rank))).ToArray(),
            s.AverageDamagePercent, s.SkillDamagePercent);
    }

    /// <summary>The hero's Technique Scrolls by book id.</summary>
    private static int[] BookCounts(Account a)
    {
        var counts = new int[Books.Count];
        foreach (BookStack b in a.Books)
            if (Books.Valid(b.BookId)) counts[b.BookId] = b.Count;
        return counts;
    }

    /// <summary>Sets a stack on the tracked hero (a row appears the first time a book is held).</summary>
    private static void SetBooks(Account a, int bookId, int count)
    {
        BookStack? stack = a.Books.FirstOrDefault(b => b.BookId == bookId);
        if (stack == null)
        {
            if (count <= 0) return;
            a.Books.Add(new BookStack { AccountId = a.Id, BookId = bookId, Count = count });
        }
        else stack.Count = Math.Max(0, count);
    }

    private static void AddBooks(Account a, int bookId, int count) =>
        SetBooks(a, bookId, (a.Books.FirstOrDefault(b => b.BookId == bookId)?.Count ?? 0) + count);

    /// <summary>Adds to another hero's stack in one statement (the Exchange, trades): an upsert, safe under concurrent use.</summary>
    private Task AddBooksElsewhereAsync(Guid accountId, int bookId, int count, CancellationToken ct) =>
        _db.Database.ExecuteSqlInterpolatedAsync(
            $@"INSERT INTO ""BookStacks"" (""AccountId"", ""BookId"", ""Count"") VALUES ({accountId}, {bookId}, {count})
               ON CONFLICT (""AccountId"", ""BookId"") DO UPDATE SET ""Count"" = ""BookStacks"".""Count"" + EXCLUDED.""Count""", ct);

    private static Inventory Snapshot(Account a)
    {
        var inventory = new Inventory
        {
            Sorn = a.Sorn, Potions = a.Potions, Materials = a.Materials, ScrollsOfMercy = a.ScrollsOfMercy,
            KhansAlloys = a.KhansAlloys, AnvilWards = a.AnvilWards, Turnstones = a.Turnstones,
            EtchingNeedles = a.EtchingNeedles, SummoningMarkers = a.SummoningMarkers, Xp = a.Xp,
            HuntMarks = a.HuntMarks, PinningWax = a.PinningWax, MastersNeedles = a.MastersNeedles, Oathstones = a.Oathstones,
        };
        int[] shards = ParseShards(a.Korshards);
        Array.Copy(shards, inventory.Korshards, Math.Min(shards.Length, inventory.Korshards.Length));
        Array.Copy(BookCounts(a), inventory.Books, Books.Count);
        inventory.Skins.AddRange(ParseSkins(a.Skins));
        return inventory;
    }

    /// <summary>
    /// Writes settled currency back and turns dropped gear into item rows, trimming the loot list. Dropped wardrobe pieces
    /// become held ones. <paramref name="hunt"/>: the gain was hunted, so a worn companion adds its XP or sorn to it.
    /// </summary>
    private void Apply(Account a, Inventory i, bool hunt = false)
    {
        if (hunt)
        {
            List<WardrobeDef> worn = WornPieces(a);
            int xp = Wardrobe.Bonus(worn, WardrobePerk.Xp), sorn = Wardrobe.Bonus(worn, WardrobePerk.Sorn);
            if (xp > 0 && i.Xp > a.Xp) i.Xp += (i.Xp - a.Xp) * xp / 100;
            if (sorn > 0 && i.Sorn > a.Sorn) i.Sorn += (i.Sorn - a.Sorn) * sorn / 100;
        }
        if (i.WardrobeDrops.Count > 0) HoldDrops(a, i.WardrobeDrops);

        a.Sorn = i.Sorn; a.Potions = i.Potions; a.Materials = i.Materials; a.ScrollsOfMercy = i.ScrollsOfMercy;
        a.KhansAlloys = i.KhansAlloys; a.AnvilWards = i.AnvilWards; a.Turnstones = i.Turnstones;
        a.EtchingNeedles = i.EtchingNeedles; a.SummoningMarkers = i.SummoningMarkers; a.Xp = i.Xp;
        a.HuntMarks = i.HuntMarks; a.PinningWax = i.PinningWax; a.MastersNeedles = i.MastersNeedles; a.Oathstones = i.Oathstones;
        for (int b = 0; b < Books.Count; b++) SetBooks(a, b, i.Books[b]);
        a.Korshards = string.Join(';', i.Korshards);
        a.Skins = string.Join(';', i.Skins);

        if (i.Loot.Count == 0) return;

        // The bag (owner, 26 Sep 2026): new drops fill what room is left, the best rarity first; when it is full they are
        // left behind. Nothing already in the bag is ever removed to make room.
        int held = a.Items.Count(x => !x.Equipped && !x.Destroyed && !x.OutOfBag);
        List<ItemState> fitting = Bag.Fitting(held, i.Loot);
        LeftBehind += i.Loot.Count - fitting.Count;
        foreach (ItemState drop in fitting) a.Items.Add(Item.From(drop, a.Id, equipped: false));
        i.Loot.Clear();
    }

    private static LedgerEntry Entry(Guid accountId, Guid? itemId, string kind, string detail, long sornDelta, string requestId) => new()
    {
        // The column holds 512 characters: a longer line is clipped rather than failing the whole save.
        AccountId = accountId, ItemId = itemId, Kind = kind, Detail = detail.Length > 512 ? detail[..509] + "..." : detail, SornDelta = sornDelta,
        RequestId = requestId, Utc = DateTime.UtcNow,
    };

    private static string NewToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
