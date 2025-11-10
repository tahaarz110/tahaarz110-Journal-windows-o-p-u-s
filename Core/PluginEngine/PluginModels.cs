// 📁 Core/PluginEngine/PluginModels.cs
// ===== شروع کد =====

using System;
using System.Collections.Generic;

namespace TradingJournal.Core.PluginEngine
{
    /// <summary>
    /// کانتکست اجرای پلاگین
    /// </summary>
    public interface IPluginContext
    {
        /// <summary>
        /// دسترسی به دیتابیس (فقط خواندنی)
        /// </summary>
        IPluginDataAccess DataAccess { get; }
        
        /// <summary>
        /// دسترسی به تنظیمات
        /// </summary>
        IPluginSettingsStore SettingsStore { get; }
        
        /// <summary>
        /// سرویس لاگ
        /// </summary>
        IPluginLogger Logger { get; }
        
        /// <summary>
        /// سرویس کش
        /// </summary>
        IPluginCache Cache { get; }
        
        /// <summary>
        /// Event Bus برای ارسال رویداد
        /// </summary>
        IPluginEventBus EventBus { get; }
    }
    
    /// <summary>
    /// تنظیمات پلاگین
    /// </summary>
    public class PluginSetting
    {
        public string Key { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public SettingType Type { get; set; }
        public object DefaultValue { get; set; }
        public object CurrentValue { get; set; }
        public bool IsRequired { get; set; }
        public string ValidationRule { get; set; }
        public Dictionary<string, object> Metadata { get; set; }
    }
    
    public enum SettingType
    {
        Text,
        Number,
        Boolean,
        Date,
        Time,
        DateTime,
        Dropdown,
        MultiSelect,
        Color,
        File,
        Password
    }
    
    /// <summary>
    /// درخواست اجرای پلاگین
    /// </summary>
    public class PluginRequest
    {
        public string Action { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        public DateTime RequestTime { get; set; }
        public string UserId { get; set; }
    }
    
    /// <summary>
    /// نتیجه اجرای پلاگین
    /// </summary>
    public class PluginResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public PluginResultType ResultType { get; set; }
        public object Data { get; set; }
        public Dictionary<string, object> Metadata { get; set; }
        public List<PluginError> Errors { get; set; }
    }
    
    public enum PluginResultType
    {
        Data,           // داده خام
        Table,          // جدول
        Chart,          // نمودار
        Card,           // کارت اطلاعاتی
        Notification,   // پیام
        File,          // فایل
        Html,          // محتوای HTML
        Action         // درخواست اجرای عملیات
    }
    
    /// <summary>
    /// ویجت پلاگین برای نمایش در داشبورد
    /// </summary>
    public class PluginWidget
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public WidgetType Type { get; set; }
        public WidgetSize DefaultSize { get; set; }
        public bool IsResizable { get; set; }
        public int RefreshInterval { get; set; } // ثانیه
        public Func<Task<WidgetData>> DataProvider { get; set; }
    }
    
    public enum WidgetType
    {
        Chart,
        Table,
        Card,
        List,
        Calendar,
        Custom
    }
    
    public class WidgetSize
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int MinWidth { get; set; }
        public int MinHeight { get; set; }
        public int MaxWidth { get; set; }
        public int MaxHeight { get; set; }
    }
    
    public class WidgetData
    {
        public object Data { get; set; }
        public Dictionary<string, object> Configuration { get; set; }
        public DateTime UpdateTime { get; set; }
    }
    
    /// <summary>
    /// رویداد پلاگین
    /// </summary>
    public class PluginEvent
    {
        public string EventName { get; set; }
        public object EventData { get; set; }
        public DateTime EventTime { get; set; }
        public string Source { get; set; }
    }
    
    /// <summary>
    /// خطای پلاگین
    /// </summary>
    public class PluginError
    {
        public string Code { get; set; }
        public string Message { get; set; }
        public string Details { get; set; }
        public ErrorSeverity Severity { get; set; }
    }
    
    public enum ErrorSeverity
    {
        Info,
        Warning,
        Error,
        Critical
    }
}

// ===== پایان کد =====