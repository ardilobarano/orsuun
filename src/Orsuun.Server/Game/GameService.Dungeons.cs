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
        Apply(account, inventory);
        return floors;
    }

    public async Task<DungeonResultDto> EnterDungeonAsync(Account account, DungeonEnterRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        DungeonDef dungeon = Dungeons.Find(request.DungeonId) ?? throw new GameException("no_dungeon", "Unknown dungeon.");
        if (account.HighestStageCleared < dungeon.UnlockStage)
            throw new GameException("stage_locked", $"Clear {Content.StageName(dungeon.UnlockStage)} first.");
        if (account.DungeonRunAtSmith != 0) throw new GameException("run_open", "The Chained Smith is still waiting for your last run.");
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

        bool atSmith = fellOn == 0 && dungeon.SmithFloor > 0;
        string chest = "";
        string text;
        if (fellOn > 0) text = $"You fell on floor {fellOn} of {dungeon.Name}.";
        else if (atSmith)
        {
            run.State = 0;
            text = $"Floor {dungeon.SmithFloor}: the Chained Smith waits, hammer in his chained hands.";
        }
        else
        {
            chest = Chest(account, level);
            text = $"{dungeon.Name} is cleared! The Warden's chest: {chest}.";
        }
        await SaveAsync(ct);   // the run's id
        if (atSmith) account.DungeonRunAtSmith = run.Id;
        _db.Ledger.Add(Entry(account.Id, null, "dungeon", $"run={run.Id} dungeon={dungeon.Id} level={level} floors={run.FloorsCleared} fell={fellOn} smith={atSmith}", 0, request.RequestId));
        await SaveAsync(ct);
        await tx.CommitAsync(ct);
        return new DungeonResultDto(ToState(account), run.Id, dungeon.Id, level, floors.ToArray(), atSmith, fellOn == 0 && !atSmith, fellOn, chest, null, "", text);
    }

    /// <summary>Development: sets how far the campaign is cleared (dungeon and map tests without pushing there).</summary>
    public async Task<StateDto> DevStageAsync(Account account, int cleared, CancellationToken ct)
    {
        account.HighestStageCleared = Math.Clamp(cleared, 0, Content.TotalStages);
        await SaveAsync(ct);
        return ToState(account);
    }

    /// <summary>The Warden's chest, into the account.</summary>
    private string Chest(Account account, int level)
    {
        var inventory = Snapshot(account);
        string chest = Dungeons.WardenChest(inventory, level, _rng);
        Apply(account, inventory);
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
            throw new GameException("no_run", "No run is waiting at the Chained Smith.");
        DungeonRun run = await _db.DungeonRuns.FirstOrDefaultAsync(r => r.Id == request.RunId && r.AccountId == account.Id && r.State == 0, ct)
            ?? throw new GameException("no_run", "No run is waiting at the Chained Smith.");
        DungeonDef dungeon = Dungeons.Find(run.DungeonId)!;

        ForgeResultDto? smith = null;
        string smithItem = "";
        string text = "";
        if (!string.IsNullOrEmpty(request.ItemId))
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
        string chest = "";
        if (fellOn > 0) text += $"You fell on floor {fellOn}.";
        else
        {
            chest = Chest(account, run.Level);
            text += $"{dungeon.Name} is cleared! The Warden's chest: {chest}.";
        }
        _db.Ledger.Add(Entry(account.Id, null, "dungeon-end", $"run={run.Id} floors={run.FloorsCleared} fell={fellOn}", 0, request.RequestId));
        await SaveAsync(ct);
        return new DungeonResultDto(ToState(account), run.Id, dungeon.Id, run.Level, floors.ToArray(), false, fellOn == 0, fellOn, chest, smith, smithItem, text);
    }
}
