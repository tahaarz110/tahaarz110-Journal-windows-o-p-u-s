// ابتدای فایل: Data/DatabaseContext.cs
// مسیر: /Data/DatabaseContext.cs

using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TradingJournal.Core.Configuration;
using TradingJournal.Data.Models;

namespace TradingJournal.Data
{
    public class DatabaseContext : DbContext
    {
        public DbSet<Trade> Trades { get; set; }
        public DbSet<TradeImage> TradeImages { get; set; }
        public DbSet<DynamicField> DynamicFields { get; set; }
        public DbSet<TabConfiguration> TabConfigurations { get; set; }
        public DbSet<WidgetConfiguration> WidgetConfigurations { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var dbPath = AppSettings.Instance.DatabasePath;
                optionsBuilder.UseSqlite($"Data Source={dbPath}");
                optionsBuilder.EnableSensitiveDataLogging();
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Trade configuration
            modelBuilder.Entity<Trade>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Symbol);
                entity.HasIndex(e => e.EntryDate);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.IsDeleted);

                entity.Property(e => e.EntryPrice).HasPrecision(18, 8);
                entity.Property(e => e.ExitPrice).HasPrecision(18, 8);
                entity.Property(e => e.Volume).HasPrecision(18, 8);
                entity.Property(e => e.StopLoss).HasPrecision(18, 8);
                entity.Property(e => e.TakeProfit).HasPrecision(18, 8);
                entity.Property(e => e.Commission).HasPrecision(18, 4);
                entity.Property(e => e.Swap).HasPrecision(18, 4);
                entity.Property(e => e.ProfitLoss).HasPrecision(18, 4);
                entity.Property(e => e.ProfitLossPercent).HasPrecision(18, 4);
                entity.Property(e => e.RiskRewardRatio).HasPrecision(18, 4);
                entity.Property(e => e.RiskAmount).HasPrecision(18, 4);
                entity.Property(e => e.RiskPercent).HasPrecision(18, 4);
                entity.Property(e => e.AccountBalance).HasPrecision(18, 4);

                // Relationships
                entity.HasMany(e => e.Images)
                    .WithOne(e => e.Trade)
                    .HasForeignKey(e => e.TradeId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // TradeImage configuration
            modelBuilder.Entity<TradeImage>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.TradeId);
            });

            // DynamicField configuration
            modelBuilder.Entity<DynamicField>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.FieldName).IsUnique();
                entity.HasIndex(e => e.OrderIndex);
            });

            // TabConfiguration configuration
            modelBuilder.Entity<TabConfiguration>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.TabKey).IsUnique();
                entity.HasIndex(e => e.OrderIndex);
            });

            // WidgetConfiguration configuration
            modelBuilder.Entity<WidgetConfiguration>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.WidgetKey).IsUnique();
                entity.HasIndex(e => e.TabKey);
            });

            // Global query filters for soft delete
            modelBuilder.Entity<Trade>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<TradeImage>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<DynamicField>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<TabConfiguration>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<WidgetConfiguration>().HasQueryFilter(e => !e.IsDeleted);
        }

        public override int SaveChanges()
        {
            UpdateTimestamps();
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateTimestamps();
            return await base.SaveChangesAsync(cancellationToken);
        }

        private void UpdateTimestamps()
        {
            var entries = ChangeTracker.Entries<BaseEntity>();
            var now = DateTime.Now;

            foreach (var entry in entries)
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedAt = now;
                }
                else if (entry.State == EntityState.Modified)
                {
                    entry.Entity.UpdatedAt = now;
                }
            }
        }
    }
}

// پایان فایل: Data/DatabaseContext.cs