// 📁 Core/PluginEngine/PluginHotReload.cs
// ===== شروع کد =====

using System;
using System.IO;
using System.Threading.Tasks;

namespace TradingJournal.Core.PluginEngine
{
    /// <summary>
    /// پشتیبانی از Hot Reload برای توسعه پلاگین‌ها
    /// </summary>
    public class PluginHotReloadService
    {
        private readonly PluginManager _pluginManager;
        private readonly PluginLoader _pluginLoader;
        private readonly Dictionary<string, FileSystemWatcher> _watchers;
        
        public event EventHandler<PluginReloadEventArgs> PluginReloaded;
        public bool IsEnabled { get; private set; }
        
        public PluginHotReloadService(PluginManager pluginManager, PluginLoader pluginLoader)
        {
            _pluginManager = pluginManager;
            _pluginLoader = pluginLoader;
            _watchers = new Dictionary<string, FileSystemWatcher>();
        }
        
        public void EnableHotReload(string pluginId = null)
        {
            IsEnabled = true;
            
            if (pluginId != null)
            {
                // Hot reload برای پلاگین خاص
                WatchPlugin(pluginId);
            }
            else
            {
                // Hot reload برای همه پلاگین‌ها
                foreach (var container in _pluginLoader.LoadedPlugins)
                {
                    WatchPlugin(container.Plugin.Id);
                }
            }
        }
        
        public void DisableHotReload()
        {
            IsEnabled = false;
            
            foreach (var watcher in _watchers.Values)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            _watchers.Clear();
        }
        
        private void WatchPlugin(string pluginId)
        {
            var container = _pluginLoader.GetPlugin(pluginId);
            if (container == null) return;
            
            var directory = Path.GetDirectoryName(container.AssemblyPath);
            var fileName = Path.GetFileName(container.AssemblyPath);
            
            if (_watchers.ContainsKey(pluginId))
            {
                _watchers[pluginId].Dispose();
            }
            
            var watcher = new FileSystemWatcher(directory)
            {
                Filter = fileName,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };
            
            watcher.Changed += async (sender, e) => await OnPluginFileChanged(pluginId, e.FullPath);
            _watchers[pluginId] = watcher;
        }
        
        private async Task OnPluginFileChanged(string pluginId, string filePath)
        {
            if (!IsEnabled) return;
            
            // صبر برای اتمام عملیات نوشتن فایل
            await Task.Delay(500);
            
            try
            {
                // غیرفعال کردن پلاگین قدیمی
                await _pluginManager.DisablePluginAsync(pluginId);
                
                // حذف از حافظه
                await _pluginLoader.UnloadPluginAsync(pluginId);
                
                // صبر برای آزادسازی فایل
                await Task.Delay(100);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                
                // بارگذاری مجدد
                var result = await _pluginLoader.LoadPluginAsync(Path.GetDirectoryName(filePath));
                
                if (result.Success)
                {
                    // فعال‌سازی پلاگین جدید
                    await _pluginManager.EnablePluginAsync(pluginId);
                    
                    PluginReloaded?.Invoke(this, new PluginReloadEventArgs
                    {
                        PluginId = pluginId,
                        Success = true,
                        Message = "پلاگین با موفقیت بارگذاری مجدد شد"
                    });
                }
                else
                {
                    PluginReloaded?.Invoke(this, new PluginReloadEventArgs
                    {
                        PluginId = pluginId,
                        Success = false,
                        Message = result.ErrorMessage
                    });
                }
            }
            catch (Exception ex)
            {
                PluginReloaded?.Invoke(this, new PluginReloadEventArgs
                {
                    PluginId = pluginId,
                    Success = false,
                    Message = $"خطا در بارگذاری مجدد: {ex.Message}"
                });
            }
        }
    }
    
    public class PluginReloadEventArgs : EventArgs
    {
        public string PluginId { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
    }
}

// ===== پایان کد =====