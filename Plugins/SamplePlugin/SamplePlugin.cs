// مسیر فایل: Plugins/SamplePlugin/SamplePlugin.cs
// ابتدای کد
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TradingJournal.Core.PluginEngine;

namespace TradingJournal.Plugins.Sample
{
    public class SamplePlugin : IPlugin
    {
        private IPluginHost _host;
        private PluginSettings _settings;
        private bool _isRunning;

        public string Id => "sample-plugin";
        public string Name => "نمونه پلاگین";
        public Version Version => new Version(1, 0, 0);
        public string Description => "یک پلاگین نمونه برای نمایش قابلیت‌های سیستم پلاگین";
        public string Author => "توسعه‌دهنده";
        public string Website => "https://example.com";
        public string Icon => "Extension";
        public PluginCategory Category => PluginCategory.Utility;
        public int LoadPriority => 100;

        public List<PluginDependency> Dependencies => new List<PluginDependency>();

        public List<PluginPermission> RequiredPermissions => new List<PluginPermission>
        {
            new PluginPermission 
            { 
                Name = "ReadTrades", 
                Description = "خواندن اطلاعات معاملات",
                Level = PermissionLevel.Read 
            }
        };

        public async Task<bool> InitializeAsync(IPluginHost host)
        {
            try
            {
                _host = host;
                _settings = new PluginSettings();
                
                // Load saved settings
                var dataAccess = _host.GetDataAccess();
                var savedSettings = await dataAccess.GetAsync<PluginSettings>($"{Id}_settings");
                if (savedSettings != null)
                {
                    _settings = savedSettings;
                }

                // Register menu
                _host.RegisterMenu(new PluginMenuItem
                {
                    Id = $"{Id}_menu",
                    Title = "نمونه پلاگین",
                    Icon = "Extension",
                    OnClick = ShowPluginWindow
                });

                // Register tab
                _host.RegisterTab(new PluginTab
                {
                    Id = $"{Id}_tab",
                    Title = "تب نمونه",
                    Icon = "Tab",
                    Content = CreateTabContent(),
                    Order = 999
                });

                // Register widget
                _host.RegisterWidget(new PluginWidget
                {
                    Id = $"{Id}_widget",
                    Title = "ویجت نمونه",
                    Size = WidgetSize.Medium,
                    Content = CreateWidgetContent(),
                    RefreshInterval = TimeSpan.FromSeconds(30)
                });

                // Register command
                _host.RegisterCommand(new PluginCommand
                {
                    Id = $"{Id}_command",
                    Name = "دستور نمونه",
                    Description = "اجرای عملیات نمونه",
                    Shortcut = "Ctrl+Alt+S",
                    Execute = ExecuteSampleCommand,
                    CanExecute = _ => _isRunning
                });

                // Subscribe to events
                _host.SubscribeEvent("TradeAdded", OnTradeAdded);
                _host.SubscribeEvent("TradeUpdated", OnTradeUpdated);

                _host.ShowMessage("پلاگین نمونه با موفقیت بارگذاری شد", MessageType.Success);
                
                return true;
            }
            catch (Exception ex)
            {
                _host?.ShowMessage($"خطا در راه‌اندازی پلاگین: {ex.Message}", MessageType.Error);
                return false;
            }
        }

        public async Task StartAsync()
        {
            _isRunning = true;
            _host.ShowMessage("پلاگین نمونه شروع به کار کرد", MessageType.Info);
            
            // Start background tasks if needed
            await Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            _isRunning = false;
            _host.ShowMessage("پلاگین نمونه متوقف شد", MessageType.Info);
            
            // Stop background tasks
            await Task.CompletedTask;
        }

        public async Task UnloadAsync()
        {
            // Cleanup
            _host.UnsubscribeEvent("TradeAdded", OnTradeAdded);
            _host.UnsubscribeEvent("TradeUpdated", OnTradeUpdated);
            
            // Save settings
            await SaveSettingsAsync(_settings);
            
            _host.ShowMessage("پلاگین نمونه حذف شد", MessageType.Info);
        }

        public PluginSettings GetSettings()
        {
            return _settings;
        }

        public async Task SaveSettingsAsync(PluginSettings settings)
        {
            _settings = settings;
            var dataAccess = _host.GetDataAccess();
            await dataAccess.SaveAsync($"{Id}_settings", settings);
        }

        private FrameworkElement CreateTabContent()
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var header = new TextBlock
            {
                Text = "محتوای تب نمونه پلاگین",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(16)
            };
            Grid.SetRow(header, 0);
            grid.Children.Add(header);

            var content = new TextBox
            {
                Text = "این یک تب ایجاد شده توسط پلاگین است.\nمی‌توانید هر محتوایی را اینجا قرار دهید.",
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(16)
            };
            Grid.SetRow(content, 1);
            grid.Children.Add(content);

            return grid;
        }

        private FrameworkElement CreateWidgetContent()
        {
            var border = new Border
            {
                Background = System.Windows.Media.Brushes.LightBlue,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16)
            };

            var stack = new StackPanel();
            
            stack.Children.Add(new TextBlock
            {
                Text = "ویجت نمونه",
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 8)
            });

            stack.Children.Add(new TextBlock
            {
                Text = $"زمان: {DateTime.Now:HH:mm:ss}"
            });

            stack.Children.Add(new Button
            {
                Content = "کلیک کنید",
                Margin = new Thickness(0, 8, 0, 0),
                Padding = new Thickness(8, 4)
            });

            border.Child = stack;
            return border;
        }

        private void ShowPluginWindow()
        {
            var window = new Window
            {
                Title = "پنجره پلاگین نمونه",
                Width = 600,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var content = new TextBlock
            {
                Text = "این یک پنجره ایجاد شده توسط پلاگین است",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 16
            };
            Grid.SetRow(content, 0);
            grid.Children.Add(content);

            var button = new Button
            {
                Content = "بستن",
                Width = 100,
                Margin = new Thickness(16),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            button.Click += (s, e) => window.Close();
            Grid.SetRow(button, 1);
            grid.Children.Add(button);

            window.Content = grid;
            window.ShowDialog();
        }

        private void ExecuteSampleCommand(object parameter)
        {
            _host.ShowMessage($"دستور نمونه اجرا شد با پارامتر: {parameter}", MessageType.Info);
        }

        private void OnTradeAdded(PluginEvent eventData)
        {
            _host.ShowMessage("معامله جدید اضافه شد", MessageType.Info);
        }

        private void OnTradeUpdated(PluginEvent eventData)
        {
            _host.ShowMessage("معامله به‌روزرسانی شد", MessageType.Info);
        }
    }
}
// پایان کد