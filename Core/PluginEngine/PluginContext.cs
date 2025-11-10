// 📁 Core/PluginEngine/PluginContext.cs
// ===== شروع کد =====

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Serilog;
using TradingJournal.Data;

namespace TradingJournal.Core.PluginEngine
{
    /// <summary>
    /// پیاده‌سازی کانتکست پلاگین با دسترسی‌های محدود و امن
    /// </summary>
    public class PluginContext : IPluginContext
    {
        private readonly string _pluginId;
        private readonly AppDbContext _dbContext;
        private readonly IMemoryCache _cache;
        private readonly ILogger _logger;
        
        public IPluginDataAccess DataAccess { get; }
        public IPluginSettingsStore SettingsStore { get; }
        public IPluginLogger Logger { get; }
        public IPluginCache Cache { get; }
        public IPluginEventBus EventBus { get; }
        public IPluginFileSystem FileSystem { get; }
        public IPluginNotification Notification { get; }
        
        public PluginContext(string pluginId, AppDbContext dbContext, IMemoryCache cache, ILogger logger)
        {
            _pluginId = pluginId;
            _dbContext = dbContext;
            _cache = cache;
            _logger = logger;
            
            DataAccess = new PluginDataAccess(_dbContext, pluginId);
            SettingsStore = new PluginSettingsStore(pluginId);
            Logger = new PluginLogger(_logger, pluginId);
            Cache = new PluginCache(_cache, pluginId);
            EventBus = new PluginEventBus();
            FileSystem = new PluginFileSystem(pluginId);
            Notification = new PluginNotification();
        }
    }
    
    /// <summary>
    /// دسترسی به دیتابیس برای پلاگین‌ها (فقط خواندنی)
    /// </summary>
    public class PluginDataAccess : IPluginDataAccess
    {
        private readonly AppDbContext _context;
        private readonly string _pluginId;
        
        public PluginDataAccess(AppDbContext context, string pluginId)
        {
            _context = context;
            _pluginId = pluginId;
        }
        
        public async Task<IEnumerable<TradeData>> GetTradesAsync(QueryFilter filter = null)
        {
            var query = _context.Trades.AsNoTracking();
            
            if (filter != null)
            {
                if (filter.StartDate.HasValue)
                    query = query.Where(t => t.EntryTime >= filter.StartDate.Value);
                
                if (filter.EndDate.HasValue)
                    query = query.Where(t => t.EntryTime <= filter.EndDate.Value);
                
                if (!string.IsNullOrEmpty(filter.Symbol))
                    query = query.Where(t => t.Symbol == filter.Symbol);
                
                if (!string.IsNullOrEmpty(filter.Strategy))
                    query = query.Where(t => t.Strategy == filter.Strategy);
                
                query = query
                    .Skip(filter.Skip)
                    .Take(filter.Take);
            }
            
            return await query
                .Select(t => new TradeData
                {
                    Id = t.Id,
                    Symbol = t.Symbol,
                    Type = t.TradeType,
                    EntryTime = t.EntryTime,
                    EntryPrice = t.EntryPrice,
                    ExitTime = t.ExitTime,
                    ExitPrice = t.ExitPrice,
                    Volume = t.Volume,
                    Profit = t.Profit,
                    Commission = t.Commission,
                    Strategy = t.Strategy,
                    Tags = t.Tags
                })
                .ToListAsync();
        }
        
        public async Task<Dictionary<string, object>> GetStatisticsAsync()
        {
            var stats = new Dictionary<string, object>();
            
            stats["TotalTrades"] = await _context.Trades.CountAsync();
            stats["WinningTrades"] = await _context.Trades.CountAsync(t => t.Profit > 0);
            stats["LosingTrades"] = await _context.Trades.CountAsync(t => t.Profit < 0);
            stats["TotalProfit"] = await _context.Trades.SumAsync(t => t.Profit);
            stats["AverageProfit"] = await _context.Trades.AverageAsync(t => t.Profit);
            
            return stats;
        }
        
        public async Task<IEnumerable<T>> ExecuteQueryAsync<T>(string query, object parameters = null)
        {
            // فقط اجازه SELECT داریم
            if (!query.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            {
                throw new SecurityException("فقط دستورات SELECT مجاز هستند");
            }
            
            // اجرای query با پارامترها
            return await _context.Database
                .SqlQueryRaw<T>(query, parameters)
                .ToListAsync();
        }
    }
    
    /// <summary>
    /// مدیریت تنظیمات پلاگین
    /// </summary>
    public class PluginSettingsStore : IPluginSettingsStore
    {
        private readonly string _pluginId;
        private readonly string _settingsPath;
        
        public PluginSettingsStore(string pluginId)
        {
            _pluginId = pluginId;
            _settingsPath = Path.Combine("Plugins", "Settings", $"{pluginId}.json");
        }
        
        public async Task<T> GetSettingAsync<T>(string key)
        {
            var settings = await LoadSettingsAsync();
            if (settings.ContainsKey(key))
            {
                return JsonConvert.DeserializeObject<T>(settings[key].ToString());
            }
            return default(T);
        }
        
        public async Task SaveSettingAsync(string key, object value)
        {
            var settings = await LoadSettingsAsync();
            settings[key] = value;
            await SaveSettingsAsync(settings);
        }
        
        public async Task<Dictionary<string, object>> GetAllSettingsAsync()
        {
            return await LoadSettingsAsync();
        }
        
        public async Task SaveSettingsAsync(Dictionary<string, object> settings)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath));
            var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            await File.WriteAllTextAsync(_settingsPath, json);
        }
        
        private async Task<Dictionary<string, object>> LoadSettingsAsync()
        {
            if (!File.Exists(_settingsPath))
                return new Dictionary<string, object>();
            
            var json = await File.ReadAllTextAsync(_settingsPath);
            return JsonConvert.DeserializeObject<Dictionary<string, object>>(json) 
                   ?? new Dictionary<string, object>();
        }
    }
    
    /// <summary>
    /// سیستم لاگ برای پلاگین‌ها
    /// </summary>
    public class PluginLogger : IPluginLogger
    {
        private readonly ILogger _logger;
        private readonly string _pluginId;
        
        public PluginLogger(ILogger logger, string pluginId)
        {
            _logger = logger;
            _pluginId = pluginId;
        }
        
        public void LogInfo(string message)
        {
            _logger.Information($"[Plugin: {_pluginId}] {message}");
        }
        
        public void LogWarning(string message)
        {
            _logger.Warning($"[Plugin: {_pluginId}] {message}");
        }
        
        public void LogError(string message, Exception exception = null)
        {
            if (exception != null)
                _logger.Error(exception, $"[Plugin: {_pluginId}] {message}");
            else
                _logger.Error($"[Plugin: {_pluginId}] {message}");
        }
        
        public void LogDebug(string message)
        {
            _logger.Debug($"[Plugin: {_pluginId}] {message}");
        }
    }
    
    /// <summary>
    /// سیستم کش برای پلاگین‌ها
    /// </summary>
    public class PluginCache : IPluginCache
    {
        private readonly IMemoryCache _cache;
        private readonly string _pluginId;
        
        public PluginCache(IMemoryCache cache, string pluginId)
        {
            _cache = cache;
            _pluginId = pluginId;
        }
        
        public async Task<T> GetAsync<T>(string key)
        {
            var fullKey = $"{_pluginId}:{key}";
            if (_cache.TryGetValue(fullKey, out T value))
            {
                return value;
            }
            return default(T);
        }
        
        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
        {
            var fullKey = $"{_pluginId}:{key}";
            var options = new MemoryCacheEntryOptions();
            
            if (expiration.HasValue)
                options.SetAbsoluteExpiration(expiration.Value);
            else
                options.SetSlidingExpiration(TimeSpan.FromMinutes(15));
            
            _cache.Set(fullKey, value, options);
        }
        
        public async Task InvalidateAsync(string key)
        {
            var fullKey = $"{_pluginId}:{key}";
            _cache.Remove(fullKey);
        }
        
        public async Task ClearAsync()
        {
            // حذف همه کش‌های این پلاگین
            // نیاز به پیاده‌سازی پیچیده‌تر دارد
        }
    }
}

// ===== پایان کد =====