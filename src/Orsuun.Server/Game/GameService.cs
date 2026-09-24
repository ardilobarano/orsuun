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
public sealed class GameService
{
    /// <summary>Gaps longer than this count as offline time at the offline rate.</summary>
    public static readonly TimeSpan OnlineGrace = TimeSpan.FromMinutes(3);
    /// <summary>Loot list cap; the oldest common pieces are dropped past it.</summary>
    public const int MaxLoot = 60;

    private readonly GameDb _db;
    private readonly IRandom _rng;
    private readonly BellClock _bells;
    private readonly ForgeService _forge = new();
    private readonly EtchingService _etchings = new();
    private readonly SocketService _sockets = new();

    public GameService(GameDb db, IRandom rng, BellClock bells)
    {
        _db = db;
        _rng = rng;
        _bells = bells;
    }

    /// <summary>
    /// Device tokens are minted by the client, so a script could farm starter kits and boss-bracket entries.
    /// Until platform attestation (App Attest, Play Integrity) arrives, cap new accounts per network per day.
    /// Loopback is exempt so local dev and the smoke test keep working.
    /// </summary>
    public const int MaxNewAccountsPerIpPerDay = 10;

    public async Task<GuestLoginResponse> GuestLoginAsync(string deviceToken, string? clientIp, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(deviceToken) || deviceToken.Length > 128)
            throw new GameException("bad_device_token", "Device token missing or too long.");

        Account? account = await _db.Accounts.SingleOrDefaultAsync(a => a.DeviceToken == deviceToken, ct);
        bool created = account == null;
        if (account == null)
        {
            DateTime now = DateTime.UtcNow;
            bool loopback = clientIp == null || System.Net.IPAddress.TryParse(clientIp, out var ip) && System.Net.IPAddress.IsLoopback(ip);
            if (!loopback)
            {
                DateTime since = now.AddDays(-1);
                int recent = await _db.Accounts.CountAsync(a => a.CreatedIp == clientIp && a.CreatedUtc > since, ct);
                if (recent >= MaxNewAccountsPerIpPerDay)
                    throw new GameException("too_many_accounts", "Too many new accounts from this network today. Try again tomorrow.");
            }
            account = new Account
            {
                Id = Guid.NewGuid(),
                DeviceToken = deviceToken,
                CreatedUtc = now,
                CreatedIp = clientIp,
                LastHeartbeatUtc = now,
                Sorn = 20_000,
                Potions = 30,
                ScrollsOfMercy = 2,
                Turnstones = 5,
            };
            account.Items.Add(Item.From(NewStarter(EquipSlot.Weapon), account.Id, equipped: true));
            _db.Accounts.Add(account);
            _db.Ledger.Add(Entry(account.Id, null, "account-created", "starter kit", account.Sorn, Guid.NewGuid().ToString("N")));
        }

        if (account.LaneSeed == 0) NewLane(account);
        account.SessionToken = NewToken();
        await _db.SaveChangesAsync(ct);
        return new GuestLoginResponse(account.Id, account.SessionToken, created);
    }

    public async Task<Account?> AuthenticateAsync(string? sessionToken, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(sessionToken)) return null;
        return await _db.Accounts.SingleOrDefaultAsync(a => a.SessionToken == sessionToken, ct);
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
        SettlementDto settlement = Settle(account, now, activeBp, verified) with { ActiveBp = activeBp, LoopsVerified = verified };
        account.LastHeartbeatUtc = now;
        await SaveAsync(ct);
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
        string goalText = string.Join(",", goal.Select(t => $"{t.EntryId}:T{t.MinTier}"));
        _db.Ledger.Add(Entry(account.Id, item.Id, "turn", $"turns={turns} spent={spent} stopped={stopped} goal={goalText} etchings={item.Etchings}", 0, request.RequestId));
        await SaveAsync(ct);
        return ToState(account, turn: new TurnResultDto(turns, spent, stopped));
    }

    /// <summary>
    /// Deletes the account and everything tied to it: items, the ledger and client error reports (Apple requires
    /// in-app account deletion; the game holds no other personal data). The session dies with it.
    /// </summary>
    public async Task DeleteAccountAsync(Account account, CancellationToken ct)
    {
        await _db.Ledger.Where(l => l.AccountId == account.Id).ExecuteDeleteAsync(ct);
        await _db.ClientLogs.Where(l => l.AccountId == account.Id).ExecuteDeleteAsync(ct);
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
            return account.Items.SingleOrDefault(i => i.Id == id && !i.Destroyed)
                ?? throw new GameException("no_item", "You do not own that item.");
        return account.EquippedIn(slot) ?? throw new GameException("no_item", "Nothing is equipped in that slot.");
    }

    /// <summary>Sets a Korshard on an owned item: the shard is spent, 70% it takes, 30% a Dead Shard blocks the socket.</summary>
    public async Task<StateDto> SocketInsertAsync(Account account, SocketInsertRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        Item item = account.Items.SingleOrDefault(i => i.Id == request.ItemId && !i.Destroyed)
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
        Item item = account.Items.SingleOrDefault(i => i.Id == request.ItemId && !i.Destroyed)
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
        Item item = account.Items.SingleOrDefault(i => i.Id == request.ItemId && !i.Destroyed)
            ?? throw new GameException("no_item", "You do not own that item.");
        if (item.Equipped) throw new GameException("already_equipped", "That piece is already equipped.");

        foreach (Item worn in account.Items.Where(i => i.Equipped && i.Slot == item.Slot)) worn.Equipped = false;
        item.Equipped = true;
        _db.Ledger.Add(Entry(account.Id, item.Id, "equip", item.Slot.ToString(), 0, request.RequestId));
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
        Apply(account, inventory);
        if (run.Cleared) account.HighestStageCleared = target;

        _db.Ledger.Add(Entry(account.Id, null, "push", $"stage={target} seed={seed} cleared={run.Cleared} ticks={run.Ticks}", 0, request.RequestId));
        await SaveAsync(ct);
        return ToState(account, push: new PushResultDto(target, run.Cleared, seed, run.Ticks, account.HighestStageCleared, potionsAtStart, _bells.Active));
    }

    /// <summary>
    /// One Commander fight. The boss must be up (server-wide clock) and this account may fight it once per spawn.
    /// The fight is scored here with a seed the client replays; the chest follows the damage bracket.
    /// </summary>
    public async Task<StateDto> FightBossAsync(Account account, BossFightRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        BossDef boss = Content.Boss(request.BossId) ?? throw new GameException("no_boss", "Unknown Commander.");
        if (!Content.IsUnlocked(boss.ZoneId, account.HighestStageCleared))
            throw new GameException("stage_locked", "That Commander Ground is not unlocked.");

        DateTime now = DateTime.UtcNow;
        BossClock clock = await ClockAsync(boss, now, ct);
        if (now >= clock.SpawnUtc.AddSeconds(BossDef.WindowSeconds) || now < clock.SpawnUtc)
            throw new GameException("boss_down", boss.Name + " is not up.");

        string spawnKey = "boss:" + boss.Id + ":" + clock.SpawnUtc.Ticks;
        if (await _db.Ledger.AnyAsync(l => l.AccountId == account.Id && l.Kind == spawnKey, ct))
            throw new GameException("already_fought", "You already fought " + boss.Name + " this spawn.");

        ulong seed = BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(8));
        int potionsAtStart = account.Potions;
        Bell bell = _bells.Active;
        var inventory = Snapshot(account);
        BossRunResult run = BossRun.Simulate(boss, Hero(account), inventory, seed, bell);
        int rank = BossRun.Rank(run.Damage, boss, _rng);
        string chest = HuntYield.LootCommander(boss, rank, inventory, _rng);
        Apply(account, inventory);

        _db.Ledger.Add(Entry(account.Id, null, spawnKey, $"seed={seed} damage={run.Damage} killed={run.Killed} rank={rank} bell={bell} chest={chest}", 0, request.RequestId));
        await SaveAsync(ct);
        return ToState(account, bossFight: new BossFightResultDto(boss.Id, seed, run.Damage, run.Killed, rank, chest, potionsAtStart, bell));
    }

    /// <summary>Playtest only; disabled outside Development.</summary>
    public async Task<StateDto> DevGrantAsync(Account account, CancellationToken ct)
    {
        account.Sorn += 500_000;
        account.Materials += 10;
        account.ScrollsOfMercy += 5;
        account.KhansAlloys += 1;
        account.Turnstones += 20;
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
        while (now >= clock.SpawnUtc.AddSeconds(boss.RespawnSeconds))
            clock.SpawnUtc = clock.SpawnUtc.AddSeconds(boss.RespawnSeconds);
        return clock;
    }

    private static BossStatusDto[] BossStatuses(IReadOnlyDictionary<int, BossClock> clocks, ISet<int> foughtIds, DateTime now)
    {
        var list = new List<BossStatusDto>();
        foreach (BossDef boss in Content.Bosses)
        {
            if (!clocks.TryGetValue(boss.Id, out BossClock? clock)) continue;
            DateTime windowEnd = clock.SpawnUtc.AddSeconds(BossDef.WindowSeconds);
            bool up = now >= clock.SpawnUtc && now < windowEnd;
            long secondsLeft = up ? (long)(windowEnd - now).TotalSeconds : (long)(clock.SpawnUtc.AddSeconds(boss.RespawnSeconds) - now).TotalSeconds;
            list.Add(new BossStatusDto(boss.Id, boss.Name, boss.Mechanic.ToString(), up, Math.Max(0, secondsLeft), foughtIds.Contains(boss.Id)));
        }
        return list.ToArray();
    }

    private SettlementDto Settle(Account account, DateTime now, int onlineEfficiencyBp = RandomExtensions.FullBp, int loopsVerified = 0)
    {
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
        Apply(account, inventory);

        if (s.CountedSeconds > 0)
            _db.Ledger.Add(Entry(account.Id, null, offline ? "settle-offline" : "settle-online",
                $"stage={account.ParkedStage} seconds={s.CountedSeconds} packs={s.Packs} korstones={s.Korstones} efficiencyBp={efficiency} loopsVerified={loopsVerified}", s.SornEarned, Guid.NewGuid().ToString("N")));

        return new SettlementDto(s.CountedSeconds, s.Packs, s.Korstones, s.SornEarned, offline);
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
        HeroFactory.FromEquipment(a.Items.Where(i => i.Equipped && !i.Destroyed).Select(i => i.ToState()), Content.LevelFor(a.Xp), a.Class);

    private static int[] ParseShards(string s) => s.Split(';').Select(int.Parse).ToArray();
    private static string[] ParseSkins(string s) => s.Split(';', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Boss statuses need the clocks and this account's fights this spawn; loaded per request by the endpoints.</summary>
    public async Task<StateDto> WithBossesAsync(Account account, StateDto state, CancellationToken ct)
    {
        DateTime now = DateTime.UtcNow;
        var clocks = new Dictionary<int, BossClock>();
        var fought = new HashSet<int>();
        foreach (BossDef boss in Content.Bosses)
        {
            BossClock clock = await ClockAsync(boss, now, ct);
            clocks[boss.Id] = clock;
            string spawnKey = "boss:" + boss.Id + ":" + clock.SpawnUtc.Ticks;
            if (await _db.Ledger.AnyAsync(l => l.AccountId == account.Id && l.Kind == spawnKey, ct)) fought.Add(boss.Id);
        }
        await _db.SaveChangesAsync(ct);
        return state with { Bosses = BossStatuses(clocks, fought, now) };
    }

    private StateDto ToState(Account account, SettlementDto? settlement = null, ForgeResultDto? forge = null, PushResultDto? push = null, BossFightResultDto? bossFight = null, SocketResultDto? socket = null, TurnResultDto? turn = null)
    {
        Item weapon = account.Weapon;
        ItemState state = weapon.ToState();
        var forgeService = new ForgeService();
        bool maxed = state.UpgradeLevel >= ItemState.MaxUpgradeLevel;
        HeroStats hero = Hero(account);

        return new StateDto(
            account.Id,
            new InventoryDto(account.Sorn, account.Potions, account.Materials, account.ScrollsOfMercy, account.KhansAlloys, account.AnvilWards, account.Turnstones,
                account.EtchingNeedles, account.SummoningMarkers, account.Xp, Content.LevelFor(account.Xp), ParseShards(account.Korshards), ParseSkins(account.Skins)),
            ToDto(weapon),
            account.Items.Where(i => !i.Destroyed).OrderByDescending(i => i.Equipped).ThenByDescending(i => i.CreatedUtc).Select(ToDto).ToArray(),
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
            account.Class);
    }

    private static ItemDto ToDto(Item item)
    {
        ItemState s = item.ToState();
        EtchingPool pool = EtchingPool.For(s.Slot);
        return new ItemDto(item.Id, s.Slot, item.Equipped, s.DisplayName, s.ItemLevel, s.Rarity, s.UpgradeLevel, s.PatienceBp, s.LockedEtchingIndex,
            s.Etchings.Select(e => new EtchingDto(e.EntryId, pool.Entries[e.EntryId].Name, e.Tier, e.Value)).ToArray(),
            s.Sockets.Select(k => new SocketDto(k.Dead, k.Type?.ToString(), k.Rank,
                k.Dead ? "Dead Shard" : k.Type == null ? "empty" : SocketRules.Name(k.Type.Value) + " " + Content.KorshardRanks[k.Rank] + ": " + SocketRules.Describe(k.Type.Value, k.Rank))).ToArray());
    }

    private static Inventory Snapshot(Account a)
    {
        var inventory = new Inventory
        {
            Sorn = a.Sorn, Potions = a.Potions, Materials = a.Materials, ScrollsOfMercy = a.ScrollsOfMercy,
            KhansAlloys = a.KhansAlloys, AnvilWards = a.AnvilWards, Turnstones = a.Turnstones,
            EtchingNeedles = a.EtchingNeedles, SummoningMarkers = a.SummoningMarkers, Xp = a.Xp,
        };
        int[] shards = ParseShards(a.Korshards);
        Array.Copy(shards, inventory.Korshards, Math.Min(shards.Length, inventory.Korshards.Length));
        inventory.Skins.AddRange(ParseSkins(a.Skins));
        return inventory;
    }

    /// <summary>Writes settled currency back and turns dropped gear into item rows, trimming the loot list.</summary>
    private void Apply(Account a, Inventory i)
    {
        a.Sorn = i.Sorn; a.Potions = i.Potions; a.Materials = i.Materials; a.ScrollsOfMercy = i.ScrollsOfMercy;
        a.KhansAlloys = i.KhansAlloys; a.AnvilWards = i.AnvilWards; a.Turnstones = i.Turnstones;
        a.EtchingNeedles = i.EtchingNeedles; a.SummoningMarkers = i.SummoningMarkers; a.Xp = i.Xp;
        a.Korshards = string.Join(';', i.Korshards);
        a.Skins = string.Join(';', i.Skins);

        if (i.Loot.Count == 0) return;

        // Keep the best MaxLoot loose pieces: new drops compete with what is already stored by rarity, then age.
        // Pieces the player has forged up are never pushed out (bag items can be forged since 24 Sep 2026).
        var stored = a.Items.Where(x => !x.Equipped && !x.Destroyed).ToList();
        var keptDrops = new List<ItemState>();
        var candidates = stored.Select(x => (Rarity: x.Rarity, Worked: x.UpgradeLevel > 0 || x.PatienceBp > 0, Stored: (Item?)x, Drop: (ItemState?)null))
            .Concat(i.Loot.Select(d => (Rarity: d.Rarity, Worked: false, Stored: (Item?)null, Drop: (ItemState?)d)))
            .OrderByDescending(c => c.Worked).ThenByDescending(c => (int)c.Rarity).ThenBy(c => c.Stored == null ? 0 : 1)
            .ToList();

        foreach (var c in candidates.Take(MaxLoot))
            if (c.Drop != null) keptDrops.Add(c.Drop);
        foreach (var c in candidates.Skip(MaxLoot))
            if (c.Stored != null) { _db.Items.Remove(c.Stored); a.Items.Remove(c.Stored); }

        foreach (ItemState drop in keptDrops) a.Items.Add(Item.From(drop, a.Id, equipped: false));
        i.Loot.Clear();
    }

    private static LedgerEntry Entry(Guid accountId, Guid? itemId, string kind, string detail, long sornDelta, string requestId) => new()
    {
        AccountId = accountId, ItemId = itemId, Kind = kind, Detail = detail, SornDelta = sornDelta, RequestId = requestId, Utc = DateTime.UtcNow,
    };

    private static string NewToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
