// 📁 Core/Backup/BackupModels.cs
// ===== شروع کد =====

using System;
using System.Collections.Generic;

namespace TradingJournal.Core.Backup
{
    public class BackupOptions
    {
        public BackupType Type { get; set; } = BackupType.Manual;
        public string Description { get; set; }
        public bool IncludeDatabase { get; set; } = true;
        public bool IncludeImages { get; set; } = true;
        public bool IncludeSettings { get; set; } = true;
        public bool IncludePlugins { get; set; } = true;
        public bool Encrypt { get; set; } = false;
        public string Password { get; set; }
        public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Optimal;
    }
    
    public class RestoreOptions
    {
        public bool RestoreDatabase { get; set; } = true;
        public bool RestoreImages { get; set; } = true;
        public bool RestoreSettings { get; set; } = true;
        public bool RestorePlugins { get; set; } = true;
        public bool CreateBackupBeforeRestore { get; set; } = true;
        public bool ClearExistingImages { get; set; } = false;
        public string Password { get; set; }
    }
    
    public class BackupResult
    {
        public bool Success { get; set; }
        public string BackupPath { get; set; }
        public long BackupSize { get; set; }
        public string Checksum { get; set; }
        public string ErrorMessage { get; set; }
        public BackupMetadata Metadata { get; set; }
        public TimeSpan Duration { get; set; }
    }
    
    public class RestoreResult
    {
        public bool Success { get; set; }
        public string RestoredFrom { get; set; }
        public DateTime RestoreDate { get; set; }
        public List<string> RestoredComponents { get; set; } = new List<string>();
        public string ErrorMessage { get; set; }
        public BackupMetadata Metadata { get; set; }
        public TimeSpan Duration { get; set; }
    }
    
    public class BackupMetadata
    {
        public string Version { get; set; }
        public DateTime CreatedAt { get; set; }
        public string MachineName { get; set; }
        public string Description { get; set; }
        public BackupType Type { get; set; }
        public List<string> IncludedComponents { get; set; }
        public DatabaseInfo DatabaseInfo { get; set; }
        public ImagesInfo ImagesInfo { get; set; }
        public PluginsInfo PluginsInfo { get; set; }
    }
    
    public class DatabaseInfo
    {
        public int RecordCount { get; set; }
        public long Size { get; set; }
    }
    
    public class ImagesInfo
    {
        public int Count { get; set; }
        public long TotalSize { get; set; }
    }
    
    public class PluginsInfo
    {
        public int Count { get; set; }
        public List<string> PluginIds { get; set; }
    }
    
    public class BackupInfo
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Description { get; set; }
        public bool IsEncrypted { get; set; }
        public BackupMetadata Metadata { get; set; }
    }
    
    public enum BackupType
    {
        Manual,
        Automatic,
        Scheduled,
        BeforeUpdate,
        BeforeRestore
    }
    
    public enum CompressionLevel
    {
        Fastest,
        Optimal,
        Maximum
    }
    
    public class BackupProgressEventArgs : EventArgs
    {
        public string Message { get; set; }
        public int Percentage { get; set; }
    }
    
    public class BackupEventArgs : EventArgs
    {
        public BackupResult Result { get; set; }
        public RestoreResult RestoreResult { get; set; }
    }
}

// ===== پایان کد =====