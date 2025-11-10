// ابتدای فایل: UI/Views/FieldManagementView.xaml.cs
// مسیر: /UI/Views/FieldManagementView.xaml.cs

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Newtonsoft.Json;
using Serilog;
using TradingJournal.Core.MetadataEngine;
using TradingJournal.Data.Models;
using TradingJournal.Services;
using TradingJournal.UI.Dialogs;

namespace TradingJournal.UI.Views
{
    public partial class FieldManagementView : UserControl
    {
        private readonly IMetadataService _metadataService;
        private readonly MetadataEngine _metadataEngine;
        private ObservableCollection<DynamicField> _fields;
        private string _currentGroup = "all";

        public FieldManagementView()
        {
            InitializeComponent();
            _metadataService = ServiceLocator.GetService<IMetadataService>();
            _metadataEngine = new MetadataEngine();
            _fields = new ObservableCollection<DynamicField>();
            
            _ = LoadFieldsAsync();
        }

        private async Task LoadFieldsAsync()
        {
            try
            {
                var fields = await _metadataService.GetFieldsAsync(_currentGroup == "all" ? null : _currentGroup);
                _fields.Clear();
                foreach (var field in fields)
                {
                    _fields.Add(field);
                }
                FieldsDataGrid.ItemsSource = _fields;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "خطا در بارگذاری فیلدها");
                ShowError("خطا در بارگذاری فیلدها");
            }
        }

        private async void AddFieldButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FieldEditDialog();
            dialog.SetField(null); // New field
            
            if (dialog.ShowDialog() == true)
            {
                var newField = dialog.GetField();
                await _metadataService.SaveFieldAsync(newField);
                await LoadFieldsAsync();
                ShowSuccess("فیلد جدید با موفقیت اضافه شد");
            }
        }

        private async void EditField_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is DynamicField field)
            {
                var dialog = new FieldEditDialog();
                dialog.SetField(field);
                
                if (dialog.ShowDialog() == true)
                {
                    var updatedField = dialog.GetField();
                    await _metadataService.SaveFieldAsync(updatedField);
                    await LoadFieldsAsync();
                    ShowSuccess("فیلد با موفقیت ویرایش شد");
                }
            }
        }

        private async void DuplicateField_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is DynamicField field)
            {
                var duplicated = new DynamicField
                {
                    FieldName = field.FieldName + "_copy",
                    DisplayName = field.DisplayName + " (Copy)",
                    DisplayNameFa = field.DisplayNameFa + " (کپی)",
                    FieldType = field.FieldType,
                    DefaultValue = field.DefaultValue,
                    IsRequired = field.IsRequired,
                    IsVisible = field.IsVisible,
                    IsEditable = field.IsEditable,
                    GroupName = field.GroupName,
                    ValidationRules = field.ValidationRules,
                    Options = field.Options,
                    Formula = field.Formula,
                    Metadata = field.Metadata,
                    OrderIndex = field.OrderIndex + 1
                };
                
                await _metadataService.SaveFieldAsync(duplicated);
                await LoadFieldsAsync();
                ShowSuccess("فیلد با موفقیت کپی شد");
            }
        }

        private async void DeleteField_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is DynamicField field)
            {
                var result = MessageBox.Show(
                    $"آیا از حذف فیلد '{field.DisplayNameFa ?? field.DisplayName}' اطمینان دارید؟",
                    "تایید حذف",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                
                if (result == MessageBoxResult.Yes)
                {
                    await _metadataService.DeleteFieldAsync(field.Id);
                    await LoadFieldsAsync();
                    ShowSuccess("فیلد با موفقیت حذف شد");
                }
            }
        }

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                Title = "انتخاب فایل فیلدها"
            };
            
            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    var json = await System.IO.File.ReadAllTextAsync(openFileDialog.FileName);
                    await _metadataService.ImportMetadataAsync(json);
                    await LoadFieldsAsync();
                    ShowSuccess("فیلدها با موفقیت وارد شدند");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "خطا در ورود فیلدها");
                    ShowError("خطا در ورود فیلدها: " + ex.Message);
                }
            }
        }

        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                Title = "ذخیره فیلدها",
                FileName = $"fields_{DateTime.Now:yyyyMMdd_HHmmss}.json"
            };
            
            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    var json = await _metadataService.ExportMetadataAsync();
                    await System.IO.File.WriteAllTextAsync(saveFileDialog.FileName, json);
                    ShowSuccess("فیلدها با موفقیت صادر شدند");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "خطا در صدور فیلدها");
                    ShowError("خطا در صدور فیلدها: " + ex.Message);
                }
            }
        }

        private void FieldGroupsTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeViewItem item)
            {
                _currentGroup = item.Header.ToString() ?? "all";
                _ = LoadFieldsAsync();
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (FieldsDataGrid.ItemsSource is ObservableCollection<DynamicField> fields)
            {
                var searchText = SearchBox.Text.ToLower();
                
                if (string.IsNullOrWhiteSpace(searchText))
                {
                    FieldsDataGrid.ItemsSource = _fields;
                }
                else
                {
                    var filtered = _fields.Where(f =>
                        f.FieldName.ToLower().Contains(searchText) ||
                        (f.DisplayName?.ToLower().Contains(searchText) ?? false) ||
                        (f.DisplayNameFa?.ToLower().Contains(searchText) ?? false)
                    ).ToList();
                    
                    FieldsDataGrid.ItemsSource = new ObservableCollection<DynamicField>(filtered);
                }
            }
        }

        private void ShowSuccess(string message)
        {
            // Show success snackbar or notification
            MessageBox.Show(message, "موفقیت", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ShowError(string message)
        {
            MessageBox.Show(message, "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

// پایان فایل: UI/Views/FieldManagementView.xaml.cs