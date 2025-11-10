// ابتدای فایل: Core/WidgetEngine/WidgetBuilder.cs
// مسیر: /Core/WidgetEngine/WidgetBuilder.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using MaterialDesignThemes.Wpf;
using Newtonsoft.Json.Linq;
using OxyPlot;
using OxyPlot.Wpf;
using OxyPlot.Series;
using OxyPlot.Axes;
using TradingJournal.Core.QueryEngine;
using TradingJournal.Data;
using TradingJournal.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace TradingJournal.Core.WidgetEngine
{
    public class WidgetBuilder
    {
        private readonly QueryEngine.QueryEngine _queryEngine;
        private readonly DatabaseContext _dbContext;

        public WidgetBuilder()
        {
            _queryEngine = new QueryEngine.QueryEngine();
            _dbContext = new DatabaseContext();
        }

        public async Task<UIElement> BuildWidgetAsync(JObject widgetMetadata)
        {
            var widgetType = widgetMetadata["type"]?.ToString() ?? "card";
            var config = widgetMetadata["configuration"] as JObject;

            return widgetType.ToLower() switch
            {
                "kpi" => await BuildKpiWidget(config),
                "chart" => await BuildChartWidget(config),
                "table" => await BuildTableWidget(config),
                "card" => await BuildCardWidget(config),
                _ => BuildPlaceholderWidget(widgetMetadata)
            };
        }

        private async Task<UIElement> BuildKpiWidget(JObject config)
        {
            var card = new Card
            {
                Padding = new Thickness(16),
                Margin = new Thickness(8)
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Icon
            var iconName = config["icon"]?.ToString() ?? "Counter";
            var iconColor = config["iconColor"]?.ToString() ?? "#2196F3";
            
            var icon = new PackIcon
            {
                Kind = Enum.TryParse<PackIconKind>(iconName, out var kind) ? kind : PackIconKind.Counter,
                Width = 48,
                Height = 48,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(iconColor)),
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(icon, 0);
            grid.Children.Add(icon);

            // Value
            var value = await CalculateKpiValue(config["query"] as JObject);
            var format = config["format"] as JObject;
            var formattedValue = FormatValue(value, format);

            var valueText = new TextBlock
            {
                Text = formattedValue,
                FontSize = 28,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4)
            };
            Grid.SetRow(valueText, 1);
            grid.Children.Add(valueText);

            // Label
            var label = config["widgetNameFa"]?.ToString() ?? config["widgetName"]?.ToString() ?? "";
            var labelText = new TextBlock
            {
                Text = label,
                FontSize = 14,
                Opacity = 0.7,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetRow(labelText, 2);
            grid.Children.Add(labelText);

            card.Content = grid;
            return card;
        }

        private async Task<UIElement> BuildChartWidget(JObject config)
        {
            var card = new Card
            {
                Padding = new Thickness(16),
                Margin = new Thickness(8)
            };

            var chartType = config["chartType"]?.ToString() ?? "line";
            var plotModel = new PlotModel
            {
                Background = OxyColors.Transparent,
                PlotAreaBorderThickness = new OxyThickness(0)
            };

            // Get data
            var data = await GetChartData(config["query"] as JObject);

            switch (chartType.ToLower())
            {
                case "line":
                    AddLineSeries(plotModel, data, config);
                    break;
                case "bar":
                    AddBarSeries(plotModel, data, config);
                    break;
                case "pie":
                    AddPieSeries(plotModel, data, config);
                    break;
            }

            var plotView = new PlotView
            {
                Model = plotModel,
                MinHeight = 200
            };

            card.Content = plotView;
            return card;
        }

        private async Task<UIElement> BuildTableWidget(JObject config)
        {
            var card = new Card
            {
                Padding = new Thickness(16),
                Margin = new Thickness(8)
            };

            var dataGrid = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                IsReadOnly = true,
                MaxHeight = 400
            };

            // Define columns from config
            var columns = config["columns"] as JArray;
            if (columns != null)
            {
                foreach (JObject column in columns)
                {
                    var field = column["field"]?.ToString() ?? "";
                    var label = column["labelFa"]?.ToString() ?? column["label"]?.ToString() ?? field;
                    var width = column["width"]?.Value<double>() ?? 100;

                    dataGrid.Columns.Add(new DataGridTextColumn
                    {
                        Header = label,
                        Binding = new System.Windows.Data.Binding(field),
                        Width = width
                    });
                }
            }

            // Load data
            var data = await GetTableData(config["query"] as JObject);
            dataGrid.ItemsSource = data;

            card.Content = dataGrid;
            return card;
        }

        private async Task<UIElement> BuildCardWidget(JObject config)
        {
            var card = new Card
            {
                Padding = new Thickness(16),
                Margin = new Thickness(8)
            };

            var content = new StackPanel();
            
            // Title
            var title = config["title"]?.ToString();
            if (!string.IsNullOrEmpty(title))
            {
                content.Children.Add(new TextBlock
                {
                    Text = title,
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 8)
                });
            }

            // Content
            var text = config["content"]?.ToString() ?? "Card content";
            content.Children.Add(new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap
            });

            card.Content = content;
            return await Task.FromResult(card);
        }

        private UIElement BuildPlaceholderWidget(JObject metadata)
        {
            var card = new Card
            {
                Padding = new Thickness(16),
                Margin = new Thickness(8)
            };

            card.Content = new TextBlock
            {
                Text = metadata["widgetNameFa"]?.ToString() ?? metadata["widgetName"]?.ToString() ?? "Widget",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0.5
            };

            return card;
        }

        private async Task<object> CalculateKpiValue(JObject query)
        {
            if (query == null) return 0;

            var entity = query["entity"]?.ToString() ?? "Trade";
            var aggregation = query["aggregation"]?.ToString() ?? "count";
            var field = query["field"]?.ToString();

            switch (aggregation.ToLower())
            {
                case "count":
                    return await _dbContext.Trades.CountAsync();
                    
                case "sum":
                    if (field == "profitLoss")
                        return await _dbContext.Trades.SumAsync(t => t.ProfitLoss ?? 0);
                    break;
                    
                case "average":
                    if (field == "profitLoss")
                        return await _dbContext.Trades.AverageAsync(t => t.ProfitLoss ?? 0);
                    break;
                    
                case "max":
                    if (field == "profitLoss")
                        return await _dbContext.Trades.MaxAsync(t => t.ProfitLoss ?? 0);
                    break;
                    
                case "winrate":
                    var totalClosed = await _dbContext.Trades.Where(t => t.Status == TradeStatus.Closed).CountAsync();
                    if (totalClosed == 0) return 0;
                    var wins = await _dbContext.Trades.Where(t => t.Status == TradeStatus.Closed && t.ProfitLoss > 0).CountAsync();
                    return (double)wins / totalClosed * 100;
            }

            return 0;
        }

        private string FormatValue(object value, JObject format)
        {
            if (format == null) return value?.ToString() ?? "0";

            var type = format["type"]?.ToString() ?? "number";
            var decimals = format["decimals"]?.Value<int>() ?? 2;

            return type.ToLower() switch
            {
                "currency" => $"${Convert.ToDouble(value):N{decimals}}",
                "percentage" => $"{Convert.ToDouble(value):F{decimals}}%",
                "number" => $"{Convert.ToDouble(value):N{decimals}}",
                _ => value?.ToString() ?? "0"
            };
        }

        private async Task<List<ChartDataPoint>> GetChartData(JObject query)
        {
            // Simplified implementation - should use QueryEngine
            var data = new List<ChartDataPoint>();
            
            var trades = await _dbContext.Trades
                .Where(t => t.Status == TradeStatus.Closed)
                .OrderBy(t => t.EntryDate)
                .Take(30)
                .ToListAsync();

            foreach (var trade in trades)
            {
                data.Add(new ChartDataPoint
                {
                    X = trade.EntryDate.ToOADate(),
                    Y = (double)(trade.ProfitLoss ?? 0),
                    Label = trade.EntryDate.ToString("MMM dd")
                });
            }

            return data;
        }

        private async Task<List<Trade>> GetTableData(JObject query)
        {
            var limit = query?["limit"]?.Value<int>() ?? 10;
            
            return await _dbContext.Trades
                .OrderByDescending(t => t.EntryDate)
                .Take(limit)
                .ToListAsync();
        }

        private void AddLineSeries(PlotModel model, List<ChartDataPoint> data, JObject config)
        {
            var series = new LineSeries
            {
                StrokeThickness = 2,
                Color = OxyColor.Parse(config["colors"]?[0]?.ToString() ?? "#2196F3")
            };

            foreach (var point in data)
            {
                series.Points.Add(new DataPoint(point.X, point.Y));
            }

            model.Series.Add(series);
            
            model.Axes.Add(new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                StringFormat = "MMM dd"
            });
            
            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                StringFormat = "C0"
            });
        }

        private void AddBarSeries(PlotModel model, List<ChartDataPoint> data, JObject config)
        {
            var series = new ColumnSeries
            {
                FillColor = OxyColor.Parse(config["colors"]?[0]?.ToString() ?? "#673AB7")
            };

            for (int i = 0; i < data.Count; i++)
            {
                series.Items.Add(new ColumnItem(data[i].Y, i));
            }

            model.Series.Add(series);
            
            model.Axes.Add(new CategoryAxis
            {
                Position = AxisPosition.Bottom,
                ItemsSource = data.Select(d => d.Label).ToList()
            });
            
            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left
            });
        }

        private void AddPieSeries(PlotModel model, List<ChartDataPoint> data, JObject config)
        {
            var series = new PieSeries
            {
                InnerDiameter = 0,
                ExplodedDistance = 0.1,
                Stroke = OxyColors.White,
                StrokeThickness = 1
            };

            var colors = config["colors"] as JArray;
            var colorList = colors?.Select(c => OxyColor.Parse(c.ToString())).ToList() 
                ?? new List<OxyColor> { OxyColors.Blue, OxyColors.Red, OxyColors.Green };

            for (int i = 0; i < data.Count; i++)
            {
                series.Slices.Add(new PieSlice(data[i].Label, data[i].Y)
                {
                    Fill = colorList[i % colorList.Count]
                });
            }

            model.Series.Add(series);
        }

        private class ChartDataPoint
        {
            public double X { get; set; }
            public double Y { get; set; }
            public string Label { get; set; } = "";
        }
    }
}

// پایان فایل: Core/WidgetEngine/WidgetBuilder.cs