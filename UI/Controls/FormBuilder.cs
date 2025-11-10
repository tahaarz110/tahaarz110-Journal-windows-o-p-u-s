// ابتدای فایل: UI/Controls/FormBuilder.cs
// مسیر: /UI/Controls/FormBuilder.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using MaterialDesignThemes.Wpf;
using Newtonsoft.Json.Linq;
using TradingJournal.Data.Models;

namespace TradingJournal.UI.Controls
{
    public class FormBuilder
    {
        private readonly Dictionary<string, Control> _controls;
        private readonly Dictionary<string, object> _values;

        public FormBuilder()
        {
            _controls = new Dictionary<string, Control>();
            _values = new Dictionary<string, object>();
        }

        public Grid BuildForm(JObject formMetadata)
        {
            var grid = new Grid();
            var groups = formMetadata["groups"] as JArray ?? new JArray();
            
            int rowIndex = 0;
            foreach (JObject group in groups)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                
                var groupControl = BuildGroup(group);
                Grid.SetRow(groupControl, rowIndex++);
                grid.Children.Add(groupControl);
            }

            return grid;
        }

        private Border BuildGroup(JObject groupMetadata)
        {
            var border = new Border
            {
                BorderBrush = System.Windows.Media.Brushes.LightGray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(8),
                Padding = new Thickness(12)
            };

            var stackPanel = new StackPanel();
            
            // Group header
            var groupName = groupMetadata["groupNameFa"]?.ToString() ?? groupMetadata["groupName"]?.ToString();
            if (!string.IsNullOrEmpty(groupName))
            {
                stackPanel.Children.Add(new TextBlock
                {
                    Text = groupName,
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 12)
                });
            }

            // Fields grid
            var fieldsGrid = new Grid();
            var columns = groupMetadata["columns"]?.Value<int>() ?? 2;
            
            for (int i = 0; i < columns; i++)
            {
                fieldsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            var fields = groupMetadata["fields"] as JArray ?? new JArray();
            int row = 0, col = 0;
            
            foreach (JObject field in fields)
            {
                if (col >= columns)
                {
                    col = 0;
                    row++;
                    fieldsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                }

                var fieldControl = BuildField(field);
                Grid.SetRow(fieldControl, row);
                Grid.SetColumn(fieldControl, col);
                fieldsGrid.Children.Add(fieldControl);
                
                col++;
            }

            stackPanel.Children.Add(fieldsGrid);
            border.Child = stackPanel;
            
            return border;
        }

        private FrameworkElement BuildField(JObject fieldMetadata)
        {
            var fieldName = fieldMetadata["fieldName"]?.ToString() ?? "";
            var displayName = fieldMetadata["displayNameFa"]?.ToString() ?? fieldMetadata["displayName"]?.ToString() ?? fieldName;
            var fieldType = fieldMetadata["fieldType"]?.ToString() ?? "text";
            var required = fieldMetadata["required"]?.Value<bool>() ?? false;
            var defaultValue = fieldMetadata["defaultValue"]?.ToString();

            var container = new StackPanel { Margin = new Thickness(8) };

            Control? control = null;

            switch (fieldType.ToLower())
            {
                case "text":
                    control = new TextBox
                    {
                        Name = $"Field_{fieldName}",
                        Text = defaultValue,
                        Style = Application.Current.FindResource("MaterialDesignOutlinedTextBox") as Style
                    };
                    HintAssist.SetHint(control, displayName);
                    break;

                case "number":
                case "decimal":
                    control = new TextBox
                    {
                        Name = $"Field_{fieldName}",
                        Text = defaultValue,
                        Style = Application.Current.FindResource("MaterialDesignOutlinedTextBox") as Style
                    };
                    HintAssist.SetHint(control, displayName);
                    // Add numeric validation
                    break;

                case "date":
                    control = new DatePicker
                    {
                        Name = $"Field_{fieldName}",
                        Style = Application.Current.FindResource("MaterialDesignOutlinedDatePicker") as Style
                    };
                    HintAssist.SetHint(control, displayName);
                    break;

                case "dropdown":
                    var combo = new ComboBox
                    {
                        Name = $"Field_{fieldName}",
                        Style = Application.Current.FindResource("MaterialDesignOutlinedComboBox") as Style
                    };
                    HintAssist.SetHint(combo, displayName);
                    
                    var options = fieldMetadata["options"] as JArray;
                    if (options != null)
                    {
                        foreach (var option in options)
                        {
                            combo.Items.Add(option.ToString());
                        }
                    }
                    control = combo;
                    break;

                case "checkbox":
                    control = new CheckBox
                    {
                        Name = $"Field_{fieldName}",
                        Content = displayName,
                        IsChecked = defaultValue?.ToLower() == "true"
                    };
                    break;

                case "textarea":
                    control = new TextBox
                    {
                        Name = $"Field_{fieldName}",
                        Text = defaultValue,
                        TextWrapping = TextWrapping.Wrap,
                        AcceptsReturn = true,
                        MinHeight = 80,
                        MaxHeight = 200,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        Style = Application.Current.FindResource("MaterialDesignOutlinedTextBox") as Style
                    };
                    HintAssist.SetHint(control, displayName);
                    break;
            }

            if (control != null)
            {
                _controls[fieldName] = control;
                
                if (required && fieldType != "checkbox")
                {
                    HintAssist.SetHint(control, displayName + " *");
                }
                
                container.Children.Add(control);
            }

            return container;
        }

        public Dictionary<string, object> GetFormValues()
        {
            var values = new Dictionary<string, object>();
            
            foreach (var kvp in _controls)
            {
                var control = kvp.Value;
                object? value = null;

                switch (control)
                {
                    case TextBox textBox:
                        value = textBox.Text;
                        break;
                    case ComboBox comboBox:
                        value = comboBox.SelectedItem;
                        break;
                    case DatePicker datePicker:
                        value = datePicker.SelectedDate;
                        break;
                    case CheckBox checkBox:
                        value = checkBox.IsChecked;
                        break;
                }

                values[kvp.Key] = value ?? "";
            }

            return values;
        }

        public void SetFormValues(Dictionary<string, object> values)
        {
            foreach (var kvp in values)
            {
                if (_controls.TryGetValue(kvp.Key, out var control))
                {
                    switch (control)
                    {
                        case TextBox textBox:
                            textBox.Text = kvp.Value?.ToString() ?? "";
                            break;
                        case ComboBox comboBox:
                            comboBox.SelectedItem = kvp.Value;
                            break;
                        case DatePicker datePicker:
                            if (kvp.Value is DateTime date)
                                datePicker.SelectedDate = date;
                            break;
                        case CheckBox checkBox:
                            checkBox.IsChecked = kvp.Value as bool? ?? false;
                            break;
                    }
                }
            }
        }

        public bool ValidateForm()
        {
            // Implement validation logic
            return true;
        }
    }
}

// پایان فایل: UI/Controls/FormBuilder.cs