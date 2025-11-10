// مسیر فایل: UI/Views/FormDesigner.xaml.cs
// ابتدای کد
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using TradingJournal.Core.FormEngine;
using TradingJournal.Core.FormEngine.Models;
using TradingJournal.Core.MetadataEngine;

namespace TradingJournal.UI.Views
{
    public partial class FormDesigner : UserControl
    {
        private readonly DynamicFormEngine _formEngine;
        private readonly MetadataManager _metadataManager;
        
        public ObservableCollection<DynamicField> Fields { get; set; }
        public ObservableCollection<string> FormNames { get; set; }
        
        private string _currentFormName;
        private Grid _previewGrid;

        public FormDesigner()
        {
            InitializeComponent();
            
            _metadataManager = new MetadataManager();
            _formEngine = new DynamicFormEngine(_metadataManager);
            
            Fields = new ObservableCollection<DynamicField>();
            FormNames = new ObservableCollection<string>();
            
            DataContext = this;
            
            LoadFormNames();
        }

        private void LoadFormNames()
        {
            FormNames.Clear();
            var forms = _metadataManager.GetAllFormNames();
            foreach (var form in forms)
            {
                FormNames.Add(form);
            }
        }

        private void OnFormSelected(object sender, SelectionChangedEventArgs e)
        {
            if (FormComboBox.SelectedItem is string formName)
            {
                _currentFormName = formName;
                LoadFormFields(formName);
                UpdatePreview();
            }
        }

        private void LoadFormFields(string formName)
        {
            Fields.Clear();
            var metadata = _metadataManager.GetFormMetadata(formName);
            
            if (metadata?.Fields != null)
            {
                foreach (var field in metadata.Fields.OrderBy(f => f.Order))
                {
                    Fields.Add(field);
                }
            }
        }

        private void OnAddField(object sender, RoutedEventArgs e)
        {
            var dialog = new AddFieldDialog();
            if (dialog.ShowDialog() == true)
            {
                var newField = dialog.Field;
                newField.Order = Fields.Count;
                Fields.Add(newField);
                SaveFormMetadata();
                UpdatePreview();
            }
        }

        private void OnEditField(object sender, RoutedEventArgs e)
        {
            if (FieldsDataGrid.SelectedItem is DynamicField field)
            {
                var dialog = new AddFieldDialog(field);
                if (dialog.ShowDialog() == true)
                {
                    var index = Fields.IndexOf(field);
                    Fields[index] = dialog.Field;
                    SaveFormMetadata();
                    UpdatePreview();
                }
            }
        }

        private void OnDeleteField(object sender, RoutedEventArgs e)
        {
            if (FieldsDataGrid.SelectedItem is DynamicField field)
            {
                var result = MessageBox.Show(
                    $"آیا از حذف فیلد '{field.Label}' مطمئن هستید؟",
                    "تأیید حذف",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    Fields.Remove(field);
                    ReorderFields();
                    SaveFormMetadata();
                    UpdatePreview();
                }
            }
        }

        private void OnMoveUp(object sender, RoutedEventArgs e)
        {
            if (FieldsDataGrid.SelectedItem is DynamicField field)
            {
                var index = Fields.IndexOf(field);
                if (index > 0)
                {
                    Fields.Move(index, index - 1);
                    ReorderFields();
                    SaveFormMetadata();
                    UpdatePreview();
                }
            }
        }

        private void OnMoveDown(object sender, RoutedEventArgs e)
        {
            if (FieldsDataGrid.SelectedItem is DynamicField field)
            {
                var index = Fields.IndexOf(field);
                if (index < Fields.Count - 1)
                {
                    Fields.Move(index, index + 1);
                    ReorderFields();
                    SaveFormMetadata();
                    UpdatePreview();
                }
            }
        }

        private void ReorderFields()
        {
            for (int i = 0; i < Fields.Count; i++)
            {
                Fields[i].Order = i;
            }
        }

        private void SaveFormMetadata()
        {
            if (string.IsNullOrEmpty(_currentFormName))
                return;

            var metadata = new FormMetadata
            {
                Name = _currentFormName,
                Fields = Fields.ToList()
            };

            _metadataManager.SaveFormMetadata(metadata);
        }

        private void UpdatePreview()
        {
            PreviewContainer.Children.Clear();
            
            if (!string.IsNullOrEmpty(_currentFormName))
            {
                _previewGrid = _formEngine.GenerateForm(_currentFormName);
                PreviewContainer.Children.Add(_previewGrid);
            }
        }

        private void OnCreateNewForm(object sender, RoutedEventArgs e)
        {
            var dialog = new TextInputDialog("نام فرم جدید را وارد کنید:");
            if (dialog.ShowDialog() == true)
            {
                var formName = dialog.InputText;
                
                if (!FormNames.Contains(formName))
                {
                    FormNames.Add(formName);
                    FormComboBox.SelectedItem = formName;
                    
                    var metadata = new FormMetadata
                    {
                        Name = formName,
                        Fields = new List<DynamicField>()
                    };
                    
                    _metadataManager.SaveFormMetadata(metadata);
                }
            }
        }

        private void OnDeleteForm(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_currentFormName))
            {
                var result = MessageBox.Show(
                    $"آیا از حذف فرم '{_currentFormName}' مطمئن هستید؟",
                    "تأیید حذف",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    _metadataManager.DeleteForm(_currentFormName);
                    FormNames.Remove(_currentFormName);
                    Fields.Clear();
                    _currentFormName = null;
                    PreviewContainer.Children.Clear();
                }
            }
        }

        private void OnExportForm(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_currentFormName))
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "JSON files (*.json)|*.json",
                    FileName = $"{_currentFormName}_form.json"
                };

                if (dialog.ShowDialog() == true)
                {
                    _metadataManager.ExportForm(_currentFormName, dialog.FileName);
                    MessageBox.Show("فرم با موفقیت خروجی گرفته شد", "موفقیت", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void OnImportForm(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json"
            };

            if (dialog.ShowDialog() == true)
            {
                var formName = _metadataManager.ImportForm(dialog.FileName);
                if (!string.IsNullOrEmpty(formName))
                {
                    LoadFormNames();
                    FormComboBox.SelectedItem = formName;
                    MessageBox.Show("فرم با موفقیت وارد شد", "موفقیت",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }
    }
}
// پایان کد