// ابتدای فایل: UI/Dialogs/FieldEditDialog.xaml.cs
// مسیر: /UI/Dialogs/FieldEditDialog.xaml.cs

using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;
using TradingJournal.Data.Models;

namespace TradingJournal.UI.Dialogs
{
    public partial class FieldEditDialog : Window
    {
        private DynamicField? _field;
        private bool _isEditMode;

        public FieldEditDialog()
        {
            InitializeComponent();
        }

        public void SetField(DynamicField? field)
        {
            _field = field;
            _isEditMode = field != null;

            if (_isEditMode && field != null)
            {
                HeaderText.Text = "ویرایش فیلد";
                LoadFieldData(field);
            }
            else
            {
                HeaderText.Text = "افزودن فیلد جدید";
                SetDefaults();
            }
        }

        private void LoadFieldData(DynamicField field)
        {
            FieldNameTextBox.Text = field.FieldName;
            DisplayNameTextBox.Text = field.DisplayName;
            DisplayNameFaTextBox.Text = field.DisplayNameFa;
            
            // Set field type
            foreach (ComboBoxItem item in FieldTypeComboBox.Items)
            {
                if (item.Tag?.ToString() == field.FieldType.ToString())
                {
                    FieldTypeComboBox.SelectedItem = item;
                    break;
                }
            }

            GroupComboBox.Text = field.GroupName ?? "";
            DefaultValueTextBox.Text = field.DefaultValue ?? "";
            OrderIndexTextBox.Text = field.OrderIndex.ToString();
            
            IsRequiredCheckBox.IsChecked = field.IsRequired;
            IsVisibleCheckBox.IsChecked = field.IsVisible;
            IsEditableCheckBox.IsChecked = field.IsEditable;

            // Load options if applicable
            if (!string.IsNullOrEmpty(field.Options))
            {
                try
                {
                    var options = JsonConvert.DeserializeObject<string[]>(field.Options);
                    if (options != null)
                    {
                        OptionsTextBox.Text = string.Join(Environment.NewLine, options);
                    }
                }
                catch { }
            }

            // Load validation rules
            if (!string.IsNullOrEmpty(field.ValidationRules))
            {
                try
                {
                    dynamic validation = JsonConvert.DeserializeObject(field.ValidationRules);
                    if (validation != null)
                    {
                        MinValueTextBox.Text = validation.min?.ToString() ?? "";
                        MaxValueTextBox.Text = validation.max?.ToString() ?? "";
                        PatternTextBox.Text = validation.pattern?.ToString() ?? "";
                    }
                }
                catch { }
            }

            // Load formula
            if (!string.IsNullOrEmpty(field.Formula))
            {
                FormulaTextBox.Text = field.Formula;
            }
        }

        private void SetDefaults()
        {
            FieldTypeComboBox.SelectedIndex = 0;
            IsVisibleCheckBox.IsChecked = true;
            IsEditableCheckBox.IsChecked = true;
            OrderIndexTextBox.Text = "0";
        }

        public DynamicField GetField()
        {
            if (_field == null)
            {
                _field = new DynamicField();
            }

            _field.FieldName = FieldNameTextBox.Text;
            _field.DisplayName = DisplayNameTextBox.Text;
            _field.DisplayNameFa = DisplayNameFaTextBox.Text;
            
            if (FieldTypeComboBox.SelectedItem is ComboBoxItem item)
            {
                var typeStr = item.Tag?.ToString() ?? "Text";
                _field.FieldType = Enum.Parse<FieldType>(typeStr);
            }

            _field.GroupName = string.IsNullOrWhiteSpace(GroupComboBox.Text) ? null : GroupComboBox.Text;
            _field.DefaultValue = string.IsNullOrWhiteSpace(DefaultValueTextBox.Text) ? null : DefaultValueTextBox.Text;
            
            if (int.TryParse(OrderIndexTextBox.Text, out int orderIndex))
            {
                _field.OrderIndex = orderIndex;
            }

            _field.IsRequired = IsRequiredCheckBox.IsChecked ?? false;
            _field.IsVisible = IsVisibleCheckBox.IsChecked ?? true;
            _field.IsEditable = IsEditableCheckBox.IsChecked ?? true;

            // Save options if applicable
            if (OptionsCard.Visibility == Visibility.Visible && !string.IsNullOrWhiteSpace(OptionsTextBox.Text))
            {
                var options = OptionsTextBox.Text
                    .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(o => o.Trim())
                    .Where(o => !string.IsNullOrEmpty(o))
                    .ToArray();
                
                _field.Options = JsonConvert.SerializeObject(options);
            }

            // Save validation rules
            var validation = new
            {
                min = string.IsNullOrWhiteSpace(MinValueTextBox.Text) ? null : MinValueTextBox.Text,
                max = string.IsNullOrWhiteSpace(MaxValueTextBox.Text) ? null : MaxValueTextBox.Text,
                pattern = string.IsNullOrWhiteSpace(PatternTextBox.Text) ? null : PatternTextBox.Text
            };

            if (validation.min != null || validation.max != null || validation.pattern != null)
            {
                _field.ValidationRules = JsonConvert.SerializeObject(validation);
            }

            // Save formula
            if (FormulaCard.Visibility == Visibility.Visible && !string.IsNullOrWhiteSpace(FormulaTextBox.Text))
            {
                _field.Formula = FormulaTextBox.Text;
            }

            return _field;
        }

        private void FieldTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FieldTypeComboBox.SelectedItem is ComboBoxItem item)
            {
                var fieldType = item.Tag?.ToString() ?? "";
                
                // Show/hide options card for dropdown and radio
                OptionsCard.Visibility = (fieldType == "Dropdown" || fieldType == "Radio") 
                    ? Visibility.Visible 
                    : Visibility.Collapsed;

                // Show/hide formula card
                FormulaCard.Visibility = fieldType == "Formula" 
                    ? Visibility.Visible 
                    : Visibility.Collapsed;

                // Show/hide validation based on type
                ValidationCard.Visibility = (fieldType == "Number" || fieldType == "Decimal" || fieldType == "Text")
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(FieldNameTextBox.Text))
            {
                MessageBox.Show("نام فیلد اجباری است", "خطا", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (FieldTypeComboBox.SelectedItem == null)
            {
                MessageBox.Show("نوع فیلد اجباری است", "خطا", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

// پایان فایل: UI/Dialogs/FieldEditDialog.xaml.cs