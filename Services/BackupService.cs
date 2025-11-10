// ابتدای فایل: Services/BackupService.cs
// مسیر: /Services/BackupService.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Serilog;
using TradingJournal.Core.Configuration;
using TradingJournal.Data;

namespace TradingJournal.Services
{
    public class BackupInfo
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public long FileSize { get; set; }
        public string Version { get; set; } = "1.0.0";
        public bool IsEncrypted { get; set; }
        public bool IsAutoBackup { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class BackupOptions
    {
        public bool IncludeDatabase { get; set; } = true;
        public bool IncludeImages { get; set; } = true;
        public bool IncludeSettings { get; set; } = true;
        public bool IncludeMetadata { get; set; } = true;
        public bool IncludePlugins { get; set; } = false;
        public bool Compress { get; set; } = true;
        public bool Encrypt { get; set; } = false;
        public string? Password { get; set; }
        public string? Description { get; set; }
    }

    public interface IBackupService
    {
        Task<string> CreateBackupAsync(BackupOptions options);
        Task<bool> RestoreBackupAsync(string backupPath, string? password = null);
        Task<List<BackupInfo>> GetBackupsAsync();
        Task<bool> DeleteBackupAsync(string backupPath);
        Task<bool> ValidateBackupAsync(string backupPath);
        Task<bool> ScheduleAutoBackupAsync(TimeSpan interval);
        void CancelAutoBackup();
    }

    public class BackupService : IBackupService
    {
        private readonly DatabaseContext _dbContext;
        private readonly string _backupPath;
        private readonly string _tempPath;
        private System.Timers.Timer? _autoBackupTimer;

        public BackupService()
        {
            _dbContext = new DatabaseContext();
            _backupPath = AppSettings.Instance.BackupPath;
            _tempPath = Path.Combine(Path.GetTempPath(), "TradingJournal", "Backup");
            
            Directory.CreateDirectory(_backupPath);
            Directory.CreateDirectory(_tempPath);
        }

        public async Task<string> CreateBackupAsync(BackupOptions options)
        {
            try
            {
                Log.Information("شروع ایجاد نسخه پشتیبان");

                // Create temp directory for this backup
                var backupId = Guid.NewGuid().ToString();
                var backupTempPath = Path.Combine(_tempPath, backupId);
                Directory.CreateDirectory(backupTempPath);

                // Create backup manifest
                var manifest = new
                {
                    Version = "1.0.0",
                    CreatedDate = DateTime.Now,
                    Options = options,
                    Contents = new List<string>()
                };

                // Backup database
                if (options.IncludeDatabase)
                {
                    await BackupDatabaseAsync(backupTempPath);
                    ((List<string>)manifest.Contents).Add("database.db");
                }

                // Backup images
                if (options.IncludeImages)
                {
                    await BackupImagesAsync(backupTempPath);
                    ((List<string>)manifest.Contents).Add("Images/");
                }

                // Backup settings
                if (options.IncludeSettings)
                {
                    await BackupSettingsAsync(backupTempPath);
                    ((List<string>)manifest.Contents).Add("settings.json");
                }

                // Backup metadata
                if (options.IncludeMetadata)
                {
                    await BackupMetadataAsync(backupTempPath);
                    ((List<string>)manifest.Contents).Add("Metadata/");
                }

                // Save manifest
                var manifestJson = JsonConvert.SerializeObject(manifest, Formatting.Indented);
                await File.WriteAllTextAsync(Path.Combine(backupTempPath, "manifest.json"), manifestJson);

                // Create final backup file
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var fileName = $"TradingJournal_Backup_{timestamp}.tjb";
                var finalPath = Path.Combine(_backupPath, fileName);

                if (options.Compress)
                {
                    if (options.Encrypt && !string.IsNullOrEmpty(options.Password))
                    {
                        await CreateEncryptedZipAsync(backupTempPath, finalPath, options.Password);
                    }
                    else
                    {
                        ZipFile.CreateFromDirectory(backupTempPath, finalPath, CompressionLevel.Optimal, false);
                    }
                }
                else
                {
                    // Move directory as-is
                    Directory.Move(backupTempPath, finalPath);
                }

                // Cleanup temp directory
                if (Directory.Exists(backupTempPath))
                {
                    Directory.Delete(backupTempPath, true);
                }

                // Manage backup retention
                await ManageBackupRetentionAsync();

                Log.Information($"نسخه پشتیبان با موفقیت ایجاد شد: {fileName}");
                
                // Send notification
                NotificationService.Instance.Show(
                    "پشتیبان‌گیری", 
                    "نسخه پشتیبان با موفقیت ایجاد شد",
                    NotificationType.Success
                );

                return finalPath;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "خطا در ایجاد نسخه پشتیبان");
                throw;
            }
        }

        private async Task BackupDatabaseAsync(string targetPath)
        {
            var dbPath = AppSettings.Instance.DatabasePath;
            var targetDbPath = Path.Combine(targetPath, "database.db");
            
            // Ensure all changes are saved
            await _dbContext.SaveChangesAsync();
            
            // Close connections
            await _dbContext.Database.CloseConnectionAsync();
            
            // Copy database file
            File.Copy(dbPath, targetDbPath, true);
            
            // Copy WAL and SHM files if they exist
            var walPath = dbPath + "-wal";
            var shmPath = dbPath + "-shm";
            
            if (File.Exists(walPath))
            {
                File.Copy(walPath, targetDbPath + "-wal", true);
            }
            
            if (File.Exists(shmPath))
            {
                File.Copy(shmPath, targetDbPath + "-shm", true);
            }
        }

        private async Task BackupImagesAsync(string targetPath)
        {
            var imagesPath = AppSettings.Instance.ImagesPath;
            var targetImagesPath = Path.Combine(targetPath, "Images");
            
            if (Directory.Exists(imagesPath))
            {
                await CopyDirectoryAsync(imagesPath, targetImagesPath);
            }
        }

        private async Task BackupSettingsAsync(string targetPath)
        {
            AppSettings.Save();
            
            var settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "TradingJournal",
                "settings.json"
            );
            
            if (File.Exists(settingsPath))
            {
                var targetSettingsPath = Path.Combine(targetPath, "settings.json");
                await Task.Run(() => File.Copy(settingsPath, targetSettingsPath, true));
            }
        }

        private async Task BackupMetadataAsync(string targetPath)
        {
            var metadataPath = AppSettings.Instance.MetadataPath;
            var targetMetadataPath = Path.Combine(targetPath, "Metadata");
            
            if (Directory.Exists(metadataPath))
            {
                await CopyDirectoryAsync(metadataPath, targetMetadataPath);
            }
        }

        private async Task CopyDirectoryAsync(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            // Copy files
            var files = Directory.GetFiles(sourceDir);
            var tasks = files.Select(file =>
            {
                var fileName = Path.GetFileName(file);
                var destFile = Path.Combine(targetDir, fileName);
                return Task.Run(() => File.Copy(file, destFile, true));
            });
            await Task.WhenAll(tasks);

            // Copy subdirectories
            var dirs = Directory.GetDirectories(sourceDir);
            var dirTasks = dirs.Select(dir =>
            {
                var dirName = Path.GetFileName(dir);
                var destDir = Path.Combine(targetDir, dirName);
                return CopyDirectoryAsync(dir, destDir);
            });
            await Task.WhenAll(dirTasks);
        }

        private async Task CreateEncryptedZipAsync(string sourceDir, string targetPath, string password)
        {
            await Task.Run(() =>
            {
                using var fs = new FileStream(targetPath, FileMode.Create);
                using var archive = new ZipArchive(fs, ZipArchiveMode.Create);
                
                var files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
                
                foreach (var file in files)
                {
                    var entryName = Path.GetRelativePath(sourceDir, file);
                    var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                    
                    using var entryStream = entry.Open();
                    var fileBytes = File.ReadAllBytes(file);
                    var encryptedBytes = EncryptData(fileBytes, password);
                    entryStream.Write(encryptedBytes, 0, encryptedBytes.Length);
                }
            });
        }

        private byte[] EncryptData(byte[] data, string password)
        {
            using var aes = Aes.Create();
            var salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }
            
            var key = new Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256);
            aes.Key = key.GetBytes(32);
            aes.IV = key.GetBytes(16);
            
            using var ms = new MemoryStream();
            ms.Write(salt, 0, salt.Length);
            
            using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
            {
                cs.Write(data, 0, data.Length);
            }
            
            return ms.ToArray();
        }

        private byte[] DecryptData(byte[] data, string password)
        {
            using var aes = Aes.Create();
            var salt = new byte[16];
            Array.Copy(data, 0, salt, 0, 16);
            
            var key = new Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256);
            aes.Key = key.GetBytes(32);
            aes.IV = key.GetBytes(16);
            
            using var ms = new MemoryStream(data, 16, data.Length - 16);
            using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
            using var result = new MemoryStream();
            cs.CopyTo(result);
            
            return result.ToArray();
        }

        public async Task<bool> RestoreBackupAsync(string backupPath, string? password = null)
        {
            try
            {
                Log.Information($"شروع بازیابی از: {backupPath}");

                // Extract backup to temp directory
                var restoreId = Guid.NewGuid().ToString();
                var restoreTempPath = Path.Combine(_tempPath, restoreId);
                Directory.CreateDirectory(restoreTempPath);

                // Extract backup
                if (Path.GetExtension(backupPath).ToLower() == ".tjb")
                {
                    if (!string.IsNullOrEmpty(password))
                    {
                        await ExtractEncryptedZipAsync(backupPath, restoreTempPath, password);
                    }
                    else
                    {
                        ZipFile.ExtractToDirectory(backupPath, restoreTempPath);
                    }
                }
                else
                {
                    await CopyDirectoryAsync(backupPath, restoreTempPath);
                }

                // Read manifest
                var manifestPath = Path.Combine(restoreTempPath, "manifest.json");
                if (!File.Exists(manifestPath))
                {
                    throw new InvalidOperationException("فایل manifest در نسخه پشتیبان یافت نشد");
                }

                var manifestJson = await File.ReadAllTextAsync(manifestPath);
                dynamic manifest = JsonConvert.DeserializeObject(manifestJson)!;

                // Close database connections
                await _dbContext.Database.CloseConnectionAsync();

                // Restore database
                if (((List<string>)manifest.Contents).Contains("database.db"))
                {
                    var sourceDbPath = Path.Combine(restoreTempPath, "database.db");
                    var targetDbPath = AppSettings.Instance.DatabasePath;
                    
                    // Backup current database before overwriting
                    if (File.Exists(targetDbPath))
                    {
                        File.Copy(targetDbPath, targetDbPath + ".backup", true);
                    }
                    
                    File.Copy(sourceDbPath, targetDbPath, true);
                }

                // Restore images
                if (((List<string>)manifest.Contents).Contains("Images/"))
                {
                    var sourceImagesPath = Path.Combine(restoreTempPath, "Images");
                    var targetImagesPath = AppSettings.Instance.ImagesPath;
                    
                    if (Directory.Exists(sourceImagesPath))
                    {
                        await CopyDirectoryAsync(sourceImagesPath, targetImagesPath);
                    }
                }

                // Restore settings
                if (((List<string>)manifest.Contents).Contains("settings.json"))
                {
                    var sourceSettingsPath = Path.Combine(restoreTempPath, "settings.json");
                    var targetSettingsPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "TradingJournal",
                        "settings.json"
                    );
                    
                    File.Copy(sourceSettingsPath, targetSettingsPath, true);
                    AppSettings.Load();
                }

                // Restore metadata
                if (((List<string>)manifest.Contents).Contains("Metadata/"))
                {
                    var sourceMetadataPath = Path.Combine(restoreTempPath, "Metadata");
                    var targetMetadataPath = AppSettings.Instance.MetadataPath;
                    
                    if (Directory.Exists(sourceMetadataPath))
                    {
                        await CopyDirectoryAsync(sourceMetadataPath, targetMetadataPath);
                    }
                }

                // Cleanup temp directory
                Directory.Delete(restoreTempPath, true);

                Log.Information("بازیابی با موفقیت انجام شد");
                
                NotificationService.Instance.Show(
                    "بازیابی", 
                    "نسخه پشتیبان با موفقیت بازیابی شد",
                    NotificationType.Success
                );

                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "خطا در بازیابی نسخه پشتیبان");
                
                NotificationService.Instance.Show(
                    "خطا در بازیابی", 
                    ex.Message,
                    NotificationType.Error
                );
                
                return false;
            }
        }

        private async Task ExtractEncryptedZipAsync(string zipPath, string targetPath, string password)
        {
            await Task.Run(() =>
            {
                using var fs = new FileStream(zipPath, FileMode.Open);
                using var archive = new ZipArchive(fs, ZipArchiveMode.Read);
                
                foreach (var entry in archive.Entries)
                {
                    var targetFile = Path.Combine(targetPath, entry.FullName);
                    Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
                    
                    using var entryStream = entry.Open();
                    using var ms = new MemoryStream();
                    entryStream.CopyTo(ms);
                    
                    var encryptedBytes = ms.ToArray();
                    var decryptedBytes = DecryptData(encryptedBytes, password);
                    
                    File.WriteAllBytes(targetFile, decryptedBytes);
                }
            });
        }

        public async Task<List<BackupInfo>> GetBackupsAsync()
        {
            var backups = new List<BackupInfo>();
            
            if (Directory.Exists(_backupPath))
            {
                var files = Directory.GetFiles(_backupPath, "*.tjb");
                
                foreach (var file in files.OrderByDescending(f => f))
                {
                    var fileInfo = new FileInfo(file);
                    var backup = new BackupInfo
                    {
                        FileName = fileInfo.Name,
                        FilePath = fileInfo.FullName,
                        CreatedDate = fileInfo.CreationTime,
                        FileSize = fileInfo.Length,
                        IsAutoBackup = fileInfo.Name.Contains("Auto")
                    };
                    
                    backups.Add(backup);
                }
            }
            
            return await Task.FromResult(backups);
        }

        public async Task<bool> DeleteBackupAsync(string backupPath)
        {
            try
            {
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                    
                    Log.Information($"نسخه پشتیبان حذف شد: {backupPath}");
                    return await Task.FromResult(true);
                }
                
                return await Task.FromResult(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"خطا در حذف نسخه پشتیبان: {backupPath}");
                return false;
            }
        }

        public async Task<bool> ValidateBackupAsync(string backupPath)
        {
            try
            {
                if (!File.Exists(backupPath))
                    return false;

                // Try to open as zip
                using var fs = new FileStream(backupPath, FileMode.Open, FileAccess.Read);
                using var archive = new ZipArchive(fs, ZipArchiveMode.Read);
                
                // Check for manifest
                var manifestEntry = archive.GetEntry("manifest.json");
                if (manifestEntry == null)
                    return false;

                // Read and validate manifest
                using var manifestStream = manifestEntry.Open();
                using var reader = new StreamReader(manifestStream);
                var manifestJson = await reader.ReadToEndAsync();
                
                dynamic manifest = JsonConvert.DeserializeObject(manifestJson)!;
                
                // Check version compatibility
                var version = manifest.Version?.ToString();
                // Add version compatibility check logic here
                
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> ScheduleAutoBackupAsync(TimeSpan interval)
        {
            try
            {
                CancelAutoBackup();
                
                _autoBackupTimer = new System.Timers.Timer(interval.TotalMilliseconds);
                _autoBackupTimer.Elapsed += async (s, e) =>
                {
                    var options = new BackupOptions
                    {
                        Description = "Automatic backup"
                    };
                    
                    var fileName = await CreateBackupAsync(options);
                    Log.Information($"پشتیبان‌گیری خودکار انجام شد: {fileName}");
                };
                _autoBackupTimer.Start();
                
                return await Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "خطا در زمان‌بندی پشتیبان‌گیری خودکار");
                return false;
            }
        }

        public void CancelAutoBackup()
        {
            if (_autoBackupTimer != null)
            {
                _autoBackupTimer.Stop();
                _autoBackupTimer.Dispose();
                _autoBackupTimer = null;
            }
        }

        private async Task ManageBackupRetentionAsync()
        {
            var maxBackups = AppSettings.Instance.MaxBackupFiles;
            var backups = await GetBackupsAsync();
            
            if (backups.Count > maxBackups)
            {
                var backupsToDelete = backups
                    .OrderByDescending(b => b.CreatedDate)
                    .Skip(maxBackups)
                    .ToList();
                
                foreach (var backup in backupsToDelete)
                {
                    await DeleteBackupAsync(backup.FilePath);
                }
            }
        }
    }
}

// پایان فایل: Services/BackupService.cs