using Microsoft.EntityFrameworkCore;
using Orsuun.Rules;
using Orsuun.Server.Data;

namespace Orsuun.Server.Game;

// Fishing at Old Nergui's river (owner, 28 Sep 2026; Rules.Fishing): the hero goes there from ZONES and the hunt stops;
// CAST and REEL in time for a fish or a mussel; fish are eaten for a hunting boost, mussels opened for pearls; the
// Tireless Rod (Amber) lands a catch every 30 seconds by itself while the hero stays there, online or away.
public sealed partial class GameService
{
    /// <summary>What the Tireless Rod brought in during this request (a heartbeat's auto catches), for RiverOf.</summary>
    private int[]? _autoFish;
    private int _autoMussels;

    private static int[] ParseCounts(string s, int length)
    {
        var counts = new int[length];
        string[] parts = (s ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < Math.Min(length, parts.Length); i++) counts[i] = int.TryParse(parts[i], out int n) ? n : 0;
        return counts;
    }

    /// <summary>Each fish's boost, until when (null: none), by fish id.</summary>
    private static DateTime?[] MealsOf(Account a)
    {
        var until = new DateTime?[Fishing.Fish.Length];
        string[] parts = (a.Meals ?? "").Split(';');
        for (int i = 0; i < until.Length && i < parts.Length; i++)
            if (long.TryParse(parts[i], out long unix) && unix > 0) until[i] = DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;
        return until;
    }

    private static void SetMeals(Account a, DateTime?[] until) =>
        a.Meals = string.Join(';', until.Select(u => u == null ? 0L : new DateTimeOffset(DateTime.SpecifyKind(u.Value, DateTimeKind.Utc)).ToUnixTimeSeconds()));

    private RiverDto RiverOf(Account a)
    {
        DateTime now = DateTime.UtcNow;
        long[] meals = MealsOf(a).Select(u => u is DateTime m && m > now ? (long)(m - now).TotalSeconds : 0L).ToArray();
        long rod = a.AutoRodUntilUtc is DateTime r && r > now ? (long)(r - now).TotalSeconds : 0;
        return new RiverDto(a.AtRiver, meals, rod, _autoFish, _autoMussels);
    }

    /// <summary>The Tireless Rod's catches since it was last counted, while the hero stands at the river and holds it.</summary>
    private void LandAuto(Account a, DateTime now)
    {
        if (!a.AtRiver || a.AutoRodUntilUtc is not DateTime until || a.AutoFromUtc is not DateTime from) return;
        DateTime to = now < until ? now : until;
        int catches = Fishing.AutoCatches(from, to);
        if (catches > 0)
        {
            int[] fish = ParseCounts(a.Fish, Fishing.Fish.Length);
            _autoFish ??= new int[Fishing.Fish.Length];
            for (int i = 0; i < catches; i++)
            {
                (CatchKind kind, int id) = Fishing.Land(_rng);
                if (kind == CatchKind.Mussel) { a.Mussels++; _autoMussels++; }
                else { fish[id]++; _autoFish[id]++; }
            }
            a.Fish = string.Join(';', fish);
            _db.Ledger.Add(Entry(a.Id, null, "auto-fish", $"catches={catches}", 0, Guid.NewGuid().ToString("N")));
        }
        // The rod gone quiet stops counting; a full twelve hours drops what lay beyond them.
        a.AutoFromUtc = to >= until ? null : catches >= Fishing.AutoCap ? to : from.AddSeconds((double)catches * Fishing.AutoSeconds);
    }

    /// <summary>Goes to the river: the hunt so far is settled and stops (no hunting there).</summary>
    public async Task<StateDto> GoToRiverAsync(Account account, CancellationToken ct)
    {
        if (account.AtRiver) return ToState(account);
        DateTime now = DateTime.UtcNow;
        SettlementDto settlement = Settle(account, now);
        account.LastHeartbeatUtc = now;
        account.AtRiver = true;
        account.AutoFromUtc = now;
        account.CastUtc = null;
        await SaveAsync(ct);
        return ToState(account, settlement: settlement);
    }

    /// <summary>Back to the hunt where the hero was parked, with a fresh lane (as a park does).</summary>
    public async Task<StateDto> LeaveRiverAsync(Account account, CancellationToken ct)
    {
        if (!account.AtRiver) return ToState(account);
        DateTime now = DateTime.UtcNow;
        LeaveRiver(account, now);
        await SaveAsync(ct);
        return ToState(account);
    }

    private void LeaveRiver(Account account, DateTime now)
    {
        LandAuto(account, now);
        Settle(account, now);   // nothing is hunted at the river: this only moves the clock
        account.LastHeartbeatUtc = now;
        account.AtRiver = false;
        account.AutoFromUtc = null;
        account.CastUtc = null;
        account.HuntCarryTicks = 0;
        account.HuntEncounter = 0;
        NewLane(account);
    }

    /// <summary>A cast: the float goes under after a delay the server draws, for Fishing.WindowMs.</summary>
    public async Task<CastBiteDto> CastAsync(Account account, CancellationToken ct)
    {
        if (!account.AtRiver) throw new GameException("not_at_river", "Go to Old Nergui's river first (ZONES).");
        account.CastUtc = DateTime.UtcNow;
        account.CastBiteMs = Fishing.BiteMinMs + _rng.NextInt(Fishing.BiteMaxMs - Fishing.BiteMinMs + 1);
        await SaveAsync(ct);
        return new CastBiteDto(account.CastBiteMs, Fishing.WindowMs);
    }

    /// <summary>A reel: in time it hooks a fish (landed by LandAsync, after the catch) or brings up a mussel; too soon or too
    /// late the fish gets away.</summary>
    public async Task<ReelDto> ReelAsync(Account account, CancellationToken ct)
    {
        if (account.CastUtc is not DateTime cast) throw new GameException("no_cast", "Cast first.");
        long elapsed = (long)(DateTime.UtcNow - cast).TotalMilliseconds;
        account.CastUtc = null;
        account.HookedFish = -1;
        account.HookedUtc = null;
        string kind = "escaped", message;
        int fishId = -1;
        if (elapsed < account.CastBiteMs - Fishing.EarlyMs) message = "Too soon: nothing had bitten yet.";
        else if (!Fishing.InTime(elapsed, account.CastBiteMs)) message = "Too slow: it stole the bait and swam off.";
        else
        {
            (CatchKind caught, int id) = Fishing.Land(_rng);
            if (caught == CatchKind.Mussel)
            {
                account.Mussels++;
                kind = "mussel";
                message = "A river mussel! Old Nergui can open it.";
            }
            else
            {
                // On the line: the catch decides it (LandAsync).
                account.HookedFish = id;
                account.HookedUtc = DateTime.UtcNow;
                kind = "fight";
                fishId = id;
                message = $"A {Fishing.Fish[id].Name} is on the line! Keep it in the box.";
            }
        }
        await SaveAsync(ct);
        return new ReelDto(ToState(account), kind, fishId, message);
    }

    /// <summary>The catch's end: a fish kept in the box until the bar filled is landed (no sooner than a full bar can fill,
    /// no later than Fishing.LandMaxMs after the hook); else it gets away.</summary>
    public async Task<ReelDto> LandAsync(Account account, LandRequest request, CancellationToken ct)
    {
        if (account.HookedUtc is not DateTime hooked || Fishing.FishById(account.HookedFish) is not FishDef def)
            throw new GameException("no_fish_on", "Nothing is on the line.");
        long elapsed = (long)(DateTime.UtcNow - hooked).TotalMilliseconds;
        account.HookedFish = -1;
        account.HookedUtc = null;
        bool landed = request.Landed && elapsed >= Fishing.LandMinMs && elapsed <= Fishing.LandMaxMs;
        string message;
        if (landed)
        {
            int[] fish = ParseCounts(account.Fish, Fishing.Fish.Length);
            fish[def.Id]++;
            account.Fish = string.Join(';', fish);
            message = $"You caught a {def.Name}!";
            if (def.Id == Fishing.Fish.Length - 1) SystemLine(Chat.World, $"{DisplayName(account)} landed a Golden Taimen at Old Nergui's river!");
        }
        else message = "It slipped the hook and got away.";
        await SaveAsync(ct);
        return new ReelDto(ToState(account), landed ? "fish" : "escaped", def.Id, message);
    }

    /// <summary>Eats a fish: its boost runs for its minutes beside any other fish's (one already running gets its time added,
    /// up to Fishing.MealMaxMinutes ahead).</summary>
    public async Task<StateDto> EatAsync(Account account, EatRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        FishDef def = Fishing.FishById(request.Fish) ?? throw new GameException("no_fish", "No such fish.");
        int[] fish = ParseCounts(account.Fish, Fishing.Fish.Length);
        if (fish[def.Id] <= 0) throw new GameException("no_fish", "You have no " + def.Name + ".");
        // The hunt so far is paid with the meal it had.
        DateTime now = DateTime.UtcNow;
        SettlementDto settlement = Settle(account, now);
        account.LastHeartbeatUtc = now;
        fish[def.Id]--;
        account.Fish = string.Join(';', fish);
        DateTime?[] meals = MealsOf(account);
        meals[def.Id] = Fishing.MealUntil(def, now, meals[def.Id]);
        SetMeals(account, meals);
        _db.Ledger.Add(Entry(account.Id, null, "eat", def.Name, 0, request.RequestId));
        await SaveAsync(ct);
        return ToState(account, settlement: settlement);
    }

    /// <summary>Old Nergui opens mussels: each may hold a Moon, Tide or Heart Pearl.</summary>
    public async Task<OpenMusselsDto> OpenMusselsAsync(Account account, OpenMusselsRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        int count = Math.Min(Math.Min(Math.Max(1, request.Count), Fishing.OpenMax), account.Mussels);
        if (count <= 0) throw new GameException("no_mussels", "You have no mussels to open.");
        int[] pearls = ParseCounts(account.Pearls, 3);
        var found = new int[3];
        for (int i = 0; i < count; i++)
            if (Fishing.Open(_rng) is Pearl p) found[(int)p]++;
        account.Mussels -= count;
        for (int i = 0; i < 3; i++) pearls[i] += found[i];
        account.Pearls = string.Join(';', pearls);
        _db.Ledger.Add(Entry(account.Id, null, "mussels", $"opened={count} moon={found[0]} tide={found[1]} heart={found[2]}", 0, request.RequestId));
        if (found[(int)Pearl.Heart] > 0) SystemLine(Chat.World, $"{DisplayName(account)} found a Heart Pearl in a river mussel!");
        await SaveAsync(ct);
        int total = found.Sum();
        string message = total == 0
            ? (count == 1 ? "Empty. \"Most of them are,\" says Old Nergui." : $"{count} mussels, all empty. \"Most of them are,\" says Old Nergui.")
            : $"{count} opened: " + string.Join(", ", Enumerable.Range(0, 3).Where(i => found[i] > 0).Select(i => $"{found[i]} {Fishing.PearlNames[i]}{(found[i] > 1 ? "s" : "")}")) + "!";
        return new OpenMusselsDto(ToState(account), count, found, message);
    }

    /// <summary>The Tireless Rod from the Caravan (Amber, the login's): days added to any still held.</summary>
    public async Task<StateDto> BuyAutoRodAsync(Account account, AutoRodRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        int index = Array.IndexOf(Fishing.RodDays, request.Days);
        if (index < 0) throw new GameException("not_sold", "The Caravan does not sell the rod for so long.");
        int price = Fishing.RodAmber[index];
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        Login login = await LockLoginAsync(ct);
        if (login.Amber < price) throw new GameException("no_amber", "Not enough Amber.");
        login.Amber -= price;
        DateTime now = DateTime.UtcNow;
        LandAuto(account, now);
        DateTime start = account.AutoRodUntilUtc is DateTime held && held > now ? held : now;
        account.AutoRodUntilUtc = start.AddDays(request.Days);
        if (account.AtRiver && account.AutoFromUtc == null) account.AutoFromUtc = now;
        _db.Ledger.Add(Entry(account.Id, null, "caravan", $"tireless-rod {request.Days}d for {price} Amber", 0, request.RequestId));
        await SaveAsync(ct);
        await tx.CommitAsync(ct);
        return ToState(account);
    }
}
