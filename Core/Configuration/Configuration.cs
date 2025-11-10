// مسیر فایل: Core/Configuration/Configuration.cs
// ابتدای کد
using System;
using System.IO;
using System.Text.Json;

namespace TradingJournal.Core.Configuration
{
    public interface IConfiguration
    {
        AppSettings Settings { get; }
        void Load();
        void Save();
        T GetValue<T>(string key);
        void SetValue<T>(string key, T value);
    }

    public class Configuration : IConfiguration
    {
        private readonly string _settingsPath;
        private AppSettings _settings;

        public AppSettings Settings => _settings;

        public Configuration()
        {
            _settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            Load();
        }

        public void Load()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    _settings = JsonSerializer.Deserialize<AppSettings>(json);
                }
                else
                {
                    _settings = GetDefaultSettings();
                    Save();
                }
            }
            catch
            {
                _settings = GetDefaultSettings();
            }
        }

        public void Save()
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                var json = JsonSerializer.Serialize(_settings, options);
                File.WriteAllText(_settingsPath, json);
            }
            catch (Exception ex)
            {
                // Log error
                Console.WriteLine($"Error saving settings: {ex.Message}");
            }
        }

        public T GetValue<T>(string key)
        {
            var property = _settings.GetType().GetProperty(key);
            if (property != null)
            {
                return (T)property.GetValue(_settings);
            }
            return default;
        }

        public void SetValue<T>(string key, T value)
        {
            var property = _settings.GetType().GetProperty(key);
            if (property != null)
            {
                property.SetValue(_settings, value);
            }
        }

        private AppSettings GetDefaultSettings()
        {
            return new AppSettings
            {
                Language = "fa-IR",
                Theme = "Light",
                AccentColor = "Blue",
                FontSize = 14,
                FontFamily = "B Nazanin",
                
                DatabaseSettings = new DatabaseSettings
                {
                    Provider = "SQLite",
                    ConnectionString = "Data Source=Data/TradingJournal.db",
                    EnableLogging = false
                },

                BackupSettings = new BackupSettings
                {
                    AutoBackup = true,
                    BackupInterval = 24, // hours
                    MaxBackups = 10,
                    BackupPath = "Backups",
                    EncryptBackups = false
                },

                TradingSettings = new TradingSettings
                {
                    DefaultLeverage = 100,
                    DefaultRiskPercentage = 2,
                    DefaultCommission = 7,
                    ShowPipsCalculator = true,
                    AutoCalculateRiskReward = true
                },

                MetaTraderSettings = new MetaTraderSettings
                {
                    EnableAutoSync = false,
                    ServerUrl = "http://localhost:5000",
                    ApiKey = "",
                    SyncInterval = 60, // seconds
                    CaptureScreenshots = true
                },

                UISettings = new UISettings
                {
                    ShowSplashScreen = true,
                    AnimationsEnabled = true,
                    CompactMode = false,
                    ShowToolTips = true,
                    ConfirmDelete = true,
                    RememberWindowPosition = true,
                    WindowState = "Normal",
                    LastWindowWidth = 1400,
                    LastWindowHeight = 800
                }
            };
        }
    }

    public class AppSettings
    {
        public string Language { get; set; }
        public string Theme { get; set; }
        public string AccentColor { get; set; }
        public int FontSize { get; set; }
        public string FontFamily { get; set; }
        public DatabaseSettings DatabaseSettings { get; set; }
        public BackupSettings BackupSettings { get; set; }
        public TradingSettings TradingSettings { get; set; }
        public MetaTraderSettings MetaTraderSettings { get; set; }
        public UISettings UISettings { get; set; }
    }

    public class DatabaseSettings
    {
        public string Provider { get; set; }
        public string ConnectionString { get; set; }
        public bool EnableLogging { get; set; }
    }

    public class BackupSettings
    {
        public bool AutoBackup { get; set; }
        public int BackupInterval { get; set; }
        public int MaxBackups { get; set; }
        public string BackupPath { get; set; }
        public bool EncryptBackups { get; set; }
    }

    public class TradingSettings
    {
        public int DefaultLeverage { get; set; }
        public decimal DefaultRiskPercentage { get; set; }
        public decimal DefaultCommission { get; set; }
        public bool ShowPipsCalculator { get; set; }
        public bool AutoCalculateRiskReward { get; set; }
    }

    public class MetaTraderSettings
    {
        public bool EnableAutoSync { get; set; }
        public string ServerUrl { get; set; }
        public string ApiKey { get; set; }
        public int SyncInterval { get; set; }
        public bool CaptureScreenshots { get; set; }
    }

    public class UISettings
    {
        public bool ShowSplashScreen { get; set; }
        public bool AnimationsEnabled { get; set; }
        public bool CompactMode { get; set; }
        public bool ShowToolTips { get; set; }
        public bool ConfirmDelete { get; set; }
        public bool RememberWindowPosition { get; set; }
        public string WindowState { get; set; }
        public double LastWindowWidth { get; set; }
        public double LastWindowHeight { get; set; }
    }
}
// پایان کد