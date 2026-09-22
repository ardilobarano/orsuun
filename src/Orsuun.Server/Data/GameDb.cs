using Microsoft.EntityFrameworkCore;

namespace Orsuun.Server.Data;

public sealed class GameDb : DbContext
{
    public GameDb(DbContextOptions<GameDb> options) : base(options) { }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<LedgerEntry> Ledger => Set<LedgerEntry>();
    public DbSet<BossClock> BossClocks => Set<BossClock>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Account>(e =>
        {
            e.Property(a => a.Id).ValueGeneratedNever();
            e.HasIndex(a => a.DeviceToken).IsUnique();
            e.HasIndex(a => a.SessionToken);
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

        b.Entity<LedgerEntry>(e =>
        {
            e.HasIndex(l => new { l.AccountId, l.RequestId }).IsUnique();
            e.HasIndex(l => new { l.AccountId, l.Utc });
        });
    }
}
