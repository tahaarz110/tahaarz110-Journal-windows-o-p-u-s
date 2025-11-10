// مسیر فایل: UI/Dialogs/DashboardCustomizeDialog.xaml.cs
// ابتدای کد
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TradingJournal.Core.MetadataEngine;
using TradingJournal.Core.MetadataEngine.Models;

namespace TradingJournal.UI.Dialogs
{
    public partial class DashboardCustomizeDialog : Window
    {
        private readonly MetadataManager _metadataManager;
        public ObservableCollection<WidgetMetadata> AvailableWidgets { get; set; }
        public ObservableCollection<WidgetMetadata> ActiveWidgets { get; set; }
        
        private WidgetMetadata _selectedWidget;
        private bool _isDirty = false;

        public DashboardCustomizeDialog(MetadataManager metadataManager)
        {
            InitializeComponent();
            _metadataManager = metadataManager;
            
            AvailableWidgets = new ObservableCollection<WidgetMetadata>();
            ActiveWidgets = new ObservableCollection<WidgetMetadata>();
            
            DataContext = this;
            LoadWidgets();
        }

        private void LoadWidgets()
        {
            AvailableWidgets.Clear();
            ActiveWidgets.Clear();

            var allWidgets = _metadataManager.GetAllWidgets();
            
            foreach (var widget in allWidgets)
            {
                if (widget.IsVisible)
                    ActiveWidgets.Add(widget);
                else
                    AvailableWidgets.Add(widget);
            }
        }

        private void OnAddWidget(object sender, RoutedEventArgs e)
        {
            if (AvailableListBox.SelectedItem is WidgetMetadata widget)
            {
                widget.IsVisible = true;
                AvailableWidgets.Remove(widget);
                ActiveWidgets.Add(widget);
                _isDirty = true;
            }
        }

        private void OnRemoveWidget(object sender, RoutedEventArgs e)
        {
            if (ActiveListBox.SelectedItem is WidgetMetadata widget)
            {
                widget.IsVisible = false;
                ActiveWidgets.Remove(widget);
                AvailableWidgets.Add(widget);
                _isDirty = true;
            }
        }

        private void OnMoveUp(object sender, RoutedEventArgs e)
        {
            if (ActiveListBox.SelectedItem is WidgetMetadata widget)
            {
                var index = ActiveWidgets.IndexOf(widget);
                if (index > 0)
                {
                    ActiveWidgets.Move(index, index - 1);
                    UpdateWidgetOrders();
                    _isDirty = true;
                }
            }
        }

        private void OnMoveDown(object sender, RoutedEventArgs e)
        {
            if (ActiveListBox.SelectedItem is WidgetMetadata widget)
            {
                var index = ActiveWidgets.IndexOf(widget);
                if (index < ActiveWidgets.Count - 1)
                {
                    ActiveWidgets.Move(index, index + 1);
                    UpdateWidgetOrders();
                    _isDirty = true;
                }
            }
        }

        private void UpdateWidgetOrders()
        {
            for (int i = 0; i < ActiveWidgets.Count; i++)
            {
                ActiveWidgets[i].Row = i / 3;
                ActiveWidgets[i].Column = i % 3;
            }
        }

        private void OnWidgetSelected(object sender, SelectionChangedEventArgs e)
        {
            if (ActiveListBox.SelectedItem is WidgetMetadata widget)
            {
                _selectedWidget = widget;
                LoadWidgetSettings(widget);
            }
        }

        private void LoadWidgetSettings(WidgetMetadata widget)
        {
            WidgetNameTextBox.Text = widget.Name;
            WidgetTitleTextBox.Text = widget.Title;
            WidgetTypeComboBox.SelectedItem = widget.Type;
            WidgetSizeComboBox.SelectedItem = widget.Size;
            DataSourceTextBox.Text = widget.DataSource;
            RefreshIntervalTextBox.Text = widget.RefreshInterval;
        }

        private void OnSaveSettings(object sender, RoutedEventArgs e)
        {
            if (_selectedWidget != null)
            {
                _selectedWidget.Name = WidgetNameTextBox.Text;
                _selectedWidget.Title = WidgetTitleTextBox.Text;
                _selectedWidget.Type = (WidgetType)WidgetTypeComboBox.SelectedItem;
                _selectedWidget.Size = (WidgetSize)WidgetSizeComboBox.SelectedItem;
                _selectedWidget.DataSource = DataSourceTextBox.Text;
                _selectedWidget.RefreshInterval = RefreshIntervalTextBox.Text;
                
                _isDirty = true;
                MessageBox.Show("تنظیمات ویجت ذخیره شد", "موفقیت", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void OnCreateNewWidget(object sender, RoutedEventArgs e)
        {
            var widget = new WidgetMetadata
            {
                Name = "ویجت جدید",
                Title = "ویجت جدید",
                Type = WidgetType.Card,
                Size = WidgetSize.Medium,
                IsVisible = false
            };

            AvailableWidgets.Add(widget);
            AvailableListBox.SelectedItem = widget;
            _isDirty = true;
        }

        private void OnDeleteWidget(object sender, RoutedEventArgs e)
        {
            if (_selectedWidget != null)
            {
                var result = MessageBox.Show(
                    $"آیا از حذف ویجت '{_selectedWidget.Title}' مطمئن هستید؟",
                    "تأیید حذف",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    if (ActiveWidgets.Contains(_selectedWidget))
                        ActiveWidgets.Remove(_selectedWidget);
                    else
                        AvailableWidgets.Remove(_selectedWidget);

                    _metadataManager.DeleteWidgetAsync(_selectedWidget.Name);
                    _selectedWidget = null;
                }
            }
        }

        private async void OnOK(object sender, RoutedEventArgs e)
        {
            if (_isDirty)
            {
                // Save all widgets
                foreach (var widget in ActiveWidgets.Concat(AvailableWidgets))
                {
                    await _metadataManager.SaveWidgetMetadataAsync(widget);
                }
            }

            DialogResult = true;
            Close();
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            if (_isDirty)
            {
                var result = MessageBox.Show(
                    "تغییرات ذخیره نشده وجود دارد. آیا می‌خواهید خارج شوید؟",
                    "هشدار",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                    return;
            }

            DialogResult = false;
            Close();
        }
    }
}
// پایان کد