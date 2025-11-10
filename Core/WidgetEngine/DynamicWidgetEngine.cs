// مسیر فایل: Core/WidgetEngine/DynamicWidgetEngine.cs
// ابتدای کد
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OxyPlot;
using OxyPlot.Series;
using TradingJournal.Core.MetadataEngine.Models;
using TradingJournal.Data.Models;
using TradingJournal.Data.Repositories;

namespace TradingJournal.Core.WidgetEngine
{
    public class DynamicWidgetEngine
    {
        private readonly ITradeRepository _tradeRepository;
        private readonly Dictionary<string, IWidgetRenderer> _renderers;

        public DynamicWidgetEngine(ITradeRepository tradeRepository)
        {
            _tradeRepository = tradeRepository;
            _renderers = new Dictionary<string, IWidgetRenderer>
            {
                ["Chart"] = new ChartWidgetRenderer(),
                ["Table"] = new TableWidgetRenderer(),
                ["Card"] = new CardWidgetRenderer(),
                ["KPI"] = new KPIWidgetRenderer(),
                ["Gauge"] = new GaugeWidgetRenderer()
            };
        }

        public async Task<FrameworkElement> RenderWidget(WidgetMetadata widget)
        {
            try
            {
                // Get data for widget
                var data = await GetWidgetData(widget);
                
                // Get appropriate renderer
                if (_renderers.TryGetValue(widget.Type.ToString(), out var renderer))
                {
                    return renderer.Render(widget, data);
                }
                
                // Default renderer
                return CreateDefaultWidget(widget);
            }
            catch (Exception ex)
            {
                return CreateErrorWidget(widget, ex.Message);
            }
        }

        private async Task<WidgetData> GetWidgetData(WidgetMetadata widget)
        {
            var data = new WidgetData();
            
            // Parse data source
            if (!string.IsNullOrEmpty(widget.DataSource))
            {
                var parts = widget.DataSource.Split(':');
                var source = parts[0];
                var query = parts.Length > 1 ? parts[1] : "";
                
                switch (source)
                {
                    case "trades":
                        data.Trades = await GetTradeData(query);
                        break;
                    case "statistics":
                        data.Statistics = await GetStatistics(query);
                        break;
                    case "analysis":
                        data.Analysis = await GetAnalysis(query);
                        break;
                }
            }
            
            return data;
        }

        private async Task<List<Trade>> GetTradeData(string query)
        {
            var trades = await _tradeRepository.GetAllAsync();
            
            // Apply query filters
            if (!string.IsNullOrEmpty(query))
            {
                switch (query)
                {
                    case "today":
                        trades = trades.Where(t => t.EntryDate.Date == DateTime.Today);
                        break;
                    case "week":
                        var weekStart = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
                        trades = trades.Where(t => t.EntryDate >= weekStart);
                        break;
                    case "month":
                        trades = trades.Where(t => t.EntryDate.Month == DateTime.Now.Month);
                        break;
                    case "open":
                        trades = trades.Where(t => t.IsOpen);
                        break;
                    case "closed":
                        trades = trades.Where(t => !t.IsOpen);
                        break;
                }
            }
            
            return trades.ToList();
        }

        private async Task<Dictionary<string, object>> GetStatistics(string type)
        {
            var stats = new Dictionary<string, object>();
            var trades = await _tradeRepository.GetAllAsync();
            
            switch (type)
            {
                case "summary":
                    stats["TotalTrades"] = trades.Count();
                    stats["TotalProfit"] = trades.Sum(t => t.Profit ?? 0);
                    stats["WinRate"] = CalculateWinRate(trades);
                    stats["AverageProfit"] = trades.Average(t => t.Profit ?? 0);
                    break;
                    
                case "daily":
                    var todayTrades = trades.Where(t => t.EntryDate.Date == DateTime.Today);
                    stats["TodayTrades"] = todayTrades.Count();
                    stats["TodayProfit"] = todayTrades.Sum(t => t.Profit ?? 0);
                    break;
                    
                case "monthly":
                    var monthlyGroups = trades.GroupBy(t => new { t.EntryDate.Year, t.EntryDate.Month });
                    stats["MonthlyData"] = monthlyGroups.Select(g => new
                    {
                        Month = g.Key,
                        Count = g.Count(),
                        Profit = g.Sum(t => t.Profit ?? 0)
                    }).ToList();
                    break;
            }
            
            return stats;
        }

        private async Task<Dictionary<string, object>> GetAnalysis(string type)
        {
            var analysis = new Dictionary<string, object>();
            
            // Analysis implementation
            await Task.Delay(0); // Placeholder
            
            return analysis;
        }

        private double CalculateWinRate(IEnumerable<Trade> trades)
        {
            var total = trades.Count();
            if (total == 0) return 0;
            
            var wins = trades.Count(t => t.IsWin);
            return (double)wins / total * 100;
        }

        private FrameworkElement CreateDefaultWidget(WidgetMetadata widget)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Colors.LightGray),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(8)
            };

            var content = new TextBlock
            {
                Text = widget.Title,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 16,
                Margin = new Thickness(16)
            };

            card.Child = content;
            return card;
        }

        private FrameworkElement CreateErrorWidget(WidgetMetadata widget, string error)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Colors.MistyRose),
                BorderBrush = new SolidColorBrush(Colors.Red),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(8)
            };

            var stack = new StackPanel { Margin = new Thickness(16) };
            stack.Children.Add(new TextBlock
            {
                Text = widget.Title,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 8)
            });
            stack.Children.Add(new TextBlock
            {
                Text = $"خطا: {error}",
                Foreground = new SolidColorBrush(Colors.Red)
            });

            card.Child = stack;
            return card;
        }

        public void RegisterRenderer(string type, IWidgetRenderer renderer)
        {
            _renderers[type] = renderer;
        }
    }

    public interface IWidgetRenderer
    {
        FrameworkElement Render(WidgetMetadata widget, WidgetData data);
    }

    public class ChartWidgetRenderer : IWidgetRenderer
    {
        public FrameworkElement Render(WidgetMetadata widget, WidgetData data)
        {
            var plotModel = new PlotModel
            {
                Title = widget.Title,
                Background = OxyColors.White
            };

            // Get chart type from configuration
            var chartType = widget.Configuration.GetValueOrDefault("ChartType", "Line").ToString();
            
            switch (chartType)
            {
                case "Line":
                    AddLineSeries(plotModel, data);
                    break;
                case "Bar":
                    AddBarSeries(plotModel, data);
                    break;
                case "Pie":
                    AddPieSeries(plotModel, data);
                    break;
            }

            var plotView = new OxyPlot.Wpf.PlotView
            {
                Model = plotModel,
                Height = GetWidgetHeight(widget.Size),
                Width = GetWidgetWidth(widget.Size),
                Margin = new Thickness(8)
            };

            return WrapInCard(plotView, widget);
        }

        private void AddLineSeries(PlotModel model, WidgetData data)
        {
            var series = new LineSeries
            {
                Title = "Data",
                Color = OxyColors.Blue,
                StrokeThickness = 2
            };

            if (data.Trades != null)
            {
                double cumulative = 0;
                foreach (var trade in data.Trades.OrderBy(t => t.EntryDate))
                {
                    cumulative += (double)(trade.Profit ?? 0);
                    series.Points.Add(new DataPoint(
                        OxyPlot.Axes.DateTimeAxis.ToDouble(trade.EntryDate),
                        cumulative));
                }
            }

            model.Series.Add(series);
        }

        private void AddBarSeries(PlotModel model, WidgetData data)
        {
            var series = new ColumnSeries
            {
                Title = "Data",
                FillColor = OxyColors.Orange
            };

            if (data.Statistics != null && data.Statistics.ContainsKey("MonthlyData"))
            {
                // Add monthly data bars
            }

            model.Series.Add(series);
        }

        private void AddPieSeries(PlotModel model, WidgetData data)
        {
            var series = new PieSeries
            {
                StrokeThickness = 2,
                InsideLabelPosition = 0.8
            };

            if (data.Trades != null)
            {
                var groups = data.Trades.GroupBy(t => t.Strategy ?? "Unknown");
                foreach (var group in groups)
                {
                    series.Slices.Add(new PieSlice(group.Key, group.Count()));
                }
            }

            model.Series.Add(series);
        }

        private double GetWidgetHeight(WidgetSize size)
        {
            return size switch
            {
                WidgetSize.Small => 200,
                WidgetSize.Medium => 300,
                WidgetSize.Large => 400,
                WidgetSize.ExtraLarge => 500,
                _ => 300
            };
        }

        private double GetWidgetWidth(WidgetSize size)
        {
            return size switch
            {
                WidgetSize.Small => 300,
                WidgetSize.Medium => 450,
                WidgetSize.Large => 600,
                WidgetSize.ExtraLarge => 800,
                WidgetSize.FullWidth => double.NaN,
                _ => 450
            };
        }

        private FrameworkElement WrapInCard(FrameworkElement content, WidgetMetadata widget)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Colors.LightGray),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(8)
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            if (!string.IsNullOrEmpty(widget.Title))
            {
                var title = new TextBlock
                {
                    Text = widget.Title,
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(16, 16, 16, 8)
                };
                Grid.SetRow(title, 0);
                grid.Children.Add(title);
            }

            Grid.SetRow(content, 1);
            grid.Children.Add(content);

            card.Child = grid;
            return card;
        }
    }

    public class TableWidgetRenderer : IWidgetRenderer
    {
        public FrameworkElement Render(WidgetMetadata widget, WidgetData data)
        {
            var dataGrid = new DataGrid
            {
                AutoGenerateColumns = true,
                CanUserAddRows = false,
                Height = GetHeight(widget.Size),
                Margin = new Thickness(8)
            };

            if (data.Trades != null)
            {
                dataGrid.ItemsSource = data.Trades.Take(10);
            }

            return WrapInCard(dataGrid, widget);
        }

        private double GetHeight(WidgetSize size)
        {
            return size switch
            {
                WidgetSize.Small => 200,
                WidgetSize.Medium => 300,
                WidgetSize.Large => 400,
                _ => 300
            };
        }

        private FrameworkElement WrapInCard(FrameworkElement content, WidgetMetadata widget)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Colors.LightGray),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(8),
                Child = content
            };

            return card;
        }
    }

    public class CardWidgetRenderer : IWidgetRenderer
    {
        public FrameworkElement Render(WidgetMetadata widget, WidgetData data)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Colors.LightGray),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Width = GetWidth(widget.Size),
                Height = 120,
                Margin = new Thickness(8)
            };

            var grid = new Grid { Margin = new Thickness(16) };
            
            if (widget.Configuration.ContainsKey("Icon"))
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                // Add icon
                var icon = new TextBlock
                {
                    Text = widget.Configuration["Icon"].ToString(),
                    FontSize = 32,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 16, 0)
                };
                Grid.SetColumn(icon, 0);
                grid.Children.Add(icon);
            }

            var contentStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center
            };

            contentStack.Children.Add(new TextBlock
            {
                Text = widget.Title,
                FontSize = 14,
                Opacity = 0.7
            });

            var value = GetCardValue(widget, data);
            contentStack.Children.Add(new TextBlock
            {
                Text = value,
                FontSize = 24,
                FontWeight = FontWeights.Bold
            });

            Grid.SetColumn(contentStack, grid.ColumnDefinitions.Count > 1 ? 1 : 0);
            grid.Children.Add(contentStack);

            card.Child = grid;
            return card;
        }

        private string GetCardValue(WidgetMetadata widget, WidgetData data)
        {
            if (data.Statistics != null && widget.Configuration.ContainsKey("Field"))
            {
                var field = widget.Configuration["Field"].ToString();
                if (data.Statistics.ContainsKey(field))
                {
                    return data.Statistics[field].ToString();
                }
            }
            return "N/A";
        }

        private double GetWidth(WidgetSize size)
        {
            return size switch
            {
                WidgetSize.Small => 200,
                WidgetSize.Medium => 280,
                WidgetSize.Large => 360,
                _ => 280
            };
        }
    }

    public class KPIWidgetRenderer : IWidgetRenderer
    {
        public FrameworkElement Render(WidgetMetadata widget, WidgetData data)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Colors.LightGray),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Width = 280,
                Height = 150,
                Margin = new Thickness(8)
            };

            var stack = new StackPanel
            {
                Margin = new Thickness(16),
                VerticalAlignment = VerticalAlignment.Center
            };

            // Title
            stack.Children.Add(new TextBlock
            {
                Text = widget.Title,
                FontSize = 14,
                Opacity = 0.7,
                Margin = new Thickness(0, 0, 0, 8)
            });

            // Value
            var value = GetKPIValue(widget, data);
            stack.Children.Add(new TextBlock
            {
                Text = value.ToString("F2"),
                FontSize = 32,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            // Progress bar
            var progress = new ProgressBar
            {
                Value = Math.Min(100, value),
                Height = 8,
                Margin = new Thickness(0, 8, 0, 0)
            };
            stack.Children.Add(progress);

            card.Child = stack;
            return card;
        }

        private double GetKPIValue(WidgetMetadata widget, WidgetData data)
        {
            if (data.Statistics != null && widget.Configuration.ContainsKey("KPI"))
            {
                var kpi = widget.Configuration["KPI"].ToString();
                if (data.Statistics.ContainsKey(kpi))
                {
                    if (double.TryParse(data.Statistics[kpi].ToString(), out var value))
                    {
                        return value;
                    }
                }
            }
            return 0;
        }
    }

    public class GaugeWidgetRenderer : IWidgetRenderer
    {
        public FrameworkElement Render(WidgetMetadata widget, WidgetData data)
        {
            // Implement gauge visualization
            return new CardWidgetRenderer().Render(widget, data);
        }
    }

    public class WidgetData
    {
        public List<Trade> Trades { get; set; }
        public Dictionary<string, object> Statistics { get; set; }
        public Dictionary<string, object> Analysis { get; set; }
        public object CustomData { get; set; }
    }
}
// پایان کد