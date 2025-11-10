// 📁 Core/Backup/BackupService.cs
// ===== شروع کد =====

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TradingJournal.Core.Backup
{
    public class BackupService
    {
        private readonly string _databasePath;
        private readonly string _imagesPath;
        private readonly string _settingsPath;
        private readonly string _pluginsPath;
        private readonly string _backupsPath;
        
        public event EventHandler<BackupProgressEventArgs> ProgressChanged;
        public event EventHandler<BackupEventArgs> BackupCompleted;
        public event EventHandler<BackupEventArgs> RestoreCompleted;
        
        public BackupService()
        {
            var appPath = AppDomain.CurrentDomain.BaseDirectory;
            _databasePath = Path.Combine(appPath, "Data", "TradingJournal.db");
            _imagesPath = Path.Combine(appPath, "Images");
            _settingsPath = Path.Combine(appPath, "Settings");
            _pluginsPath = Path.Combine(appPath, "Plugins");
            _backupsPath = Path.Combine(appPath, "Backups");
            
            if (!Directory.Exists(_backupsPath))
            {
                Directory.CreateDirectory(_backupsPath);
            }
        }
        
        /// <summary>
        /// ایجاد بکاپ کامل
        /// </summary>
        public async Task<BackupResult> CreateBackupAsync(BackupOptions options = null)
        {
            options ??= new BackupOptions();
            var result = new BackupResult();
            
            try
            {
                ReportProgress("شروع فرآیند بکاپ‌گیری", 0);
                
                // ایجاد پوشه موقت
                var tempPath = Path.Combine(Path.GetTempPath(), $"Backup_{DateTime.Now:yyyyMMddHHmmss}");
                Directory.CreateDirectory(tempPath);
                
                // ایجاد metadata
                var metadata = new BackupMetadata
                {
                    Version = "1.0.0",
                    CreatedAt = DateTime.Now,
                    MachineName = Environment.MachineName,
                    Description = options.Description,
                    Type = options.Type,
                    IncludedComponents = new List<string>()
                };
                
                // 1. کپی دیتابیس
                if (options.IncludeDatabase)
                {
                    ReportProgress("در حال کپی دیتابیس...", 20);
                    var dbBackupPath = Path.Combine(tempPath, "Database");
                    Directory.CreateDirectory(dbBackupPath);
                    
                    await CopyDatabaseAsync(_databasePath, Path.Combine(dbBackupPath, "TradingJournal.db"));
                    metadata.IncludedComponents.Add("Database");
                    metadata.DatabaseInfo = new DatabaseInfo
                    {
                        RecordCount = await GetRecordCountAsync(),
                        Size = new FileInfo(_databasePath).Length
                    };
                }
                
                // 2. کپی تصاویر
                if (options.IncludeImages && Directory.Exists(_imagesPath))
                {
                    ReportProgress("در حال کپی تصاویر...", 40);
                    var imagesBackupPath = Path.Combine(tempPath, "Images");
                    await CopyDirectoryAsync(_imagesPath, imagesBackupPath);
                    metadata.IncludedComponents.Add("Images");
                    metadata.ImagesInfo = new ImagesInfo
                    {
                        Count = Directory.GetFiles(_imagesPath, "*", SearchOption.AllDirectories).Length,
                        TotalSize = GetDirectorySize(_imagesPath)
                    };
                }
                
                // 3. کپی تنظیمات
                if (options.IncludeSettings && Directory.Exists(_settingsPath))
                {
                    ReportProgress("در حال کپی تنظیمات...", 60);
                    var settingsBackupPath = Path.Combine(tempPath, "Settings");
                    await CopyDirectoryAsync(_settingsPath, settingsBackupPath);
                    metadata.IncludedComponents.Add("Settings");
                }
                
                // 4. کپی پلاگین‌ها
                if (options.IncludePlugins && Directory.Exists(_pluginsPath))
                {
                    ReportProgress("در حال کپی پلاگین‌ها...", 70);
                    var pluginsBackupPath = Path.Combine(tempPath, "Plugins");
                    await CopyDirectoryAsync(_pluginsPath, pluginsBackupPath);
                    metadata.IncludedComponents.Add("Plugins");
                    metadata.PluginsInfo = new PluginsInfo
                    {
                        Count = Directory.GetDirectories(_pluginsPath).Length
                    };
                }
                
                // 5. ذخیره metadata
                var metadataJson = JsonConvert.SerializeObject(metadata, Formatting.Indented);
                await File.WriteAllTextAsync(Path.Combine(tempPath, "backup.meta"), metadataJson);
                
                // 6. فشرده‌سازی
                ReportProgress("در حال فشرده‌سازی...", 80);
                var backupFileName = GenerateBackupFileName(options);
                var backupFilePath = Path.Combine(_backupsPath, backupFileName);
                
                if (File.Exists(backupFilePath))
                {
                    File.Delete(backupFilePath);
                }
                
                ZipFile.CreateFromDirectory(tempPath, backupFilePath, CompressionLevel.Optimal, false);
                
                // 7. رمزنگاری (در صورت نیاز)
                if (options.Encrypt && !string.IsNullOrEmpty(options.Password))
                {
                    ReportProgress("در حال رمزنگاری...", 90);
                    await EncryptFileAsync(backupFilePath, options.Password);
                }
                
                // 8. پاکسازی پوشه موقت
                ReportProgress("در حال پاکسازی...", 95);
                Directory.Delete(tempPath, true);
                
                // 9. محاسبه checksum
                var checksum = await CalculateChecksumAsync(backupFilePath);
                
                result.Success = true;
                result.BackupPath = backupFilePath;
                result.BackupSize = new FileInfo(backupFilePath).Length;
                result.Checksum = checksum;
                result.Metadata = metadata;
                
                ReportProgress("بکاپ با موفقیت تکمیل شد", 100);
                BackupCompleted?.Invoke(this, new BackupEventArgs { Result = result });
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                ReportProgress($"خطا: {ex.Message}", -1);
            }
            
            return result;
        }
        
        /// <summary>
        /// بازیابی از بکاپ
        /// </summary>
        public async Task<RestoreResult> RestoreBackupAsync(string backupPath, RestoreOptions options = null)
        {
            options ??= new RestoreOptions();
            var result = new RestoreResult();
            
            try
            {
                ReportProgress("شروع فرآیند بازیابی", 0);
                
                if (!File.Exists(backupPath))
                {
                    throw new FileNotFoundException("فایل بکاپ یافت نشد");
                }
                
                // 1. بررسی رمزنگاری
                var isEncrypted = await IsFileEncryptedAsync(backupPath);
                if (isEncrypted)
                {
                    if (string.IsNullOrEmpty(options.Password))
                    {
                        throw new InvalidOperationException("فایل رمزنگاری شده است. رمز عبور را وارد کنید");
                    }
                    
                    ReportProgress("در حال رمزگشایی...", 10);
                    backupPath = await DecryptFileAsync(backupPath, options.Password);
                }
                
                // 2. استخراج به پوشه موقت
                ReportProgress("در حال استخراج فایل‌ها...", 20);
                var tempPath = Path.Combine(Path.GetTempPath(), $"Restore_{DateTime.Now:yyyyMMddHHmmss}");
                ZipFile.ExtractToDirectory(backupPath, tempPath);
                
                // 3. خواندن metadata
                var metadataPath = Path.Combine(tempPath, "backup.meta");
                if (!File.Exists(metadataPath))
                {
                    throw new InvalidOperationException("فایل بکاپ معتبر نیست (metadata یافت نشد)");
                }
                
                var metadataJson = await File.ReadAllTextAsync(metadataPath);
                var metadata = JsonConvert.DeserializeObject<BackupMetadata>(metadataJson);
                
                // 4. ایجاد بکاپ از وضعیت فعلی (در صورت درخواست)
                if (options.CreateBackupBeforeRestore)
                {
                    ReportProgress("در حال ایجاد بکاپ از وضعیت فعلی...", 30);
                    await CreateBackupAsync(new BackupOptions
                    {
                        Type = BackupType.BeforeRestore,
                        Description = $"Auto backup before restore at {DateTime.Now}"
                    });
                }
                
                // 5. بازیابی دیتابیس
                if (metadata.IncludedComponents.Contains("Database") && options.RestoreDatabase)
                {
                    ReportProgress("در حال بازیابی دیتابیس...", 50);
                    var dbSourcePath = Path.Combine(tempPath, "Database", "TradingJournal.db");
                    
                    // بستن اتصالات دیتابیس
                    await CloseAllDatabaseConnectionsAsync();
                    
                    // کپی دیتابیس
                    File.Copy(dbSourcePath, _databasePath, true);
                    result.RestoredComponents.Add("Database");
                }
                
                // 6. بازیابی تصاویر
                if (metadata.IncludedComponents.Contains("Images") && options.RestoreImages)
                {
                    ReportProgress("در حال بازیابی تصاویر...", 60);
                    var imagesSourcePath = Path.Combine(tempPath, "Images");
                    
                    if (options.ClearExistingImages && Directory.Exists(_imagesPath))
                    {
                        Directory.Delete(_imagesPath, true);
                    }
                    
                    await CopyDirectoryAsync(imagesSourcePath, _imagesPath);
                    result.RestoredComponents.Add("Images");
                }
                
                // 7. بازیابی تنظیمات
                if (metadata.IncludedComponents.Contains("Settings") && options.RestoreSettings)
                {
                    ReportProgress("در حال بازیابی تنظیمات...", 70);
                    var settingsSourcePath = Path.Combine(tempPath, "Settings");
                    await CopyDirectoryAsync(settingsSourcePath, _settingsPath);
                    result.RestoredComponents.Add("Settings");
                }
                
                // 8. بازیابی پلاگین‌ها
                if (metadata.IncludedComponents.Contains("Plugins") && options.RestorePlugins)
                {
                    ReportProgress("در حال بازیابی پلاگین‌ها...", 80);
                    var pluginsSourcePath = Path.Combine(tempPath, "Plugins");
                    await CopyDirectoryAsync(pluginsSourcePath, _pluginsPath);
                    result.RestoredComponents.Add("Plugins");
                }
                
                // 9. پاکسازی
                ReportProgress("در حال پاکسازی...", 90);
                Directory.Delete(tempPath, true);
                
                // 10. پاکسازی فایل موقت رمزگشایی شده
                if (isEncrypted)
                {
                    File.Delete(backupPath);
                }
                
                result.Success = true;
                result.RestoredFrom = backupPath;
                result.RestoreDate = DateTime.Now;
                result.Metadata = metadata;
                
                ReportProgress("بازیابی با موفقیت انجام شد", 100);
                RestoreCompleted?.Invoke(this, new BackupEventArgs { RestoreResult = result });
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                ReportProgress($"خطا: {ex.Message}", -1);
            }
            
            return result;
        }
        
        /// <summary>
        /// دریافت لیست بکاپ‌ها
        /// </summary>
        public async Task<List<BackupInfo>> GetBackupListAsync()
        {
            var backups = new List<BackupInfo>();
            
            if (!Directory.Exists(_backupsPath))
                return backups;
            
            var files = Directory.GetFiles(_backupsPath, "*.tjb")
                .Concat(Directory.GetFiles(_backupsPath, "*.tjb.enc"));
            
            foreach (var file in files)
            {
                try
                {
                    var info = await GetBackupInfoAsync(file);
                    if (info != null)
                        backups.Add(info);
                }
                catch
                {
                    // فایل معتبر نیست، نادیده می‌گیریم
                }
            }
            
            return backups.OrderByDescending(b => b.CreatedAt).ToList();
        }
        
        /// <summary>
        /// دریافت اطلاعات یک بکاپ
        /// </summary>
        public async Task<BackupInfo> GetBackupInfoAsync(string backupPath)
        {
            var info = new BackupInfo
            {
                FilePath = backupPath,
                FileName = Path.GetFileName(backupPath),
                FileSize = new FileInfo(backupPath).Length,
                IsEncrypted = Path.GetExtension(backupPath) == ".enc"
            };
            
            // تلاش برای خواندن metadata (اگر رمزنگاری نشده باشد)
            if (!info.IsEncrypted)
            {
                try
                {
                    using (var archive = ZipFile.OpenRead(backupPath))
                    {
                        var metaEntry = archive.GetEntry("backup.meta");
                        if (metaEntry != null)
                        {
                            using (var stream = metaEntry.Open())
                            using (var reader = new StreamReader(stream))
                            {
                                var json = await reader.ReadToEndAsync();
                                info.Metadata = JsonConvert.DeserializeObject<BackupMetadata>(json);
                                info.CreatedAt = info.Metadata.CreatedAt;
                                info.Description = info.Metadata.Description;
                            }
                        }
                    }
                }
                catch
                {
                    // نمی‌توانیم metadata را بخوانیم
                }
            }
            
            // اگر metadata نداریم، از نام فایل تاریخ را استخراج می‌کنیم
            if (info.CreatedAt == default)
            {
                info.CreatedAt = File.GetCreationTime(backupPath);
            }
            
            return info;
        }
        
        /// <summary>
        /// حذف بکاپ
        /// </summary>
        public async Task<bool> DeleteBackupAsync(string backupPath)
        {
            try
            {
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
        
        // Helper Methods
        
        private async Task CopyDatabaseAsync(string source, string destination)
        {
            // بستن کانکشن‌های دیتابیس قبل از کپی
            GC.Collect();
            GC.WaitForPendingFinalizers();
            
            File.Copy(source, destination, true);
        }
        
        private async Task CopyDirectoryAsync(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);
            
            // کپی فایل‌ها
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var fileName = Path.GetFileName(file);
                var destFile = Path.Combine(targetDir, fileName);
                File.Copy(file, destFile, true);
            }
            
            // کپی زیرپوشه‌ها
            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                var dirName = Path.GetFileName(subDir);
                await CopyDirectoryAsync(subDir, Path.Combine(targetDir, dirName));
            }
        }
        
        private long GetDirectorySize(string path)
        {
            var dirInfo = new DirectoryInfo(path);
            return dirInfo.GetFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);
        }
        
        private async Task<int> GetRecordCountAsync()
        {
            // این باید از دیتابیس تعداد رکوردها را بخواند
            return 0; // TODO: implement
        }
        
        private string GenerateBackupFileName(BackupOptions options)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var type = options.Type.ToString().ToLower();
            var extension = options.Encrypt ? ".tjb.enc" : ".tjb";
            
            return $"backup_{type}_{timestamp}{extension}";
        }
        
        private async Task<string> CalculateChecksumAsync(string filePath)
        {
            using (var sha256 = SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                var hash = await Task.Run(() => sha256.ComputeHash(stream));
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }
        
        private async Task<bool> IsFileEncryptedAsync(string filePath)
        {
            return Path.GetExtension(filePath) == ".enc";
        }
        
        private async Task CloseAllDatabaseConnectionsAsync()
        {
            // Force close all database connections
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Task.Delay(100);
        }
        
        private void ReportProgress(string message, int percentage)
        {
            ProgressChanged?.Invoke(this, new BackupProgressEventArgs
            {
                Message = message,
                Percentage = percentage
            });
        }
        
        // رمزنگاری و رمزگشایی در بخش بعدی...
    }
}

// ===== پایان کد =====