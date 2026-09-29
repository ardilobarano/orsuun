using Orsuun.Rules;
using Orsuun.Server.Data;

namespace Orsuun.Server.Game;

// Titles and achievements (Rules.Achievements, owner 27 Sep 2026).
public sealed partial class GameService
{
    /// <summary>Adds to a hero's lifetime counter (Count does this for every bounty metric too).</summary>
    /// <summary>Sets a bit of one of the hero's masks (Commanders fought, maps with a cache opened); a new one raises the
    /// feat by one.</summary>
    private void MarkNew(Account account, FeatMetric metric, int bit, bool cacheMaps)
    {
        if (bit < 0 || bit > 30) return;
        int mask = cacheMaps ? account.CacheMapsMask : account.CommandersMask;
        if ((mask & (1 << bit)) != 0) return;
        if (cacheMaps) account.CacheMapsMask = mask | (1 << bit);
        else account.CommandersMask = mask | (1 << bit);
        Feat(account, metric, 1);
    }

    private void Feat(Account account, FeatMetric metric, long amount)
    {
        if (amount <= 0) return;
        FeatCounters c = FeatCounters.Parse(account.Feats);
        if (c[metric] == 0) Mark(account, "first-" + metric);   // the funnel's firsts (GameService.Funnel)
        c.Add(metric, amount);
        account.Feats = c.Serialize();
        // The townsfolk's errands count the same deeds, a day at a time.
        ErrandProgress errands = ErrandsOf(account);
        errands.Add(metric, amount);
        account.Errands = errands.Serialize();
    }

    /// <summary>Raises a hero's best (the highest upgrade a Forge gave).</summary>
    private static void FeatBest(Account account, FeatMetric metric, long value)
    {
        FeatCounters c = FeatCounters.Parse(account.Feats);
        c.Raise(metric, value);
        account.Feats = c.Serialize();
    }

    private static FeatSnapshot FeatsOf(Account account)
    {
        int[] grades = Rules.SkillGrades.Parse(account.SkillGrades);
        return new FeatSnapshot
        {
            Counters = FeatCounters.Parse(account.Feats),
            Level = Content.LevelFor(account.Xp),
            HighestStageCleared = account.HighestStageCleared,
            PitWins = account.PitWins,
            WeaponsBroken = account.WeaponsBroken,
            InGuild = account.GuildId != null,
            BestSkillGrade = grades.Length > 0 ? grades.Max() : 0,
            BestOwnedUpgrade = account.Items.Where(i => !i.Destroyed).Select(i => i.UpgradeLevel).DefaultIfEmpty(0).Max(),
        };
    }

    /// <summary>The title the hero wears, or the Pits' season title when he wears none.</summary>
    private static string? TitleOf(Account account) => Achievements.TitleOf(account.TitleId) ?? account.PitTitle
        ?? (account.AnglerUntilUtc > DateTime.UtcNow ? Fishing.AnglerTitle : null);

    private static int AchievementsReady(Account account) =>
        Achievements.Ready(FeatsOf(account), Achievements.ParseClaimed(account.FeatsClaimed));

    public Task<AchievementsDto> AchievementsAsync(Account account, CancellationToken ct) => Task.FromResult(AchievementsView(account, ""));

    private AchievementsDto AchievementsView(Account account, string message)
    {
        FeatSnapshot s = FeatsOf(account);
        HashSet<int> claimed = Achievements.ParseClaimed(account.FeatsClaimed);
        // Hunt time is counted in seconds and shown in hours.
        long Unit(AchievementDef d) => d.Source == FeatSource.Counter && d.Metric == FeatMetric.HuntSeconds ? 3600 : 1;
        AchievementDto[] list = Achievements.All.Select(d => new AchievementDto(d.Id, d.Name, d.Text, Math.Min(Achievements.Progress(d, s), d.Target) / Unit(d),
            d.Target / Unit(d), d.Honor, Achievements.Sorn(d, account.HighestStageCleared), d.Title, Achievements.Done(d, s), claimed.Contains(d.Id))).ToArray();
        return new AchievementsDto(ToState(account), list, account.TitleId, Achievements.TitleOf(account.TitleId) ?? "", message);
    }

    /// <summary>Claims a done achievement: its Honor and sorn, and its title to wear.</summary>
    public async Task<AchievementsDto> ClaimAchievementAsync(Account account, AchievementClaimRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        AchievementDef def = Achievements.Find(request.Id) ?? throw new GameException("no_achievement", "No such achievement.");
        HashSet<int> claimed = Achievements.ParseClaimed(account.FeatsClaimed);
        if (claimed.Contains(def.Id)) throw new GameException("claimed", "Already claimed.");
        if (!Achievements.Done(def, FeatsOf(account))) throw new GameException("not_done", "Not done yet.");
        long sorn = Achievements.Sorn(def, account.HighestStageCleared);
        claimed.Add(def.Id);
        account.FeatsClaimed = Achievements.SerializeClaimed(claimed);
        account.Honor += def.Honor;
        account.Sorn += sorn;
        // The first title a hero earns he wears at once; later ones wait for him to choose.
        if (def.Title != null && account.TitleId == 0) account.TitleId = def.Id;
        _db.Ledger.Add(Entry(account.Id, null, "achievement", $"id={def.Id} honor={def.Honor}", sorn, request.RequestId));
        await SaveAsync(ct);
        return AchievementsView(account, $"{def.Name} claimed: +{def.Honor} Honor, +{SornText(sorn)} sorn." + (def.Title != null ? $" New title: {def.Title}." : ""));
    }

    /// <summary>Wears a claimed achievement's title (Id 0 wears none).</summary>
    public async Task<AchievementsDto> WearTitleAsync(Account account, TitleRequest request, CancellationToken ct)
    {
        if (request.Id != 0)
        {
            AchievementDef def = Achievements.Find(request.Id) ?? throw new GameException("no_achievement", "No such achievement.");
            if (def.Title == null) throw new GameException("no_title", "That achievement gives no title.");
            if (!Achievements.ParseClaimed(account.FeatsClaimed).Contains(def.Id)) throw new GameException("not_claimed", "Claim it first.");
        }
        account.TitleId = request.Id;
        await SaveAsync(ct);
        return AchievementsView(account, request.Id == 0 ? "You wear no title." : $"You wear the title {Achievements.TitleOf(request.Id)}.");
    }
}
