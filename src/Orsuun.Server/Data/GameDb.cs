using Microsoft.EntityFrameworkCore;

namespace Orsuun.Server.Data;

public sealed class GameDb : DbContext
{
    public GameDb(DbContextOptions<GameDb> options) : base(options) { }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<LedgerEntry> Ledger => Set<LedgerEntry>();
    public DbSet<BossClock> BossClocks => Set<BossClock>();
    public DbSet<ClientLog> ClientLogs => Set<ClientLog>();
    public DbSet<Milestone> Milestones => Set<Milestone>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<NameReport> NameReports => Set<NameReport>();
    public DbSet<PushToken> PushTokens => Set<PushToken>();
    public DbSet<BossHit> BossHits => Set<BossHit>();
    public DbSet<BannerScore> BannerScores => Set<BannerScore>();
    public DbSet<Fortress> Fortresses => Set<Fortress>();
    public DbSet<Guild> Guilds => Set<Guild>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<ChatReport> ChatReports => Set<ChatReport>();
    public DbSet<PrivateMessage> PrivateMessages => Set<PrivateMessage>();
    public DbSet<Letter> Letters => Set<Letter>();
    public DbSet<GuildRequest> GuildRequests => Set<GuildRequest>();
    public DbSet<GuildInvite> GuildInvites => Set<GuildInvite>();
    public DbSet<Friendship> Friendships => Set<Friendship>();
    public DbSet<PitSeasonRecord> PitSeasons => Set<PitSeasonRecord>();
    public DbSet<BookStack> BookStacks => Set<BookStack>();
    public DbSet<MarketListing> MarketListings => Set<MarketListing>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<AdminAction> AdminActions => Set<AdminAction>();
    public DbSet<ExternalLogin> ExternalLogins => Set<ExternalLogin>();
    public DbSet<GuildWarSignup> GuildWarSignups => Set<GuildWarSignup>();
    public DbSet<GuildWarNight> GuildWarNights => Set<GuildWarNight>();
    public DbSet<GuildWar> GuildWars => Set<GuildWar>();
    public DbSet<GuildWarEntry> GuildWarEntries => Set<GuildWarEntry>();
    public DbSet<FortressBid> FortressBids => Set<FortressBid>();
    public DbSet<DungeonRun> DungeonRuns => Set<DungeonRun>();
    public DbSet<Login> Logins => Set<Login>();
    public DbSet<TradeSession> Trades => Set<TradeSession>();
    public DbSet<WorldEvent> WorldEvents => Set<WorldEvent>();
    public DbSet<GuildRaid> GuildRaids => Set<GuildRaid>();
    public DbSet<GuildRaidHit> GuildRaidHits => Set<GuildRaidHit>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Account>(e =>
        {
            e.Property(a => a.Id).ValueGeneratedNever();
            e.HasIndex(a => a.DeviceToken).IsUnique();
            e.HasIndex(a => a.SessionToken);
            e.HasIndex(a => new { a.CreatedIp, a.CreatedUtc });
            e.HasIndex(a => a.GuildId);
            e.HasIndex(a => a.PitRating);
            e.HasIndex(a => a.LoginId);
            e.HasIndex(a => a.NameKey).IsUnique().HasFilter("\"NameKey\" <> ''");
            e.HasIndex(a => a.InviteCode).IsUnique().HasFilter("\"InviteCode\" <> ''");
            e.HasIndex(a => a.InvitedById);
            // Optimistic concurrency on PostgreSQL's xmin system column: two requests for one account never both win.
            e.Property(a => a.Version).IsRowVersion();
            e.HasMany(a => a.Items).WithOne().HasForeignKey(i => i.OwnerId);
            e.Navigation(a => a.Items).AutoInclude();
            e.HasMany(a => a.Books).WithOne().HasForeignKey(b => b.AccountId);
            e.Navigation(a => a.Books).AutoInclude();
            e.Ignore(a => a.Weapon);
        });

        b.Entity<Item>(e =>
        {
            // Ids are minted in code; without this EF treats a pre-set Guid added via navigation as an existing row.
            e.Property(i => i.Id).ValueGeneratedNever();
            e.HasIndex(i => i.OwnerId);
            e.HasIndex(i => i.DepotLoginId);
            e.Property(i => i.Rarity).HasConversion<int>();
            e.Property(i => i.Slot).HasConversion<int>();
        });

        b.Entity<BossClock>(e =>
        {
            e.HasKey(c => c.BossId);
            e.Property(c => c.BossId).ValueGeneratedNever();
        });

        b.Entity<ClientLog>(e => e.HasIndex(l => new { l.AccountId, l.Utc }));
        b.Entity<Milestone>(e => e.HasIndex(m => new { m.AccountId, m.Name }).IsUnique());
        b.Entity<Purchase>(e => { e.HasIndex(p => new { p.Store, p.TransactionId }).IsUnique(); e.HasIndex(p => p.LoginId); });
        b.Entity<PushToken>(e => { e.HasIndex(t => t.Token).IsUnique(); e.HasIndex(t => t.LoginId); });
        b.Entity<NameReport>(e => { e.HasIndex(r => new { r.Kind, r.TargetId, r.Name, r.ReporterId }).IsUnique(); e.HasIndex(r => r.Reviewed); });

        b.Entity<BossHit>(e => e.HasIndex(h => new { h.BossId, h.SpawnUtc }));
        b.Entity<BannerScore>(e => e.HasKey(s => new { s.Season, s.Banner }));
        b.Entity<Fortress>(e => e.Property(f => f.Id).ValueGeneratedNever());
        b.Entity<Guild>(e =>
        {
            e.Property(g => g.Id).ValueGeneratedNever();
            e.HasIndex(g => g.NameKey).IsUnique();
            e.HasIndex(g => g.Tag).IsUnique();
            e.HasIndex(g => g.Xp);
        });
        b.Entity<ChatMessage>(e =>
        {
            e.HasIndex(m => new { m.Channel, m.Id });
            e.HasIndex(m => new { m.Reports, m.Reviewed });
            e.HasIndex(m => m.AccountId);
        });
        b.Entity<AdminAction>(e => e.HasIndex(a => a.Utc));
        b.Entity<ExternalLogin>(e =>
        {
            e.HasIndex(l => new { l.Provider, l.Subject }).IsUnique();
            e.HasIndex(l => l.LoginId);
        });
        b.Entity<ChatReport>(e => e.HasIndex(r => new { r.MessageId, r.ReporterId }).IsUnique());
        b.Entity<PrivateMessage>(e =>
        {
            e.HasIndex(m => new { m.FromId, m.ToId, m.Id });
            e.HasIndex(m => new { m.ToId, m.Read });
        });
        b.Entity<Letter>(e =>
        {
            e.HasIndex(l => new { l.AccountId, l.Id });
            e.HasIndex(l => new { l.AccountId, l.Read });
        });
        b.Entity<GuildRequest>(e =>
        {
            e.HasIndex(r => new { r.GuildId, r.AccountId }).IsUnique();
            e.HasIndex(r => r.AccountId);
        });

        b.Entity<GuildInvite>(e =>
        {
            e.HasIndex(r => new { r.GuildId, r.AccountId }).IsUnique();
            e.HasIndex(r => r.AccountId);
        });

        b.Entity<BookStack>(e => e.HasKey(k => new { k.AccountId, k.BookId }));

        b.Entity<Friendship>(e =>
        {
            e.HasIndex(f => new { f.FromId, f.ToId }).IsUnique();
            e.HasIndex(f => f.ToId);
        });
        b.Entity<MarketListing>(e =>
        {
            e.HasIndex(l => new { l.Status, l.Slot, l.Price });
            e.HasIndex(l => new { l.Status, l.ExpiresUtc });
            e.HasIndex(l => new { l.Status, l.ClosedUtc });   // price histories read the recent sales
            e.HasIndex(l => new { l.SellerId, l.Status });
            e.HasIndex(l => l.ItemId);
            e.Property(l => l.Slot).HasConversion<int>();
            e.Property(l => l.Rarity).HasConversion<int>();
        });
        b.Entity<Device>(e =>
        {
            e.HasKey(d => d.Token);
            e.HasIndex(d => d.SessionToken);
            e.HasIndex(d => d.AccountId);
            e.HasIndex(d => d.LoginId);
        });
        b.Entity<Login>(e =>
        {
            e.Property(l => l.Id).ValueGeneratedNever();
            e.HasIndex(l => new { l.CreatedIp, l.CreatedUtc });
            e.HasIndex(l => l.Email).IsUnique();
        });

        b.Entity<GuildWarSignup>(e => e.HasKey(w => new { w.Night, w.GuildId }));
        b.Entity<GuildWarNight>(e => e.HasKey(n => n.Night));
        b.Entity<GuildRaid>(e => e.HasIndex(r => new { r.GuildId, r.Week }).IsUnique());
        b.Entity<GuildRaidHit>(e => e.HasIndex(h => new { h.RaidId, h.AccountId, h.Day }));
        b.Entity<WorldEvent>(e =>
        {
            e.HasIndex(w => new { w.Kind, w.StartsUtc }).IsUnique();
            e.HasIndex(w => w.EndsUtc);
        });
        b.Entity<GuildWar>(e =>
        {
            e.HasIndex(w => new { w.Night, w.GuildA });
            e.HasIndex(w => new { w.Night, w.GuildB });
            e.HasIndex(w => new { w.Result, w.EndsUtc });
            e.Ignore(w => w.Fronts);
        });
        b.Entity<GuildWarEntry>(e =>
        {
            e.HasKey(x => new { x.WarId, x.AccountId });
            e.HasIndex(x => x.AccountId);
        });
        b.Entity<FortressBid>(e =>
        {
            e.HasKey(x => new { x.Week, x.GuildId });
            e.HasIndex(x => new { x.Week, x.FortressId });
        });

        b.Entity<DungeonRun>(e => e.HasIndex(r => new { r.AccountId, r.State }));

        b.Entity<TradeSession>(e =>
        {
            e.HasIndex(t => new { t.FromId, t.State });
            e.HasIndex(t => new { t.ToId, t.State });
            e.Property(t => t.State).HasConversion<int>();
            e.Property(t => t.FromStep).HasConversion<int>();
            e.Property(t => t.ToStep).HasConversion<int>();
        });

        b.Entity<LedgerEntry>(e =>
        {
            e.HasIndex(l => new { l.AccountId, l.RequestId }).IsUnique();
            e.HasIndex(l => new { l.AccountId, l.Utc });
        });
    }
}
