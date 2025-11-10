// ابتدای فایل: UI/Views/WidgetManagementView.xaml.cs
// مسیر: /UI/Views/WidgetManagementView.xaml.cs

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using TradingJournal.Core.WidgetEngine;
using TradingJournal.Data.Models;
using TradingJournal.Services;

namespace TradingJournal.UI.Views
{
    public partial class WidgetManagementView : UserControl
    {
        private readonly IMetadataService _metadataService;
        private readonly WidgetBuilder _widgetBuilder;
        private ObservableCollection<WidgetConfiguration> _widgets;
        private ObservableCollection<TabConfiguration> _tabs;
        private WidgetConfiguration? _selectedWidget;
        private bool _isEditing;

        public WidgetManagementView()
        {
            InitializeComponent();
            _metadataService = ServiceLocator.GetService<IMetadataService>();
            _widgetBuilder = new WidgetBuilder();
            _widgets = new ObservableCollection<WidgetConfiguration>();
            _tabs = new ObservableCollection<TabConfiguration>();
            
            LoadIcons();
            _ = LoadInitialDataAsync();
        }

        private void LoadIcons()
        {
            var icons = Enum.GetValues(typeof(PackIconKind))
                .Cast<PackIconKind>()
                .Take(50)
                .ToList();
            
            IconComboBox.ItemsSource = icons;
        }

        private async Task LoadInitialDataAsync()
        {
            await LoadTabsAsync();
            await LoadWidgetsAsync();
        }

        private async Task LoadTabsAsync()
        {
            try
            {
                var tabs = await _metadataService.GetTabsAsync();
                _tabs.Clear();
                
                TabFilterComboBox.Items.Clear();
                TabFilterComboBox.Items.Add(new ComboBoxItem { Content = "همه تب‌ها", IsSelected = true });
                
                foreach (var tab in tabs)
                {
                    _tabs.Add(tab);
                    TabFilterComboBox.Items.Add(new ComboBoxItem 
                    { 
                        Content = tab.TabNameFa ?? tab.TabName,
                        Tag = tab.TabKey
                    });
                    
                    TabKeyComboBox.Items.Add(new ComboBoxItem
                    {
                        Content = tab.TabNameFa ?? tab.TabName,
                        Tag = tab.TabKey
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "خطا در بارگذاری تب‌ها");
            }
        }

        private async Task LoadWidgetsAsync()
        {
            try
            {
                var selectedTab = (TabFilterComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
                var widgets = await _metadataService.GetWidgetsAsync(selectedTab);
                
                _widgets.Clear();
                var searchText = SearchWidgetBox?.Text?.ToLower() ?? "";
                
                foreach (var widget in widgets)
                {
                    if (!string.IsNullOrEmpty(searchText))
                    {
                        if (!widget.WidgetKey.ToLower().Contains(searchText) &&
                            !widget.WidgetName.ToLower().Contains(searchText) &&
                            !(widget.WidgetNameFa?.ToLower().Contains(searchText) ?? false))
                            continue;
                    }
                    
                    // Add position text for display
                    dynamic expando = new System.Dynamic.ExpandoObject();
                    var dict = (System.Collections.Generic.IDictionary<string, object>)expando;
                    
                    foreach (var prop in widget.GetType().GetProperties())
                    {
                        dict[prop.Name] = prop.GetValue(widget);
                    }
                    
                    dict["PositionText"] = $"R{widget.Row}C{widget.Column}";
                    dict["IconName"] = GetIconForWidgetType(widget.WidgetType);
                    
                    _widgets.Add(widget);
                }
                
                WidgetsItemsControl.ItemsSource = _widgets;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "خطا در بارگذاری ویجت‌ها");
                ShowError("خطا در بارگذاری ویجت‌ها");
            }
        }

        private string GetIconForWidgetType(WidgetType type)
        {
            return type switch
            {
                WidgetType.Chart => "ChartLine",
                WidgetType.Table => "TableLarge",
                WidgetType.Card => "CardText",
                WidgetType.KPI => "Counter",
                WidgetType.Form => "FormSelect",
                WidgetType.Filter => "FilterVariant",
                _ => "Widgets"
            };
        }

        private void AddWidgetButton_Click(object sender, RoutedEventArgs e)
        {
            _selectedWidget = new WidgetConfiguration
            {
                WidgetKey = $"widget_{Guid.NewGuid().ToString().Substring(0, 8)}",
                WidgetName = "New Widget",
                WidgetNameFa = "ویجت جدید",
                WidgetType = WidgetType.Card,
                Row = 1,
                Column = 1,
                RowSpan = 1,
                ColumnSpan = 1,
                IsVisible = true
            };
            
            _isEditing = false;
            ShowEditor(_selectedWidget);
        }

        private void EditWidget_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is WidgetConfiguration widget)
            {
                _selectedWidget = widget;
                _isEditing = true;
                ShowEditor(widget);
            }
        }

        private async void DuplicateWidget_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is WidgetConfiguration widget)
            {
                var duplicated = new WidgetConfiguration
                {
                    WidgetKey = $"{widget.WidgetKey}_copy",
                    WidgetName = widget.WidgetName + " (Copy)",
                    WidgetNameFa = widget.WidgetNameFa + " (کپی)",
                    WidgetType = widget.WidgetType,
                    TabKey = widget.TabKey,
                    Row = widget.Row,
                    Column = widget.Column + widget.ColumnSpan,
                    RowSpan = widget.RowSpan,
                    ColumnSpan = widget.ColumnSpan,
                    DataSource = widget.DataSource,
                    Configuration = widget.Configuration,
                    IsVisible = widget.IsVisible,
                    RefreshInterval = widget.RefreshInterval
                };
                
                await _metadataService.SaveWidgetAsync(duplicated);
                await LoadWidgetsAsync();
                ShowSuccess("ویجت با موفقیت کپی شد");
            }
        }

        private async void DeleteWidget_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is WidgetConfiguration widget)
            {
                var result = MessageBox.Show(
                    $"آیا از حذف ویجت '{widget.WidgetNameFa ?? widget.WidgetName}' اطمینان دارید؟",
                    "تایید حذف",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                
                if (result == MessageBoxResult.Yes)
                {
                    await _metadataService.DeleteWidgetAsync(widget.Id);
                    await LoadWidgetsAsync();
                    ShowSuccess("ویجت با موفقیت حذف شد");
                }
            }
        }

        private void ShowEditor(WidgetConfiguration widget)
        {
            EditorHeaderText.Text = _isEditing ? "ویرایش ویجت" : "ایجاد ویجت جدید";
            
            // Basic Info
            WidgetKeyTextBox.Text = widget.WidgetKey;
            WidgetNameTextBox.Text = widget.WidgetName;
            WidgetNameFaTextBox.Text = widget.WidgetNameFa;
            
            // Widget Type
            foreach (ComboBoxItem item in WidgetTypeComboBox.Items)
            {
                if (item.Tag?.ToString() == widget.WidgetType.ToString().ToLower())
                {
                    WidgetTypeComboBox.SelectedItem = item;
                    break;
                }
            }
            
            // Tab
            foreach (ComboBoxItem item in TabKeyComboBox.Items)
            {
                if (item.Tag?.ToString() == widget.TabKey)
                {
                    TabKeyComboBox.SelectedItem = item;
                    break;
                }
            }
            
            // Position
            RowTextBox.Text = widget.Row.ToString();
            ColumnTextBox.Text = widget.Column.ToString();
            RowSpanTextBox.Text = widget.RowSpan.ToString();
            ColumnSpanTextBox.Text = widget.ColumnSpan.ToString();
            IsVisibleCheckBox.IsChecked = widget.IsVisible;
            
            // Data Configuration
            if (!string.IsNullOrEmpty(widget.Configuration))
            {
                try
                {
                    var config = JObject.Parse(widget.Configuration);
                    
                    // Query settings
                    if (config["query"] != null)
                    {
                        var query = config["query"];
                        QueryFieldComboBox.Text = query["field"]?.ToString() ?? "";
                        AggregationComboBox.Text = query["aggregation"]?.ToString() ?? "";
                        FilterTextBox.Text = query["filter"]?.ToString() ?? "{}";
                    }
                    
                    // Visual settings
                    if (config["chartType"] != null)
                    {
                        foreach (ComboBoxItem item in ChartTypeComboBox.Items)
                        {
                            if (item.Tag?.ToString() == config["chartType"].ToString())
                            {
                                ChartTypeComboBox.SelectedItem = item;
                                break;
                            }
                        }
                    }
                    
                    if (config["colors"] is JArray colors)
                    {
                        ColorsTextBox.Text = string.Join(",", colors);
                    }
                    
                    ShowLegendCheckBox.IsChecked = config["showLegend"]?.Value<bool>() ?? false;
                    ShowLabelsCheckBox.IsChecked = config["showLabels"]?.Value<bool>() ?? false;
                    ShowGridCheckBox.IsChecked = config["showGrid"]?.Value<bool>() ?? false;
                    
                    ConfigurationJsonTextBox.Text = config.ToString(Formatting.Indented);
                }
                catch { }
            }
            
            // Refresh settings
            AutoRefreshCheckBox.IsChecked = !string.IsNullOrEmpty(widget.RefreshInterval);
            RefreshIntervalTextBox.Text = widget.RefreshInterval ?? "60";
            
            EditorCard.Visibility = Visibility.Visible;
        }

        private async void SaveWidget_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedWidget == null)
                return;
            
            try
            {
                // Update widget properties
                _selectedWidget.WidgetName = WidgetNameTextBox.Text;
                _selectedWidget.WidgetNameFa = WidgetNameFaTextBox.Text;
                
                if (WidgetTypeComboBox.SelectedItem is ComboBoxItem typeItem)
                {
                    _selectedWidget.WidgetType = Enum.Parse<WidgetType>(
                        typeItem.Tag?.ToString() ?? "Custom", true);
                }
                
                if (TabKeyComboBox.SelectedItem is ComboBoxItem tabItem)
                {
                    _selectedWidget.TabKey = tabItem.Tag?.ToString();
                }
                
                // Position
                _selectedWidget.Row = int.TryParse(RowTextBox.Text, out int row) ? row : 1;
                _selectedWidget.Column = int.TryParse(ColumnTextBox.Text, out int col) ? col : 1;
                _selectedWidget.RowSpan = int.TryParse(RowSpanTextBox.Text, out int rowSpan) ? rowSpan : 1;
                _selectedWidget.ColumnSpan = int.TryParse(ColumnSpanTextBox.Text, out int colSpan) ? colSpan : 1;
                _selectedWidget.IsVisible = IsVisibleCheckBox.IsChecked ?? true;
                
                // Build configuration
                var config = new JObject();
                
                // Query configuration
                if (!string.IsNullOrEmpty(QueryFieldComboBox.Text))
                {
                    config["query"] = new JObject
                    {
                        ["field"] = QueryFieldComboBox.Text,
                        ["aggregation"] = (AggregationComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "count",
                        ["filter"] = FilterTextBox.Text
                    };
                }
                
                // Visual configuration
                if (ChartTypeComboBox.SelectedItem is ComboBoxItem chartItem)
                {
                    config["chartType"] = chartItem.Tag?.ToString();
                }
                
                if (!string.IsNullOrEmpty(ColorsTextBox.Text))
                {
                    config["colors"] = new JArray(ColorsTextBox.Text.Split(',').Select(c => c.Trim()));
                }
                
                config["showLegend"] = ShowLegendCheckBox.IsChecked ?? false;
                config["showLabels"] = ShowLabelsCheckBox.IsChecked ?? false;
                config["showGrid"] = ShowGridCheckBox.IsChecked ?? false;
                
                _selectedWidget.Configuration = config.ToString(Formatting.None);
                
                // Refresh settings
                _selectedWidget.RefreshInterval = AutoRefreshCheckBox.IsChecked == true 
                    ? RefreshIntervalTextBox.Text : null;
                
                // Save
                await _metadataService.SaveWidgetAsync(_selectedWidget);
                await LoadWidgetsAsync();
                
                EditorCard.Visibility = Visibility.Collapsed;
                ShowSuccess(_isEditing ? "ویجت با موفقیت ویرایش شد" : "ویجت جدید با موفقیت ایجاد شد");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "خطا در ذخیره ویجت");
                ShowError("خطا در ذخیره ویجت: " + ex.Message);
            }
        }

        private void CancelEdit_Click(object sender, RoutedEventArgs e)
        {
            EditorCard.Visibility = Visibility.Collapsed;
            _selectedWidget = null;
        }

        private void CloseEditor_Click(object sender, RoutedEventArgs e)
        {
            EditorCard.Visibility = Visibility.Collapsed;
        }

        private async void PreviewWidget_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedWidget == null)
                return;
            
            try
            {
                // Build widget metadata
                var metadata = JObject.FromObject(_selectedWidget);
                metadata["configuration"] = JObject.Parse(_selectedWidget.Configuration ?? "{}");
                
                // Build and show widget
                var widgetElement = await _widgetBuilder.BuildWidgetAsync(metadata);
                
                // Show in preview window
                var previewWindow = new Window
                {
                    Title = "پیش‌نمایش ویجت",
                    Width = 600,
                    Height = 400,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Content = widgetElement
                };
                
                previewWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "خطا در پیش‌نمایش ویجت");
                ShowError("خطا در پیش‌نمایش: " + ex.Message);
            }
        }

        private void WidgetTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (WidgetTypeComboBox.SelectedItem is ComboBoxItem item)
            {
                var type = item.Tag?.ToString() ?? "";
                
                // Show/hide relevant sections
                ChartTypeComboBox.Visibility = type == "chart" ? Visibility.Visible : Visibility.Collapsed;
                DataExpander.IsExpanded = type != "custom";
                VisualExpander.IsExpanded = type == "chart" || type == "kpi";
            }
        }

        private async void TabFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await LoadWidgetsAsync();
        }

        private async void SearchWidgetBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            await LoadWidgetsAsync();
        }

        private void WidgetTypesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Filter widgets by type if needed
        }

        private void WidgetLibraryButton_Click(object sender, RoutedEventArgs e)
        {
            // Show widget library/templates
            ShowInfo("کتابخانه ویجت‌ها در حال توسعه است");
        }

        private void AutoRefreshCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            RefreshIntervalTextBox.IsEnabled = AutoRefreshCheckBox.IsChecked ?? false;
        }

        private void ShowSuccess(string message)
        {
            MessageBox.Show(message, "موفقیت", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ShowError(string message)
        {
            MessageBox.Show(message, "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void ShowInfo(string message)
        {
            MessageBox.Show(message, "اطلاعات", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}

// پایان فایل: UI/Views/WidgetManagementView.xaml.cs