// مسیر فایل: Core/Services/BackupService.cs
// ابتدای کد
using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using TradingJournal.Data;

namespace TradingJournal.Core.Services
{
    public class BackupService
    {
        private readonly string _backupPath;
        private readonly string _dataPath;
        private readonly string _imagesPath;
        private readonly string _metadataPath;

        public BackupService()
        {
            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            _backupPath = Path.Combine(basePath, "Backups");
            _dataPath = Path.Combine(basePath, "Data");
            _imagesPath = Path.Combine(basePath, "Images");
            _metadataPath = Path.Combine(basePath, "Metadata");
            
            Directory.CreateDirectory(_backupPath);
        }

        public async Task<BackupResult> CreateBackupAsync(string password = null)
        {
            var result = new BackupResult();
            
            try
            {
                // Create temp folder
                var tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempFolder);

                // Copy database
                var dbSource = Path.Combine(_dataPath, "TradingJournal.db");
                if (File.Exists(dbSource))
                {
                    var dbDest = Path.Combine(tempFolder, "Data", "TradingJournal.db");
                    Directory.CreateDirectory(Path.GetDirectoryName(dbDest));
                    File.Copy(dbSource, dbDest);
                }

                // Copy images
                if (Directory.Exists(_imagesPath))
                {
                    CopyDirectory(_imagesPath, Path.Combine(tempFolder, "Images"));
                }

                // Copy metadata
                if (Directory.Exists(_metadataPath))
                {
                    CopyDirectory(_metadataPath, Path.Combine(tempFolder, "Metadata"));
                }

                // Create backup info
                var backupInfo = new BackupInfo
                {
                    BackupDate = DateTime.Now,
                    Version = "1.0.0",
                    MachineName = Environment.MachineName,
                    DatabaseSize = new FileInfo(dbSource).Length,
                    TotalFiles = Directory.GetFiles(tempFolder, "*", SearchOption.AllDirectories).Length
                };

                var infoJson = System.Text.Json.JsonSerializer.Serialize(backupInfo);
                await File.WriteAllTextAsync(Path.Combine(tempFolder, "backup_info.json"), infoJson);

                // Create zip file
                var backupFileName = $"Backup_{DateTime.Now:yyyyMMdd_HHmmss}.zip";
                var backupFilePath = Path.Combine(_backupPath, backupFileName);
                
                if (!string.IsNullOrEmpty(password))
                {
                    // Create encrypted backup
                    CreateEncryptedZip(tempFolder, backupFilePath, password);
                }
                else
                {
                    // Create regular backup
                    ZipFile.CreateFromDirectory(tempFolder, backupFilePath);
                }

                // Clean up temp folder
                Directory.Delete(tempFolder, true);

                result.Success = true;
                result.FilePath = backupFilePath;
                result.FileSize = new FileInfo(backupFilePath).Length;
                result.Message = "نسخه پشتیبان با موفقیت ایجاد شد";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"خطا در ایجاد نسخه پشتیبان: {ex.Message}";
            }

            return result;
        }

        public async Task<RestoreResult> RestoreBackupAsync(string backupFile, string password = null)
        {
            var result = new RestoreResult();
            
            try
            {
                if (!File.Exists(backupFile))
                {
                    result.Success = false;
                    result.Message = "فایل پشتیبان یافت نشد";
                    return result;
                }

                // Create temp folder for extraction
                var tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempFolder);

                // Extract backup
                if (!string.IsNullOrEmpty(password))
                {
                    ExtractEncryptedZip(backupFile, tempFolder, password);
                }
                else
                {
                    ZipFile.ExtractToDirectory(backupFile, tempFolder);
                }

                // Verify backup info
                var infoFile = Path.Combine(tempFolder, "backup_info.json");
                if (File.Exists(infoFile))
                {
                    var infoJson = await File.ReadAllTextAsync(infoFile);
                    var backupInfo = System.Text.Json.JsonSerializer.Deserialize<BackupInfo>(infoJson);
                    result.BackupInfo = backupInfo;
                }

                // Backup current data before restore
                await CreateBackupAsync();

                // Restore database
                var dbSource = Path.Combine(tempFolder, "Data", "TradingJournal.db");
                if (File.Exists(dbSource))
                {
                    var dbDest = Path.Combine(_dataPath, "TradingJournal.db");
                    File.Copy(dbSource, dbDest, true);
                    result.DatabaseRestored = true;
                }

                // Restore images
                var imagesSource = Path.Combine(tempFolder, "Images");
                if (Directory.Exists(imagesSource))
                {
                    if (Directory.Exists(_imagesPath))
                        Directory.Delete(_imagesPath, true);
                    CopyDirectory(imagesSource, _imagesPath);
                    result.ImagesRestored = true;
                }

                // Restore metadata
                var metadataSource = Path.Combine(tempFolder, "Metadata");
                if (Directory.Exists(metadataSource))
                {
                    if (Directory.Exists(_metadataPath))
                        Directory.Delete(_metadataPath, true);
                    CopyDirectory(metadataSource, _metadataPath);
                    result.MetadataRestored = true;
                }

                // Clean up temp folder
                Directory.Delete(tempFolder, true);

                result.Success = true;
                result.Message = "بازیابی با موفقیت انجام شد";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"خطا در بازیابی: {ex.Message}";
            }

            return result;
        }

        public async Task<List<BackupFileInfo>> GetBackupListAsync()
        {
            var backups = new List<BackupFileInfo>();
            
            if (Directory.Exists(_backupPath))
            {
                var files = Directory.GetFiles(_backupPath, "*.zip");
                foreach (var file in files)
                {
                    var info = new FileInfo(file);
                    backups.Add(new BackupFileInfo
                    {
                        FileName = info.Name,
                        FilePath = info.FullName,
                        FileSize = info.Length,
                        CreatedDate = info.CreationTime,
                        IsEncrypted = await IsEncryptedBackup(file)
                    });
                }
            }

            return backups.OrderByDescending(b => b.CreatedDate).ToList();
        }

        private void CreateEncryptedZip(string sourceFolder, string destFile, string password)
        {
            // First create regular zip
            var tempZip = Path.GetTempFileName();
            ZipFile.CreateFromDirectory(sourceFolder, tempZip);

            // Then encrypt it
            var zipData = File.ReadAllBytes(tempZip);
            var encryptedData = EncryptData(zipData, password);
            File.WriteAllBytes(destFile, encryptedData);

            // Clean up temp file
            File.Delete(tempZip);
        }

        private void ExtractEncryptedZip(string sourceFile, string destFolder, string password)
        {
            // Decrypt the file
            var encryptedData = File.ReadAllBytes(sourceFile);
            var decryptedData = DecryptData(encryptedData, password);

            // Write to temp file and extract
            var tempZip = Path.GetTempFileName();
            File.WriteAllBytes(tempZip, decryptedData);
            ZipFile.ExtractToDirectory(tempZip, destFolder);

            // Clean up temp file
            File.Delete(tempZip);
        }

        private byte[] EncryptData(byte[] data, string password)
        {
            using var aes = Aes.Create();
            var key = DeriveKey(password, 32);
            aes.Key = key;
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor();
            using var ms = new MemoryStream();
            
            // Write IV to the beginning
            ms.Write(aes.IV, 0, aes.IV.Length);
            
            // Write encrypted data
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            {
                cs.Write(data, 0, data.Length);
            }

            return ms.ToArray();
        }

        private byte[] DecryptData(byte[] encryptedData, string password)
        {
            using var aes = Aes.Create();
            var key = DeriveKey(password, 32);
            aes.Key = key;

            // Read IV from the beginning
            var iv = new byte[aes.BlockSize / 8];
            Array.Copy(encryptedData, 0, iv, 0, iv.Length);
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            using var ms = new MemoryStream(encryptedData, iv.Length, encryptedData.Length - iv.Length);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var result = new MemoryStream();
            
            cs.CopyTo(result);
            return result.ToArray();
        }

        private byte[] DeriveKey(string password, int keyLength)
        {
            var salt = Encoding.UTF8.GetBytes("TradingJournal2024");
            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 10000);
            return pbkdf2.GetBytes(keyLength);
        }

        private async Task<bool> IsEncryptedBackup(string filePath)
        {
            try
            {
                // Try to open as regular zip
                using var archive = ZipFile.OpenRead(filePath);
                return false; // Not encrypted
            }
            catch
            {
                return true; // Probably encrypted
            }
        }

        private void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var fileName = Path.GetFileName(file);
                var destFile = Path.Combine(targetDir, fileName);
                File.Copy(file, destFile, true);
            }

            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                var dirName = Path.GetFileName(subDir);
                var destDir = Path.Combine(targetDir, dirName);
                CopyDirectory(subDir, destDir);
            }
        }
    }

    public class BackupResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string FilePath { get; set; }
        public long FileSize { get; set; }
    }

    public class RestoreResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public bool DatabaseRestored { get; set; }
        public bool ImagesRestored { get; set; }
        public bool MetadataRestored { get; set; }
        public BackupInfo BackupInfo { get; set; }
    }

    public class BackupInfo
    {
        public DateTime BackupDate { get; set; }
        public string Version { get; set; }
        public string MachineName { get; set; }
        public long DatabaseSize { get; set; }
        public int TotalFiles { get; set; }
    }

    public class BackupFileInfo
    {
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public long FileSize { get; set; }
        public DateTime CreatedDate { get; set; }
        public bool IsEncrypted { get; set; }
    }
}
// پایان کد