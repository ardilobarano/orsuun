using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Orsuun.Rules;
using Orsuun.Server.Data;

namespace Orsuun.Server.Game;

/// <summary>
/// Guilds (owner, 24 Sep 2026): create, join, leave, ranks, the daily donation to the treasury, Guild Tallies and the
/// guild shop, guild skills. The guild row is locked (FOR UPDATE) whenever its treasury, skills or head count change;
/// other members' rows are changed with single UPDATE statements.
/// </summary>
public sealed partial class GameService
{
    private Guild? _guild;

    /// <summary>The account's guild, loaded once per request (AuthenticateAsync preloads it).</summary>
    private Guild? GuildOf(Account account)
    {
        if (account.GuildId is not Guid id) return null;
        if (_guild?.Id != id) _guild = _db.Guilds.Find(id);
        return _guild;
    }

    private GuildBriefDto? Brief(Account account) =>
        GuildOf(account) is Guild g ? new GuildBriefDto(g.Tag, g.Name, g.Color, account.GuildRank) : null;

    /// <summary>The name other players see: the generated name, behind the guild tag.</summary>
    private string DisplayName(Account account)
    {
        string name = Banners.GeneratedName(account.Id);
        return GuildOf(account) is Guild g ? $"[{g.Tag}] {name}" : name;
    }

    /// <summary>The guild row, locked for the rest of the transaction and read fresh.</summary>
    private async Task<Guild> LockGuildAsync(Guid id, CancellationToken ct)
    {
        Guild guild = (await _db.Guilds.FromSql($@"SELECT * FROM ""Guilds"" WHERE ""Id"" = {id} FOR UPDATE").ToListAsync(ct)).FirstOrDefault()
            ?? throw new GameException("no_guild", "That guild is gone.");
        await _db.Entry(guild).ReloadAsync(ct);
        _guild = guild;
        return guild;
    }

    private Account InGuild(Account account) =>
        account.GuildId == null ? throw new GameException("no_guild", "You are not in a guild.") : account;

    private void RequireManager(Account account)
    {
        if (!Guilds.CanManage(InGuild(account).GuildRank)) throw new GameException("guild_rank", "Only the leader or an officer can do that.");
    }

    private long DonatedToday(Account account) =>
        account.GuildDonationDay == Rules.Bounties.DayKey(_bells.LocalNow) ? account.GuildDonatedToday : 0;

    /// <summary>Hunting sorn bonus from the guild: Plunder, and each fortress flying the guild's flag.</summary>
    private async Task<int> GuildBonusPercentAsync(Account account, CancellationToken ct)
    {
        if (GuildOf(account) is not Guild g) return 0;
        int flags = await _db.Fortresses.AsNoTracking().CountAsync(f => f.FlagGuildId == g.Id, ct);
        return g.Plunder + flags * Guilds.FlagBonusPercent;
    }

    private Task<int> MemberCountAsync(Guid guildId, CancellationToken ct) => _db.Accounts.CountAsync(a => a.GuildId == guildId, ct);

    /// <summary>Guild XP from outside the guild screen (sieges, Commanders): one UPDATE, no lock needed.</summary>
    private async Task AddGuildXpAsync(Guid? guildId, long xp, CancellationToken ct)
    {
        if (guildId is not Guid id || xp <= 0) return;
        await _db.Guilds.Where(g => g.Id == id).ExecuteUpdateAsync(s => s.SetProperty(g => g.Xp, g => g.Xp + xp), ct);
    }

    public async Task<GuildViewDto> GuildAsync(Account account, string? search, CancellationToken ct) => await ViewAsync(account, search, "", ct);

    private async Task<GuildViewDto> ViewAsync(Account account, string? search, string message, CancellationToken ct)
    {
        long today = DonatedToday(account);
        StateDto state = ToState(account);
        if (GuildOf(account) is Guild g)
        {
            DateTime now = DateTime.UtcNow;
            var rows = await _db.Accounts.AsNoTracking().Where(a => a.GuildId == g.Id)
                .Select(a => new { a.Id, a.Banner, a.GuildRank, a.Xp, a.GuildDonated, a.LastHeartbeatUtc, a.GuildJoinedUtc }).ToListAsync(ct);
            GuildMemberDto[] members = rows
                .OrderByDescending(r => r.GuildRank).ThenByDescending(r => r.GuildDonated).ThenBy(r => r.GuildJoinedUtc)
                .Select(r => new GuildMemberDto(r.Id, Banners.GeneratedName(r.Id), r.Banner, r.GuildRank, Content.LevelFor(r.Xp), r.GuildDonated,
                    (int)Math.Max(0, (now - r.LastHeartbeatUtc).TotalMinutes), r.Id == account.Id))
                .ToArray();
            string[] forts = (await _db.Fortresses.AsNoTracking().Where(f => f.FlagGuildId == g.Id).Select(f => f.Id).ToListAsync(ct))
                .Select(id => Fortresses.Find(id)!.Name).ToArray();
            var mine = new GuildDto(g.Id, g.Name, g.Tag, g.Color, g.Open, Guilds.Level(g.Xp), g.Xp, Guilds.NextLevelXp(g.Xp), g.Treasury, g.Plunder, g.Muster,
                members.Length, Guilds.MaxMembers(g.Muster), g.Plunder + forts.Length * Guilds.FlagBonusPercent, forts, g.LastEvent);
            return new GuildViewDto(state, mine, members, Array.Empty<GuildListItemDto>(), today, Guilds.DailyDonationCap, message);
        }

        IQueryable<Guild> query = _db.Guilds.AsNoTracking();
        string q = (search ?? "").Trim();
        if (q.Length > 20) q = q.Substring(0, 20);
        if (q.Length > 0)
        {
            string key = q.ToLowerInvariant(), tag = q.ToUpperInvariant();
            query = query.Where(g => g.NameKey.Contains(key) || g.Tag == tag);
        }
        var guilds = await query.OrderByDescending(g => g.Open).ThenByDescending(g => g.Xp).Take(20).ToListAsync(ct);
        var ids = guilds.Select(g => (Guid?)g.Id).ToList();
        var counts = await _db.Accounts.AsNoTracking().Where(a => ids.Contains(a.GuildId)).GroupBy(a => a.GuildId)
            .Select(x => new { x.Key, Count = x.Count() }).ToListAsync(ct);
        GuildListItemDto[] browse = guilds.Select(g => new GuildListItemDto(g.Id, g.Name, g.Tag, g.Color, Guilds.Level(g.Xp),
            counts.Where(c => c.Key == g.Id).Select(c => c.Count).FirstOrDefault(), Guilds.MaxMembers(g.Muster), g.Open)).ToArray();
        return new GuildViewDto(state, null, Array.Empty<GuildMemberDto>(), browse, today, Guilds.DailyDonationCap, message);
    }

    public async Task<GuildViewDto> CreateGuildAsync(Account account, GuildCreateRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        if (account.GuildId != null) throw new GameException("in_guild", "Leave your guild first.");
        if (Guilds.NameProblem(request.Name) is string nameProblem) throw new GameException("bad_name", nameProblem);
        string tag = Guilds.NormaliseTag(request.Tag ?? "");
        if (Guilds.TagProblem(tag) is string tagProblem) throw new GameException("bad_tag", tagProblem);
        if (!Guilds.Colors.Contains(request.Color)) throw new GameException("bad_color", "Choose one of the guild colours.");
        if (account.Sorn < Guilds.CreateCost) throw new GameException("sorn", $"A guild charter costs {Guilds.CreateCost.ToString("N0", CultureInfo.InvariantCulture)} sorn.");
        string name = Guilds.NormaliseName(request.Name);
        string key = name.ToLowerInvariant();
        if (await _db.Guilds.AnyAsync(g => g.NameKey == key, ct)) throw new GameException("name_taken", "That name is taken.");
        if (await _db.Guilds.AnyAsync(g => g.Tag == tag, ct)) throw new GameException("tag_taken", "That tag is taken.");

        DateTime now = DateTime.UtcNow;
        var guild = new Guild
        {
            Id = Guid.NewGuid(), Name = name, NameKey = key, Tag = tag, Color = request.Color, Open = true, CreatedUtc = now,
            LastEvent = $"{Banners.GeneratedName(account.Id)} founded {name}.",
        };
        _db.Guilds.Add(guild);
        account.Sorn -= Guilds.CreateCost;
        account.GuildId = guild.Id;
        account.GuildRank = GuildRank.Leader;
        account.GuildJoinedUtc = now;
        account.GuildDonated = 0;
        _guild = guild;
        _db.Ledger.Add(Entry(account.Id, null, "guild-create", $"guild={guild.Id} name={name} tag={tag}", -Guilds.CreateCost, request.RequestId));
        try { await SaveAsync(ct); }
        catch (DbUpdateException) { throw new GameException("name_taken", "That name or tag was just taken."); }
        return await ViewAsync(account, null, $"{name} is founded. Its banner is yours to raise.", ct);
    }

    public async Task<GuildViewDto> JoinGuildAsync(Account account, GuildJoinRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        if (account.GuildId != null) throw new GameException("in_guild", "Leave your guild first.");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        Guild guild = await LockGuildAsync(request.GuildId, ct);
        if (!guild.Open) throw new GameException("guild_closed", guild.Name + " takes no one new.");
        if (await MemberCountAsync(guild.Id, ct) >= Guilds.MaxMembers(guild.Muster)) throw new GameException("guild_full", guild.Name + " is full.");
        account.GuildId = guild.Id;
        account.GuildRank = GuildRank.Member;
        account.GuildJoinedUtc = DateTime.UtcNow;
        account.GuildDonated = 0;
        guild.LastEvent = $"{Banners.GeneratedName(account.Id)} joined.";
        _db.Ledger.Add(Entry(account.Id, null, "guild-join", $"guild={guild.Id}", 0, request.RequestId));
        await SaveAsync(ct);
        await tx.CommitAsync(ct);
        return await ViewAsync(account, null, "Welcome to " + guild.Name + ".", ct);
    }

    /// <summary>
    /// Takes the account out of its guild. A leader leaving hands the guild to the highest rank that stayed longest;
    /// the last member leaving disbands it (its fortress flags come down). The caller saves.
    /// </summary>
    private async Task LeaveCoreAsync(Account account, CancellationToken ct)
    {
        Guid id = InGuild(account).GuildId!.Value;
        Guild guild = await LockGuildAsync(id, ct);
        string name = Banners.GeneratedName(account.Id);
        var heir = await _db.Accounts.AsNoTracking().Where(a => a.GuildId == id && a.Id != account.Id)
            .OrderByDescending(a => a.GuildRank).ThenBy(a => a.GuildJoinedUtc).Select(a => new { a.Id }).FirstOrDefaultAsync(ct);
        if (heir == null)
        {
            await _db.Fortresses.Where(f => f.FlagGuildId == id).ExecuteUpdateAsync(s => s.SetProperty(f => f.FlagGuildId, (Guid?)null), ct);
            _db.Guilds.Remove(guild);
            _guild = null;
        }
        else
        {
            if (account.GuildRank == GuildRank.Leader)
            {
                await _db.Accounts.Where(a => a.Id == heir.Id).ExecuteUpdateAsync(s => s.SetProperty(a => a.GuildRank, GuildRank.Leader), ct);
                guild.LastEvent = $"{name} left; {Banners.GeneratedName(heir.Id)} leads now.";
            }
            else guild.LastEvent = $"{name} left.";
        }
        account.GuildId = null;
        account.GuildRank = GuildRank.Member;
        account.GuildJoinedUtc = null;
        account.GuildDonated = 0;
    }

    public async Task<GuildViewDto> LeaveGuildAsync(Account account, GuildLeaveRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        string left = GuildOf(InGuild(account))?.Name ?? "the guild";
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await LeaveCoreAsync(account, ct);
        _db.Ledger.Add(Entry(account.Id, null, "guild-leave", "left " + left, 0, request.RequestId));
        await SaveAsync(ct);
        await tx.CommitAsync(ct);
        return await ViewAsync(account, null, "You left " + left + ".", ct);
    }

    public async Task<GuildViewDto> KickAsync(Account account, GuildMemberRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        Guid id = InGuild(account).GuildId!.Value;
        if (request.AccountId == account.Id) throw new GameException("self", "Use LEAVE to leave.");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        Guild guild = await LockGuildAsync(id, ct);
        var target = await _db.Accounts.AsNoTracking().Where(a => a.Id == request.AccountId && a.GuildId == id).Select(a => new { a.GuildRank }).FirstOrDefaultAsync(ct)
            ?? throw new GameException("not_member", "Not a member of your guild.");
        if (!Guilds.CanKick(account.GuildRank, target.GuildRank)) throw new GameException("guild_rank", "You cannot remove that member.");
        await _db.Accounts.Where(a => a.Id == request.AccountId && a.GuildId == id).ExecuteUpdateAsync(s => s
            .SetProperty(a => a.GuildId, (Guid?)null).SetProperty(a => a.GuildRank, GuildRank.Member)
            .SetProperty(a => a.GuildJoinedUtc, (DateTime?)null).SetProperty(a => a.GuildDonated, 0L), ct);
        guild.LastEvent = $"{Banners.GeneratedName(request.AccountId)} was sent away by {Banners.GeneratedName(account.Id)}.";
        _db.Ledger.Add(Entry(account.Id, null, "guild-kick", $"guild={id} member={request.AccountId}", 0, request.RequestId));
        await SaveAsync(ct);
        await tx.CommitAsync(ct);
        return await ViewAsync(account, null, Banners.GeneratedName(request.AccountId) + " is no longer in the guild.", ct);
    }

    /// <summary>The leader promotes to officer, demotes to member, or hands over the lead (becoming an officer).</summary>
    public async Task<GuildViewDto> SetGuildRankAsync(Account account, GuildMemberRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        Guid id = InGuild(account).GuildId!.Value;
        if (account.GuildRank != GuildRank.Leader) throw new GameException("guild_rank", "Only the leader sets ranks.");
        if (request.AccountId == account.Id) throw new GameException("self", "Choose another member.");
        if (!Enum.IsDefined(request.Rank)) throw new GameException("bad_rank", "Unknown rank.");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        Guild guild = await LockGuildAsync(id, ct);
        if (!await _db.Accounts.AnyAsync(a => a.Id == request.AccountId && a.GuildId == id, ct)) throw new GameException("not_member", "Not a member of your guild.");
        if (request.Rank == GuildRank.Officer
            && await _db.Accounts.CountAsync(a => a.GuildId == id && a.GuildRank == GuildRank.Officer && a.Id != request.AccountId, ct) >= Guilds.MaxOfficers)
            throw new GameException("officers_full", $"A guild has at most {Guilds.MaxOfficers} officers.");
        await _db.Accounts.Where(a => a.Id == request.AccountId && a.GuildId == id).ExecuteUpdateAsync(s => s.SetProperty(a => a.GuildRank, request.Rank), ct);
        string them = Banners.GeneratedName(request.AccountId);
        string text;
        if (request.Rank == GuildRank.Leader)
        {
            account.GuildRank = GuildRank.Officer;
            text = $"{them} leads {guild.Name} now.";
        }
        else text = request.Rank == GuildRank.Officer ? $"{them} is an officer now." : $"{them} is a member now.";
        guild.LastEvent = text;
        _db.Ledger.Add(Entry(account.Id, null, "guild-rank", $"guild={id} member={request.AccountId} rank={request.Rank}", 0, request.RequestId));
        await SaveAsync(ct);
        await tx.CommitAsync(ct);
        return await ViewAsync(account, null, text, ct);
    }

    /// <summary>Sorn into the treasury (daily cap per member): guild XP for the guild, Guild Tallies for the donor.</summary>
    public async Task<GuildViewDto> DonateAsync(Account account, GuildDonateRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        Guid id = InGuild(account).GuildId!.Value;
        long sorn = request.Sorn;
        if (sorn < Guilds.SornPerXp || sorn % Guilds.SornPerXp != 0) throw new GameException("bad_amount", "Donate in whole thousands of sorn.");
        if (sorn > account.Sorn) throw new GameException("sorn", "Not enough sorn.");
        long today = DonatedToday(account);
        if (today + sorn > Guilds.DailyDonationCap)
            throw new GameException("donation_cap", $"You can give {(Guilds.DailyDonationCap - today).ToString("N0", CultureInfo.InvariantCulture)} more sorn today.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        Guild guild = await LockGuildAsync(id, ct);
        int levelBefore = Guilds.Level(guild.Xp);
        int tallies = Guilds.TalliesFor(today + sorn) - Guilds.TalliesFor(today);
        account.Sorn -= sorn;
        account.Tallies += tallies;
        account.GuildDonated += sorn;
        account.GuildDonatedToday = today + sorn;
        account.GuildDonationDay = Rules.Bounties.DayKey(_bells.LocalNow);
        guild.Treasury += sorn;
        guild.Xp += sorn / Guilds.SornPerXp;
        int levelAfter = Guilds.Level(guild.Xp);
        string text = $"You gave {sorn.ToString("N0", CultureInfo.InvariantCulture)} sorn: +{tallies} Guild Tallies.";
        if (levelAfter > levelBefore)
        {
            guild.LastEvent = $"{guild.Name} reached level {levelAfter}.";
            text += $" {guild.Name} reached level {levelAfter}!";
        }
        _db.Ledger.Add(Entry(account.Id, null, "guild-donate", $"guild={id} sorn={sorn} tallies={tallies} treasury={guild.Treasury} xp={guild.Xp}", -sorn, request.RequestId));
        await SaveAsync(ct);
        await tx.CommitAsync(ct);
        return await ViewAsync(account, null, text, ct);
    }

    public async Task<GuildViewDto> RaiseSkillAsync(Account account, GuildSkillRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        RequireManager(account);
        if (!Enum.IsDefined(request.Skill)) throw new GameException("bad_skill", "Unknown guild skill.");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        Guild guild = await LockGuildAsync(account.GuildId!.Value, ct);
        int current = request.Skill == GuildSkill.Plunder ? guild.Plunder : guild.Muster;
        if (Guilds.SkillProblem(request.Skill, current, Guilds.Level(guild.Xp), guild.Treasury) is string problem) throw new GameException("skill", problem);
        long cost = Guilds.SkillCost(request.Skill, current + 1);
        guild.Treasury -= cost;
        if (request.Skill == GuildSkill.Plunder) guild.Plunder++; else guild.Muster++;
        string text = request.Skill == GuildSkill.Plunder
            ? $"Plunder {guild.Plunder}: every member hunts with +{guild.Plunder}% sorn."
            : $"Muster {guild.Muster}: room for {Guilds.MaxMembers(guild.Muster)} members.";
        guild.LastEvent = text;
        _db.Ledger.Add(Entry(account.Id, null, "guild-skill", $"guild={guild.Id} skill={request.Skill} level={current + 1} cost={cost}", 0, request.RequestId));
        await SaveAsync(ct);
        await tx.CommitAsync(ct);
        return await ViewAsync(account, null, text, ct);
    }

    public async Task<GuildViewDto> GuildBuyAsync(Account account, GuildShopRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        GuildShopItem item = Guilds.ShopItem(request.ItemId) ?? throw new GameException("no_item", "Unknown guild shop item.");
        var inventory = Snapshot(account);
        int tallies = account.Tallies;
        try { Guilds.Buy(item, ref tallies, inventory); }
        catch (InvalidOperationException ex) { throw new GameException("shop", ex.Message); }
        account.Tallies = tallies;
        Apply(account, inventory);
        _db.Ledger.Add(Entry(account.Id, null, "guild-shop", $"{item.Name} tallies={item.Tallies}", 0, request.RequestId));
        await SaveAsync(ct);
        return await ViewAsync(account, null, "Bought " + item.Name + ".", ct);
    }

    public async Task<GuildViewDto> GuildSettingsAsync(Account account, GuildSettingsRequest request, CancellationToken ct)
    {
        await EnsureFreshRequestAsync(account, request.RequestId, ct);
        RequireManager(account);
        if (!Guilds.Colors.Contains(request.Color)) throw new GameException("bad_color", "Choose one of the guild colours.");
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        Guild guild = await LockGuildAsync(account.GuildId!.Value, ct);
        guild.Open = request.Open;
        guild.Color = request.Color;
        _db.Ledger.Add(Entry(account.Id, null, "guild-settings", $"guild={guild.Id} open={request.Open} color={request.Color}", 0, request.RequestId));
        await SaveAsync(ct);
        await tx.CommitAsync(ct);
        return await ViewAsync(account, null, guild.Open ? "The gates are open: anyone may join." : "The gates are shut: no one new may join.", ct);
    }
}
