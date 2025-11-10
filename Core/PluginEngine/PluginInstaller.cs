// 📁 Core/PluginEngine/PluginInstaller.cs
// ===== شروع کد =====

using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TradingJournal.Core.PluginEngine
{
    /// <summary>
    /// نصب و حذف پلاگین‌ها از فایل‌های پکیج شده
    /// </summary>
    public class PluginInstaller
    {
        private readonly string _pluginsDirectory;
        
        public PluginInstaller(string pluginsDirectory = "Plugins")
        {
            _pluginsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, pluginsDirectory);
        }
        
        public async Task<InstallResult> InstallFromZipAsync(string zipPath)
        {
            var result = new InstallResult();
            
            try
            {
                // استخراج به پوشه موقت
                var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempPath);
                
                ZipFile.ExtractToDirectory(zipPath, tempPath);
                
                // خواندن manifest
                var manifestPath = Path.Combine(tempPath, "manifest.json");
                if (!File.Exists(manifestPath))
                {
                    result.Success = false;
                    result.Message = "فایل manifest.json یافت نشد";
                    Directory.Delete(tempPath, true);
                    return result;
                }
                
                var manifestJson = await File.ReadAllTextAsync(manifestPath);
                var manifest = JsonConvert.DeserializeObject<PluginManifest>(manifestJson);
                
                // بررسی نسخه برنامه
                if (!IsVersionCompatible(manifest.MinAppVersion))
                {
                    result.Success = false;
                    result.Message = $"پلاگین نیاز به نسخه {manifest.MinAppVersion} یا بالاتر دارد";
                    Directory.Delete(tempPath, true);
                    return result;
                }
                
                // بررسی وجود پلاگین قبلی
                var targetPath = Path.Combine(_pluginsDirectory, manifest.Id);
                if (Directory.Exists(targetPath))
                {
                    // بکاپ از نسخه قبلی
                    var backupPath = targetPath + ".backup";
                    if (Directory.Exists(backupPath))
                        Directory.Delete(backupPath, true);
                    Directory.Move(targetPath, backupPath);
                }
                
                // کپی فایل‌ها به مقصد
                Directory.Move(tempPath, targetPath);
                
                result.Success = true;
                result.Message = $"پلاگین {manifest.Name} با موفقیت نصب شد";
                result.InstalledPlugin = manifest;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"خطا در نصب پلاگین: {ex.Message}";
            }
            
            return result;
        }
        
        public async Task<bool> UninstallAsync(string pluginId)
        {
            try
            {
                var pluginPath = Path.Combine(_pluginsDirectory, pluginId);
                if (Directory.Exists(pluginPath))
                {
                    // ایجاد بکاپ قبل از حذف
                    var backupPath = pluginPath + ".uninstalled";
                    if (Directory.Exists(backupPath))
                        Directory.Delete(backupPath, true);
                    
                    Directory.Move(pluginPath, backupPath);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error uninstalling plugin: {ex.Message}");
                return false;
            }
        }
        
        public async Task<UpdateResult> UpdatePluginAsync(string pluginId, string updateZipPath)
        {
            var result = new UpdateResult();
            
            try
            {
                // ابتدا پلاگین قدیمی را uninstall می‌کنیم
                await UninstallAsync(pluginId);
                
                // سپس نسخه جدید را نصب می‌کنیم
                var installResult = await InstallFromZipAsync(updateZipPath);
                
                result.Success = installResult.Success;
                result.Message = installResult.Message;
                
                if (installResult.Success)
                {
                    result.UpdatedToVersion = installResult.InstalledPlugin.Version;
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"خطا در به‌روزرسانی: {ex.Message}";
            }
            
            return result;
        }
        
        private bool IsVersionCompatible(string minVersion)
        {
            try
            {
                var required = Version.Parse(minVersion);
                var current = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                return current >= required;
            }
            catch
            {
                return true; // در صورت خطا، سازگار فرض می‌کنیم
            }
        }
    }
    
    public class InstallResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public PluginManifest InstalledPlugin { get; set; }
    }
    
    public class UpdateResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string UpdatedToVersion { get; set; }
    }
    
    public class PluginManifest
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Version { get; set; }
        public string MinAppVersion { get; set; }
        public Author Author { get; set; }
        public string Main { get; set; }
        public string Icon { get; set; }
        public string Category { get; set; }
        public List<string> Tags { get; set; }
        public List<string> Permissions { get; set; }
        public Dictionary<string, string> Dependencies { get; set; }
    }
    
    public class Author
    {
        public string Name { get; set; }
        public string Email { get; set; }
        public string Url { get; set; }
    }
}

// ===== پایان کد =====