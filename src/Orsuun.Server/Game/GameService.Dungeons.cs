using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Orsuun.Rules;
using Orsuun.Rules.Combat;
using Orsuun.Server.Data;

namespace Orsuun.Server.Game;

/// <summary>
/// Dungeons (owner, 25 Sep 2026: "dungeons"; Rules.Dungeons): two free runs a bounty day. Entering fights the floors up
/// to the Chained Smith and stops there; the smith's answer (a forge at +10 points, or walking on) fights the rest and
/// pays the Warden's chest. Every floor is scored here with its own seed; the client replays them in order.
/// </summary>
public sealed partial class GameService
{
    private int DungeonRunsLeft(Account account) =>
        Math.Max(0, Dungeons.FreeRunsPerDay - (account.DungeonDay == Rules.Bounties.DayKey(_bells.LocalNow) ? account.DungeonRuns : 0));

    /// <summary>Fights floors from..to in order (the smith's floor is skipped) and stops at the first fall.</summary>
    private List<DungeonFloorDto> FightFloors(Account account, DungeonDef dungeon, int level, int from, int to, out int fellOn)
    {
        var floors = new List<DungeonFloorDto>();
        fellOn = 0;
        HeroStats hero = Hero(account);
        var inventory = Snapshot(account);
        for (int floor = from; floor <= to; floor++)
        {
            if (!Dungeons.Fought(dungeon, floor)) continue;
            ulong seed = BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(8));
            int potions = inventory.Potions;
            StageRunResult run = StageRun.Simulate(Dungeons.Floor(dungeon, floor, level), hero, inventory, seed);
            floors.Add(new DungeonFloorDto(floor, seed, potions, run.Cleared));
            if (!run.Cleared)
            {
                fellOn = floor;
                break;
            }
        }
        Apply(account, inventory, hunt: true);
        return floors;
    }

    public async Task<DungeonResultDto> EnterDungeonAsync(Account account, DungeonEnterRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        DungeonDef dungeon = Dungeons.Find(request.DungeonId) ?? throw new GameException("no_dungeon", "Unknown dungeon.");
        if (account.HighestStageCleared < dungeon.UnlockStage)
            throw new GameException("stage_locked", $"Clear {Content.StageName(dungeon.UnlockStage)} first.");
        if (account.DungeonRunAtSmith != 0)
            throw new GameException("run_open", (Dungeons.Find(account.DungeonPausedId)?.Pause == DungeonPause.RuneLock ? "A rune lock" : "The Chained Smith")
                + " is still waiting for your last run.");
        if (DungeonRunsLeft(account) <= 0) throw new GameException("no_keys", "Today's dungeon keys are used. New ones come at 20:00.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        string day = Rules.Bounties.DayKey(_bells.LocalNow);
        account.DungeonRuns = (account.DungeonDay == day ? account.DungeonRuns : 0) + 1;
        account.DungeonDay = day;
        int level = Dungeons.Level(dungeon, account.HighestStageCleared);
        int until = dungeon.SmithFloor > 0 ? dungeon.SmithFloor - 1 : dungeon.Floors;
        List<DungeonFloorDto> floors = FightFloors(account, dungeon, level, 1, until, out int fellOn);
        var run = new DungeonRun { AccountId = account.Id, DungeonId = dungeon.Id, Level = level, StartedUtc = DateTime.UtcNow, State = 1 };
        run.FloorsCleared = fellOn > 0 ? fellOn - 1 : until;
        _db.DungeonRuns.Add(run);

        bool atSmith = fellOn == 0 && dungeon.Pause != DungeonPause.None;
        string chest = "";
        string text;
        if (fellOn > 0) text = $"You fell on {Dungeons.FloorName(dungeon, fellOn).ToLowerInvariant()} of {dungeon.Name}.";
        else if (atSmith)
        {
            run.State = 0;
            text = dungeon.Pause == DungeonPause.RuneLock
                ? $"Floor {dungeon.SmithFloor}: a vault door, sealed by a rune lock with a riddle carved in it."
                : $"Floor {dungeon.SmithFloor}: the Chained Smith waits, hammer in his chained hands.";
        }
        else
        {
            chest = Chest(account, level, dungeon, false);
            text = $"{dungeon.Name} is cleared! {dungeon.WardenName}'s chest: {chest}.";
        }
        await SaveAsync(ct);   // the run's id
        if (atSmith)
        {
            account.DungeonRunAtSmith = run.Id;
            account.DungeonPausedId = dungeon.Id;
        }
        _db.Ledger.Add(Entry(account.Id, null, "dungeon", $"run={run.Id} dungeon={dungeon.Id} level={level} floors={run.FloorsCleared} fell={fellOn} smith={atSmith}", 0, request.RequestId));
        await SaveAsync(ct);
        await tx.CommitAsync(ct);
        return WithPause(new DungeonResultDto(ToState(account), run.Id, dungeon.Id, level, floors.ToArray(), atSmith, fellOn == 0 && !atSmith, fellOn, chest, null, "", text),
            atSmith ? dungeon : null);
    }

    /// <summary>
    /// Development: a full worn set of Rare pieces at an item level and forge level (and the hero's XP to that level), so
    /// late dungeons and maps can be tried without farming there. The pieces worn before go to the bag.
    /// </summary>
    public async Task<StateDto> DevGearAsync(Account account, int itemLevel, int upgrade, CancellationToken ct)
    {
        itemLevel = Math.Clamp(itemLevel, 1, Content.MaxLevel);
        upgrade = Math.Clamp(upgrade, 0, ItemState.MaxUpgradeLevel);
        foreach (Item worn in account.Items.Where(i => i.Equipped && !i.Destroyed)) worn.Equipped = false;
        for (int s = 0; s < 8; s++)
        {
            var state = new ItemState(itemLevel, Rarity.Rare, (EquipSlot)s) { UpgradeLevel = upgrade };
            account.Items.Add(Item.From(state, account.Id, equipped: true));
        }
        account.Xp = Math.Max(account.Xp, Content.XpForLevel(itemLevel));
        _db.Ledger.Add(Entry(account.Id, null, "dev-gear", $"level={itemLevel} +{upgrade}", 0, Guid.NewGuid().ToString("N")));
        await SaveAsync(ct);
        return ToState(account);
    }

    /// <summary>Development: sets how far the campaign is cleared (dungeon and map tests without pushing there).</summary>
    public async Task<StateDto> DevStageAsync(Account account, int cleared, CancellationToken ct)
    {
        account.HighestStageCleared = Math.Clamp(cleared, 0, Content.TotalStages);
        await SaveAsync(ct);
        return ToState(account);
    }

    /// <summary>A run stopped at its pause: what waits there (and a rune lock's riddle, drawn from the run's id).</summary>
    private static DungeonResultDto WithPause(DungeonResultDto result, DungeonDef? paused)
    {
        if (paused == null) return result;
        if (paused.Pause != DungeonPause.RuneLock) return result with { Pause = paused.Pause.ToString() };
        var riddle = Dungeons.RiddleFor(result.RunId);
        return result with { Pause = paused.Pause.ToString(), Riddle = riddle.Text, Runes = riddle.Runes };
    }

    /// <summary>The Warden's chest, into the account.</summary>
    private string Chest(Account account, int level, DungeonDef dungeon, bool vaultOpen)
    {
        var inventory = Snapshot(account);
        string chest = Dungeons.WardenChest(inventory, level, _rng, dungeon, vaultOpen);
        Apply(account, inventory);
        account.Honor += Rules.SkillGrades.HonorPerWarden;
        return chest;
    }

    /// <summary>
    /// The Chained Smith: forges one owned piece at +10 points of success (ForgeMethod.ChainedSmith: the Forge's cost and
    /// failure rule), or nothing when ItemId is empty; then the rest of the run is fought.
    /// </summary>
    public async Task<DungeonResultDto> DungeonSmithAsync(Account account, DungeonSmithRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        if (account.DungeonRunAtSmith == 0 || account.DungeonRunAtSmith != request.RunId)
            throw new GameException("no_run", "No run is waiting on its pause floor.");
        DungeonRun run = await _db.DungeonRuns.FirstOrDefaultAsync(r => r.Id == request.RunId && r.AccountId == account.Id && r.State == 0, ct)
            ?? throw new GameException("no_run", "No run is waiting on its pause floor.");
        DungeonDef dungeon = Dungeons.Find(run.DungeonId)!;

        ForgeResultDto? smith = null;
        string smithItem = "";
        string text = "";
        bool vaultOpen = false;
        if (dungeon.Pause == DungeonPause.RuneLock)
        {
            // The Carvers' rune lock: the right rune opens the vault (the Last Carver's chest then holds a Master's Needle).
            var riddle = Dungeons.RiddleFor(run.Id);
            vaultOpen = string.Equals(request.Rune, riddle.Answer, StringComparison.OrdinalIgnoreCase);
            text = string.IsNullOrEmpty(request.Rune) ? "You leave the vault shut. "
                : vaultOpen ? $"The {riddle.Answer} rune turns and the vault door opens. " : $"The {request.Rune} rune does not turn: the vault stays shut. ";
            _db.Ledger.Add(Entry(account.Id, null, "rune-lock", $"run={run.Id} rune={request.Rune} open={vaultOpen}", 0, request.RequestId + ":rune"));
        }
        else if (!string.IsNullOrEmpty(request.ItemId))
        {
            if (!Guid.TryParse(request.ItemId, out Guid itemId)) throw new GameException("no_item", "You do not own that item.");
            Item item = AnvilItem(account, itemId, EquipSlot.Weapon);
            ItemState state = item.ToState();
            if (state.UpgradeLevel >= ItemState.MaxUpgradeLevel) throw new GameException("already_max", "That piece is already +9.");
            long cost = ForgeRules.Cost(state.ItemLevel, state.UpgradeLevel);
            int materials = ForgeRules.MaterialsNeeded(state.UpgradeLevel + 1);
            if (account.Sorn < cost) throw new GameException("no_sorn", "Not enough sorn for the smith.");
            if (account.Materials < materials) throw new GameException("no_materials", "Not enough materials for the smith.");
            account.Sorn -= cost;
            account.Materials -= materials;
            smithItem = state.DisplayName;
            ForgeResult result = _forge.Attempt(state, ForgeMethod.ChainedSmith, _rng);
            item.ApplyState(state);
            if (result.Outcome == ForgeOutcome.Oathbreak)
            {
                // As at the Forge: a worn piece leaves a starter in its slot, a piece from the bag is simply gone.
                bool worn = item.Equipped;
                item.Equipped = false;
                if (item.Slot == EquipSlot.Weapon) account.WeaponsBroken++;
                if (worn) account.Items.Add(Item.From(NewStarter(item.Slot), account.Id, equipped: true));
            }
            Count(account, BountyMetric.ForgeAttempts, 1);
            if (result.LevelAfter > result.LevelBefore && result.LevelAfter >= 8)
                SystemLine(Chat.World, $"{DisplayName(account)} forged {state.DisplayName} to +{result.LevelAfter} at the Chained Smith!");
            _db.Ledger.Add(Entry(account.Id, item.Id, "smith",
                $"run={run.Id} +{result.LevelBefore}->+{result.LevelAfter} chance={result.ChanceBp} outcome={result.Outcome}", -cost, request.RequestId + ":smith"));
            smith = new ForgeResultDto(result.Outcome, result.ChanceBp, result.LevelBefore, result.LevelAfter);
            text = result.Outcome == ForgeOutcome.Success ? $"The Chained Smith raised your {smithItem} to +{result.LevelAfter}. "
                : result.Outcome == ForgeOutcome.Oathbreak ? $"The Chained Smith broke your {smithItem}. "
                : $"The Chained Smith failed: your {smithItem} is +{result.LevelAfter}. ";
        }

        List<DungeonFloorDto> floors = FightFloors(account, dungeon, run.Level, dungeon.SmithFloor + 1, dungeon.Floors, out int fellOn);
        run.State = 1;
        run.FloorsCleared = fellOn > 0 ? fellOn - 1 : dungeon.Floors;
        account.DungeonRunAtSmith = 0;
        account.DungeonPausedId = 0;
        string chest = "";
        if (fellOn > 0) text += $"You fell on floor {fellOn}.";
        else
        {
            chest = Chest(account, run.Level, dungeon, vaultOpen);
            text += $"{dungeon.Name} is cleared! {dungeon.WardenName}'s chest: {chest}.";
        }
        _db.Ledger.Add(Entry(account.Id, null, "dungeon-end", $"run={run.Id} floors={run.FloorsCleared} fell={fellOn}", 0, request.RequestId));
        await SaveAsync(ct);
        return new DungeonResultDto(ToState(account), run.Id, dungeon.Id, run.Level, floors.ToArray(), false, fellOn == 0, fellOn, chest, smith, smithItem, text);
    }
}
