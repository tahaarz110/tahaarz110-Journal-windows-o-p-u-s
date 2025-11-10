// 📁 UI/Views/MetaTraderSettingsView.xaml.cs
// ===== شروع کد =====

using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;
using TradingJournal.MetaTrader;

namespace TradingJournal.UI.Views
{
    public partial class MetaTraderSettingsView : UserControl
    {
        private TradeSyncService _syncService;
        private DispatcherTimer _statusTimer;
        private DateTime _lastSyncTime;
        
        public MetaTraderSettingsView()
        {
            InitializeComponent();
            LoadSettings();
            InitializeStatusTimer();
        }
        
        private void LoadSettings()
        {
            // بارگذاری تنظیمات از دیتابیس یا فایل config
            var settings = Properties.Settings.Default;
            PortTextBox.Text = settings.MetaTraderPort.ToString();
            ApiKeyBox.Password = settings.MetaTraderApiKey;
            
            // انتخاب بازه به‌روزرسانی
            foreach (ComboBoxItem item in UpdateIntervalCombo.Items)
            {
                if (item.Tag?.ToString() == settings.MetaTraderUpdateInterval.ToString())
                {
                    item.IsSelected = true;
                    break;
                }
            }
        }
        
        private void InitializeStatusTimer()
        {
            _statusTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _statusTimer.Tick += UpdateStatusDisplay;
        }
        
        private async void OnConnectionToggled(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ConnectionToggle.IsChecked == true)
                {
                    await StartConnection();
                }
                else
                {
                    await StopConnection();
                }
            }
            catch (Exception ex)
            {
                ShowError($"خطا در تغییر وضعیت اتصال: {ex.Message}");
                ConnectionToggle.IsChecked = false;
            }
        }
        
        private async Task StartConnection()
        {
            // ایجاد سرویس sync
            var context = new Data.AppDbContext();
            _syncService = new TradeSyncService(context);
            
            await _syncService.StartAsync();
            
            // به‌روزرسانی UI
            StatusIcon.Kind = PackIconKind.CheckCircle;
            StatusIcon.Foreground = new SolidColorBrush(Colors.Green);
            StatusText.Text = "فعال";
            
            _statusTimer.Start();
            
            ShowSuccess("اتصال برقرار شد");
        }
        
        private async Task StopConnection()
        {
            if (_syncService != null)
            {
                await _syncService.StopAsync();
                _syncService = null;
            }
            
            // به‌روزرسانی UI
            StatusIcon.Kind = PackIconKind.CircleOutline;
            StatusIcon.Foreground = new SolidColorBrush(Colors.Gray);
            StatusText.Text = "غیرفعال";
            
            _statusTimer.Stop();
            
            ShowInfo("اتصال قطع شد");
        }
        
        private void UpdateStatusDisplay(object sender, EventArgs e)
        {
            if (_lastSyncTime != default)
            {
                var elapsed = DateTime.Now - _lastSyncTime;
                
                string timeText;
                if (elapsed.TotalSeconds < 60)
                    timeText = $"{(int)elapsed.TotalSeconds} ثانیه پیش";
                else if (elapsed.TotalMinutes < 60)
                    timeText = $"{(int)elapsed.TotalMinutes} دقیقه پیش";
                else
                    timeText = $"{(int)elapsed.TotalHours} ساعت پیش";
                
                LastSyncText.Text = $"آخرین همگام‌سازی: {timeText}";
            }
        }
        
        private void OnSaveSettings(object sender, RoutedEventArgs e)
        {
            try
            {
                var settings = Properties.Settings.Default;
                settings.MetaTraderPort = int.Parse(PortTextBox.Text);
                settings.MetaTraderApiKey = ApiKeyBox.Password;
                
                var selectedItem = UpdateIntervalCombo.SelectedItem as ComboBoxItem;
                settings.MetaTraderUpdateInterval = int.Parse(selectedItem?.Tag?.ToString() ?? "10");
                
                settings.Save();
                
                ShowSuccess("تنظیمات ذخیره شد");
            }
            catch (Exception ex)
            {
                ShowError($"خطا در ذخیره تنظیمات: {ex.Message}");
            }
        }
        
        private async void OnTestConnection(object sender, RoutedEventArgs e)
        {
            try
            {
                // تست اتصال با ارسال درخواست ping
                var testService = new MetaTraderService(
                    int.Parse(PortTextBox.Text),
                    ApiKeyBox.Password
                );
                
                await testService.StartAsync();
                await Task.Delay(1000); // صبر برای راه‌اندازی
                await testService.StopAsync();
                
                ShowSuccess("اتصال با موفقیت تست شد");
            }
            catch (Exception ex)
            {
                ShowError($"خطا در تست اتصال: {ex.Message}");
            }
        }
        
        private void OnDownloadExpert(object sender, RoutedEventArgs e)
        {
            try
            {
                // ذخیره فایل اکسپرت
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = "TradingJournalConnector.mq4",
                    Filter = "MQL4 Files (*.mq4)|*.mq4",
                    DefaultExt = ".mq4"
                };
                
                if (dialog.ShowDialog() == true)
                {
                    // خواندن محتوای اکسپرت از منابع
                    var expertContent = Properties.Resources.TradingJournalConnector_mq4;
                    File.WriteAllText(dialog.FileName, expertContent);
                    
                    ShowSuccess("فایل اکسپرت ذخیره شد");
                }
            }
            catch (Exception ex)
            {
                ShowError($"خطا در ذخیره فایل: {ex.Message}");
            }
        }
        
        private void ShowSuccess(string message)
        {
            var messageQueue = new SnackbarMessageQueue(TimeSpan.FromSeconds(3));
            messageQueue.Enqueue(message);
        }
        
        private void ShowError(string message)
        {
            MessageBox.Show(message, "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        
        private void ShowInfo(string message)
        {
            var messageQueue = new SnackbarMessageQueue(TimeSpan.FromSeconds(2));
            messageQueue.Enqueue(message);
        }
    }
}

// ===== پایان کد =====