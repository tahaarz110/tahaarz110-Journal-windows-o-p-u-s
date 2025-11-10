// ابتدای فایل: UI/Views/TabManagementView.xaml.cs
// مسیر: /UI/Views/TabManagementView.xaml.cs

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;
using Newtonsoft.Json;
using Serilog;
using TradingJournal.Data.Models;
using TradingJournal.Services;
using TradingJournal.UI.Dialogs;

namespace TradingJournal.UI.Views
{
    public partial class TabManagementView : UserControl
    {
        private readonly IMetadataService _metadataService;
        private ObservableCollection<TabConfiguration> _tabs;
        private TabConfiguration? _selectedTab;
        private bool _isEditing;

        public TabManagementView()
        {
            InitializeComponent();
            _metadataService = ServiceLocator.GetService<IMetadataService>();
            _tabs = new ObservableCollection<TabConfiguration>();
            
            LoadIcons();
            _ = LoadTabsAsync();
        }

        private void LoadIcons()
        {
            var icons = new[]
            {
                PackIconKind.ViewDashboard,
                PackIconKind.TableLarge,
                PackIconKind.FileDocument,
                PackIconKind.ChartLine,
                PackIconKind.CurrencyUsd,
                PackIconKind.Calculator,
                PackIconKind.Settings,
                PackIconKind.Puzzle,
                PackIconKind.Home,
                PackIconKind.Account
            };
            
            IconComboBox.ItemsSource = icons;
        }

        private async Task LoadTabsAsync()
        {
            try
            {
                var tabs = await _metadataService.GetTabsAsync();
                _tabs.Clear();
                
                var showInactive = ShowInactiveToggle?.IsChecked ?? false;
                var searchText = SearchBox?.Text?.ToLower() ?? "";
                
                foreach (var tab in tabs)
                {
                    if (!showInactive && !tab.IsEnabled)
                        continue;
                    
                    if (!string.IsNullOrEmpty(searchText))
                    {
                        if (!tab.TabKey.ToLower().Contains(searchText) &&
                            !tab.TabName.ToLower().Contains(searchText) &&
                            !(tab.TabNameFa?.ToLower().Contains(searchText) ?? false))
                            continue;
                    }
                    
                    _tabs.Add(tab);
                }
                
                TabsListBox.ItemsSource = _tabs.OrderBy(t => t.OrderIndex);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "خطا در بارگذاری تب‌ها");
                ShowError("خطا در بارگذاری تب‌ها");
            }
        }

        private async void AddTabButton_Click(object sender, RoutedEventArgs e)
        {
            _selectedTab = new TabConfiguration
            {
                TabKey = $"tab_{Guid.NewGuid().ToString().Substring(0, 8)}",
                TabName = "New Tab",
                TabNameFa = "تب جدید",
                TabType = TabType.Custom,
                OrderIndex = _tabs.Count + 1,
                IsVisible = true,
                IsEnabled = true,
                IsCloseable = true
            };
            
            _isEditing = false;
            ShowProperties(_selectedTab);
            PropertiesCard.Visibility = Visibility.Visible;
        }

        private void EditTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is TabConfiguration tab)
            {
                _selectedTab = tab;
                _isEditing = true;
                ShowProperties(tab);
                PropertiesCard.Visibility = Visibility.Visible;
            }
        }

        private async void DuplicateTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is TabConfiguration tab)
            {
                var duplicated = new TabConfiguration
                {
                    TabKey = $"{tab.TabKey}_copy",
                    TabName = tab.TabName + " (Copy)",
                    TabNameFa = tab.TabNameFa + " (کپی)",
                    IconName = tab.IconName,
                    TabType = tab.TabType,
                    OrderIndex = tab.OrderIndex + 1,
                    IsVisible = tab.IsVisible,
                    IsEnabled = true,
                    IsCloseable = tab.IsCloseable,
                    Permissions = tab.Permissions,
                    Configuration = tab.Configuration
                };
                
                await _metadataService.SaveTabAsync(duplicated);
                await LoadTabsAsync();
                ShowSuccess("تب با موفقیت کپی شد");
            }
        }

        private async void DeleteTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is TabConfiguration tab)
            {
                var result = MessageBox.Show(
                    $"آیا از حذف تب '{tab.TabNameFa ?? tab.TabName}' اطمینان دارید؟",
                    "تایید حذف",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                
                if (result == MessageBoxResult.Yes)
                {
                    await _metadataService.DeleteTabAsync(tab.Id);
                    await LoadTabsAsync();
                    ShowSuccess("تب با موفقیت حذف شد");
                }
            }
        }

        private void ShowProperties(TabConfiguration tab)
        {
            TabKeyTextBox.Text = tab.TabKey;
            TabNameTextBox.Text = tab.TabName;
            TabNameFaTextBox.Text = tab.TabNameFa;
            
            // Set tab type
            foreach (ComboBoxItem item in TabTypeComboBox.Items)
            {
                if (item.Tag?.ToString() == tab.TabType.ToString())
                {
                    TabTypeComboBox.SelectedItem = item;
                    break;
                }
            }
            
            // Set icon
            if (Enum.TryParse<PackIconKind>(tab.IconName, out var iconKind))
            {
                IconComboBox.SelectedItem = iconKind;
            }
            
            OrderIndexTextBox.Text = tab.OrderIndex.ToString();
            IsVisibleCheckBox.IsChecked = tab.IsVisible;
            IsEnabledCheckBox.IsChecked = tab.IsEnabled;
            IsCloseableCheckBox.IsChecked = tab.IsCloseable;
            ConfigurationTextBox.Text = tab.Configuration ?? "{}";
        }

        private async void SaveTab_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTab == null)
                return;
            
            // Update tab properties
            _selectedTab.TabName = TabNameTextBox.Text;
            _selectedTab.TabNameFa = TabNameFaTextBox.Text;
            
            if (TabTypeComboBox.SelectedItem is ComboBoxItem typeItem)
            {
                _selectedTab.TabType = Enum.Parse<TabType>(typeItem.Tag?.ToString() ?? "Custom");
            }
            
            if (IconComboBox.SelectedItem is PackIconKind icon)
            {
                _selectedTab.IconName = icon.ToString();
            }
            
            if (int.TryParse(OrderIndexTextBox.Text, out int orderIndex))
            {
                _selectedTab.OrderIndex = orderIndex;
            }
            
            _selectedTab.IsVisible = IsVisibleCheckBox.IsChecked ?? true;
            _selectedTab.IsEnabled = IsEnabledCheckBox.IsChecked ?? true;
            _selectedTab.IsCloseable = IsCloseableCheckBox.IsChecked ?? true;
            _selectedTab.Configuration = ConfigurationTextBox.Text;
            
            try
            {
                await _metadataService.SaveTabAsync(_selectedTab);
                await LoadTabsAsync();
                PropertiesCard.Visibility = Visibility.Collapsed;
                ShowSuccess(_isEditing ? "تب با موفقیت ویرایش شد" : "تب جدید با موفقیت اضافه شد");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "خطا در ذخیره تب");
                ShowError("خطا در ذخیره تب: " + ex.Message);
            }
        }

        private void CancelEdit_Click(object sender, RoutedEventArgs e)
        {
            PropertiesCard.Visibility = Visibility.Collapsed;
            _selectedTab = null;
        }

        private void TabsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TabsListBox.SelectedItem is TabConfiguration tab)
            {
                _selectedTab = tab;
                _isEditing = true;
                ShowProperties(tab);
                PropertiesCard.Visibility = Visibility.Visible;
            }
        }

        private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            await LoadTabsAsync();
        }

        private async void ShowInactiveToggle_Changed(object sender, RoutedEventArgs e)
        {
            await LoadTabsAsync();
        }

        private void ReorderButton_Click(object sender, RoutedEventArgs e)
        {
            // Show reorder dialog
            // TODO: Implement drag & drop reordering
        }

        private void PreviewButton_Click(object sender, RoutedEventArgs e)
        {
            // Show preview window with current tab configuration
            // TODO: Implement preview functionality
        }

        private void ShowSuccess(string message)
        {
            MessageBox.Show(message, "موفقیت", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ShowError(string message)
        {
            MessageBox.Show(message, "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

// پایان فایل: UI/Views/TabManagementView.xaml.cs