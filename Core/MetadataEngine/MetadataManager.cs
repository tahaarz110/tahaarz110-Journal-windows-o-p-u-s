// مسیر فایل: Core/MetadataEngine/MetadataManager.cs
// ابتدای کد
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TradingJournal.Core.MetadataEngine.Models;

namespace TradingJournal.Core.MetadataEngine
{
    public class MetadataManager
    {
        private readonly string _metadataPath;
        private readonly JsonSerializerOptions _jsonOptions;
        private Dictionary<string, FormMetadata> _formsCache;
        private Dictionary<string, TabMetadata> _tabsCache;
        private Dictionary<string, WidgetMetadata> _widgetsCache;

        public event EventHandler<MetadataChangedEventArgs> MetadataChanged;

        public MetadataManager()
        {
            _metadataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Metadata");
            Directory.CreateDirectory(_metadataPath);
            Directory.CreateDirectory(Path.Combine(_metadataPath, "Forms"));
            Directory.CreateDirectory(Path.Combine(_metadataPath, "Tabs"));
            Directory.CreateDirectory(Path.Combine(_metadataPath, "Widgets"));

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            _formsCache = new Dictionary<string, FormMetadata>();
            _tabsCache = new Dictionary<string, TabMetadata>();
            _widgetsCache = new Dictionary<string, WidgetMetadata>();

            LoadAllMetadata();
        }

        #region Forms Management

        public async Task<FormMetadata> SaveFormMetadataAsync(FormMetadata form)
        {
            if (form == null)
                throw new ArgumentNullException(nameof(form));

            form.ModifiedDate = DateTime.Now;
            
            var filePath = Path.Combine(_metadataPath, "Forms", $"{form.Name}.json");
            var json = JsonSerializer.Serialize(form, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json);

            _formsCache[form.Name] = form;
            
            OnMetadataChanged(new MetadataChangedEventArgs
            {
                Type = MetadataType.Form,
                Name = form.Name,
                Action = MetadataAction.Save
            });

            return form;
        }

        public FormMetadata SaveFormMetadata(FormMetadata form)
        {
            return SaveFormMetadataAsync(form).GetAwaiter().GetResult();
        }

        public async Task<FormMetadata> LoadFormMetadataAsync(string name)
        {
            if (_formsCache.ContainsKey(name))
                return _formsCache[name];

            var filePath = Path.Combine(_metadataPath, "Forms", $"{name}.json");
            
            if (!File.Exists(filePath))
                return null;

            var json = await File.ReadAllTextAsync(filePath);
            var form = JsonSerializer.Deserialize<FormMetadata>(json, _jsonOptions);
            
            _formsCache[name] = form;
            return form;
        }

        public FormMetadata GetFormMetadata(string name)
        {
            return LoadFormMetadataAsync(name).GetAwaiter().GetResult();
        }

        public List<string> GetAllFormNames()
        {
            var files = Directory.GetFiles(Path.Combine(_metadataPath, "Forms"), "*.json");
            return files.Select(f => Path.GetFileNameWithoutExtension(f)).ToList();
        }

        public async Task<bool> DeleteFormAsync(string name)
        {
            var filePath = Path.Combine(_metadataPath, "Forms", $"{name}.json");
            
            if (!File.Exists(filePath))
                return false;

            File.Delete(filePath);
            _formsCache.Remove(name);
            
            OnMetadataChanged(new MetadataChangedEventArgs
            {
                Type = MetadataType.Form,
                Name = name,
                Action = MetadataAction.Delete
            });

            return true;
        }

        public void DeleteForm(string name)
        {
            DeleteFormAsync(name).GetAwaiter().GetResult();
        }

        public async Task<string> ExportFormAsync(string name, string exportPath)
        {
            var form = await LoadFormMetadataAsync(name);
            if (form == null)
                return null;

            var json = JsonSerializer.Serialize(form, _jsonOptions);
            await File.WriteAllTextAsync(exportPath, json);
            return exportPath;
        }

        public void ExportForm(string name, string exportPath)
        {
            ExportFormAsync(name, exportPath).GetAwaiter().GetResult();
        }

        public async Task<string> ImportFormAsync(string importPath)
        {
            if (!File.Exists(importPath))
                return null;

            var json = await File.ReadAllTextAsync(importPath);
            var form = JsonSerializer.Deserialize<FormMetadata>(json, _jsonOptions);
            
            if (form != null)
            {
                // Check for duplicate names
                var existingForms = GetAllFormNames();
                var baseName = form.Name;
                var counter = 1;
                
                while (existingForms.Contains(form.Name))
                {
                    form.Name = $"{baseName}_{counter++}";
                }

                await SaveFormMetadataAsync(form);
                return form.Name;
            }

            return null;
        }

        public string ImportForm(string importPath)
        {
            return ImportFormAsync(importPath).GetAwaiter().GetResult();
        }

        #endregion

        #region Tabs Management

        public async Task<TabMetadata> SaveTabMetadataAsync(TabMetadata tab)
        {
            if (tab == null)
                throw new ArgumentNullException(nameof(tab));

            var filePath = Path.Combine(_metadataPath, "Tabs", $"{tab.Name}.json");
            var json = JsonSerializer.Serialize(tab, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json);

            _tabsCache[tab.Name] = tab;
            
            OnMetadataChanged(new MetadataChangedEventArgs
            {
                Type = MetadataType.Tab,
                Name = tab.Name,
                Action = MetadataAction.Save
            });

            return tab;
        }

        public async Task<TabMetadata> LoadTabMetadataAsync(string name)
        {
            if (_tabsCache.ContainsKey(name))
                return _tabsCache[name];

            var filePath = Path.Combine(_metadataPath, "Tabs", $"{name}.json");
            
            if (!File.Exists(filePath))
                return null;

            var json = await File.ReadAllTextAsync(filePath);
            var tab = JsonSerializer.Deserialize<TabMetadata>(json, _jsonOptions);
            
            _tabsCache[name] = tab;
            return tab;
        }

        public List<TabMetadata> GetAllTabs()
        {
            var files = Directory.GetFiles(Path.Combine(_metadataPath, "Tabs"), "*.json");
            var tabs = new List<TabMetadata>();

            foreach (var file in files)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var tab = JsonSerializer.Deserialize<TabMetadata>(json, _jsonOptions);
                    if (tab != null)
                        tabs.Add(tab);
                }
                catch
                {
                    // Ignore corrupted files
                }
            }

            return tabs.OrderBy(t => t.Order).ToList();
        }

        public async Task<bool> DeleteTabAsync(string name)
        {
            var filePath = Path.Combine(_metadataPath, "Tabs", $"{name}.json");
            
            if (!File.Exists(filePath))
                return false;

            File.Delete(filePath);
            _tabsCache.Remove(name);
            
            OnMetadataChanged(new MetadataChangedEventArgs
            {
                Type = MetadataType.Tab,
                Name = name,
                Action = MetadataAction.Delete
            });

            return true;
        }

        #endregion

        #region Widgets Management

        public async Task<WidgetMetadata> SaveWidgetMetadataAsync(WidgetMetadata widget)
        {
            if (widget == null)
                throw new ArgumentNullException(nameof(widget));

            var filePath = Path.Combine(_metadataPath, "Widgets", $"{widget.Name}.json");
            var json = JsonSerializer.Serialize(widget, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json);

            _widgetsCache[widget.Name] = widget;
            
            OnMetadataChanged(new MetadataChangedEventArgs
            {
                Type = MetadataType.Widget,
                Name = widget.Name,
                Action = MetadataAction.Save
            });

            return widget;
        }

        public async Task<WidgetMetadata> LoadWidgetMetadataAsync(string name)
        {
            if (_widgetsCache.ContainsKey(name))
                return _widgetsCache[name];

            var filePath = Path.Combine(_metadataPath, "Widgets", $"{name}.json");
            
            if (!File.Exists(filePath))
                return null;

            var json = await File.ReadAllTextAsync(filePath);
            var widget = JsonSerializer.Deserialize<WidgetMetadata>(json, _jsonOptions);
            
            _widgetsCache[name] = widget;
            return widget;
        }

        public List<WidgetMetadata> GetAllWidgets()
        {
            var files = Directory.GetFiles(Path.Combine(_metadataPath, "Widgets"), "*.json");
            var widgets = new List<WidgetMetadata>();

            foreach (var file in files)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var widget = JsonSerializer.Deserialize<WidgetMetadata>(json, _jsonOptions);
                    if (widget != null)
                        widgets.Add(widget);
                }
                catch
                {
                    // Ignore corrupted files
                }
            }

            return widgets;
        }

        public async Task<bool> DeleteWidgetAsync(string name)
        {
            var filePath = Path.Combine(_metadataPath, "Widgets", $"{name}.json");
            
            if (!File.Exists(filePath))
                return false;

            File.Delete(filePath);
            _widgetsCache.Remove(name);
            
            OnMetadataChanged(new MetadataChangedEventArgs
            {
                Type = MetadataType.Widget,
                Name = name,
                Action = MetadataAction.Delete
            });

            return true;
        }

        #endregion

        #region Backup and Restore

        public async Task<string> BackupMetadataAsync(string backupPath)
        {
            var tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempFolder);

            try
            {
                // Copy all metadata files
                CopyDirectory(_metadataPath, tempFolder);

                // Create zip file
                System.IO.Compression.ZipFile.CreateFromDirectory(tempFolder, backupPath);
                
                return backupPath;
            }
            finally
            {
                // Clean up temp folder
                if (Directory.Exists(tempFolder))
                    Directory.Delete(tempFolder, true);
            }
        }

        public async Task<bool> RestoreMetadataAsync(string backupPath)
        {
            if (!File.Exists(backupPath))
                return false;

            var tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            try
            {
                // Extract backup
                System.IO.Compression.ZipFile.ExtractToDirectory(backupPath, tempFolder);

                // Clear current metadata
                ClearAllMetadata();

                // Restore metadata
                CopyDirectory(tempFolder, _metadataPath);

                // Reload caches
                LoadAllMetadata();

                OnMetadataChanged(new MetadataChangedEventArgs
                {
                    Type = MetadataType.All,
                    Action = MetadataAction.Restore
                });

                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                // Clean up temp folder
                if (Directory.Exists(tempFolder))
                    Directory.Delete(tempFolder, true);
            }
        }

        #endregion

        #region Private Methods

        private void LoadAllMetadata()
        {
            // Load forms
            var formFiles = Directory.GetFiles(Path.Combine(_metadataPath, "Forms"), "*.json");
            foreach (var file in formFiles)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var form = JsonSerializer.Deserialize<FormMetadata>(json, _jsonOptions);
                    if (form != null)
                        _formsCache[form.Name] = form;
                }
                catch { }
            }

            // Load tabs
            var tabFiles = Directory.GetFiles(Path.Combine(_metadataPath, "Tabs"), "*.json");
            foreach (var file in tabFiles)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var tab = JsonSerializer.Deserialize<TabMetadata>(json, _jsonOptions);
                    if (tab != null)
                        _tabsCache[tab.Name] = tab;
                }
                catch { }
            }

            // Load widgets
            var widgetFiles = Directory.GetFiles(Path.Combine(_metadataPath, "Widgets"), "*.json");
            foreach (var file in widgetFiles)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var widget = JsonSerializer.Deserialize<WidgetMetadata>(json, _jsonOptions);
                    if (widget != null)
                        _widgetsCache[widget.Name] = widget;
                }
                catch { }
            }
        }

        private void ClearAllMetadata()
        {
            var directories = new[] { "Forms", "Tabs", "Widgets" };
            
            foreach (var dir in directories)
            {
                var path = Path.Combine(_metadataPath, dir);
                if (Directory.Exists(path))
                {
                    foreach (var file in Directory.GetFiles(path))
                    {
                        File.Delete(file);
                    }
                }
            }

            _formsCache.Clear();
            _tabsCache.Clear();
            _widgetsCache.Clear();
        }

        private void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var fileName = Path.GetFileName(file);
                var destFile = Path.Combine(targetDir, fileName);
                File.Copy(file, destFile, true);
            }

            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                var dirName = Path.GetFileName(subDir);
                var destDir = Path.Combine(targetDir, dirName);
                CopyDirectory(subDir, destDir);
            }
        }

        protected virtual void OnMetadataChanged(MetadataChangedEventArgs e)
        {
            MetadataChanged?.Invoke(this, e);
        }

        #endregion
    }

    public class MetadataChangedEventArgs : EventArgs
    {
        public MetadataType Type { get; set; }
        public string Name { get; set; }
        public MetadataAction Action { get; set; }
        public object Data { get; set; }
    }

    public enum MetadataType
    {
        Form,
        Tab,
        Widget,
        All
    }

    public enum MetadataAction
    {
        Save,
        Delete,
        Restore,
        Import,
        Export
    }
}
// پایان کد