// مسیر فایل: UI/Views/SettingsView.xaml.cs
// ابتدای کد
using System;
using System.Windows;
using System.Windows.Controls;
using TradingJournal.Core.Configuration;
using TradingJournal.Core.Services;

namespace TradingJournal.UI.Views
{
    public partial class SettingsView : UserControl
    {
        private readonly IConfiguration _configuration;
        private readonly IThemeService _themeService;
        private readonly BackupService _backupService;
        private bool _isDirty = false;

        public SettingsView(IConfiguration configuration, IThemeService themeService, BackupService backupService)
        {
            InitializeComponent();
            
            _configuration = configuration;
            _themeService = themeService;
            _backupService = backupService;
            
            LoadSettings();
        }

        private void LoadSettings()
        {
            var settings = _configuration.Settings;
            
            // General Settings
            LanguageComboBox.SelectedValue = settings.Language;
            ThemeComboBox.SelectedValue = settings.Theme;
            AccentColorComboBox.ItemsSource = _themeService.AvailableColors;
            AccentColorComboBox.SelectedValue = settings.AccentColor;
            FontSizeSlider.Value = settings.FontSize;
            FontFamilyComboBox.Text = settings.FontFamily;
            
            // Trading Settings
            DefaultLeverageTextBox.Text = settings.TradingSettings.DefaultLeverage.ToString();
            DefaultRiskTextBox.Text = settings.TradingSettings.DefaultRiskPercentage.ToString();
            DefaultCommissionTextBox.Text = settings.TradingSettings.DefaultCommission.ToString();
            ShowPipsCalculatorCheckBox.IsChecked = settings.TradingSettings.ShowPipsCalculator;
            AutoCalculateRRCheckBox.IsChecked = settings.TradingSettings.AutoCalculateRiskReward;
            
            // Backup Settings
            AutoBackupCheckBox.IsChecked = settings.BackupSettings.AutoBackup;
            BackupIntervalTextBox.Text = settings.BackupSettings.BackupInterval.ToString();
            MaxBackupsTextBox.Text = settings.BackupSettings.MaxBackups.ToString();
            BackupPathTextBox.Text = settings.BackupSettings.BackupPath;
            EncryptBackupsCheckBox.IsChecked = settings.BackupSettings.EncryptBackups;
            
            // MetaTrader Settings
            EnableAutoSyncCheckBox.IsChecked = settings.MetaTraderSettings.EnableAutoSync;
            ServerUrlTextBox.Text = settings.MetaTraderSettings.ServerUrl;
            ApiKeyTextBox.Text = settings.MetaTraderSettings.ApiKey;
            SyncIntervalTextBox.Text = settings.MetaTraderSettings.SyncInterval.ToString();
            CaptureScreenshotsCheckBox.IsChecked = settings.MetaTraderSettings.CaptureScreenshots;
            
            // UI Settings
            ShowSplashCheckBox.IsChecked = settings.UISettings.ShowSplashScreen;
            AnimationsCheckBox.IsChecked = settings.UISettings.AnimationsEnabled;
            CompactModeCheckBox.IsChecked = settings.UISettings.CompactMode;
            ShowTooltipsCheckBox.IsChecked = settings.UISettings.ShowToolTips;
            ConfirmDeleteCheckBox.IsChecked = settings.UISettings.ConfirmDelete;
            RememberPositionCheckBox.IsChecked = settings.UISettings.RememberWindowPosition;
        }

        private void SaveSettings()
        {
            var settings = _configuration.Settings;
            
            // General Settings
            settings.Language = LanguageComboBox.SelectedValue?.ToString();
            settings.Theme = ThemeComboBox.SelectedValue?.ToString();
            settings.AccentColor = AccentColorComboBox.SelectedValue?.ToString();
            settings.FontSize = (int)FontSizeSlider.Value;
            settings.FontFamily = FontFamilyComboBox.Text;
            
            // Trading Settings
            settings.TradingSettings.DefaultLeverage = int.Parse(DefaultLeverageTextBox.Text);
            settings.TradingSettings.DefaultRiskPercentage = decimal.Parse(DefaultRiskTextBox.Text);
            settings.TradingSettings.DefaultCommission = decimal.Parse(DefaultCommissionTextBox.Text);
            settings.TradingSettings.ShowPipsCalculator = ShowPipsCalculatorCheckBox.IsChecked ?? false;
            settings.TradingSettings.AutoCalculateRiskReward = AutoCalculateRRCheckBox.IsChecked ?? false;
            
            // Backup Settings
            settings.BackupSettings.AutoBackup = AutoBackupCheckBox.IsChecked ?? false;
            settings.BackupSettings.BackupInterval = int.Parse(BackupIntervalTextBox.Text);
            settings.BackupSettings.MaxBackups = int.Parse(MaxBackupsTextBox.Text);
            settings.BackupSettings.BackupPath = BackupPathTextBox.Text;
            settings.BackupSettings.EncryptBackups = EncryptBackupsCheckBox.IsChecked ?? false;
            
            // MetaTrader Settings
            settings.MetaTraderSettings.EnableAutoSync = EnableAutoSyncCheckBox.IsChecked ?? false;
            settings.MetaTraderSettings.ServerUrl = ServerUrlTextBox.Text;
            settings.MetaTraderSettings.ApiKey = ApiKeyTextBox.Text;
            settings.MetaTraderSettings.SyncInterval = int.Parse(SyncIntervalTextBox.Text);
            settings.MetaTraderSettings.CaptureScreenshots = CaptureScreenshotsCheckBox.IsChecked ?? false;
            
            // UI Settings
            settings.UISettings.ShowSplashScreen = ShowSplashCheckBox.IsChecked ?? false;
            settings.UISettings.AnimationsEnabled = AnimationsCheckBox.IsChecked ?? false;
            settings.UISettings.CompactMode = CompactModeCheckBox.IsChecked ?? false;
            settings.UISettings.ShowToolTips = ShowTooltipsCheckBox.IsChecked ?? false;
            settings.UISettings.ConfirmDelete = ConfirmDeleteCheckBox.IsChecked ?? false;
            settings.UISettings.RememberWindowPosition = RememberPositionCheckBox.IsChecked ?? false;
            
            _configuration.Save();
            _isDirty = false;
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            MessageBox.Show("تنظیمات با موفقیت ذخیره شد", "موفقیت", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OnResetClick(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "آیا از بازگردانی تنظیمات به حالت پیش‌فرض مطمئن هستید؟",
                "تأیید",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                // Reset to defaults
                LoadSettings();
            }
        }

        private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_themeService != null && ThemeComboBox.SelectedValue != null)
            {
                _themeService.ChangeTheme(ThemeComboBox.SelectedValue.ToString());
                _isDirty = true;
            }
        }

        private void OnAccentColorChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_themeService != null && AccentColorComboBox.SelectedValue != null)
            {
                _themeService.ChangePrimaryColor(AccentColorComboBox.SelectedValue.ToString());
                _isDirty = true;
            }
        }

        private void OnBrowseBackupPath(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                BackupPathTextBox.Text = dialog.SelectedPath;
                _isDirty = true;
            }
        }

        private void OnTestConnection(object sender, RoutedEventArgs e)
        {
            // Test MetaTrader connection
            MessageBox.Show("در حال تست اتصال...", "تست اتصال", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OnSettingChanged(object sender, EventArgs e)
        {
            _isDirty = true;
        }
    }
}
// پایان کد