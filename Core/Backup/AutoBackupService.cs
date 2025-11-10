// 📁 Core/Backup/AutoBackupService.cs
// ===== شروع کد =====

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Timer = System.Timers.Timer;

namespace TradingJournal.Core.Backup
{
    public class AutoBackupService : IHostedService, IDisposable
    {
        private readonly BackupService _backupService;
        private readonly ILogger<AutoBackupService> _logger;
        private Timer _timer;
        private readonly AutoBackupSettings _settings;
        
        public event EventHandler<AutoBackupEventArgs> BackupStarted;
        public event EventHandler<AutoBackupEventArgs> BackupCompleted;
        
        public AutoBackupService(
            BackupService backupService, 
            ILogger<AutoBackupService> logger,
            AutoBackupSettings settings)
        {
            _backupService = backupService;
            _logger = logger;
            _settings = settings;
        }
        
        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (!_settings.IsEnabled)
            {
                _logger.LogInformation("Auto backup is disabled");
                return Task.CompletedTask;
            }
            
            _logger.LogInformation($"Auto backup service started. Interval: {_settings.Interval}");
            
            _timer = new Timer(GetIntervalMilliseconds(_settings.Interval));
            _timer.Elapsed += async (sender, e) => await PerformAutoBackup();
            _timer.Start();
            
            // اجرای اولین بکاپ بعد از شروع
            if (_settings.BackupOnStartup)
            {
                Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(10)); // صبر برای آماده شدن برنامه
                    await PerformAutoBackup();
                });
            }
            
            return Task.CompletedTask;
        }
        
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Auto backup service stopping");
            _timer?.Stop();
            return Task.CompletedTask;
        }
        
        private async Task PerformAutoBackup()
        {
            try
            {
                _logger.LogInformation("Starting automatic backup");
                
                BackupStarted?.Invoke(this, new AutoBackupEventArgs
                {
                    StartTime = DateTime.Now,
                    Type = BackupType.Automatic
                });
                
                var options = new BackupOptions
                {
                    Type = BackupType.Automatic,
                    Description = $"Automatic backup at {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                    IncludeDatabase = _settings.IncludeDatabase,
                    IncludeImages = _settings.IncludeImages,
                    IncludeSettings = _settings.IncludeSettings,
                    IncludePlugins = _settings.IncludePlugins,
                    Encrypt = _settings.Encrypt,
                    Password = _settings.Password
                };
                
                var result = await _backupService.CreateBackupAsync(options);
                
                if (result.Success)
                {
                    _logger.LogInformation($"Automatic backup completed successfully: {result.BackupPath}");
                    
                    // حذف بکاپ‌های قدیمی
                    if (_settings.RetentionDays > 0)
                    {
                        await CleanOldBackups();
                    }
                }
                else
                {
                    _logger.LogError($"Automatic backup failed: {result.ErrorMessage}");
                }
                
                BackupCompleted?.Invoke(this, new AutoBackupEventArgs
                {
                    StartTime = DateTime.Now,
                    Type = BackupType.Automatic,
                    Result = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during automatic backup");
            }
        }
        
        private async Task CleanOldBackups()
        {
            try
            {
                var backups = await _backupService.GetBackupListAsync();
                var cutoffDate = DateTime.Now.AddDays(-_settings.RetentionDays);
                
                foreach (var backup in backups)
                {
                    if (backup.Metadata?.Type == BackupType.Automatic && 
                        backup.CreatedAt < cutoffDate)
                    {
                        await _backupService.DeleteBackupAsync(backup.FilePath);
                        _logger.LogInformation($"Deleted old backup: {backup.FileName}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning old backups");
            }
        }
        
        private double GetIntervalMilliseconds(BackupInterval interval)
        {
            return interval switch
            {
                BackupInterval.Hourly => TimeSpan.FromHours(1).TotalMilliseconds,
                BackupInterval.Daily => TimeSpan.FromDays(1).TotalMilliseconds,
                BackupInterval.Weekly => TimeSpan.FromDays(7).TotalMilliseconds,
                BackupInterval.Monthly => TimeSpan.FromDays(30).TotalMilliseconds,
                _ => TimeSpan.FromDays(1).TotalMilliseconds
            };
        }
        
        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
    
    public class AutoBackupSettings
    {
        public bool IsEnabled { get; set; } = true;
        public BackupInterval Interval { get; set; } = BackupInterval.Daily;
        public bool BackupOnStartup { get; set; } = false;
        public bool IncludeDatabase { get; set; } = true;
        public bool IncludeImages { get; set; } = true;
        public bool IncludeSettings { get; set; } = true;
        public bool IncludePlugins { get; set; } = false;
        public bool Encrypt { get; set; } = false;
        public string Password { get; set; }
        public int RetentionDays { get; set; } = 30;
        public int MaxBackupCount { get; set; } = 10;
    }
    
    public enum BackupInterval
    {
        Hourly,
        Daily,
        Weekly,
        Monthly
    }
    
    public class AutoBackupEventArgs : EventArgs
    {
        public DateTime StartTime { get; set; }
        public BackupType Type { get; set; }
        public BackupResult Result { get; set; }
    }
}

// ===== پایان کد =====