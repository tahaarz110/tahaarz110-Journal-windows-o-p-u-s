// مسیر فایل: Core/PluginEngine/IPlugin.cs
// ابتدای کد
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;

namespace TradingJournal.Core.PluginEngine
{
    /// <summary>
    /// رابط اصلی که همه پلاگین‌ها باید پیاده‌سازی کنند
    /// </summary>
    public interface IPlugin
    {
        /// <summary>
        /// شناسه یکتای پلاگین
        /// </summary>
        string Id { get; }

        /// <summary>
        /// نام پلاگین
        /// </summary>
        string Name { get; }

        /// <summary>
        /// نسخه پلاگین
        /// </summary>
        Version Version { get; }

        /// <summary>
        /// توضیحات پلاگین
        /// </summary>
        string Description { get; }

        /// <summary>
        /// نام توسعه‌دهنده
        /// </summary>
        string Author { get; }

        /// <summary>
        /// وب‌سایت پلاگین
        /// </summary>
        string Website { get; }

        /// <summary>
        /// آیکون پلاگین
        /// </summary>
        string Icon { get; }

        /// <summary>
        /// دسته‌بندی پلاگین
        /// </summary>
        PluginCategory Category { get; }

        /// <summary>
        /// اولویت بارگذاری
        /// </summary>
        int LoadPriority { get; }

        /// <summary>
        /// وابستگی‌های پلاگین
        /// </summary>
        List<PluginDependency> Dependencies { get; }

        /// <summary>
        /// مجوزهای مورد نیاز
        /// </summary>
        List<PluginPermission> RequiredPermissions { get; }

        /// <summary>
        /// راه‌اندازی پلاگین
        /// </summary>
        Task<bool> InitializeAsync(IPluginHost host);

        /// <summary>
        /// شروع پلاگین
        /// </summary>
        Task StartAsync();

        /// <summary>
        /// توقف پلاگین
        /// </summary>
        Task StopAsync();

        /// <summary>
        /// حذف پلاگین
        /// </summary>
        Task UnloadAsync();

        /// <summary>
        /// دریافت تنظیمات پلاگین
        /// </summary>
        PluginSettings GetSettings();

        /// <summary>
        /// ذخیره تنظیمات پلاگین
        /// </summary>
        Task SaveSettingsAsync(PluginSettings settings);
    }

    /// <summary>
    /// رابط میزبان پلاگین
    /// </summary>
    public interface IPluginHost
    {
        /// <summary>
        /// دسترسی به سرویس‌های سیستم
        /// </summary>
        T GetService<T>() where T : class;

        /// <summary>
        /// ثبت سرویس جدید
        /// </summary>
        void RegisterService<T>(T service) where T : class;

        /// <summary>
        /// ثبت منوی پلاگین
        /// </summary>
        void RegisterMenu(PluginMenuItem menuItem);

        /// <summary>
        /// ثبت تب پلاگین
        /// </summary>
        void RegisterTab(PluginTab tab);

        /// <summary>
        /// ثبت ویجت پلاگین
        /// </summary>
        void RegisterWidget(PluginWidget widget);

        /// <summary>
        /// ثبت دستور پلاگین
        /// </summary>
        void RegisterCommand(PluginCommand command);

        /// <summary>
        /// نمایش پیام
        /// </summary>
        void ShowMessage(string message, MessageType type = MessageType.Info);

        /// <summary>
        /// نمایش دیالوگ
        /// </summary>
        Task<bool> ShowDialogAsync(string title, FrameworkElement content);

        /// <summary>
        /// دسترسی به دیتابیس
        /// </summary>
        IPluginDataAccess GetDataAccess();

        /// <summary>
        /// دسترسی به فایل سیستم
        /// </summary>
        IPluginFileSystem GetFileSystem();

        /// <summary>
        /// ارسال رویداد
        /// </summary>
        void PublishEvent(PluginEvent eventData);

        /// <summary>
        /// اشتراک در رویداد
        /// </summary>
        void SubscribeEvent(string eventName, Action<PluginEvent> handler);

        /// <summary>
        /// لغو اشتراک رویداد
        /// </summary>
        void UnsubscribeEvent(string eventName, Action<PluginEvent> handler);
    }

    /// <summary>
    /// دسته‌بندی پلاگین
    /// </summary>
    public enum PluginCategory
    {
        Analysis,
        Trading,
        Reporting,
        DataImportExport,
        Charting,
        RiskManagement,
        Automation,
        Communication,
        Utility,
        Other
    }

    /// <summary>
    /// نوع پیام
    /// </summary>
    public enum MessageType
    {
        Info,
        Success,
        Warning,
        Error
    }

    /// <summary>
    /// وابستگی پلاگین
    /// </summary>
    public class PluginDependency
    {
        public string PluginId { get; set; }
        public Version MinVersion { get; set; }
        public bool IsRequired { get; set; }
    }

    /// <summary>
    /// مجوز پلاگین
    /// </summary>
    public class PluginPermission
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public PermissionLevel Level { get; set; }
    }

    public enum PermissionLevel
    {
        Read,
        Write,
        Execute,
        Admin
    }

    /// <summary>
    /// تنظیمات پلاگین
    /// </summary>
    public class PluginSettings
    {
        public Dictionary<string, object> Values { get; set; } = new Dictionary<string, object>();

        public T GetValue<T>(string key, T defaultValue = default)
        {
            if (Values.TryGetValue(key, out var value))
            {
                try
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    return defaultValue;
                }
            }
            return defaultValue;
        }

        public void SetValue<T>(string key, T value)
        {
            Values[key] = value;
        }
    }

    /// <summary>
    /// آیتم منوی پلاگین
    /// </summary>
    public class PluginMenuItem
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Icon { get; set; }
        public Action OnClick { get; set; }
        public List<PluginMenuItem> SubItems { get; set; }
    }

    /// <summary>
    /// تب پلاگین
    /// </summary>
    public class PluginTab
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Icon { get; set; }
        public FrameworkElement Content { get; set; }
        public int Order { get; set; }
    }

    /// <summary>
    /// ویجت پلاگین
    /// </summary>
    public class PluginWidget
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public WidgetSize Size { get; set; }
        public FrameworkElement Content { get; set; }
        public TimeSpan? RefreshInterval { get; set; }
    }

    public enum WidgetSize
    {
        Small,
        Medium,
        Large,
        ExtraLarge
    }

    /// <summary>
    /// دستور پلاگین
    /// </summary>
    public class PluginCommand
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Shortcut { get; set; }
        public Action<object> Execute { get; set; }
        public Func<object, bool> CanExecute { get; set; }
    }

    /// <summary>
    /// رویداد پلاگین
    /// </summary>
    public class PluginEvent
    {
        public string Name { get; set; }
        public string Source { get; set; }
        public DateTime Timestamp { get; set; }
        public object Data { get; set; }
    }

    /// <summary>
    /// دسترسی به داده برای پلاگین
    /// </summary>
    public interface IPluginDataAccess
    {
        Task<T> GetAsync<T>(string key) where T : class;
        Task SaveAsync<T>(string key, T data) where T : class;
        Task DeleteAsync(string key);
        Task<List<T>> QueryAsync<T>(string query) where T : class;
        Task ExecuteAsync(string command);
    }

    /// <summary>
    /// دسترسی به فایل سیستم برای پلاگین
    /// </summary>
    public interface IPluginFileSystem
    {
        string GetPluginDataPath(string pluginId);
        Task<string> ReadTextAsync(string path);
        Task WriteTextAsync(string path, string content);
        Task<byte[]> ReadBytesAsync(string path);
        Task WriteBytesAsync(string path, byte[] data);
        bool FileExists(string path);
        void DeleteFile(string path);
    }
}
// پایان کد