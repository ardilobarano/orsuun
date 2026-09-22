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

    private static readonly StageConfig Stage = new();
    private static readonly EtchingPool Pool = EtchingPool.Weapon();

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
            _db.Accounts.Add(account);
            Item weapon = NewWeapon(account.Id);
            _db.Items.Add(weapon);
            account.EquippedWeapon = weapon;
            _db.Ledger.Add(Entry(account.Id, null, "account-created", "starter kit", account.Sorn, Guid.NewGuid().ToString("N")));
        }

        account.SessionToken = NewToken();
        await _db.SaveChangesAsync(ct);
        return new GuestLoginResponse(account.Id, account.SessionToken, created);
    }

    public async Task<Account?> AuthenticateAsync(string? sessionToken, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(sessionToken)) return null;
        return await _db.Accounts.Include(a => a.EquippedWeapon).SingleOrDefaultAsync(a => a.SessionToken == sessionToken, ct);
    }

    /// <summary>Credits hunting time since the last heartbeat and moves the heartbeat forward.</summary>
    public async Task<StateDto> HeartbeatAsync(Account account, CancellationToken ct)
    {
        DateTime now = DateTime.UtcNow;
        SettlementDto settlement = Settle(account, now);
        account.LastHeartbeatUtc = now;
        await _db.SaveChangesAsync(ct);
        return ToState(account, settlement, null);
    }

    public StateDto GetState(Account account) => ToState(account, null, null);

    public async Task<StateDto> ForgeAsync(Account account, ForgeRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        if (request.Method == ForgeMethod.ChainedSmith || request.Method == ForgeMethod.AnvilWard && account.AnvilWards <= 0)
            throw new GameException("method_unavailable", "That method is not available here.");

        Item weapon = account.EquippedWeapon ?? throw new GameException("no_weapon", "No weapon equipped.");
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
            account.WeaponsBroken++;
            Item fresh = NewWeapon(account.Id);
            _db.Items.Add(fresh);
            account.EquippedWeapon = fresh;
        }

        _db.Ledger.Add(Entry(account.Id, weapon.Id, "forge",
            $"{request.Method} +{result.LevelBefore}->+{result.LevelAfter} chance={result.ChanceBp} outcome={result.Outcome}", -cost, request.RequestId));
        await SaveAsync(ct);
        return ToState(account, null, new ForgeResultDto(result.Outcome, result.ChanceBp, result.LevelBefore, result.LevelAfter));
    }

    public async Task<StateDto> TurnAsync(Account account, TurnRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        Item weapon = account.EquippedWeapon ?? throw new GameException("no_weapon", "No weapon equipped.");
        ItemState state = weapon.ToState();
        int cost = state.LockedEtchingIndex >= 0 ? 2 : 1;
        if (account.Turnstones < cost) throw new GameException("no_turnstones", "Not enough Turnstones.");

        account.Turnstones -= _etchings.Turn(state, Pool, _rng);
        weapon.ApplyState(state);
        _db.Ledger.Add(Entry(account.Id, weapon.Id, "turn", "etchings=" + weapon.Etchings, 0, request.RequestId));
        await SaveAsync(ct);
        return ToState(account, null, null);
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
        return ToState(account, null, null);
    }

    private SettlementDto Settle(Account account, DateTime now)
    {
        TimeSpan gap = now - account.LastHeartbeatUtc;
        bool offline = gap > OnlineGrace;
        long seconds = (long)gap.TotalSeconds;
        long cap = offline ? OfflineRewards.FreeCapSeconds : (long)OnlineGrace.TotalSeconds;
        int efficiency = offline ? OfflineRewards.OfflineEfficiencyBp : RandomExtensions.FullBp;

        var inventory = Snapshot(account);
        HeroStats hero = HeroFactory.FromWeapon(account.EquippedWeapon!.ToState());
        HuntSettlement s = HuntYield.Settle(Stage, hero, seconds, cap, efficiency, inventory, _rng);
        Apply(account, inventory);

        if (s.CountedSeconds > 0)
            _db.Ledger.Add(Entry(account.Id, null, offline ? "settle-offline" : "settle-online",
                $"seconds={s.CountedSeconds} packs={s.Packs} korstones={s.Korstones}", s.SornEarned, Guid.NewGuid().ToString("N")));

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
        catch (DbUpdateConcurrencyException)
        {
            throw new GameException("conflict", "Another request changed this account. Refresh and retry.");
        }
    }

    private Item NewWeapon(Guid ownerId)
    {
        var state = new ItemState(PlayerSession.StarterItemLevel, Rarity.Rare);
        while (state.Etchings.Count < ItemState.MaxEtchings)
        {
            NeedleKind needle = state.Etchings.Count == ItemState.MaxEtchings - 1 ? NeedleKind.MastersNeedle : NeedleKind.EtchingNeedle;
            _etchings.TryAdd(state, Pool, needle, _rng);
        }
        var item = new Item { Id = Guid.NewGuid(), OwnerId = ownerId, ItemLevel = state.ItemLevel, Rarity = state.Rarity, CreatedUtc = DateTime.UtcNow };
        item.ApplyState(state);
        return item;
    }

    private static StateDto ToState(Account account, SettlementDto? settlement, ForgeResultDto? forge)
    {
        Item weapon = account.EquippedWeapon!;
        ItemState state = weapon.ToState();
        var forgeService = new ForgeService();
        bool maxed = state.UpgradeLevel >= ItemState.MaxUpgradeLevel;

        return new StateDto(
            account.Id,
            new InventoryDto(account.Sorn, account.Potions, account.Materials, account.ScrollsOfMercy, account.KhansAlloys, account.AnvilWards, account.Turnstones),
            new WeaponDto(weapon.Id, state.ItemLevel, state.Rarity, state.UpgradeLevel, state.PatienceBp, state.LockedEtchingIndex,
                state.Etchings.Select(e => new EtchingDto(e.EntryId, Pool.Entries[e.EntryId].Name, e.Tier, e.Value)).ToArray()),
            new ForgePreviewDto(
                maxed ? 0 : ForgeRules.Cost(state.ItemLevel, state.UpgradeLevel),
                maxed ? 0 : ForgeRules.MaterialsNeeded(state.UpgradeLevel + 1),
                maxed ? 0 : forgeService.ChanceBp(state, ForgeMethod.ForgeAlone),
                maxed ? 0 : forgeService.ChanceBp(state, ForgeMethod.KhansAlloy),
                !maxed && state.UpgradeLevel + 1 >= ForgeRules.FirstOathbreakTarget),
            account.WeaponsBroken,
            DateTime.UtcNow,
            settlement,
            forge);
    }

    private static Inventory Snapshot(Account a) => new()
    {
        Sorn = a.Sorn, Potions = a.Potions, Materials = a.Materials, ScrollsOfMercy = a.ScrollsOfMercy,
        KhansAlloys = a.KhansAlloys, AnvilWards = a.AnvilWards, Turnstones = a.Turnstones,
    };

    private static void Apply(Account a, Inventory i)
    {
        a.Sorn = i.Sorn; a.Potions = i.Potions; a.Materials = i.Materials; a.ScrollsOfMercy = i.ScrollsOfMercy;
        a.KhansAlloys = i.KhansAlloys; a.AnvilWards = i.AnvilWards; a.Turnstones = i.Turnstones;
    }

    private static LedgerEntry Entry(Guid accountId, Guid? itemId, string kind, string detail, long sornDelta, string requestId) => new()
    {
        AccountId = accountId, ItemId = itemId, Kind = kind, Detail = detail, SornDelta = sornDelta, RequestId = requestId, Utc = DateTime.UtcNow,
    };

    private static string NewToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
