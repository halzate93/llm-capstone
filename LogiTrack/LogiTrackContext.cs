using LogiTrack.Models;
using Microsoft.EntityFrameworkCore;

namespace LogiTrack;

public class LogiTrackContext : DbContext
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
        });
    }
}
