using Orsuun.Rules;
using Orsuun.Rules.Combat;
using Orsuun.Server.Data;

namespace Orsuun.Server.Game;

/// <summary>
/// Skill grades (owner, 26 Sep 2026; Rules.SkillGrades): one try at a skill of the class played, rolled here. A Mastered
/// step reads that skill's book (one spent a read, 70%, the skill rests 8 hours; the step needs 1, 1, 2 .. 9 good reads);
/// a Grand step or Peerless burns an Oathstone and pays Honor (60%). A grade that rises changes the hero (skill power), so
/// the time on the old hero is settled first and the lane gets a fresh seed, as with Oath Renewal and a change of class.
/// The fourth and fifth skills train only once the hero has reached their level (SkillDef.UnlockLevel).
/// </summary>
public sealed partial class GameService
{
    private static long[] ParseReads(string text)
    {
        var reads = new long[Books.Count];
        string[] parts = (text ?? "").Split(';');
        for (int i = 0; i < reads.Length && i < parts.Length; i++) long.TryParse(parts[i], out reads[i]);
        return reads;
    }

    private static long SecondsSinceRead(long ticks, DateTime now) =>
        ticks <= 0 ? long.MaxValue / 4 : (long)Math.Max(0, (now - new DateTime(ticks, DateTimeKind.Utc)).TotalSeconds);

    /// <summary>Seconds until each skill (by book id) may read again (0: now).</summary>
    private static long[] SkillReadySeconds(Account account)
    {
        DateTime now = DateTime.UtcNow;
        return ParseReads(account.SkillReads).Select(t => SkillGrades.CooldownLeft(SecondsSinceRead(t, now))).ToArray();
    }

    public async Task<SkillTrainDto> TrainSkillAsync(Account account, SkillTrainRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        if (request.Slot < 0 || request.Slot >= SkillGrades.Slots) throw new GameException("bad_slot", "No such skill.");
        SkillDef kit = SkillDef.For(account.Class)[request.Slot];
        if (Content.LevelFor(account.Xp) < kit.UnlockLevel)
            throw new GameException("skill_locked", $"{kit.Name} unlocks at level {kit.UnlockLevel}.");
        int book = Books.Id(account.Class, request.Slot);
        int[] grades = SkillGrades.Parse(account.SkillGrades);
        int[] progress = SkillGrades.Parse(account.SkillProgress, 99);
        long[] reads = ParseReads(account.SkillReads);
        DateTime now = DateTime.UtcNow;
        int grade = grades[book];
        int held = BookCounts(account)[book];
        if (SkillGrades.Problem(grade, held, account.Oathstones, account.Honor, SecondsSinceRead(reads[book], now)) is string problem)
            throw new GameException("skill_train", problem);

        bool reading = SkillGrades.NeedsBooks(grade);
        int honor = SkillGrades.HonorCost(grade);
        if (reading)
        {
            SetBooks(account, book, held - 1);
            reads[book] = now.Ticks;
        }
        else
        {
            account.Oathstones--;
            account.Honor -= honor;
        }
        (int next, int count) = SkillGrades.Train(grade, progress[book], _rng, out bool success);
        string skill = Books.SkillName(book);
        string text;
        if (next > grade)
        {
            Settle(account, now);
            account.LastHeartbeatUtc = now;
            NewLane(account);
            text = $"{skill} rises to {SkillGrades.Name(next)}: +{SkillGrades.BonusPercent(next)}% skill power.";
            if (SkillGrades.Tier(next) == SkillTier.Peerless) SystemLine(Chat.World, $"{NameOf(account)} has made {skill} Peerless.");
        }
        else if (success) text = $"The scroll's teaching took: {skill} {SkillGrades.Name(grade)}, {count} of {SkillGrades.ReadsNeeded(grade)} reads.";
        else text = reading ? $"The teaching slipped away: {skill} stays {SkillGrades.Name(grade)}, {progress[book]} of {SkillGrades.ReadsNeeded(grade)} reads."
            : $"The Oathstone cracked: {skill} stays {SkillGrades.Name(grade)}.";
        grades[book] = next;
        progress[book] = count;
        account.SkillGrades = SkillGrades.Format(grades);
        account.SkillProgress = SkillGrades.Format(progress);
        account.SkillReads = string.Join(';', reads);
        _db.Ledger.Add(Entry(account.Id, null, "skill-train",
            $"book={book} {SkillGrades.Name(grade)}->{SkillGrades.Name(next)} reads={count} item={(reading ? "book" : "oathstone")} honor={(reading ? 0 : honor)} success={success}",
            0, request.RequestId));
        await SaveAsync(ct);
        return new SkillTrainDto(ToState(account), request.Slot, success, next, text);
    }

    /// <summary>Development: sets a skill's grade (by book id) and clears its rest and reads.</summary>
    public async Task<StateDto> DevSkillAsync(Account account, int book, int grade, CancellationToken ct)
    {
        if (!Books.Valid(book)) throw new GameException("no_book", "No such book.");
        int[] grades = SkillGrades.Parse(account.SkillGrades), progress = SkillGrades.Parse(account.SkillProgress, 99);
        long[] reads = ParseReads(account.SkillReads);
        grades[book] = Math.Max(0, Math.Min(SkillGrades.Max, grade));
        progress[book] = 0;
        reads[book] = 0;
        account.SkillGrades = SkillGrades.Format(grades);
        account.SkillProgress = SkillGrades.Format(progress);
        account.SkillReads = string.Join(';', reads);
        NewLane(account);
        await SaveAsync(ct);
        return ToState(account);
    }
}
