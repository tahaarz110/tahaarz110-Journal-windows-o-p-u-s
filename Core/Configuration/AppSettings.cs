// ابتدای فایل: Core/Configuration/AppSettings.cs
// مسیر: /Core/Configuration/AppSettings.cs

using System;
using System.IO;
using Newtonsoft.Json;

namespace TradingJournal.Core.Configuration
{
    public class AppSettings
    {
        private static AppSettings? _instance;
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TradingJournal",
            "settings.json"
        );

        public string Language { get; set; } = "fa-IR";
        public string Theme { get; set; } = "Dark";
        public bool IsRTL { get; set; } = true;
        public string DatabasePath { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TradingJournal",
            "trading.db"
        );
        public string ImagesPath { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TradingJournal",
            "Images"
        );
        public string BackupPath { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "TradingJournal",
            "Backups"
        );
        public string MetadataPath { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TradingJournal",
            "Metadata"
        );
        public int AutoSaveInterval { get; set; } = 60; // seconds
        public bool EnableAutoBackup { get; set; } = true;
        public int MaxBackupFiles { get; set; } = 10;

        public static AppSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    Load();
                }
                return _instance!;
            }
        }

        public static void Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    _instance = JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
                }
                else
                {
                    _instance = new AppSettings();
                    Save();
                }
            }
            catch
            {
                _instance = new AppSettings();
            }

            // Create directories if not exist
            Directory.CreateDirectory(Path.GetDirectoryName(_instance.DatabasePath)!);
            Directory.CreateDirectory(_instance.ImagesPath);
            Directory.CreateDirectory(_instance.BackupPath);
            Directory.CreateDirectory(_instance.MetadataPath);
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
                var json = JsonConvert.SerializeObject(_instance, Formatting.Indented);
                File.WriteAllText(SettingsPath, json);
            }
            catch { }
        }
    }
}

// پایان فایل: Core/Configuration/AppSettings.cs