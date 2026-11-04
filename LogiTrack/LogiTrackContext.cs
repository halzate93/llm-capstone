using LogiTrack.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LogiTrack;

public class LogiTrackContext : IdentityDbContext<ApplicationUser>
{
    public LogiTrackContext()
    {
    }

    public LogiTrackContext(DbContextOptions<LogiTrackContext> options)
        : base(options)
    {
    }

    public DbSet<InventoryItem> InventoryItems { get; set; } = null!;
    public DbSet<Order> Orders { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        if (!options.IsConfigured)
        {
            options.UseSqlite("Data Source=logitrack.db");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(o => o.OrderId);
            entity.Property(o => o.CustomerName).IsRequired().HasMaxLength(150);
            entity.Property(o => o.SessionId).HasMaxLength(100);
            entity.Property(o => o.CreatedByUserId).HasMaxLength(100);
            entity.HasIndex(o => o.DatePlaced);
            entity.HasIndex(o => o.SessionId);
            entity.HasMany(o => o.Items)
                  .WithOne(i => i.Order)
                  .HasForeignKey(i => i.OrderId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<InventoryItem>(entity =>
        {
            entity.HasKey(i => i.ItemId);
            entity.Property(i => i.Name).IsRequired().HasMaxLength(150);
            entity.Property(i => i.Location).IsRequired().HasMaxLength(100);
            entity.HasIndex(i => i.Name);
            entity.HasIndex(i => i.OrderId);
        });
    }
}
