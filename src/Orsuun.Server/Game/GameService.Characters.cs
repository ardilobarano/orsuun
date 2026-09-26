using Microsoft.EntityFrameworkCore;
using Orsuun.Rules;
using Orsuun.Rules.Combat;
using Orsuun.Server.Data;

namespace Orsuun.Server.Game;

/// <summary>
/// Characters (owner, 25 Sep 2026: "character creation with name selection after signing up or logging in like metin2,
/// total 4 char slots with a common depot of items to trade between each other"; Amber and the Banner are the
/// account's). The character screen lists a login's characters, makes new ones with a chosen name and class, chooses
/// one for this device and deletes one after its name is typed. The depot holds 40 pieces any of them can take.
/// </summary>
public sealed partial class GameService
{
    /// <summary>The login of this request (loaded by AuthenticateAsync / AuthenticateLoginAsync).</summary>
    private Login? _login;

    /// <summary>A character's shown name: the chosen one, or for rows made before names the generated one.</summary>
    private static string NameOf(Account a) => ShownName(a.Id, a.Name);

    /// <summary>For rows read without the whole account (id and Name columns).</summary>
    private static string ShownName(Guid id, string? name) => string.IsNullOrEmpty(name) ? Banners.GeneratedName(id) : name;

    private async Task<string> NameOfAsync(Guid accountId, CancellationToken ct)
    {
        string? name = await _db.Accounts.AsNoTracking().Where(a => a.Id == accountId).Select(a => a.Name).FirstOrDefaultAsync(ct);
        return string.IsNullOrEmpty(name) ? Banners.GeneratedName(accountId) : name;
    }

    public async Task<LobbyDto> LobbyAsync(Login login, string message, CancellationToken ct)
    {
        List<Account> characters = await _db.Accounts.Where(a => a.LoginId == login.Id).OrderBy(a => a.Slot).ToListAsync(ct);
        var guildIds = characters.Where(a => a.GuildId != null).Select(a => a.GuildId!.Value).Distinct().ToList();
        var tags = await _db.Guilds.AsNoTracking().Where(g => guildIds.Contains(g.Id)).Select(g => new { g.Id, g.Tag }).ToListAsync(ct);
        CharacterSlotDto[] slots = characters.Select(a =>
        {
            ItemState? armor = a.Items.Where(i => i.Equipped && !i.Destroyed && i.Slot == EquipSlot.Armor).Select(i => i.ToState()).FirstOrDefault();
            ItemState? weapon = a.Items.Where(i => i.Equipped && !i.Destroyed && i.Slot == EquipSlot.Weapon).Select(i => i.ToState()).FirstOrDefault();
            string skin = WornPieces(a).FirstOrDefault(p => p.Kind == WardrobeKind.Skin)?.Look ?? "";
            string tag = tags.Where(t => t.Id == a.GuildId).Select(t => t.Tag).FirstOrDefault() ?? "";
            return new CharacterSlotDto(a.Id, a.Slot, NameOf(a), a.Class, Content.LevelFor(a.Xp), armor != null ? ItemLooks.Tier(armor.ItemLevel) : 0,
                weapon != null ? ItemLooks.Tier(weapon.ItemLevel) : 0, weapon?.UpgradeLevel ?? 0, skin, a.HighestStageCleared, a.LastHeartbeatUtc, tag, a.BannedUtc != null);
        }).ToArray();
        int links = await _db.ExternalLogins.CountAsync(l => l.LoginId == login.Id, ct);
        return new LobbyDto(login.Id, slots, Characters.MaxSlots, login.Banner, login.Amber, login.Email, message, links);
    }

    public async Task<LobbyDto> CreateCharacterAsync(Login login, CreateCharacterRequest request, CancellationToken ct)
    {
        string name = (request.Name ?? "").Trim();
        if (Characters.NameProblem(name) is string problem) throw new GameException("bad_name", problem);
        if (!Enum.TryParse(request.HeroClass, out HeroClass cls) || !Enum.IsDefined(cls)) throw new GameException("bad_class", "Choose a class.");
        var taken = await _db.Accounts.Where(a => a.LoginId == login.Id).Select(a => a.Slot).ToListAsync(ct);
        if (taken.Count >= Characters.MaxSlots) throw new GameException("slots_full", $"All {Characters.MaxSlots} slots are taken.");
        int slot = request.Slot >= 0 && request.Slot < Characters.MaxSlots && !taken.Contains(request.Slot)
            ? request.Slot : Enumerable.Range(0, Characters.MaxSlots).First(s => !taken.Contains(s));
        string key = Characters.NameKey(name);
        if (await _db.Accounts.AnyAsync(a => a.NameKey == key, ct)) throw new GameException("name_taken", "That name is taken.");
        NewCharacter(login, name, cls, slot);
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { throw new GameException("name_taken", "That name is taken."); }
        return await LobbyAsync(login, $"{name} steps onto the steppe.", ct);
    }

    /// <summary>Plays a character on this device.</summary>
    public async Task<StateDto> SelectCharacterAsync(Login login, Device device, CharacterRequest request, CancellationToken ct)
    {
        Account account = await _db.Accounts.SingleOrDefaultAsync(a => a.Id == request.CharacterId && a.LoginId == login.Id, ct)
            ?? throw new GameException("no_character", "No such character.");
        ThrowIfBanned(account);
        if (account.LaneSeed == 0) NewLane(account);
        // Coming back from the character screen counts as a return: time away settles like offline time.
        device.AccountId = account.Id;
        device.LastSeenUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        if (account.GuildId is Guid guildId) _guild = await _db.Guilds.FindAsync(new object[] { guildId }, ct);
        await LoadLoginsAsync(account, ct);
        return ToState(account);
    }

    /// <summary>Deletes one character after its name is typed (the login, its email and links stay).</summary>
    public async Task<LobbyDto> DeleteCharacterAsync(Login login, CharacterRequest request, CancellationToken ct)
    {
        Account account = await _db.Accounts.SingleOrDefaultAsync(a => a.Id == request.CharacterId && a.LoginId == login.Id, ct)
            ?? throw new GameException("no_character", "No such character.");
        string name = NameOf(account);
        if (!string.Equals((request.Name ?? "").Trim(), name, StringComparison.OrdinalIgnoreCase))
            throw new GameException("confirm_name", $"Type {name} to delete it.");
        // Depot pieces it put in stay in the depot: another character of the login holds them (with the last one they go).
        Account? heir = await _db.Accounts.Where(a => a.LoginId == login.Id && a.Id != account.Id).OrderBy(a => a.Slot).FirstOrDefaultAsync(ct);
        if (heir != null)
            await _db.Items.Where(i => i.OwnerId == account.Id && i.DepotLoginId == login.Id).ExecuteUpdateAsync(s => s.SetProperty(i => i.OwnerId, heir.Id), ct);
        await _db.Devices.Where(d => d.AccountId == account.Id).ExecuteUpdateAsync(s => s.SetProperty(d => d.AccountId, Guid.Empty), ct);
        await DeleteCharacterCoreAsync(account, ct);
        _db.ChangeTracker.Clear();
        return await LobbyAsync(login, $"{name} is gone.", ct);
    }

    /// <summary>MENU's DELETE ACCOUNT: every character of the login, its devices and the login itself.</summary>
    public async Task DeleteAccountAsync(Account account, CancellationToken ct)
    {
        Guid loginId = account.LoginId;
        var ids = await _db.Accounts.Where(a => a.LoginId == loginId).Select(a => a.Id).ToListAsync(ct);
        await DeleteCharacterCoreAsync(account, ct);
        foreach (Guid id in ids.Where(i => i != account.Id))
        {
            _db.ChangeTracker.Clear();
            Account? other = await _db.Accounts.SingleOrDefaultAsync(a => a.Id == id, ct);
            if (other != null) await DeleteCharacterCoreAsync(other, ct);
        }
        await _db.Items.Where(i => i.DepotLoginId == loginId).ExecuteDeleteAsync(ct);
        await _db.ExternalLogins.Where(l => l.LoginId == loginId).ExecuteDeleteAsync(ct);
        await _db.Devices.Where(d => d.LoginId == loginId).ExecuteDeleteAsync(ct);
        await _db.Logins.Where(l => l.Id == loginId).ExecuteDeleteAsync(ct);
    }

    // ---- the depot ----

    public async Task<DepotDto> DepotAsync(Account account, string message, CancellationToken ct)
    {
        List<Item> items = await _db.Items.AsNoTracking().Where(i => i.DepotLoginId == account.LoginId && !i.Destroyed).OrderBy(i => i.Slot).ThenByDescending(i => i.UpgradeLevel).ToListAsync(ct);
        return new DepotDto(ToState(account), items.Select(ToDto).ToArray(), Characters.DepotSlots, message);
    }

    /// <summary>A bag piece into the depot (worn, listed or broken pieces cannot go).</summary>
    public async Task<DepotDto> DepotPutAsync(Account account, DepotRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        Item item = account.Items.SingleOrDefault(i => i.Id == request.ItemId && !i.Destroyed && !i.OutOfBag)
            ?? throw new GameException("no_item", "That piece is not in your bag.");
        if (item.Equipped) throw new GameException("worn", "Take it off first.");
        int held = await _db.Items.CountAsync(i => i.DepotLoginId == account.LoginId && !i.Destroyed, ct);
        if (held >= Characters.DepotSlots) throw new GameException("depot_full", $"The depot holds {Characters.DepotSlots} pieces.");
        item.DepotLoginId = account.LoginId;
        _db.Ledger.Add(Entry(account.Id, item.Id, "depot-put", Content.ItemName(item.ToState(), account.Class), 0, request.RequestId));
        await SaveAsync(ct);
        return await DepotAsync(account, Content.ItemName(item.ToState(), account.Class) + " is in the depot.", ct);
    }

    /// <summary>A depot piece into this character's bag; it becomes this character's.</summary>
    public async Task<DepotDto> DepotTakeAsync(Account account, DepotRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        if (account.Items.Count(i => !i.Equipped && !i.Destroyed && !i.OutOfBag) >= MaxLoot) throw new GameException("bag_full", "Your bag is full.");
        // One UPDATE guarded on the depot, so two characters on two phones cannot both take the piece.
        int moved = await _db.Items.Where(i => i.Id == request.ItemId && i.DepotLoginId == account.LoginId && !i.Destroyed)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.OwnerId, account.Id).SetProperty(i => i.DepotLoginId, (Guid?)null), ct);
        if (moved == 0) throw new GameException("no_item", "That piece has left the depot.");
        Item? mine = account.Items.SingleOrDefault(i => i.Id == request.ItemId);
        if (mine != null)
        {
            mine.DepotLoginId = null;
            _db.Entry(mine).Property(i => i.DepotLoginId).IsModified = false;   // already written
        }
        else
        {
            // Loading the tracked row lets EF's fix-up put it in account.Items (its OwnerId is now this character).
            Item loaded = await _db.Items.SingleAsync(i => i.Id == request.ItemId, ct);
            if (!account.Items.Contains(loaded)) account.Items.Add(loaded);
        }
        Item taken = account.Items.Single(i => i.Id == request.ItemId);
        _db.Ledger.Add(Entry(account.Id, taken.Id, "depot-take", Content.ItemName(taken.ToState(), account.Class), 0, request.RequestId));
        await SaveAsync(ct);
        return await DepotAsync(account, Content.ItemName(taken.ToState(), account.Class) + " is in your bag.", ct);
    }

    /// <summary>
    /// Characters made before names (25 Sep 2026) take their generated name once at startup, so every row has one. A
    /// generated name has a space, which a chosen one never has, so the two never collide.
    /// </summary>
    public static async Task BackfillNamesAsync(GameDb db, CancellationToken ct)
    {
        List<Account> unnamed = await db.Accounts.Where(a => a.Name == "").ToListAsync(ct);
        if (unnamed.Count == 0) return;
        var keys = new HashSet<string>(await db.Accounts.Where(a => a.NameKey != "").Select(a => a.NameKey).ToListAsync(ct));
        foreach (Account a in unnamed)
        {
            string name = Banners.GeneratedName(a.Id);
            for (int n = 2; keys.Contains(Characters.NameKey(name)); n++) name = Banners.GeneratedName(a.Id) + " " + n;
            a.Name = name;
            a.NameKey = Characters.NameKey(name);
            keys.Add(a.NameKey);
        }
        await db.SaveChangesAsync(ct);
    }
}
