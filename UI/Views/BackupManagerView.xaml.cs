// 📁 UI/Views/BackupManagerView.xaml.cs
// ===== شروع کد =====

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using TradingJournal.Core.Backup;
using TradingJournal.UI.Dialogs;

namespace TradingJournal.UI.Views
{
    public partial class BackupManagerView : UserControl
    {
        private readonly BackupService _backupService;
        private ObservableCollection<BackupItemViewModel> _backups;
        private BackupItemViewModel _selectedBackup;
        
        public BackupManagerView()
        {
            InitializeComponent();
            _backupService = new BackupService();
            _backupService.ProgressChanged += OnBackupProgress;
            
            LoadBackupList();
        }
        
        private async void LoadBackupList()
        {
            try
            {
                var backups = await _backupService.GetBackupListAsync();
                _backups = new ObservableCollection<BackupItemViewModel>(
                    backups.Select(b => new BackupItemViewModel(b))
                );
                BackupGrid.ItemsSource = _backups;
            }
            catch (Exception ex)
            {
                ShowError($"خطا در بارگذاری لیست بکاپ‌ها: {ex.Message}");
            }
        }
        
        private async void OnCreateBackup(object sender, RoutedEventArgs e)
        {
            var dialog = new CreateBackupDialog();
            var result = await DialogHost.Show(dialog, "RootDialog");
            
            if (result is BackupOptions options)
            {
                await CreateBackup(options);
            }
        }
        
        private async Task CreateBackup(BackupOptions options)
        {
            try
            {
                ShowProgress(true, "ایجاد بکاپ");
                
                var result = await _backupService.CreateBackupAsync(options);
                
                if (result.Success)
                {
                    ShowSuccess("بکاپ با موفقیت ایجاد شد");
                    await LoadBackupList();
                }
                else
                {
                    ShowError($"خطا در ایجاد بکاپ: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                ShowError($"خطا: {ex.Message}");
            }
            finally
            {
                ShowProgress(false);
            }
        }
        
        private async void OnRestore(object sender, RoutedEventArgs e)
        {
            // انتخاب فایل بکاپ
            var dialog = new OpenFileDialog
            {
                Filter = "Backup Files (*.tjb;*.tjb.enc)|*.tjb;*.tjb.enc",
                Title = "انتخاب فایل بکاپ"
            };
            
            if (dialog.ShowDialog() == true)
            {
                await RestoreBackup(dialog.FileName);
            }
        }
        
        private async void OnRestoreBackup(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var backupPath = button?.Tag as string;
            
            if (!string.IsNullOrEmpty(backupPath))
            {
                await RestoreBackup(backupPath);
            }
        }
        
        private async Task RestoreBackup(string backupPath)
        {
            // تایید از کاربر
            var confirmResult = MessageBox.Show(
                "آیا از بازیابی این بکاپ اطمینان دارید؟ این عملیات غیرقابل برگشت است.",
                "تایید بازیابی",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning
            );
            
            if (confirmResult != MessageBoxResult.Yes)
                return;
            
            try
            {
                var options = new RestoreOptions();
                
                // بررسی رمزنگاری
                var info = await _backupService.GetBackupInfoAsync(backupPath);
                if (info.IsEncrypted)
                {
                    var passwordDialog = new PasswordInputDialog("رمز عبور بکاپ را وارد کنید");
                    var passwordResult = await DialogHost.Show(passwordDialog, "RootDialog");
                    
                    if (passwordResult is string password)
                    {
                        options.Password = password;
                    }
                    else
                    {
                        return;
                    }
                }
                
                ShowProgress(true, "بازیابی بکاپ");
                
                var result = await _backupService.RestoreBackupAsync(backupPath, options);
                
                if (result.Success)
                {
                    ShowSuccess("بازیابی با موفقیت انجام شد. لطفاً برنامه را مجدداً راه‌اندازی کنید.");
                    
                    // درخواست restart
                    var restartResult = MessageBox.Show(
                        "برای اعمال کامل تغییرات، برنامه باید مجدداً راه‌اندازی شود. آیا اکنون این کار انجام شود؟",
                        "راه‌اندازی مجدد",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information
                    );
                    
                    if (restartResult == MessageBoxResult.Yes)
                    {
                        System.Diagnostics.Process.Start(Application.ResourceAssembly.Location);
                        Application.Current.Shutdown();
                    }
                }
                else
                {
                    ShowError($"خطا در بازیابی: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                ShowError($"خطا: {ex.Message}");
            }
            finally
            {
                ShowProgress(false);
            }
        }
        
        private async void OnDeleteBackup(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var backupPath = button?.Tag as string;
            
            if (string.IsNullOrEmpty(backupPath))
                return;
            
            var result = MessageBox.Show(
                "آیا از حذف این بکاپ اطمینان دارید؟",
                "تایید حذف",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning
            );
            
            if (result != MessageBoxResult.Yes)
                return;
            
            try
            {
                if (await _backupService.DeleteBackupAsync(backupPath))
                {
                    ShowSuccess("بکاپ حذف شد");
                    await LoadBackupList();
                }
                else
                {
                    ShowError("حذف بکاپ با خطا مواجه شد");
                }
            }
            catch (Exception ex)
            {
                ShowError($"خطا: {ex.Message}");
            }
        }
        
        private void OnBackupSelected(object sender, SelectionChangedEventArgs e)
        {
            _selectedBackup = BackupGrid.SelectedItem as BackupItemViewModel;
            UpdateDetailsPanel();
        }
        
        private void UpdateDetailsPanel()
        {
            if (_selectedBackup == null)
            {
                DetailsPanel.Visibility = Visibility.Collapsed;
                NoSelectionText.Visibility = Visibility.Visible;
            }
            else
            {
                DetailsPanel.Visibility = Visibility.Visible;
                NoSelectionText.Visibility = Visibility.Collapsed;
                
                FileNameText.Text = _selectedBackup.FileName;
                CreatedDateText.Text = _selectedBackup.CreatedAt.ToString("yyyy/MM/dd HH:mm:ss");
                FileSizeText.Text = _selectedBackup.FileSizeFormatted;
                DescriptionText.Text = _selectedBackup.Description ?? "بدون توضیحات";
                
                if (_selectedBackup.Metadata != null)
                {
                    ComponentsList.ItemsSource = _selectedBackup.Metadata.IncludedComponents;
                    
                    RecordCountText.Text = _selectedBackup.Metadata.DatabaseInfo?.RecordCount.ToString() ?? "-";
                    ImageCountText.Text = _selectedBackup.Metadata.ImagesInfo?.Count.ToString() ?? "-";
                }
            }
        }
        
        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            FilterBackups();
        }
        
        private void OnFilterChanged(object sender, SelectionChangedEventArgs e)
        {
            FilterBackups();
        }
        
        private void FilterBackups()
        {
            if (_backups == null) return;
            
            var searchText = SearchBox.Text?.ToLower();
            var filterTag = (FilterCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            
            var filtered = _backups.AsEnumerable();
            
            // فیلتر بر اساس جستجو
            if (!string.IsNullOrEmpty(searchText))
            {
                filtered = filtered.Where(b => 
                    b.FileName.ToLower().Contains(searchText) ||
                    b.Description?.ToLower().Contains(searchText) == true
                );
            }
            
            // فیلتر بر اساس نوع
            if (filterTag != "All")
            {
                filtered = filterTag switch
                {
                    "Manual" => filtered.Where(b => b.Metadata?.Type == BackupType.Manual),
                    "Automatic" => filtered.Where(b => b.Metadata?.Type == BackupType.Automatic),
                    "Encrypted" => filtered.Where(b => b.IsEncrypted),
                    _ => filtered
                };
            }
            
            BackupGrid.ItemsSource = new ObservableCollection<BackupItemViewModel>(filtered);
        }
        
        private async void OnSettings(object sender, RoutedEventArgs e)
        {
            var dialog = new AutoBackupSettingsDialog();
            await DialogHost.Show(dialog, "RootDialog");
        }
        
        private void OnBackupProgress(object sender, BackupProgressEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                ProgressMessage.Text = e.Message;
                
                if (e.Percentage >= 0)
                {
                    ProgressBar.IsIndeterminate = false;
                    ProgressBar.Value = e.Percentage;
                    ProgressPercentage.Text = $"{e.Percentage}%";
                }
                else
                {
                    ProgressBar.IsIndeterminate = true;
                    ProgressPercentage.Text = "";
                }
            });
        }
        
        private void ShowProgress(bool show, string title = "")
        {
            ProgressOverlay.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            if (!string.IsNullOrEmpty(title))
            {
                ProgressTitle.Text = title;
            }
            ProgressBar.Value = 0;
            ProgressBar.IsIndeterminate = true;
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
    }
    
    public class BackupItemViewModel
    {
        public BackupItemViewModel(BackupInfo info)
        {
            FilePath = info.FilePath;
            FileName = info.FileName;
            FileSize = info.FileSize;
            CreatedAt = info.CreatedAt;
            Description = info.Description;
            IsEncrypted = info.IsEncrypted;
            Metadata = info.Metadata;
        }
        
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Description { get; set; }
        public bool IsEncrypted { get; set; }
        public BackupMetadata Metadata { get; set; }
        
        public string FileSizeFormatted
        {
            get
            {
                if (FileSize < 1024)
                    return $"{FileSize} B";
                if (FileSize < 1024 * 1024)
                    return $"{FileSize / 1024.0:F2} KB";
                if (FileSize < 1024 * 1024 * 1024)
                    return $"{FileSize / (1024.0 * 1024):F2} MB";
                return $"{FileSize / (1024.0 * 1024 * 1024):F2} GB";
            }
        }
        
        public string TypeText
        {
            get
            {
                return Metadata?.Type switch
                {
                    BackupType.Manual => "دستی",
                    BackupType.Automatic => "خودکار",
                    BackupType.Scheduled => "زمان‌بندی",
                    BackupType.BeforeUpdate => "قبل آپدیت",
                    BackupType.BeforeRestore => "قبل بازیابی",
                    _ => "نامشخص"
                };
            }
        }
    }
}

// ===== پایان کد =====