// 📁 Core/PluginEngine/IPluginInterfaces.cs
// ===== شروع کد =====

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TradingJournal.Core.PluginEngine
{
    public interface IPluginDataAccess
    {
        Task<IEnumerable<TradeData>> GetTradesAsync(QueryFilter filter = null);
        Task<Dictionary<string, object>> GetStatisticsAsync();
        Task<IEnumerable<T>> ExecuteQueryAsync<T>(string query, object parameters = null);
    }
    
    public interface IPluginSettingsStore
    {
        Task<T> GetSettingAsync<T>(string key);
        Task SaveSettingAsync(string key, object value);
        Task<Dictionary<string, object>> GetAllSettingsAsync();
        Task SaveSettingsAsync(Dictionary<string, object> settings);
    }
    
    public interface IPluginLogger
    {
        void LogInfo(string message);
        void LogWarning(string message);
        void LogError(string message, Exception exception = null);
        void LogDebug(string message);
    }
    
    public interface IPluginCache
    {
        Task<T> GetAsync<T>(string key);
        Task SetAsync<T>(string key, T value, TimeSpan? expiration = null);
        Task InvalidateAsync(string key);
        Task ClearAsync();
    }
    
    public interface IPluginEventBus
    {
        Task PublishAsync(string eventName, object data);
        void Subscribe(string eventName, Action<object> handler);
        void Unsubscribe(string eventName, Action<object> handler);
    }
    
    public interface IPluginFileSystem
    {
        string GetPluginDataPath();
        Task<string> ReadFileAsync(string relativePath);
        Task WriteFileAsync(string relativePath, string content);
        Task<byte[]> ReadBinaryAsync(string relativePath);
        Task WriteBinaryAsync(string relativePath, byte[] data);
        bool FileExists(string relativePath);
        void CreateDirectory(string relativePath);
    }
    
    public interface IPluginNotification
    {
        Task ShowInfoAsync(string message, int durationSeconds = 3);
        Task ShowWarningAsync(string message, int durationSeconds = 5);
        Task ShowErrorAsync(string message, int durationSeconds = 7);
        Task<bool> ShowConfirmAsync(string message, string title = "تایید");
        Task<string> ShowInputAsync(string prompt, string title = "ورودی", string defaultValue = "");
    }
    
    // Data Models
    public class TradeData
    {
        public Guid Id { get; set; }
        public string Symbol { get; set; }
        public string Type { get; set; }
        public DateTime EntryTime { get; set; }
        public decimal EntryPrice { get; set; }
        public DateTime? ExitTime { get; set; }
        public decimal? ExitPrice { get; set; }
        public decimal Volume { get; set; }
        public decimal Profit { get; set; }
        public decimal Commission { get; set; }
        public string Strategy { get; set; }
        public string Tags { get; set; }
    }
    
    public class QueryFilter
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Symbol { get; set; }
        public string Strategy { get; set; }
        public int Skip { get; set; } = 0;
        public int Take { get; set; } = 100;
        public string OrderBy { get; set; }
        public bool Descending { get; set; }
    }
    
    public class SecurityException : Exception
    {
        public SecurityException(string message) : base(message) { }
    }
}

// ===== پایان کد =====