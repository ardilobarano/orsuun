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
    private readonly ForgeService _forge = new();
    private readonly EtchingService _etchings = new();

    public GameService(GameDb db, IRandom rng)
    {
        _db = db;
        _rng = rng;
    }

    public async Task<GuestLoginResponse> GuestLoginAsync(string deviceToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(deviceToken) || deviceToken.Length > 128)
            throw new GameException("bad_device_token", "Device token missing or too long.");

        Account? account = await _db.Accounts.SingleOrDefaultAsync(a => a.DeviceToken == deviceToken, ct);
        bool created = account == null;
        if (account == null)
        {
            DateTime now = DateTime.UtcNow;
            account = new Account
            {
                Id = Guid.NewGuid(),
                DeviceToken = deviceToken,
                CreatedUtc = now,
                LastHeartbeatUtc = now,
                Sorn = 20_000,
                Potions = 30,
                ScrollsOfMercy = 2,
                Turnstones = 5,
            };
            account.Items.Add(Item.From(NewWeapon(), account.Id, equipped: true));
            _db.Accounts.Add(account);
            _db.Ledger.Add(Entry(account.Id, null, "account-created", "starter kit", account.Sorn, Guid.NewGuid().ToString("N")));
        }

        account.SessionToken = NewToken();
        await _db.SaveChangesAsync(ct);
        return new GuestLoginResponse(account.Id, account.SessionToken, created);
    }

    public async Task<Account?> AuthenticateAsync(string? sessionToken, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(sessionToken)) return null;
        return await _db.Accounts.SingleOrDefaultAsync(a => a.SessionToken == sessionToken, ct);
    }

    /// <summary>Credits hunting time since the last heartbeat and moves the heartbeat forward.</summary>
    public async Task<StateDto> HeartbeatAsync(Account account, CancellationToken ct)
    {
        DateTime now = DateTime.UtcNow;
        SettlementDto settlement = Settle(account, now);
        account.LastHeartbeatUtc = now;
        await SaveAsync(ct);
        return ToState(account, settlement: settlement);
    }

    public StateDto GetState(Account account) => ToState(account);

    public async Task<StateDto> ForgeAsync(Account account, ForgeRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        if (request.Method == ForgeMethod.ChainedSmith || request.Method == ForgeMethod.AnvilWard && account.AnvilWards <= 0)
            throw new GameException("method_unavailable", "That method is not available here.");

        Item weapon = account.Weapon;
        ItemState state = weapon.ToState();
        if (state.UpgradeLevel >= ItemState.MaxUpgradeLevel) throw new GameException("already_max", "The blade is already +9.");

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
        weapon.ApplyState(state);
        if (result.Outcome == ForgeOutcome.Oathbreak)
        {
            weapon.Equipped = false;
            account.WeaponsBroken++;
            account.Items.Add(Item.From(NewWeapon(), account.Id, equipped: true));
        }

        _db.Ledger.Add(Entry(account.Id, weapon.Id, "forge",
            $"{request.Method} +{result.LevelBefore}->+{result.LevelAfter} chance={result.ChanceBp} outcome={result.Outcome}", -cost, request.RequestId));
        await SaveAsync(ct);
        return ToState(account, forge: new ForgeResultDto(result.Outcome, result.ChanceBp, result.LevelBefore, result.LevelAfter));
    }

    public async Task<StateDto> TurnAsync(Account account, TurnRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        Item weapon = account.Weapon;
        ItemState state = weapon.ToState();
        int cost = state.LockedEtchingIndex >= 0 ? 2 : 1;
        if (account.Turnstones < cost) throw new GameException("no_turnstones", "Not enough Turnstones.");

        account.Turnstones -= _etchings.Turn(state, EtchingPool.For(state.Slot), _rng);
        weapon.ApplyState(state);
        _db.Ledger.Add(Entry(account.Id, weapon.Id, "turn", "etchings=" + weapon.Etchings, 0, request.RequestId));
        await SaveAsync(ct);
        return ToState(account);
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
        if (request.Stage < 1 || request.Stage > Content.TotalStages || request.Stage > account.HighestStageCleared + 1)
            throw new GameException("stage_locked", "That stage is not unlocked.");
        // Settle the time spent on the old stage before the rate changes.
        DateTime now = DateTime.UtcNow;
        SettlementDto settlement = Settle(account, now);
        account.LastHeartbeatUtc = now;
        account.ParkedStage = request.Stage;
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
        StageRunResult run = StageRun.Simulate(Content.Stage(target), Hero(account), inventory, seed);
        Apply(account, inventory);
        if (run.Cleared) account.HighestStageCleared = target;

        _db.Ledger.Add(Entry(account.Id, null, "push", $"stage={target} seed={seed} cleared={run.Cleared} ticks={run.Ticks}", 0, request.RequestId));
        await SaveAsync(ct);
        return ToState(account, push: new PushResultDto(target, run.Cleared, seed, run.Ticks, account.HighestStageCleared, potionsAtStart));
    }

    /// <summary>Playtest only; disabled outside Development.</summary>
    public async Task<StateDto> DevGrantAsync(Account account, CancellationToken ct)
    {
        account.Sorn += 500_000;
        account.Materials += 10;
        account.ScrollsOfMercy += 5;
        account.KhansAlloys += 1;
        account.Turnstones += 20;
        _db.Ledger.Add(Entry(account.Id, null, "dev-grant", "playtest grant", 500_000, Guid.NewGuid().ToString("N")));
        await SaveAsync(ct);
        return ToState(account);
    }

    private SettlementDto Settle(Account account, DateTime now)
    {
        TimeSpan gap = now - account.LastHeartbeatUtc;
        bool offline = gap > OnlineGrace;
        long seconds = (long)gap.TotalSeconds;
        long cap = offline ? OfflineRewards.FreeCapSeconds : (long)OnlineGrace.TotalSeconds;
        int efficiency = offline ? OfflineRewards.OfflineEfficiencyBp : RandomExtensions.FullBp;

        StageConfig stage = Content.Stage(account.ParkedStage);
        stage.GearRarityCap = offline ? Rarity.Rare : Rarity.Epic;
        var inventory = Snapshot(account);
        HuntSettlement s = HuntYield.Settle(stage, Hero(account), seconds, cap, efficiency, inventory, _rng);
        Apply(account, inventory);

        if (s.CountedSeconds > 0)
            _db.Ledger.Add(Entry(account.Id, null, offline ? "settle-offline" : "settle-online",
                $"stage={account.ParkedStage} seconds={s.CountedSeconds} packs={s.Packs} korstones={s.Korstones}", s.SornEarned, Guid.NewGuid().ToString("N")));

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

    private ItemState NewWeapon()
    {
        var state = new ItemState(PlayerSession.StarterItemLevel, Rarity.Rare);
        EtchingPool pool = EtchingPool.For(EquipSlot.Weapon);
        while (state.Etchings.Count < ItemState.MaxEtchings)
        {
            NeedleKind needle = state.Etchings.Count == ItemState.MaxEtchings - 1 ? NeedleKind.MastersNeedle : NeedleKind.EtchingNeedle;
            _etchings.TryAdd(state, pool, needle, _rng);
        }
        return state;
    }

    private static HeroStats Hero(Account a) => HeroFactory.FromEquipment(a.Items.Where(i => i.Equipped && !i.Destroyed).Select(i => i.ToState()));

    private static StateDto ToState(Account account, SettlementDto? settlement = null, ForgeResultDto? forge = null, PushResultDto? push = null)
    {
        Item weapon = account.Weapon;
        ItemState state = weapon.ToState();
        var forgeService = new ForgeService();
        bool maxed = state.UpgradeLevel >= ItemState.MaxUpgradeLevel;
        HeroStats hero = Hero(account);

        return new StateDto(
            account.Id,
            new InventoryDto(account.Sorn, account.Potions, account.Materials, account.ScrollsOfMercy, account.KhansAlloys, account.AnvilWards, account.Turnstones),
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
            DateTime.UtcNow,
            settlement,
            forge,
            push);
    }

    private static ItemDto ToDto(Item item)
    {
        ItemState s = item.ToState();
        EtchingPool pool = EtchingPool.For(s.Slot);
        return new ItemDto(item.Id, s.Slot, item.Equipped, s.DisplayName, s.ItemLevel, s.Rarity, s.UpgradeLevel, s.PatienceBp, s.LockedEtchingIndex,
            s.Etchings.Select(e => new EtchingDto(e.EntryId, pool.Entries[e.EntryId].Name, e.Tier, e.Value)).ToArray());
    }

    private static Inventory Snapshot(Account a) => new()
    {
        Sorn = a.Sorn, Potions = a.Potions, Materials = a.Materials, ScrollsOfMercy = a.ScrollsOfMercy,
        KhansAlloys = a.KhansAlloys, AnvilWards = a.AnvilWards, Turnstones = a.Turnstones,
    };

    /// <summary>Writes settled currency back and turns dropped gear into item rows, trimming the loot list.</summary>
    private void Apply(Account a, Inventory i)
    {
        a.Sorn = i.Sorn; a.Potions = i.Potions; a.Materials = i.Materials; a.ScrollsOfMercy = i.ScrollsOfMercy;
        a.KhansAlloys = i.KhansAlloys; a.AnvilWards = i.AnvilWards; a.Turnstones = i.Turnstones;

        if (i.Loot.Count == 0) return;

        // Keep the best MaxLoot loose pieces: new drops compete with what is already stored by rarity, then age.
        var stored = a.Items.Where(x => !x.Equipped && !x.Destroyed).ToList();
        var keptDrops = new List<ItemState>();
        var candidates = stored.Select(x => (Rarity: x.Rarity, Stored: (Item?)x, Drop: (ItemState?)null))
            .Concat(i.Loot.Select(d => (Rarity: d.Rarity, Stored: (Item?)null, Drop: (ItemState?)d)))
            .OrderByDescending(c => (int)c.Rarity).ThenBy(c => c.Stored == null ? 0 : 1)
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
