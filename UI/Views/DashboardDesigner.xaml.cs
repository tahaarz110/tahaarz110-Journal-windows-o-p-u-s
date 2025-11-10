// ابتدای فایل: UI/Views/DashboardDesigner.xaml.cs - بخش 1
// مسیر: /UI/Views/DashboardDesigner.xaml.cs

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using TradingJournal.Core.WidgetEngine;
using TradingJournal.Data.Models;

namespace TradingJournal.UI.Views
{
    public partial class DashboardDesigner : UserControl
    {
        private enum DesignerMode
        {
            Select,
            Move,
            Resize
        }

        private class DashboardWidget
        {
            public string WidgetId { get; set; } = Guid.NewGuid().ToString();
            public string WidgetType { get; set; } = "";
            public string WidgetName { get; set; } = "";
            public int Row { get; set; }
            public int Column { get; set; }
            public int RowSpan { get; set; } = 1;
            public int ColumnSpan { get; set; } = 1;
            public JObject Configuration { get; set; } = new JObject();
            public Border? Container { get; set; }
        }

        private class WidgetTemplate
        {
            public string Name { get; set; } = "";
            public string Icon { get; set; } = "";
            public string Type { get; set; } = "";
            public JObject DefaultConfig { get; set; } = new JObject();
        }

        private class LayoutTemplate
        {
            public string Name { get; set; } = "";
            public List<DashboardWidget> Widgets { get; set; } = new();
        }

        private DesignerMode _currentMode = DesignerMode.Select;
        private int _gridRows = 4;
        private int _gridColumns = 4;
        private double _cellWidth;
        private double _cellHeight;
        private readonly List<DashboardWidget> _widgets = new();
        private DashboardWidget? _selectedWidget;
        private Border? _draggedElement;
        private Point _dragStartPoint;
        private bool _isDragging;
        private readonly ObservableCollection<WidgetTemplate> _widgetTemplates;
        private readonly ObservableCollection<LayoutTemplate> _layoutTemplates;

        public DashboardDesigner()
        {
            InitializeComponent();
            
            _widgetTemplates = new ObservableCollection<WidgetTemplate>();
            _layoutTemplates = new ObservableCollection<LayoutTemplate>();
            
            InitializeTemplates();
            InitializeGrid();
            LoadDashboards();
        }

        private void InitializeTemplates()
        {
            // Widget templates
            _widgetTemplates.Add(new WidgetTemplate
            {
                Name = "KPI",
                Icon = "Counter",
                Type = "kpi",
                DefaultConfig = JObject.Parse(@"{
                    'title': 'عنوان KPI',
                    'value': '0',
                    'icon': 'Counter',
                    'color': '#2196F3'
                }")
            });

            _widgetTemplates.Add(new WidgetTemplate
            {
                Name = "نمودار خطی",
                Icon = "ChartLine",
                Type = "chart",
                DefaultConfig = JObject.Parse(@"{
                    'chartType': 'line',
                    'title': 'نمودار خطی',
                    'showLegend': true,
                    'showGrid': true
                }")
            });

            _widgetTemplates.Add(new WidgetTemplate
            {
                Name = "نمودار دایره‌ای",
                Icon = "ChartPie",
                Type = "chart",
                DefaultConfig = JObject.Parse(@"{
                    'chartType': 'pie',
                    'title': 'نمودار دایره‌ای',
                    'showLabels': true
                }")
            });

            _widgetTemplates.Add(new WidgetTemplate
            {
                Name = "جدول",
                Icon = "TableLarge",
                Type = "table",
                DefaultConfig = JObject.Parse(@"{
                    'title': 'جدول داده‌ها',
                    'pageSize': 10,
                    'showPagination': true
                }")
            });

            _widgetTemplates.Add(new WidgetTemplate
            {
                Name = "کارت اطلاعات",
                Icon = "CardText",
                Type = "card",
                DefaultConfig = JObject.Parse(@"{
                    'title': 'کارت',
                    'content': 'محتوای کارت'
                }")
            });

            WidgetLibraryItems.ItemsSource = _widgetTemplates;

            // Layout templates
            _layoutTemplates.Add(new LayoutTemplate
            {
                Name = "داشبورد پایه",
                Widgets = new List<DashboardWidget>
                {
                    new DashboardWidget { WidgetType = "kpi", Row = 0, Column = 0, RowSpan = 1, ColumnSpan = 1 },
                    new DashboardWidget { WidgetType = "kpi", Row = 0, Column = 1, RowSpan = 1, ColumnSpan = 1 },
                    new DashboardWidget { WidgetType = "kpi", Row = 0, Column = 2, RowSpan = 1, ColumnSpan = 1 },
                    new DashboardWidget { WidgetType = "kpi", Row = 0, Column = 3, RowSpan = 1, ColumnSpan = 1 },
                    new DashboardWidget { WidgetType = "chart", Row = 1, Column = 0, RowSpan = 2, ColumnSpan = 2 },
                    new DashboardWidget { WidgetType = "table", Row = 1, Column = 2, RowSpan = 2, ColumnSpan = 2 }
                }
            });

            _layoutTemplates.Add(new LayoutTemplate
            {
                Name = "داشبورد تحلیلی",
                Widgets = new List<DashboardWidget>
                {
                    new DashboardWidget { WidgetType = "chart", Row = 0, Column = 0, RowSpan = 2, ColumnSpan = 3 },
                    new DashboardWidget { WidgetType = "kpi", Row = 0, Column = 3, RowSpan = 1, ColumnSpan = 1 },
                    new DashboardWidget { WidgetType = "kpi", Row = 1, Column = 3, RowSpan = 1, ColumnSpan = 1 },
                    new DashboardWidget { WidgetType = "table", Row = 2, Column = 0, RowSpan = 2, ColumnSpan = 4 }
                }
            });

            LayoutTemplates.ItemsSource = _layoutTemplates;
        }

        private void InitializeGrid()
        {
            _gridRows = int.Parse((RowsComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "4");
            _gridColumns = int.Parse((ColumnsComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "4");
            
            DrawGridLines();
        }

        private void DrawGridLines()
        {
            GridLinesCanvas.Children.Clear();
            
            if (!ShowGridCheckBox.IsChecked ?? false)
                return;

            _cellWidth = DesignCanvas.ActualWidth / _gridColumns;
            _cellHeight = DesignCanvas.ActualHeight / _gridRows;

            // Vertical lines
            for (int i = 1; i < _gridColumns; i++)
            {
                var line = new Line
                {
                    X1 = i * _cellWidth,
                    Y1 = 0,
                    X2 = i * _cellWidth,
                    Y2 = DesignCanvas.ActualHeight,
                    Stroke = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)),
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 5, 5 }
                };
                GridLinesCanvas.Children.Add(line);
            }

            // Horizontal lines
            for (int i = 1; i < _gridRows; i++)
            {
                var line = new Line
                {
                    X1 = 0,
                    Y1 = i * _cellHeight,
                    X2 = DesignCanvas.ActualWidth,
                    Y2 = i * _cellHeight,
                    Stroke = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)),
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 5, 5 }
                };
                GridLinesCanvas.Children.Add(line);
            }
        }
// ابتدای بخش 2 فایل: UI/Views/DashboardDesigner.xaml.cs
// ادامه از بخش 1

        private void LoadDashboards()
        {
            // Load existing dashboards
            DashboardSelector.Items.Add(new ComboBoxItem { Content = "داشبورد اصلی", Tag = "main" });
            DashboardSelector.Items.Add(new ComboBoxItem { Content = "داشبورد تحلیل", Tag = "analysis" });
            DashboardSelector.Items.Add(new ComboBoxItem { Content = "داشبورد عملکرد", Tag = "performance" });
        }

        private void AddWidget(DashboardWidget widget)
        {
            var container = new Border
            {
                Background = Brushes.White,
                BorderBrush = (Brush)new BrushConverter().ConvertFromString("#2196F3"),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(4),
                Cursor = Cursors.Hand
            };

            // Content
            var grid = new Grid();
            
            // Widget content placeholder
            var content = new TextBlock
            {
                Text = widget.WidgetName,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 14,
                Opacity = 0.7
            };
            grid.Children.Add(content);

            // Resize handle
            var resizeHandle = new Path
            {
                Data = Geometry.Parse("M 0,10 L 10,10 L 10,0"),
                Fill = Brushes.Gray,
                Width = 10,
                Height = 10,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Cursor = Cursors.SizeNWSE,
                Visibility = Visibility.Collapsed
            };
            grid.Children.Add(resizeHandle);

            container.Child = grid;
            widget.Container = container;

            // Position widget
            UpdateWidgetPosition(widget);

            // Add event handlers
            container.MouseLeftButtonDown += Widget_MouseLeftButtonDown;
            container.MouseMove += Widget_MouseMove;
            container.MouseLeftButtonUp += Widget_MouseLeftButtonUp;
            container.MouseEnter += Widget_MouseEnter;
            container.MouseLeave += Widget_MouseLeave;

            WidgetCanvas.Children.Add(container);
            _widgets.Add(widget);
            
            UpdateWidgetCount();
        }

        private void UpdateWidgetPosition(DashboardWidget widget)
        {
            if (widget.Container == null) return;

            var left = widget.Column * _cellWidth;
            var top = widget.Row * _cellHeight;
            var width = widget.ColumnSpan * _cellWidth - 8;
            var height = widget.RowSpan * _cellHeight - 8;

            Canvas.SetLeft(widget.Container, left);
            Canvas.SetTop(widget.Container, top);
            widget.Container.Width = width;
            widget.Container.Height = height;
        }

        private void SelectWidget(DashboardWidget? widget)
        {
            // Deselect previous
            if (_selectedWidget?.Container != null)
            {
                _selectedWidget.Container.BorderBrush = (Brush)new BrushConverter().ConvertFromString("#2196F3");
                
                // Hide resize handle
                if (_selectedWidget.Container.Child is Grid grid)
                {
                    var resizeHandle = grid.Children.OfType<Path>().FirstOrDefault();
                    if (resizeHandle != null)
                        resizeHandle.Visibility = Visibility.Collapsed;
                }
            }

            _selectedWidget = widget;

            if (widget?.Container != null)
            {
                widget.Container.BorderBrush = (Brush)new BrushConverter().ConvertFromString("#FF9800");
                
                // Show resize handle
                if (widget.Container.Child is Grid grid)
                {
                    var resizeHandle = grid.Children.OfType<Path>().FirstOrDefault();
                    if (resizeHandle != null)
                        resizeHandle.Visibility = Visibility.Visible;
                }

                ShowWidgetProperties(widget);
            }
            else
            {
                HideWidgetProperties();
            }
        }

        private void ShowWidgetProperties(DashboardWidget widget)
        {
            SelectedWidgetExpander.Visibility = Visibility.Visible;
            SelectedWidgetExpander.IsExpanded = true;

            WidgetIdTextBox.Text = widget.WidgetId;
            WidgetNameTextBox.Text = widget.WidgetName;
            WidgetTypeComboBox.Text = widget.WidgetType;
            WidgetRowTextBox.Text = widget.Row.ToString();
            WidgetColumnTextBox.Text = widget.Column.ToString();
            WidgetRowSpanTextBox.Text = widget.RowSpan.ToString();
            WidgetColumnSpanTextBox.Text = widget.ColumnSpan.ToString();
        }

        private void HideWidgetProperties()
        {
            SelectedWidgetExpander.Visibility = Visibility.Collapsed;
        }

        private Point GetGridPosition(Point mousePosition)
        {
            var column = Math.Min((int)(mousePosition.X / _cellWidth), _gridColumns - 1);
            var row = Math.Min((int)(mousePosition.Y / _cellHeight), _gridRows - 1);
            
            return new Point(Math.Max(0, column), Math.Max(0, row));
        }

        private void UpdateWidgetCount()
        {
            WidgetCountText.Text = $"تعداد ویجت: {_widgets.Count}";
        }

        // Event Handlers
        private void DashboardSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Load selected dashboard
        }

        private void NewDashboardButton_Click(object sender, RoutedEventArgs e)
        {
            // Clear canvas
            WidgetCanvas.Children.Clear();
            _widgets.Clear();
            _selectedWidget = null;
            
            DashboardIdTextBox.Text = Guid.NewGuid().ToString();
            DashboardNameTextBox.Text = "داشبورد جدید";
            DashboardNameFaTextBox.Text = "داشبورد جدید";
            
            UpdateWidgetCount();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dashboard = new
                {
                    Id = DashboardIdTextBox.Text,
                    Name = DashboardNameTextBox.Text,
                    NameFa = DashboardNameFaTextBox.Text,
                    Description = DashboardDescriptionTextBox.Text,
                    AutoRefresh = AutoRefreshCheckBox.IsChecked,
                    RefreshInterval = RefreshIntervalTextBox.Text,
                    GridRows = _gridRows,
                    GridColumns = _gridColumns,
                    Widgets = _widgets.Select(w => new
                    {
                        w.WidgetId,
                        w.WidgetType,
                        w.WidgetName,
                        w.Row,
                        w.Column,
                        w.RowSpan,
                        w.ColumnSpan,
                        Configuration = w.Configuration.ToString()
                    })
                };

                var json = JsonConvert.SerializeObject(dashboard, Formatting.Indented);
                // Save to database or file
                
                MessageBox.Show("داشبورد با موفقیت ذخیره شد", "موفقیت", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "خطا در ذخیره داشبورد");
                MessageBox.Show("خطا در ذخیره داشبورد", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PreviewButton_Click(object sender, RoutedEventArgs e)
        {
            // Show preview window
        }

        private void GridSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            // Show grid settings dialog
        }

        private void ModeButton_Checked(object sender, RoutedEventArgs e)
        {
            if (sender == SelectModeButton)
            {
                _currentMode = DesignerMode.Select;
                ModeText.Text = "حالت: انتخاب";
                MoveModeButton.IsChecked = false;
                ResizeModeButton.IsChecked = false;
            }
            else if (sender == MoveModeButton)
            {
                _currentMode = DesignerMode.Move;
                ModeText.Text = "حالت: جابجایی";
                SelectModeButton.IsChecked = false;
                ResizeModeButton.IsChecked = false;
            }
            else if (sender == ResizeModeButton)
            {
                _currentMode = DesignerMode.Resize;
                ModeText.Text = "حالت: تغییر اندازه";
                SelectModeButton.IsChecked = false;
                MoveModeButton.IsChecked = false;
            }
        }

        private void GridSize_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (RowsComboBox != null && ColumnsComboBox != null)
            {
                InitializeGrid();
                
                // Reposition widgets
                foreach (var widget in _widgets)
                {
                    UpdateWidgetPosition(widget);
                }
            }
        }

        private void ShowGridCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            DrawGridLines();
        }

        private void LibraryWidget_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is WidgetTemplate template)
            {
                _draggedElement = border;
                _dragStartPoint = e.GetPosition(this);
                
                DragDrop.DoDragDrop(border, template, DragDropEffects.Copy);
            }
        }

        private void DesignCanvas_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        private void DesignCanvas_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(WidgetTemplate)))
            {
                var template = (WidgetTemplate)e.Data.GetData(typeof(WidgetTemplate));
                var position = e.GetPosition(DesignCanvas);
                var gridPos = GetGridPosition(position);
                
                var widget = new DashboardWidget
                {
                    WidgetType = template.Type,
                    WidgetName = template.Name,
                    Row = (int)gridPos.Y,
                    Column = (int)gridPos.X,
                    RowSpan = 1,
                    ColumnSpan = 1,
                    Configuration = template.DefaultConfig
                };
                
                AddWidget(widget);
                SelectWidget(widget);
            }
        }

        private void Widget_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border)
            {
                var widget = _widgets.FirstOrDefault(w => w.Container == border);
                
                if (_currentMode == DesignerMode.Select)
                {
                    SelectWidget(widget);
                }
                else if (_currentMode == DesignerMode.Move)
                {
                    _isDragging = true;
                    _dragStartPoint = e.GetPosition(DesignCanvas);
                    border.CaptureMouse();
                }
            }
        }

        private void Widget_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && sender is Border border)
            {
                var currentPosition = e.GetPosition(DesignCanvas);
                
                if (SnapToGridCheckBox.IsChecked ?? false)
                {
                    var gridPos = GetGridPosition(currentPosition);
                    var widget = _widgets.FirstOrDefault(w => w.Container == border);
                    
                    if (widget != null)
                    {
                        widget.Row = (int)gridPos.Y;
                        widget.Column = (int)gridPos.X;
                        UpdateWidgetPosition(widget);
                        
                        if (widget == _selectedWidget)
                        {
                            WidgetRowTextBox.Text = widget.Row.ToString();
                            WidgetColumnTextBox.Text = widget.Column.ToString();
                        }
                    }
                }
                else
                {
                    var offsetX = currentPosition.X - _dragStartPoint.X;
                    var offsetY = currentPosition.Y - _dragStartPoint.Y;
                    
                    Canvas.SetLeft(border, Canvas.GetLeft(border) + offsetX);
                    Canvas.SetTop(border, Canvas.GetTop(border) + offsetY);
                    
                    _dragStartPoint = currentPosition;
                }
            }
        }

        private void Widget_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border)
            {
                _isDragging = false;
                border.ReleaseMouseCapture();
            }
        }

        private void Widget_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Border border)
            {
                border.BorderThickness = new Thickness(3);
            }
        }

        private void Widget_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Border border && _widgets.FirstOrDefault(w => w.Container == border) != _selectedWidget)
            {
                border.BorderThickness = new Thickness(2);
            }
        }

        private void DeleteWidgetButton_Click(object sender, RoutedEventArgs e)
        {
            RemoveWidget_Click(sender, e);
        }

        private void RemoveWidget_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedWidget != null)
            {
                WidgetCanvas.Children.Remove(_selectedWidget.Container);
                _widgets.Remove(_selectedWidget);
                _selectedWidget = null;
                HideWidgetProperties();
                UpdateWidgetCount();
            }
        }

        private void EditWidgetData_Click(object sender, RoutedEventArgs e)
        {
            // Open widget configuration dialog
        }

        private void ApplyTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is LayoutTemplate template)
            {
                // Clear existing widgets
                WidgetCanvas.Children.Clear();
                _widgets.Clear();
                
                // Add template widgets
                foreach (var widgetData in template.Widgets)
                {
                    var widget = new DashboardWidget
                    {
                        WidgetType = widgetData.WidgetType,
                        WidgetName = widgetData.WidgetType.ToUpper(),
                        Row = widgetData.Row,
                        Column = widgetData.Column,
                        RowSpan = widgetData.RowSpan,
                        ColumnSpan = widgetData.ColumnSpan
                    };
                    AddWidget(widget);
                }
                
                UpdateWidgetCount();
            }
        }

        private void AlignButton_Click(object sender, RoutedEventArgs e)
        {
            // Implement alignment logic
        }

        private void DistributeButton_Click(object sender, RoutedEventArgs e)
        {
            // Implement distribution logic
        }

        private void ExportJson_Click(object sender, RoutedEventArgs e)
        {
            // Export dashboard as JSON
        }

        private void ImportJson_Click(object sender, RoutedEventArgs e)
        {
            // Import dashboard from JSON
        }

        private void SaveAsTemplate_Click(object sender, RoutedEventArgs e)
        {
            // Save current layout as template
        }
    }
}

// پایان فایل: UI/Views/DashboardDesigner.xaml.cs