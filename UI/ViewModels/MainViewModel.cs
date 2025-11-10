// مسیر فایل: UI/ViewModels/MainViewModel.cs
// ابتدای کد
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using TradingJournal.Core.Commands;
using TradingJournal.Core.MetadataEngine.Models;
using TradingJournal.Core.Services;

namespace TradingJournal.UI.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly INavigationService _navigationService;
        private readonly IMetadataService _metadataService;
        private readonly IThemeService _themeService;
        
        private ObservableCollection<TabMetadata> _tabs;
        private TabMetadata _selectedTab;
        private string _title;
        private bool _isBusy;
        private string _statusMessage;

        public MainViewModel(
            INavigationService navigationService,
            IMetadataService metadataService,
            IThemeService themeService)
        {
            _navigationService = navigationService;
            _metadataService = metadataService;
            _themeService = themeService;

            LoadTabs();
            InitializeCommands();
        }

        public ObservableCollection<TabMetadata> Tabs
        {
            get => _tabs;
            set { _tabs = value; OnPropertyChanged(); }
        }

        public TabMetadata SelectedTab
        {
            get => _selectedTab;
            set 
            { 
                _selectedTab = value; 
                OnPropertyChanged();
                OnTabChanged();
            }
        }

        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        // Commands
        public ICommand NewTradeCommand { get; private set; }
        public ICommand OpenSettingsCommand { get; private set; }
        public ICommand ExportCommand { get; private set; }
        public ICommand ImportCommand { get; private set; }
        public ICommand BackupCommand { get; private set; }
        public ICommand RestoreCommand { get; private set; }
        public ICommand RefreshCommand { get; private set; }
        public ICommand ChangeThemeCommand { get; private set; }
        public ICommand ShowHelpCommand { get; private set; }
        public ICommand ExitCommand { get; private set; }

        private void InitializeCommands()
        {
            NewTradeCommand = new RelayCommand(ExecuteNewTrade);
            OpenSettingsCommand = new RelayCommand(ExecuteOpenSettings);
            ExportCommand = new RelayCommand(ExecuteExport);
            ImportCommand = new RelayCommand(ExecuteImport);
            BackupCommand = new RelayCommand(ExecuteBackup);
            RestoreCommand = new RelayCommand(ExecuteRestore);
            RefreshCommand = new RelayCommand(ExecuteRefresh);
            ChangeThemeCommand = new RelayCommand<string>(ExecuteChangeTheme);
            ShowHelpCommand = new RelayCommand(ExecuteShowHelp);
            ExitCommand = new RelayCommand(ExecuteExit);
        }

        private async void LoadTabs()
        {
            try
            {
                IsBusy = true;
                StatusMessage = "در حال بارگذاری...";

                var tabs = await _metadataService.GetAllTabsAsync();
                Tabs = new ObservableCollection<TabMetadata>(tabs);

                if (Tabs.Count > 0)
                    SelectedTab = Tabs[0];

                StatusMessage = "آماده";
            }
            catch (Exception ex)
            {
                StatusMessage = $"خطا: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void OnTabChanged()
        {
            if (SelectedTab != null)
            {
                _navigationService.NavigateToTab(SelectedTab);
                Title = $"ژورنال معاملاتی - {SelectedTab.Title}";
            }
        }

        private void ExecuteNewTrade(object parameter)
        {
            _navigationService.ShowDialog("NewTradeDialog");
        }

        private void ExecuteOpenSettings(object parameter)
        {
            _navigationService.NavigateTo("SettingsView");
        }

        private async void ExecuteExport(object parameter)
        {
            try
            {
                IsBusy = true;
                StatusMessage = "در حال خروجی گرفتن...";
                
                // Export logic
                await Task.Delay(1000); // Placeholder
                
                StatusMessage = "خروجی با موفقیت ایجاد شد";
            }
            catch (Exception ex)
            {
                StatusMessage = $"خطا: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async void ExecuteImport(object parameter)
        {
            try
            {
                IsBusy = true;
                StatusMessage = "در حال وارد کردن...";
                
                // Import logic
                await Task.Delay(1000); // Placeholder
                
                StatusMessage = "داده‌ها با موفقیت وارد شد";
            }
            catch (Exception ex)
            {
                StatusMessage = $"خطا: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async void ExecuteBackup(object parameter)
        {
            try
            {
                IsBusy = true;
                StatusMessage = "در حال ایجاد نسخه پشتیبان...";
                
                var backupService = new BackupService();
                var result = await backupService.CreateBackupAsync();
                
                if (result.Success)
                    StatusMessage = $"نسخه پشتیبان در {result.FilePath} ایجاد شد";
                else
                    StatusMessage = $"خطا در ایجاد نسخه پشتیبان: {result.Message}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"خطا: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async void ExecuteRestore(object parameter)
        {
            try
            {
                IsBusy = true;
                StatusMessage = "در حال بازیابی...";
                
                var backupService = new BackupService();
                var result = await backupService.RestoreBackupAsync(parameter?.ToString());
                
                if (result.Success)
                {
                    StatusMessage = "بازیابی با موفقیت انجام شد";
                    ExecuteRefresh(null);
                }
                else
                {
                    StatusMessage = $"خطا در بازیابی: {result.Message}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"خطا: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ExecuteRefresh(object parameter)
        {
            LoadTabs();
        }

        private void ExecuteChangeTheme(string theme)
        {
            _themeService.ChangeTheme(theme);
        }

        private void ExecuteShowHelp(object parameter)
        {
            _navigationService.ShowDialog("HelpDialog");
        }

        private void ExecuteExit(object parameter)
        {
            System.Windows.Application.Current.Shutdown();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
// پایان کد