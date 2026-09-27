using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Orsuun.Rules;
using Orsuun.Rules.Combat;
using Orsuun.Server.Data;

namespace Orsuun.Server.Game;

// The guild raid (Rules.GuildRaids, owner 27 Sep 2026).
public sealed partial class GameService
{
    public Task<GuildRaidDto> GuildRaidAsync(Account account, CancellationToken ct) => RaidViewAsync(account, "", ct);

    /// <summary>The week's raid as the guild stands; before its first fight, what it will be (nothing is written).</summary>
    private async Task<GuildRaidDto> RaidViewAsync(Account account, string message, CancellationToken ct)
    {
        Guild guild = GuildOf(account) ?? throw new GameException("no_guild", "You are not in a guild.");
        DateTime local = _bells.LocalNow;
        string week = Rules.Bounties.WeekKey(local), day = Rules.Bounties.DayKey(local);
        GuildRaid? raid = await _db.GuildRaids.AsNoTracking().FirstOrDefaultAsync(r => r.GuildId == guild.Id && r.Week == week, ct);
        int map;
        long hpMax, hpLeft;
        var top = new List<RaidHitDto>();
        long mine = 0;
        int foughtToday = 0;
        if (raid == null)
        {
            List<int> stages = await _db.Accounts.AsNoTracking().Where(a => a.GuildId == guild.Id).Select(a => a.HighestStageCleared).ToListAsync(ct);
            map = GuildRaids.MapFor(stages);
            hpMax = hpLeft = GuildRaids.Pool(map, stages.Count);
        }
        else
        {
            map = raid.Map;
            hpMax = raid.HpMax;
            hpLeft = raid.HpLeft;
            var sums = await _db.GuildRaidHits.AsNoTracking().Where(h => h.RaidId == raid.Id)
                .GroupBy(h => h.AccountId).Select(g => new { g.Key, Damage = g.Sum(h => h.Damage), Name = g.Max(h => h.Name) })
                .OrderByDescending(g => g.Damage).ToListAsync(ct);
            top = sums.Take(10).Select(s => new RaidHitDto(s.Name ?? "", s.Damage)).ToList();
            mine = sums.Where(s => s.Key == account.Id).Select(s => s.Damage).FirstOrDefault();
            foughtToday = await _db.GuildRaidHits.CountAsync(h => h.RaidId == raid.Id && h.AccountId == account.Id && h.Day == day, ct);
        }
        StageConfig stage = GuildRaids.Stage(map);
        long secondsLeft = (long)(Rules.Bounties.WeekStart(local).AddDays(7) - local).TotalSeconds;
        return new GuildRaidDto(stage.BossName ?? "", map, Content.Maps[map - 1].Name, stage.BossMechanic.ToString(), hpMax, hpLeft, Math.Max(0, secondsLeft),
            Math.Max(0, GuildRaids.FightsPerDay - foughtToday), mine, top.ToArray(), raid?.SlainUtc != null, raid?.SlainBy ?? "", message);
    }

    /// <summary>One raid fight: the server rolls it, takes its damage off the guild's pool, and pays everyone if the boss falls.</summary>
    public async Task<GuildRaidFightDto> FightRaidAsync(Account account, RaidFightRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        Guild guild = GuildOf(account) ?? throw new GameException("no_guild", "You are not in a guild.");
        DateTime local = _bells.LocalNow, now = DateTime.UtcNow;
        string week = Rules.Bounties.WeekKey(local), day = Rules.Bounties.DayKey(local);
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        // The week's raid is made by its first fight, from the guild as it stands then.
        List<int> stages = await _db.Accounts.AsNoTracking().Where(a => a.GuildId == guild.Id).Select(a => a.HighestStageCleared).ToListAsync(ct);
        int map = GuildRaids.MapFor(stages);
        long pool = GuildRaids.Pool(map, stages.Count);
        DateTime starts = _bells.ToUtc(Rules.Bounties.WeekStart(local)), ends = starts.AddDays(7);
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $@"INSERT INTO ""GuildRaids"" (""GuildId"", ""Week"", ""Map"", ""Members"", ""HpMax"", ""HpLeft"", ""StartsUtc"", ""EndsUtc"", ""SlainBy"")
               VALUES ({guild.Id}, {week}, {map}, {stages.Count}, {pool}, {pool}, {starts}, {ends}, '') ON CONFLICT (""GuildId"", ""Week"") DO NOTHING", ct);
        GuildRaid raid = (await _db.GuildRaids.FromSql($@"SELECT * FROM ""GuildRaids"" WHERE ""GuildId"" = {guild.Id} AND ""Week"" = {week} FOR UPDATE").ToListAsync(ct)).First();
        StageConfig stage = GuildRaids.Stage(raid.Map);
        string boss = stage.BossName ?? "";
        if (raid.SlainUtc != null) throw new GameException("raid_slain", $"{boss} has fallen this week. The next comes on Monday at 20:00.");
        int today = await _db.GuildRaidHits.CountAsync(h => h.RaidId == raid.Id && h.AccountId == account.Id && h.Day == day, ct);
        if (today >= GuildRaids.FightsPerDay)
            throw new GameException("raid_tired", $"You have fought the raid {GuildRaids.FightsPerDay} times today. More at 20:00.");

        ulong seed = BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(8));
        int potionsAtStart = account.Potions;
        var inventory = Snapshot(account);
        BossRunResult run = BossRun.Simulate(stage, Hero(account), inventory, seed);
        long damage = Math.Min(run.Damage, raid.HpLeft);
        raid.HpLeft -= damage;
        string name = DisplayName(account);
        _db.GuildRaidHits.Add(new GuildRaidHit { RaidId = raid.Id, AccountId = account.Id, Name = name, Damage = damage, Day = day, Utc = now });
        Apply(account, inventory, hunt: true);
        string message = $"You dealt {SornText(damage)} damage to {boss}.";
        if (raid.HpLeft == 0)
        {
            raid.SlainUtc = now;
            raid.SlainBy = Clip(name, 56);
            await SaveAsync(ct);   // this fight counts in the shares
            await PayRaidAsync(raid, guild, account, boss, ct);
            message += $" {boss} has fallen! Your pay is in the mailbox.";
        }
        _db.Ledger.Add(Entry(account.Id, null, "raid:" + raid.Id, $"seed={seed} map={raid.Map} damage={damage} left={raid.HpLeft}/{raid.HpMax} killed={run.Killed}", 0, request.RequestId));
        await SaveAsync(ct);
        await tx.CommitAsync(ct);
        return new GuildRaidFightDto(await RaidViewAsync(account, message, ct), seed, damage, run.Killed, potionsAtStart, ToState(account));
    }

    /// <summary>Everyone who fought is paid by his share: Guild Tallies at once, sorn by letter; the guild gains XP.</summary>
    private async Task PayRaidAsync(GuildRaid raid, Guild guild, Account slayer, string boss, CancellationToken ct)
    {
        var shares = await _db.GuildRaidHits.AsNoTracking().Where(h => h.RaidId == raid.Id)
            .GroupBy(h => h.AccountId).Select(g => new { g.Key, Damage = g.Sum(h => h.Damage) }).ToListAsync(ct);
        List<Guid> ids = shares.Select(s => s.Key).ToList();
        var heroes = await _db.Accounts.AsNoTracking().Where(a => ids.Contains(a.Id)).Select(a => new { a.Id, a.HighestStageCleared }).ToListAsync(ct);
        foreach (var s in shares)
        {
            var hero = heroes.FirstOrDefault(h => h.Id == s.Key);
            if (hero == null) continue;   // a hero deleted since
            (int tallies, int mobs) = GuildRaids.Reward(s.Damage, raid.HpMax);
            long sorn = mobs * Content.Stage(Math.Max(1, hero.HighestStageCleared)).SornPerMob;
            if (s.Key == slayer.Id) slayer.Tallies += tallies;
            else await _db.Accounts.Where(a => a.Id == s.Key).ExecuteUpdateAsync(u => u.SetProperty(a => a.Tallies, a => a.Tallies + tallies), ct);
            int percent = (int)Math.Round(100.0 * s.Damage / Math.Max(1, raid.HpMax));
            SendLetter(s.Key, "raid", "The guild raid", $"{boss} has fallen",
                $"[{guild.Tag}] felled {boss} in the week's raid. Your share: {SornText(s.Damage)} damage ({percent}%). You have +{tallies} Guild Tallies already; the sorn is here.", sorn);
        }
        await AddGuildXpAsync(guild.Id, GuildRaids.GuildXp, ct);
        GuildLine(guild, $"{DisplayName(slayer)} struck the last blow: {boss} has fallen in the guild raid!");
        SystemLine(Chat.World, $"[{guild.Tag}] {guild.Name} felled {boss} in their guild raid.");
    }

    /// <summary>A guild's raids go with it.</summary>
    private async Task DeleteRaidsAsync(Guid guildId, CancellationToken ct)
    {
        IQueryable<long> raids = _db.GuildRaids.Where(r => r.GuildId == guildId).Select(r => r.Id);
        await _db.GuildRaidHits.Where(h => raids.Contains(h.RaidId)).ExecuteDeleteAsync(ct);
        await _db.GuildRaids.Where(r => r.GuildId == guildId).ExecuteDeleteAsync(ct);
    }
}
