// مسیر فایل: UI/Dialogs/AddFieldDialog.xaml.cs
// ابتدای کد
using System;
using System.Collections.ObjectModel;
using System.Windows;
using MaterialDesignThemes.Wpf;
using TradingJournal.Core.FormEngine.Models;

namespace TradingJournal.UI.Dialogs
{
    public partial class AddFieldDialog : Window
    {
        public DynamicField Field { get; private set; }
        public ObservableCollection<FieldOption> Options { get; set; }

        public AddFieldDialog(DynamicField existingField = null)
        {
            InitializeComponent();
            
            Options = new ObservableCollection<FieldOption>();
            DataContext = this;

            // پر کردن ComboBox نوع فیلد
            FieldTypeComboBox.ItemsSource = Enum.GetValues(typeof(FieldType));

            if (existingField != null)
            {
                // حالت ویرایش
                Field = CloneField(existingField);
                LoadFieldToUI();
                Title = "ویرایش فیلد";
            }
            else
            {
                // حالت جدید
                Field = new DynamicField();
                Title = "افزودن فیلد جدید";
            }
        }

        private DynamicField CloneField(DynamicField source)
        {
            return new DynamicField
            {
                Name = source.Name,
                Label = source.Label,
                FieldType = source.FieldType,
                IsRequired = source.IsRequired,
                IsReadOnly = source.IsReadOnly,
                ValidationRule = source.ValidationRule,
                GroupName = source.GroupName,
                MinValue = source.MinValue,
                MaxValue = source.MaxValue,
                MaxLength = source.MaxLength,
                Placeholder = source.Placeholder,
                Options = new List<FieldOption>(source.Options)
            };
        }

        private void LoadFieldToUI()
        {
            FieldNameTextBox.Text = Field.Name;
            FieldLabelTextBox.Text = Field.Label;
            FieldTypeComboBox.SelectedItem = Field.FieldType;
            IsRequiredCheckBox.IsChecked = Field.IsRequired;
            IsReadOnlyCheckBox.IsChecked = Field.IsReadOnly;
            ValidationRuleTextBox.Text = Field.ValidationRule;
            GroupNameTextBox.Text = Field.GroupName;
            PlaceholderTextBox.Text = Field.Placeholder;

            if (Field.MinValue.HasValue)
                MinValueTextBox.Text = Field.MinValue.ToString();
            
            if (Field.MaxValue.HasValue)
                MaxValueTextBox.Text = Field.MaxValue.ToString();
            
            if (Field.MaxLength.HasValue)
                MaxLengthTextBox.Text = Field.MaxLength.ToString();

            Options.Clear();
            foreach (var option in Field.Options)
            {
                Options.Add(option);
            }
        }

        private void OnFieldTypeChanged(object sender, RoutedEventArgs e)
        {
            if (FieldTypeComboBox.SelectedItem is FieldType fieldType)
            {
                // نمایش/مخفی کردن بخش‌های مرتبط
                NumericPanel.Visibility = (fieldType == FieldType.Number || 
                    fieldType == FieldType.Decimal || fieldType == FieldType.Currency || 
                    fieldType == FieldType.Percentage) ? Visibility.Visible : Visibility.Collapsed;

                TextPanel.Visibility = (fieldType == FieldType.Text || 
                    fieldType == FieldType.TextArea) ? Visibility.Visible : Visibility.Collapsed;

                OptionsPanel.Visibility = (fieldType == FieldType.ComboBox || 
                    fieldType == FieldType.RadioButton) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void OnAddOption(object sender, RoutedEventArgs e)
        {
            var dialog = new TextInputDialog("مقدار گزینه را وارد کنید:");
            if (dialog.ShowDialog() == true)
            {
                var option = new FieldOption
                {
                    Value = dialog.InputText,
                    Display = dialog.InputText,
                    IsDefault = Options.Count == 0
                };
                Options.Add(option);
            }
        }

        private void OnRemoveOption(object sender, RoutedEventArgs e)
        {
            if (OptionsListBox.SelectedItem is FieldOption option)
            {
                Options.Remove(option);
            }
        }

        private void OnOK(object sender, RoutedEventArgs e)
        {
            // اعتبارسنجی
            if (string.IsNullOrWhiteSpace(FieldNameTextBox.Text))
            {
                MessageBox.Show("نام فیلد اجباری است", "خطا", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(FieldLabelTextBox.Text))
            {
                MessageBox.Show("برچسب فیلد اجباری است", "خطا", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // ذخیره مقادیر
            Field.Name = FieldNameTextBox.Text;
            Field.Label = FieldLabelTextBox.Text;
            Field.FieldType = (FieldType)FieldTypeComboBox.SelectedItem;
            Field.IsRequired = IsRequiredCheckBox.IsChecked ?? false;
            Field.IsReadOnly = IsReadOnlyCheckBox.IsChecked ?? false;
            Field.ValidationRule = ValidationRuleTextBox.Text;
            Field.GroupName = GroupNameTextBox.Text;
            Field.Placeholder = PlaceholderTextBox.Text;

            if (double.TryParse(MinValueTextBox.Text, out double minValue))
                Field.MinValue = minValue;

            if (double.TryParse(MaxValueTextBox.Text, out double maxValue))
                Field.MaxValue = maxValue;

            if (int.TryParse(MaxLengthTextBox.Text, out int maxLength))
                Field.MaxLength = maxLength;

            Field.Options = Options.ToList();

            DialogResult = true;
            Close();
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
// پایان کد