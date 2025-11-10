// 📁 UI/Dialogs/PluginSettingsDialog.xaml.cs
// ===== شروع کد =====

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;
using TradingJournal.Core.PluginEngine;

namespace TradingJournal.UI.Dialogs
{
    public partial class PluginSettingsDialog : UserControl
    {
        private readonly string _pluginId;
        private readonly IEnumerable<PluginSetting> _settings;
        private readonly Dictionary<string, Control> _controls;
        
        public PluginSettingsDialog(string pluginId, IEnumerable<PluginSetting> settings)
        {
            InitializeComponent();
            _pluginId = pluginId;
            _settings = settings;
            _controls = new Dictionary<string, Control>();
            
            BuildSettingsUI();
        }
        
        private void BuildSettingsUI()
        {
            foreach (var setting in _settings)
            {
                var control = CreateControlForSetting(setting);
                if (control != null)
                {
                    _controls[setting.Key] = control;
                    SettingsContainer.Items.Add(control);
                }
            }
        }
        
        private Control CreateControlForSetting(PluginSetting setting)
        {
            var container = new StackPanel();
            
            switch (setting.Type)
            {
                case SettingType.Text:
                    var textBox = new TextBox
                    {
                        Tag = setting.Key,
                        Text = setting.CurrentValue?.ToString() ?? setting.DefaultValue?.ToString(),
                        Style = Application.Current.FindResource("MaterialDesignFloatingHintTextBox") as Style
                    };
                    HintAssist.SetHint(textBox, setting.DisplayName);
                    HintAssist.SetHelperText(textBox, setting.Description);
                    container.Children.Add(textBox);
                    break;
                
                case SettingType.Number:
                    var numberBox = new TextBox
                    {
                        Tag = setting.Key,
                        Text = setting.CurrentValue?.ToString() ?? setting.DefaultValue?.ToString(),
                        Style = Application.Current.FindResource("MaterialDesignFloatingHintTextBox") as Style
                    };
                    HintAssist.SetHint(numberBox, setting.DisplayName);
                    HintAssist.SetHelperText(numberBox, setting.Description);
                    container.Children.Add(numberBox);
                    break;
                
                case SettingType.Boolean:
                    var checkBox = new CheckBox
                    {
                        Tag = setting.Key,
                        Content = setting.DisplayName,
                        IsChecked = Convert.ToBoolean(setting.CurrentValue ?? setting.DefaultValue),
                        Margin = new Thickness(0, 8, 0, 4)
                    };
                    var helperText = new TextBlock
                    {
                        Text = setting.Description,
                        Foreground = System.Windows.Media.Brushes.Gray,
                        FontSize = 12,
                        Margin = new Thickness(32, 0, 0, 0)
                    };
                    container.Children.Add(checkBox);
                    container.Children.Add(helperText);
                    break;
                
                case SettingType.Dropdown:
                    var comboBox = new ComboBox
                    {
                        Tag = setting.Key,
                        Style = Application.Current.FindResource("MaterialDesignFloatingHintComboBox") as Style
                    };
                    HintAssist.SetHint(comboBox, setting.DisplayName);
                    HintAssist.SetHelperText(comboBox, setting.Description);
                    
                    // بارگذاری آیتم‌ها از Metadata
                    if (setting.Metadata?.ContainsKey("Options") == true)
                    {
                        foreach (var option in (dynamic)setting.Metadata["Options"])
                        {
                            comboBox.Items.Add(new ComboBoxItem 
                            { 
                                Content = option.Text, 
                                Tag = option.Value 
                            });
                        }
                    }
                    
                    container.Children.Add(comboBox);
                    break;
                
                case SettingType.Color:
                    var colorPicker = new ColorPicker
                    {
                        Tag = setting.Key,
                        Color = System.Windows.Media.Colors.Blue // مقدار پیش‌فرض
                    };
                    var colorLabel = new TextBlock
                    {
                        Text = setting.DisplayName,
                        Margin = new Thickness(0, 0, 0, 8)
                    };
                    container.Children.Add(colorLabel);
                    container.Children.Add(colorPicker);
                    break;
                
                case SettingType.File:
                    var filePanel = new DockPanel();
                    var fileTextBox = new TextBox
                    {
                        Tag = setting.Key + "_path",
                        Text = setting.CurrentValue?.ToString() ?? "",
                        Style = Application.Current.FindResource("MaterialDesignFloatingHintTextBox") as Style,
                        IsReadOnly = true
                    };
                    HintAssist.SetHint(fileTextBox, setting.DisplayName);
                    
                    var browseButton = new Button
                    {
                        Content = "انتخاب",
                        Tag = setting.Key,
                        Style = Application.Current.FindResource("MaterialDesignFlatButton") as Style,
                        Margin = new Thickness(8, 0, 0, 0)
                    };
                    browseButton.Click += OnBrowseFile;
                    
                    DockPanel.SetDock(browseButton, Dock.Right);
                    filePanel.Children.Add(browseButton);
                    filePanel.Children.Add(fileTextBox);
                    container.Children.Add(filePanel);
                    break;
            }
            
            return container;
        }
        
        private void OnBrowseFile(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var key = button?.Tag as string;
            
            var dialog = new Microsoft.Win32.OpenFileDialog();
            if (dialog.ShowDialog() == true)
            {
                // یافتن TextBox مربوطه و تنظیم مقدار
                foreach (var control in _controls.Values)
                {
                    if (control is DockPanel panel)
                    {
                        foreach (var child in panel.Children)
                        {
                            if (child is TextBox tb && tb.Tag?.ToString() == key + "_path")
                            {
                                tb.Text = dialog.FileName;
                                break;
                            }
                        }
                    }
                }
            }
        }
        
        public Dictionary<string, object> GetSettings()
        {
            var result = new Dictionary<string, object>();
            
            foreach (var kvp in _controls)
            {
                var control = kvp.Value;
                object value = null;
                
                if (control is TextBox textBox)
                    value = textBox.Text;
                else if (control is CheckBox checkBox)
                    value = checkBox.IsChecked;
                else if (control is ComboBox comboBox)
                    value = (comboBox.SelectedItem as ComboBoxItem)?.Tag;
                else if (control is ColorPicker colorPicker)
                    value = colorPicker.Color.ToString();
                
                if (value != null)
                    result[kvp.Key] = value;
            }
            
            return result;
        }
        
        private void OnSaveSettings(object sender, RoutedEventArgs e)
        {
            DialogHost.CloseDialogCommand.Execute(true, null);
        }
    }
}

// ===== پایان کد =====