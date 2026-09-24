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
    public DbSet<BossHit> BossHits => Set<BossHit>();
    public DbSet<BannerScore> BannerScores => Set<BannerScore>();
    public DbSet<Fortress> Fortresses => Set<Fortress>();
    public DbSet<Guild> Guilds => Set<Guild>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<ChatReport> ChatReports => Set<ChatReport>();
    public DbSet<GuildRequest> GuildRequests => Set<GuildRequest>();
    public DbSet<MarketListing> MarketListings => Set<MarketListing>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<AdminAction> AdminActions => Set<AdminAction>();
    public DbSet<ExternalLogin> ExternalLogins => Set<ExternalLogin>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Account>(e =>
        {
            e.Property(a => a.Id).ValueGeneratedNever();
            e.HasIndex(a => a.DeviceToken).IsUnique();
            e.HasIndex(a => a.SessionToken);
            e.HasIndex(a => new { a.CreatedIp, a.CreatedUtc });
            e.HasIndex(a => a.GuildId);
            e.HasIndex(a => a.Email).IsUnique();
            // Optimistic concurrency on PostgreSQL's xmin system column: two requests for one account never both win.
            e.Property(a => a.Version).IsRowVersion();
            e.HasMany(a => a.Items).WithOne().HasForeignKey(i => i.OwnerId);
            e.Navigation(a => a.Items).AutoInclude();
            e.Ignore(a => a.Weapon);
        });

        b.Entity<Item>(e =>
        {
            // Ids are minted in code; without this EF treats a pre-set Guid added via navigation as an existing row.
            e.Property(i => i.Id).ValueGeneratedNever();
            e.HasIndex(i => i.OwnerId);
            e.Property(i => i.Rarity).HasConversion<int>();
            e.Property(i => i.Slot).HasConversion<int>();
        });

        b.Entity<BossClock>(e =>
        {
            e.HasKey(c => c.BossId);
            e.Property(c => c.BossId).ValueGeneratedNever();
        });

        b.Entity<ClientLog>(e => e.HasIndex(l => new { l.AccountId, l.Utc }));

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
            e.HasIndex(l => l.AccountId);
        });
        b.Entity<ChatReport>(e => e.HasIndex(r => new { r.MessageId, r.ReporterId }).IsUnique());
        b.Entity<GuildRequest>(e =>
        {
            e.HasIndex(r => new { r.GuildId, r.AccountId }).IsUnique();
            e.HasIndex(r => r.AccountId);
        });
        b.Entity<MarketListing>(e =>
        {
            e.HasIndex(l => new { l.Status, l.Slot, l.Price });
            e.HasIndex(l => new { l.Status, l.ExpiresUtc });
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
        });

        b.Entity<LedgerEntry>(e =>
        {
            e.HasIndex(l => new { l.AccountId, l.RequestId }).IsUnique();
            e.HasIndex(l => new { l.AccountId, l.Utc });
        });
    }
}
