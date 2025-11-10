// مسیر فایل: Core/PluginEngine/PluginManager.cs
// ابتدای کد
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using TradingJournal.Core.Services;

namespace TradingJournal.Core.PluginEngine
{
    public class PluginManager : IPluginHost
    {
        private readonly string _pluginsPath;
        private readonly Dictionary<string, IPlugin> _loadedPlugins;
        private readonly Dictionary<string, PluginInfo> _pluginInfos;
        private readonly Dictionary<Type, object> _services;
        private readonly Dictionary<string, List<Action<PluginEvent>>> _eventSubscriptions;
        private CompositionContainer _container;

        public event EventHandler<PluginEventArgs> PluginLoaded;
        public event EventHandler<PluginEventArgs> PluginUnloaded;
        public event EventHandler<PluginErrorEventArgs> PluginError;

        public IReadOnlyDictionary<string, IPlugin> LoadedPlugins => _loadedPlugins;
        public IReadOnlyDictionary<string, PluginInfo> PluginInfos => _pluginInfos;

        public PluginManager()
        {
            _pluginsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
            Directory.CreateDirectory(_pluginsPath);

            _loadedPlugins = new Dictionary<string, IPlugin>();
            _pluginInfos = new Dictionary<string, PluginInfo>();
            _services = new Dictionary<Type, object>();
            _eventSubscriptions = new Dictionary<string, List<Action<PluginEvent>>>();

            InitializeServices();
        }

        private void InitializeServices()
        {
            // Register core services
            RegisterService<IPluginDataAccess>(new PluginDataAccess());
            RegisterService<IPluginFileSystem>(new PluginFileSystem());
        }

        public async Task<bool> LoadPluginAsync(string pluginPath)
        {
            try
            {
                if (!File.Exists(pluginPath))
                {
                    OnPluginError(null, $"Plugin file not found: {pluginPath}");
                    return false;
                }

                // Load assembly
                var assembly = Assembly.LoadFrom(pluginPath);
                
                // Find IPlugin implementations
                var pluginTypes = assembly.GetTypes()
                    .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract && t.IsClass);

                foreach (var pluginType in pluginTypes)
                {
                    var plugin = Activator.CreateInstance(pluginType) as IPlugin;
                    if (plugin != null)
                    {
                        // Check dependencies
                        if (!await CheckDependenciesAsync(plugin))
                        {
                            OnPluginError(plugin, "Dependencies not met");
                            continue;
                        }

                        // Initialize plugin
                        if (await plugin.InitializeAsync(this))
                        {
                            _loadedPlugins[plugin.Id] = plugin;
                            _pluginInfos[plugin.Id] = new PluginInfo
                            {
                                Id = plugin.Id,
                                Name = plugin.Name,
                                Version = plugin.Version,
                                Description = plugin.Description,
                                Author = plugin.Author,
                                FilePath = pluginPath,
                                LoadedAt = DateTime.Now,
                                Status = PluginStatus.Loaded
                            };

                            // Start plugin
                            await plugin.StartAsync();
                            _pluginInfos[plugin.Id].Status = PluginStatus.Running;

                            OnPluginLoaded(plugin);
                            return true;
                        }
                        else
                        {
                            OnPluginError(plugin, "Initialization failed");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                OnPluginError(null, $"Error loading plugin: {ex.Message}");
            }

            return false;
        }

        public async Task<bool> UnloadPluginAsync(string pluginId)
        {
            if (_loadedPlugins.TryGetValue(pluginId, out var plugin))
            {
                try
                {
                    // Stop plugin
                    await plugin.StopAsync();
                    
                    // Unload plugin
                    await plugin.UnloadAsync();
                    
                    // Remove from collections
                    _loadedPlugins.Remove(pluginId);
                    _pluginInfos.Remove(pluginId);
                    
                    OnPluginUnloaded(plugin);
                    return true;
                }
                catch (Exception ex)
                {
                    OnPluginError(plugin, $"Error unloading plugin: {ex.Message}");
                }
            }

            return false;
        }

        public async Task LoadAllPluginsAsync()
        {
            var pluginFiles = Directory.GetFiles(_pluginsPath, "*.dll", SearchOption.AllDirectories);
            
            // Sort by priority
            var pluginsToLoad = new List<(string path, int priority)>();
            
            foreach (var file in pluginFiles)
            {
                try
                {
                    var assembly = Assembly.LoadFrom(file);
                    var pluginType = assembly.GetTypes()
                        .FirstOrDefault(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract);
                    
                    if (pluginType != null)
                    {
                        var tempPlugin = Activator.CreateInstance(pluginType) as IPlugin;
                        if (tempPlugin != null)
                        {
                            pluginsToLoad.Add((file, tempPlugin.LoadPriority));
                        }
                    }
                }
                catch
                {
                    // Ignore invalid assemblies
                }
            }

            // Load plugins in priority order
            foreach (var (path, _) in pluginsToLoad.OrderBy(p => p.priority))
            {
                await LoadPluginAsync(path);
            }
        }

        private async Task<bool> CheckDependenciesAsync(IPlugin plugin)
        {
            if (plugin.Dependencies == null || !plugin.Dependencies.Any())
                return true;

            foreach (var dependency in plugin.Dependencies)
            {
                if (_loadedPlugins.TryGetValue(dependency.PluginId, out var dependentPlugin))
                {
                    if (dependentPlugin.Version < dependency.MinVersion)
                    {
                        if (dependency.IsRequired)
                            return false;
                    }
                }
                else if (dependency.IsRequired)
                {
                    return false;
                }
            }

            return true;
        }

        public async Task EnablePluginAsync(string pluginId)
        {
            if (_loadedPlugins.TryGetValue(pluginId, out var plugin))
            {
                await plugin.StartAsync();
                _pluginInfos[pluginId].Status = PluginStatus.Running;
            }
        }

        public async Task DisablePluginAsync(string pluginId)
        {
            if (_loadedPlugins.TryGetValue(pluginId, out var plugin))
            {
                await plugin.StopAsync();
                _pluginInfos[pluginId].Status = PluginStatus.Stopped;
            }
        }

        public async Task<PluginSettings> GetPluginSettingsAsync(string pluginId)
        {
            if (_loadedPlugins.TryGetValue(pluginId, out var plugin))
            {
                return plugin.GetSettings();
            }
            return null;
        }

        public async Task SavePluginSettingsAsync(string pluginId, PluginSettings settings)
        {
            if (_loadedPlugins.TryGetValue(pluginId, out var plugin))
            {
                await plugin.SaveSettingsAsync(settings);
            }
        }

        #region IPluginHost Implementation

        public T GetService<T>() where T : class
        {
            if (_services.TryGetValue(typeof(T), out var service))
            {
                return service as T;
            }
            return null;
        }

        public void RegisterService<T>(T service) where T : class
        {
            _services[typeof(T)] = service;
        }

        public void RegisterMenu(PluginMenuItem menuItem)
        {
            // Implement menu registration
            PublishEvent(new PluginEvent
            {
                Name = "MenuRegistered",
                Source = "PluginManager",
                Timestamp = DateTime.Now,
                Data = menuItem
            });
        }

        public void RegisterTab(PluginTab tab)
        {
            // Implement tab registration
            PublishEvent(new PluginEvent
            {
                Name = "TabRegistered",
                Source = "PluginManager",
                Timestamp = DateTime.Now,
                Data = tab
            });
        }

        public void RegisterWidget(PluginWidget widget)
        {
            // Implement widget registration
            PublishEvent(new PluginEvent
            {
                Name = "WidgetRegistered",
                Source = "PluginManager",
                Timestamp = DateTime.Now,
                Data = widget
            });
        }

        public void RegisterCommand(PluginCommand command)
        {
            // Implement command registration
            PublishEvent(new PluginEvent
            {
                Name = "CommandRegistered",
                Source = "PluginManager",
                Timestamp = DateTime.Now,
                Data = command
            });
        }

        public void ShowMessage(string message, MessageType type = MessageType.Info)
        {
            // Implement message display
            PublishEvent(new PluginEvent
            {
                Name = "ShowMessage",
                Source = "PluginManager",
                Timestamp = DateTime.Now,
                Data = new { Message = message, Type = type }
            });
        }

        public async Task<bool> ShowDialogAsync(string title, System.Windows.FrameworkElement content)
        {
            // Implement dialog display
            var window = new System.Windows.Window
            {
                Title = title,
                Content = content,
                Width = 600,
                Height = 400,
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen
            };

            return window.ShowDialog() ?? false;
        }

        public IPluginDataAccess GetDataAccess()
        {
            return GetService<IPluginDataAccess>();
        }

        public IPluginFileSystem GetFileSystem()
        {
            return GetService<IPluginFileSystem>();
        }

        public void PublishEvent(PluginEvent eventData)
        {
            if (_eventSubscriptions.TryGetValue(eventData.Name, out var handlers))
            {
                foreach (var handler in handlers.ToList())
                {
                    try
                    {
                        handler?.Invoke(eventData);
                    }
                    catch (Exception ex)
                    {
                        OnPluginError(null, $"Error handling event {eventData.Name}: {ex.Message}");
                    }
                }
            }
        }

        public void SubscribeEvent(string eventName, Action<PluginEvent> handler)
        {
            if (!_eventSubscriptions.ContainsKey(eventName))
            {
                _eventSubscriptions[eventName] = new List<Action<PluginEvent>>();
            }
            _eventSubscriptions[eventName].Add(handler);
        }

        public void UnsubscribeEvent(string eventName, Action<PluginEvent> handler)
        {
            if (_eventSubscriptions.TryGetValue(eventName, out var handlers))
            {
                handlers.Remove(handler);
            }
        }

        #endregion

        #region Event Handlers

        private void OnPluginLoaded(IPlugin plugin)
        {
            PluginLoaded?.Invoke(this, new PluginEventArgs { Plugin = plugin });
        }

        private void OnPluginUnloaded(IPlugin plugin)
        {
            PluginUnloaded?.Invoke(this, new PluginEventArgs { Plugin = plugin });
        }

        private void OnPluginError(IPlugin plugin, string error)
        {
            PluginError?.Invoke(this, new PluginErrorEventArgs 
            { 
                Plugin = plugin, 
                Error = error,
                Timestamp = DateTime.Now
            });
        }

        #endregion

        #region Helper Classes

        public class PluginInfo
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public Version Version { get; set; }
            public string Description { get; set; }
            public string Author { get; set; }
            public string FilePath { get; set; }
            public DateTime LoadedAt { get; set; }
            public PluginStatus Status { get; set; }
        }

        public enum PluginStatus
        {
            NotLoaded,
            Loaded,
            Running,
            Stopped,
            Error
        }

        public class PluginEventArgs : EventArgs
        {
            public IPlugin Plugin { get; set; }
        }

        public class PluginErrorEventArgs : EventArgs
        {
            public IPlugin Plugin { get; set; }
            public string Error { get; set; }
            public DateTime Timestamp { get; set; }
        }

        #endregion
    }

    /// <summary>
    /// پیاده‌سازی دسترسی به داده برای پلاگین‌ها
    /// </summary>
    public class PluginDataAccess : IPluginDataAccess
    {
        private readonly string _dataPath;

        public PluginDataAccess()
        {
            _dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PluginData");
            Directory.CreateDirectory(_dataPath);
        }

        public async Task<T> GetAsync<T>(string key) where T : class
        {
            var filePath = Path.Combine(_dataPath, $"{key}.json");
            if (File.Exists(filePath))
            {
                var json = await File.ReadAllTextAsync(filePath);
                return System.Text.Json.JsonSerializer.Deserialize<T>(json);
            }
            return null;
        }

        public async Task SaveAsync<T>(string key, T data) where T : class
        {
            var filePath = Path.Combine(_dataPath, $"{key}.json");
            var json = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            await File.WriteAllTextAsync(filePath, json);
        }

        public async Task DeleteAsync(string key)
        {
            var filePath = Path.Combine(_dataPath, $"{key}.json");
            if (File.Exists(filePath))
            {
                await Task.Run(() => File.Delete(filePath));
            }
        }

        public async Task<List<T>> QueryAsync<T>(string query) where T : class
        {
            // Simplified query implementation
            return new List<T>();
        }

        public async Task ExecuteAsync(string command)
        {
            // Command execution
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// پیاده‌سازی دسترسی به فایل سیستم برای پلاگین‌ها
    /// </summary>
    public class PluginFileSystem : IPluginFileSystem
    {
        private readonly string _basePath;

        public PluginFileSystem()
        {
            _basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PluginData");
            Directory.CreateDirectory(_basePath);
        }

        public string GetPluginDataPath(string pluginId)
        {
            var path = Path.Combine(_basePath, pluginId);
            Directory.CreateDirectory(path);
            return path;
        }

        public async Task<string> ReadTextAsync(string path)
        {
            var fullPath = GetSafePath(path);
            return await File.ReadAllTextAsync(fullPath);
        }

        public async Task WriteTextAsync(string path, string content)
        {
            var fullPath = GetSafePath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            await File.WriteAllTextAsync(fullPath, content);
        }

        public async Task<byte[]> ReadBytesAsync(string path)
        {
            var fullPath = GetSafePath(path);
            return await File.ReadAllBytesAsync(fullPath);
        }

        public async Task WriteBytesAsync(string path, byte[] data)
        {
            var fullPath = GetSafePath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            await File.WriteAllBytesAsync(fullPath, data);
        }

        public bool FileExists(string path)
        {
            var fullPath = GetSafePath(path);
            return File.Exists(fullPath);
        }

        public void DeleteFile(string path)
        {
            var fullPath = GetSafePath(path);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }

        private string GetSafePath(string path)
        {
            // Ensure path is within plugin data directory
            var fullPath = Path.GetFullPath(Path.Combine(_basePath, path));
            if (!fullPath.StartsWith(_basePath))
            {
                throw new UnauthorizedAccessException("Access to path outside plugin directory is not allowed");
            }
            return fullPath;
        }
    }
}
// پایان کد