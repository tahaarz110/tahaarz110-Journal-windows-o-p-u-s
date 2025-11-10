// 📁 Core/PluginEngine/PluginLoader.cs
// ===== شروع کد =====

using System;
using System.Collections.Generic;
using System.Composition;
using System.Composition.Convention;
using System.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;

namespace TradingJournal.Core.PluginEngine
{
    public class PluginLoader
    {
        private readonly string _pluginsDirectory;
        private readonly List<PluginContainer> _loadedPlugins;
        private readonly Dictionary<string, AssemblyLoadContext> _pluginContexts;
        
        public IReadOnlyList<PluginContainer> LoadedPlugins => _loadedPlugins;
        
        public PluginLoader(string pluginsDirectory = "Plugins")
        {
            _pluginsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, pluginsDirectory);
            _loadedPlugins = new List<PluginContainer>();
            _pluginContexts = new Dictionary<string, AssemblyLoadContext>();
            
            if (!Directory.Exists(_pluginsDirectory))
            {
                Directory.CreateDirectory(_pluginsDirectory);
            }
        }
        
        /// <summary>
        /// بارگذاری همه پلاگین‌ها از دایرکتوری
        /// </summary>
        public async Task<LoadResult> LoadAllPluginsAsync()
        {
            var result = new LoadResult();
            
            try
            {
                // جستجوی همه فولدرهای پلاگین
                var pluginFolders = Directory.GetDirectories(_pluginsDirectory);
                
                foreach (var folder in pluginFolders)
                {
                    var loadResult = await LoadPluginAsync(folder);
                    if (loadResult.Success)
                    {
                        result.LoadedCount++;
                    }
                    else
                    {
                        result.FailedPlugins.Add(new FailedPlugin
                        {
                            Path = folder,
                            Error = loadResult.ErrorMessage
                        });
                    }
                }
                
                result.Success = true;
                result.Message = $"تعداد {result.LoadedCount} پلاگین بارگذاری شد";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"خطا در بارگذاری پلاگین‌ها: {ex.Message}";
            }
            
            return result;
        }
        
        /// <summary>
        /// بارگذاری یک پلاگین
        /// </summary>
        public async Task<PluginLoadResult> LoadPluginAsync(string pluginPath)
        {
            var result = new PluginLoadResult();
            
            try
            {
                // پیدا کردن فایل اصلی پلاگین
                var pluginFile = Directory.GetFiles(pluginPath, "*.dll")
                    .FirstOrDefault(f => !f.Contains(".deps.") && !f.Contains(".resources."));
                
                if (string.IsNullOrEmpty(pluginFile))
                {
                    result.ErrorMessage = "فایل DLL پلاگین یافت نشد";
                    return result;
                }
                
                // ایجاد context ایزوله برای پلاگین
                var pluginContext = new PluginAssemblyLoadContext(pluginPath);
                _pluginContexts[pluginPath] = pluginContext;
                
                // بارگذاری assembly
                var assembly = pluginContext.LoadFromAssemblyPath(pluginFile);
                
                // جستجوی کلاس‌هایی که IPlugin را پیاده‌سازی می‌کنند
                var pluginTypes = assembly.GetTypes()
                    .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);
                
                foreach (var pluginType in pluginTypes)
                {
                    var plugin = Activator.CreateInstance(pluginType) as IPlugin;
                    if (plugin != null)
                    {
                        var container = new PluginContainer
                        {
                            Plugin = plugin,
                            AssemblyPath = pluginFile,
                            LoadContext = pluginContext,
                            IsEnabled = false,
                            LoadTime = DateTime.Now
                        };
                        
                        _loadedPlugins.Add(container);
                        
                        result.Success = true;
                        result.LoadedPlugin = container;
                        result.Message = $"پلاگین {plugin.Name} با موفقیت بارگذاری شد";
                    }
                }
                
                if (!result.Success)
                {
                    result.ErrorMessage = "هیچ کلاس معتبری که IPlugin را پیاده‌سازی کند یافت نشد";
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"خطا در بارگذاری پلاگین: {ex.Message}";
            }
            
            return result;
        }
        
        /// <summary>
        /// حذف پلاگین از حافظه
        /// </summary>
        public async Task<bool> UnloadPluginAsync(string pluginId)
        {
            try
            {
                var container = _loadedPlugins.FirstOrDefault(p => p.Plugin.Id == pluginId);
                if (container == null)
                    return false;
                
                // خاموش کردن پلاگین
                if (container.IsEnabled)
                {
                    await container.Plugin.ShutdownAsync();
                }
                
                // حذف از لیست
                _loadedPlugins.Remove(container);
                
                // آزادسازی context
                if (_pluginContexts.ContainsKey(container.AssemblyPath))
                {
                    var context = _pluginContexts[container.AssemblyPath];
                    context.Unload();
                    _pluginContexts.Remove(container.AssemblyPath);
                }
                
                // اجرای Garbage Collection
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error unloading plugin: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// دریافت پلاگین بر اساس شناسه
        /// </summary>
        public PluginContainer GetPlugin(string pluginId)
        {
            return _loadedPlugins.FirstOrDefault(p => p.Plugin.Id == pluginId);
        }
        
        /// <summary>
        /// دریافت پلاگین‌های یک دسته خاص
        /// </summary>
        public IEnumerable<PluginContainer> GetPluginsByCategory(PluginCategory category)
        {
            return _loadedPlugins.Where(p => p.Plugin.Category == category);
        }
    }
    
    /// <summary>
    /// کانتکست بارگذاری ایزوله برای هر پلاگین
    /// </summary>
    public class PluginAssemblyLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;
        
        public PluginAssemblyLoadContext(string pluginPath) : base(isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(pluginPath);
        }
        
        protected override Assembly Load(AssemblyName assemblyName)
        {
            string assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath != null)
            {
                return LoadFromAssemblyPath(assemblyPath);
            }
            
            return null;
        }
        
        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            string libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (libraryPath != null)
            {
                return LoadUnmanagedDllFromPath(libraryPath);
            }
            
            return IntPtr.Zero;
        }
    }
    
    /// <summary>
    /// محفظه نگهداری پلاگین
    /// </summary>
    public class PluginContainer
    {
        public IPlugin Plugin { get; set; }
        public string AssemblyPath { get; set; }
        public AssemblyLoadContext LoadContext { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime LoadTime { get; set; }
        public DateTime? LastExecutionTime { get; set; }
        public int ExecutionCount { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
    }
    
    /// <summary>
    /// نتیجه بارگذاری
    /// </summary>
    public class LoadResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int LoadedCount { get; set; }
        public List<FailedPlugin> FailedPlugins { get; set; } = new List<FailedPlugin>();
    }
    
    public class PluginLoadResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string ErrorMessage { get; set; }
        public PluginContainer LoadedPlugin { get; set; }
    }
    
    public class FailedPlugin
    {
        public string Path { get; set; }
        public string Error { get; set; }
    }
}

// ===== پایان کد =====