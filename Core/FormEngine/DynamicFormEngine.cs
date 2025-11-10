// مسیر فایل: Core/FormEngine/DynamicFormEngine.cs
// ابتدای کد
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using TradingJournal.Core.FormEngine.Models;
using TradingJournal.Core.MetadataEngine;

namespace TradingJournal.Core.FormEngine
{
    public class DynamicFormEngine
    {
        private readonly MetadataManager _metadataManager;
        private readonly ValidationEngine _validationEngine;

        public DynamicFormEngine(MetadataManager metadataManager)
        {
            _metadataManager = metadataManager;
            _validationEngine = new ValidationEngine();
        }

        public Grid GenerateForm(string formName)
        {
            var formMetadata = _metadataManager.GetFormMetadata(formName);
            if (formMetadata == null) return new Grid();

            var mainGrid = new Grid
            {
                Margin = new Thickness(16)
            };

            // تنظیم ستون‌ها برای RTL
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });

            var fields = formMetadata.Fields.OrderBy(f => f.GroupName).ThenBy(f => f.Order);
            var stackPanel = new StackPanel { Spacing = 16 };

            string currentGroup = null;
            GroupBox currentGroupBox = null;
            StackPanel currentGroupPanel = null;

            foreach (var field in fields)
            {
                // گروه‌بندی فیلدها
                if (field.GroupName != currentGroup)
                {
                    if (currentGroupBox != null)
                    {
                        stackPanel.Children.Add(currentGroupBox);
                    }

                    currentGroup = field.GroupName;
                    currentGroupBox = new GroupBox
                    {
                        Header = currentGroup,
                        Margin = new Thickness(0, 8, 0, 8),
                        Style = Application.Current.FindResource("MaterialDesignGroupBox") as Style
                    };

                    currentGroupPanel = new StackPanel { Spacing = 12 };
                    currentGroupBox.Content = currentGroupPanel;
                }

                var fieldControl = CreateFieldControl(field);
                if (fieldControl != null)
                {
                    var wrapper = new Border
                    {
                        Child = fieldControl,
                        Padding = new Thickness(8),
                        CornerRadius = new CornerRadius(4),
                        Background = new SolidColorBrush(Colors.Transparent)
                    };

                    if (currentGroupPanel != null)
                        currentGroupPanel.Children.Add(wrapper);
                    else
                        stackPanel.Children.Add(wrapper);
                }
            }

            if (currentGroupBox != null)
            {
                stackPanel.Children.Add(currentGroupBox);
            }

            mainGrid.Children.Add(new ScrollViewer
            {
                Content = stackPanel,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            });

            return mainGrid;
        }

        private FrameworkElement CreateFieldControl(DynamicField field)
        {
            var container = new Grid();
            container.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, MinWidth = 120 });
            container.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Label
            var label = new TextBlock
            {
                Text = field.Label + (field.IsRequired ? " *" : ""),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0),
                FontWeight = FontWeights.Medium
            };

            if (field.IsRequired)
            {
                label.Foreground = new SolidColorBrush(Colors.Red);
            }

            Grid.SetColumn(label, 0);
            container.Children.Add(label);

            // Control
            FrameworkElement control = field.FieldType switch
            {
                FieldType.Text => CreateTextBox(field),
                FieldType.Number => CreateNumberBox(field),
                FieldType.Decimal => CreateDecimalBox(field),
                FieldType.Date => CreateDatePicker(field),
                FieldType.DateTime => CreateDateTimePicker(field),
                FieldType.CheckBox => CreateCheckBox(field),
                FieldType.ComboBox => CreateComboBox(field),
                FieldType.RadioButton => CreateRadioButtons(field),
                FieldType.TextArea => CreateTextArea(field),
                FieldType.Image => CreateImagePicker(field),
                FieldType.Currency => CreateCurrencyBox(field),
                FieldType.Percentage => CreatePercentageBox(field),
                _ => new TextBox()
            };

            control.IsEnabled = !field.IsReadOnly;
            
            // Binding
            var binding = new Binding("Value")
            {
                Source = field,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };

            if (control is TextBox textBox)
                textBox.SetBinding(TextBox.TextProperty, binding);
            else if (control is CheckBox checkBox)
                checkBox.SetBinding(CheckBox.IsCheckedProperty, binding);
            else if (control is ComboBox comboBox)
                comboBox.SetBinding(ComboBox.SelectedValueProperty, binding);
            else if (control is DatePicker datePicker)
                datePicker.SetBinding(DatePicker.SelectedDateProperty, binding);

            Grid.SetColumn(control, 1);
            container.Children.Add(control);

            return container;
        }

        private TextBox CreateTextBox(DynamicField field)
        {
            var textBox = new TextBox
            {
                Style = Application.Current.FindResource("MaterialDesignFloatingHintTextBox") as Style
            };

            if (field.MaxLength.HasValue)
                textBox.MaxLength = field.MaxLength.Value;

            if (!string.IsNullOrEmpty(field.Placeholder))
                HintAssist.SetHint(textBox, field.Placeholder);

            return textBox;
        }

        private TextBox CreateNumberBox(DynamicField field)
        {
            var textBox = new TextBox
            {
                Style = Application.Current.FindResource("MaterialDesignFloatingHintTextBox") as Style
            };

            textBox.PreviewTextInput += (s, e) =>
            {
                e.Handled = !int.TryParse(e.Text, out _);
            };

            if (field.MinValue.HasValue || field.MaxValue.HasValue)
            {
                textBox.LostFocus += (s, e) =>
                {
                    if (int.TryParse(textBox.Text, out int value))
                    {
                        if (field.MinValue.HasValue && value < field.MinValue)
                            textBox.Text = field.MinValue.ToString();
                        if (field.MaxValue.HasValue && value > field.MaxValue)
                            textBox.Text = field.MaxValue.ToString();
                    }
                };
            }

            return textBox;
        }

        private TextBox CreateDecimalBox(DynamicField field)
        {
            var textBox = new TextBox
            {
                Style = Application.Current.FindResource("MaterialDesignFloatingHintTextBox") as Style
            };

            textBox.PreviewTextInput += (s, e) =>
            {
                var text = textBox.Text.Insert(textBox.SelectionStart, e.Text);
                e.Handled = !double.TryParse(text, out _);
            };

            return textBox;
        }

        private DatePicker CreateDatePicker(DynamicField field)
        {
            return new DatePicker
            {
                Style = Application.Current.FindResource("MaterialDesignFloatingHintDatePicker") as Style,
                SelectedDateFormat = DatePickerFormat.Short
            };
        }

        private StackPanel CreateDateTimePicker(DynamicField field)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            
            var datePicker = new DatePicker
            {
                Style = Application.Current.FindResource("MaterialDesignFloatingHintDatePicker") as Style,
                Width = 150
            };

            var timePicker = new TimePicker
            {
                Style = Application.Current.FindResource("MaterialDesignFloatingHintTimePicker") as Style,
                Width = 100
            };

            panel.Children.Add(datePicker);
            panel.Children.Add(timePicker);

            return panel;
        }

        private CheckBox CreateCheckBox(DynamicField field)
        {
            return new CheckBox
            {
                Content = field.Label,
                Style = Application.Current.FindResource("MaterialDesignCheckBox") as Style
            };
        }

        private ComboBox CreateComboBox(DynamicField field)
        {
            var comboBox = new ComboBox
            {
                Style = Application.Current.FindResource("MaterialDesignFloatingHintComboBox") as Style,
                ItemsSource = field.Options,
                DisplayMemberPath = "Display",
                SelectedValuePath = "Value"
            };

            var defaultOption = field.Options.FirstOrDefault(o => o.IsDefault);
            if (defaultOption != null)
                comboBox.SelectedValue = defaultOption.Value;

            return comboBox;
        }

        private StackPanel CreateRadioButtons(DynamicField field)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };

            foreach (var option in field.Options)
            {
                var radio = new RadioButton
                {
                    Content = option.Display,
                    GroupName = field.Name,
                    Tag = option.Value,
                    Style = Application.Current.FindResource("MaterialDesignRadioButton") as Style,
                    IsChecked = option.IsDefault
                };

                radio.Checked += (s, e) =>
                {
                    field.Value = (s as RadioButton)?.Tag;
                };

                panel.Children.Add(radio);
            }

            return panel;
        }

        private TextBox CreateTextArea(DynamicField field)
        {
            return new TextBox
            {
                Style = Application.Current.FindResource("MaterialDesignOutlinedTextBox") as Style,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MinHeight = 80,
                MaxHeight = 200
            };
        }

        private Button CreateImagePicker(DynamicField field)
        {
            var button = new Button
            {
                Content = "انتخاب تصویر",
                Style = Application.Current.FindResource("MaterialDesignRaisedButton") as Style
            };

            button.Click += (s, e) =>
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Image files (*.jpg, *.jpeg, *.png, *.gif)|*.jpg;*.jpeg;*.png;*.gif",
                    Multiselect = true
                };

                if (dialog.ShowDialog() == true)
                {
                    field.Value = dialog.FileNames;
                }
            };

            return button;
        }

        private TextBox CreateCurrencyBox(DynamicField field)
        {
            var textBox = CreateDecimalBox(field);
            HintAssist.SetHint(textBox, "مبلغ (ریال)");
            textBox.LostFocus += (s, e) =>
            {
                if (double.TryParse(textBox.Text, out double value))
                {
                    textBox.Text = value.ToString("N0");
                }
            };
            return textBox;
        }

        private TextBox CreatePercentageBox(DynamicField field)
        {
            var textBox = CreateDecimalBox(field);
            HintAssist.SetHint(textBox, "درصد (%)");
            textBox.LostFocus += (s, e) =>
            {
                if (double.TryParse(textBox.Text, out double value))
                {
                    if (value > 100) value = 100;
                    if (value < 0) value = 0;
                    textBox.Text = value.ToString("F2") + "%";
                }
            };
            return textBox;
        }
    }
}
// پایان کد