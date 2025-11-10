// مسیر فایل: Data/TradingJournalContext.cs
// ابتدای کد
using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using TradingJournal.Data.Models;

namespace TradingJournal.Data
{
    public class TradingJournalContext : DbContext
    {
        public DbSet<Trade> Trades { get; set; }
        public DbSet<TradeImage> TradeImages { get; set; }

        public TradingJournalContext()
        {
            Database.EnsureCreated();
        }

        public TradingJournalContext(DbContextOptions<TradingJournalContext> options)
            : base(options)
        {
            Database.EnsureCreated();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "TradingJournal.db");
                Directory.CreateDirectory(Path.GetDirectoryName(dbPath));
                
                optionsBuilder.UseSqlite($"Data Source={dbPath}");
                optionsBuilder.EnableSensitiveDataLogging();
                optionsBuilder.EnableDetailedErrors();
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // تنظیمات Trade
            modelBuilder.Entity<Trade>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Symbol).IsRequired().HasMaxLength(20);
                entity.Property(e => e.EntryPrice).HasPrecision(18, 5);
                entity.Property(e => e.ExitPrice).HasPrecision(18, 5);
                entity.Property(e => e.Volume).HasPrecision(18, 5);
                entity.Property(e => e.StopLoss).HasPrecision(18, 5);
                entity.Property(e => e.TakeProfit).HasPrecision(18, 5);
                entity.Property(e => e.Profit).HasPrecision(18, 2);
                entity.Property(e => e.Commission).HasPrecision(18, 2);
                entity.Property(e => e.Swap).HasPrecision(18, 2);
                entity.Property(e => e.RiskRewardRatio).HasPrecision(18, 2);
                entity.Property(e => e.RiskPercentage).HasPrecision(5, 2);
                
                entity.HasIndex(e => e.Symbol);
                entity.HasIndex(e => e.EntryDate);
                entity.HasIndex(e => e.Strategy);
                entity.HasIndex(e => e.AccountNumber);
                
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("datetime('now')");
            });

            // تنظیمات TradeImage
            modelBuilder.Entity<TradeImage>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
                entity.Property(e => e.FilePath).IsRequired();
                
                entity.HasOne(e => e.Trade)
                    .WithMany(t => t.Images)
                    .HasForeignKey(e => e.TradeId)
                    .OnDelete(DeleteBehavior.Cascade);
                    
                entity.HasIndex(e => e.TradeId);
            });

            // Seed Data برای تست
            SeedData(modelBuilder);
        }

        private void SeedData(ModelBuilder modelBuilder)
        {
            var random = new Random();
            var strategies = new[] { "Scalping", "Swing", "Position", "Day Trading" };
            var symbols = new[] { "EUR/USD", "GBP/USD", "USD/JPY", "AUD/USD", "USD/CHF" };
            var timeframes = new[] { "M1", "M5", "M15", "H1", "H4", "D1" };
            var emotions = new[] { "خونسرد", "مضطرب", "خوشحال", "عصبانی", "امیدوار" };

            var trades = new Trade[20];
            for (int i = 1; i <= 20; i++)
            {
                var entryDate = DateTime.Now.AddDays(-random.Next(1, 60));
                var isOpen = random.Next(100) > 80;
                var entryPrice = (decimal)(1.0000 + random.NextDouble() * 0.5);
                var exitPrice = isOpen ? null : (decimal?)(entryPrice + (decimal)(random.NextDouble() * 0.02 - 0.01));
                
                trades[i - 1] = new Trade
                {
                    Id = i,
                    Symbol = symbols[random.Next(symbols.Length)],
                    Type = (TradeType)random.Next(2),
                    EntryDate = entryDate,
                    EntryPrice = entryPrice,
                    Volume = (decimal)(0.01 * (1 + random.Next(10))),
                    ExitDate = isOpen ? null : entryDate.AddHours(random.Next(1, 48)),
                    ExitPrice = exitPrice,
                    StopLoss = entryPrice - (decimal)0.005,
                    TakeProfit = entryPrice + (decimal)0.010,
                    Profit = isOpen ? null : (decimal?)((double)(exitPrice - entryPrice) * 10000 * 0.1),
                    ProfitPips = isOpen ? null : (int?)((exitPrice - entryPrice) * 10000),
                    Commission = (decimal)(random.NextDouble() * 5),
                    Swap = (decimal)(random.NextDouble() * 2 - 1),
                    Strategy = strategies[random.Next(strategies.Length)],
                    Timeframe = timeframes[random.Next(timeframes.Length)],
                    Emotions = emotions[random.Next(emotions.Length)],
                    Rating = random.Next(1, 6),
                    RiskRewardRatio = (decimal)(1 + random.NextDouble() * 3),
                    RiskPercentage = (decimal)(0.5 + random.NextDouble() * 2),
                    AccountNumber = "1000" + random.Next(1000, 9999),
                    Platform = "MetaTrader 5",
                    CreatedAt = entryDate
                };
            }

            modelBuilder.Entity<Trade>().HasData(trades);
        }
    }

    // برای Migration
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TradingJournalContext>
    {
        public TradingJournalContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<TradingJournalContext>();
            var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "TradingJournal.db");
            optionsBuilder.UseSqlite($"Data Source={dbPath}");

            return new TradingJournalContext(optionsBuilder.Options);
        }
    }
}
// پایان کد