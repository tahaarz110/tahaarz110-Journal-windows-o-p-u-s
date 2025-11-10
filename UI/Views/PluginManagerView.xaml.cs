// مسیر فایل: UI/Views/PluginManagerView.xaml.cs
// ابتدای کد
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TradingJournal.Core.PluginEngine;

namespace TradingJournal.UI.Views
{
    public partial class PluginManagerView : UserControl
    {
        private readonly PluginManager _pluginManager;
        public ObservableCollection<PluginViewModel> Plugins { get; set; }

        public PluginManagerView(PluginManager pluginManager)
        {
            InitializeComponent();
            _pluginManager = pluginManager;
            
            Plugins = new ObservableCollection<PluginViewModel>();
            DataContext = this;
            
            LoadPlugins();
            
            _pluginManager.PluginLoaded += OnPluginLoaded;
            _pluginManager.PluginUnloaded += OnPluginUnloaded;
            _pluginManager.PluginError += OnPluginError;
        }

        private async void LoadPlugins()
        {
            ShowProgress(true);
            
            try
            {
                await _pluginManager.LoadAllPluginsAsync();
                
                foreach (var pluginInfo in _pluginManager.PluginInfos.Values)
                {
                    var plugin = _pluginManager.LoadedPlugins[pluginInfo.Id];
                    Plugins.Add(new PluginViewModel
                    {
                        Id = plugin.Id,
                        Name = plugin.Name,
                        Version = plugin.Version.ToString(),
                        Description = plugin.Description,
                        Author = plugin.Author,
                        Category = plugin.Category.ToString(),
                        Status = pluginInfo.Status,
                        Icon = GetPluginIcon(plugin.Category)
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطا در بارگذاری پلاگین‌ها: {ex.Message}", "خطا", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ShowProgress(false);
            }
        }

        private void OnPluginLoaded(object sender, PluginManager.PluginEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                var existing = Plugins.FirstOrDefault(p => p.Id == e.Plugin.Id);
                if (existing == null)
                {
                    Plugins.Add(new PluginViewModel
                    {
                        Id = e.Plugin.Id,
                        Name = e.Plugin.Name,
                        Version = e.Plugin.Version.ToString(),
                        Description = e.Plugin.Description,
                        Author = e.Plugin.Author,
                        Category = e.Plugin.Category.ToString(),
                        Status = PluginManager.PluginStatus.Running,
                        Icon = GetPluginIcon(e.Plugin.Category)
                    });
                }
                else
                {
                    existing.Status = PluginManager.PluginStatus.Running;
                }
            });
        }

        private void OnPluginUnloaded(object sender, PluginManager.PluginEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                var plugin = Plugins.FirstOrDefault(p => p.Id == e.Plugin.Id);
                if (plugin != null)
                {
                    Plugins.Remove(plugin);
                }
            });
        }

        private void OnPluginError(object sender, PluginManager.PluginErrorEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show($"خطا در پلاگین: {e.Error}", "خطای پلاگین", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }

        private async void OnInstallPlugin(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Plugin Files (*.dll)|*.dll",
                Title = "انتخاب فایل پلاگین"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    ShowProgress(true);
                    
                    // Copy plugin to plugins folder
                    var pluginName = System.IO.Path.GetFileName(dialog.FileName);
                    var targetPath = System.IO.Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory, 
                        "Plugins", 
                        pluginName);
                    
                    System.IO.File.Copy(dialog.FileName, targetPath, true);
                    
                    // Load plugin
                    await _pluginManager.LoadPluginAsync(targetPath);
                    
                    MessageBox.Show("پلاگین با موفقیت نصب شد", "موفقیت", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"خطا در نصب پلاگین: {ex.Message}", "خطا", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    ShowProgress(false);
                }
            }
        }

        private async void OnEnablePlugin(object sender, RoutedEventArgs e)
        {
            if (PluginsList.SelectedItem is PluginViewModel plugin)
            {
                try
                {
                    await _pluginManager.EnablePluginAsync(plugin.Id);
                    plugin.Status = PluginManager.PluginStatus.Running;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"خطا در فعال‌سازی پلاگین: {ex.Message}", "خطا", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void OnDisablePlugin(object sender, RoutedEventArgs e)
        {
            if (PluginsList.SelectedItem is PluginViewModel plugin)
            {
                try
                {
                    await _pluginManager.DisablePluginAsync(plugin.Id);
                    plugin.Status = PluginManager.PluginStatus.Stopped;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"خطا در غیرفعال‌سازی پلاگین: {ex.Message}", "خطا", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void OnUninstallPlugin(object sender, RoutedEventArgs e)
        {
            if (PluginsList.SelectedItem is PluginViewModel plugin)
            {
                var result = MessageBox.Show(
                    $"آیا از حذف پلاگین '{plugin.Name}' مطمئن هستید؟",
                    "تأیید حذف",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        await _pluginManager.UnloadPluginAsync(plugin.Id);
                        Plugins.Remove(plugin);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"خطا در حذف پلاگین: {ex.Message}", "خطا", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private async void OnConfigurePlugin(object sender, RoutedEventArgs e)
        {
            if (PluginsList.SelectedItem is PluginViewModel plugin)
            {
                try
                {
                    var settings = await _pluginManager.GetPluginSettingsAsync(plugin.Id);
                    
                    // Show settings dialog
                    var dialog = new PluginSettingsDialog(plugin.Name, settings);
                    if (dialog.ShowDialog() == true)
                    {
                        await _pluginManager.SavePluginSettingsAsync(plugin.Id, dialog.Settings);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"خطا در باز کردن تنظیمات: {ex.Message}", "خطا", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OnRefresh(object sender, RoutedEventArgs e)
        {
            Plugins.Clear();
            LoadPlugins();
        }

        private void ShowProgress(bool show)
        {
            ProgressBar.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }

        private string GetPluginIcon(PluginCategory category)
        {
            return category switch
            {
                PluginCategory.Analysis => "ChartLine",
                PluginCategory.Trading => "TrendingUp",
                PluginCategory.Reporting => "FileDocument",
                PluginCategory.DataImportExport => "DatabaseImport",
                PluginCategory.Charting => "ChartBar",
                PluginCategory.RiskManagement => "Shield",
                PluginCategory.Automation => "Robot",
                PluginCategory.Communication => "Message",
                PluginCategory.Utility => "Tools",
                _ => "Extension"
            };
        }
    }

    public class PluginViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        private PluginManager.PluginStatus _status;

        public string Id { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public string Description { get; set; }
        public string Author { get; set; }
        public string Category { get; set; }
        public string Icon { get; set; }
        
        public PluginManager.PluginStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged(nameof(Status));
                OnPropertyChanged(nameof(StatusColor));
            }
        }

        public string StatusColor => Status switch
        {
            PluginManager.PluginStatus.Running => "Green",
            PluginManager.PluginStatus.Stopped => "Orange",
            PluginManager.PluginStatus.Error => "Red",
            _ => "Gray"
        };

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }
}
// پایان کد