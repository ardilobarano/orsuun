using Microsoft.EntityFrameworkCore;

namespace Orsuun.Server.Data;

public sealed class GameDb : DbContext
{
    public GameDb(DbContextOptions<GameDb> options) : base(options) { }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<LedgerEntry> Ledger => Set<LedgerEntry>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Account>(e =>
        {
            e.HasIndex(a => a.DeviceToken).IsUnique();
            e.HasIndex(a => a.SessionToken);
            // Optimistic concurrency on PostgreSQL's xmin system column: two requests for one account never both win.
            e.Property(a => a.Version).IsRowVersion();
            e.HasOne(a => a.EquippedWeapon).WithMany().HasForeignKey(a => a.EquippedWeaponId);
        });

        b.Entity<Item>(e =>
        {
            e.HasIndex(i => i.OwnerId);
            e.Property(i => i.Rarity).HasConversion<int>();
        });

        b.Entity<LedgerEntry>(e =>
        {
            e.HasIndex(l => new { l.AccountId, l.RequestId }).IsUnique();
            e.HasIndex(l => new { l.AccountId, l.Utc });
        });
    }
}
