// مسیر فایل: UI/Views/DashboardView.xaml.cs
// ابتدای کد
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OxyPlot;
using OxyPlot.Series;
using TradingJournal.Core.AnalysisEngine;
using TradingJournal.Core.MetadataEngine;
using TradingJournal.Core.MetadataEngine.Models;
using TradingJournal.Data.Repositories;

namespace TradingJournal.UI.Views
{
    public partial class DashboardView : UserControl
    {
        private readonly ITradeRepository _tradeRepository;
        private readonly SmartAnalysisEngine _analysisEngine;
        private readonly MetadataManager _metadataManager;
        
        public ObservableCollection<WidgetMetadata> Widgets { get; set; }
        public ObservableCollection<PlotModel> Charts { get; set; }

        public DashboardView(
            ITradeRepository tradeRepository, 
            SmartAnalysisEngine analysisEngine,
            MetadataManager metadataManager)
        {
            InitializeComponent();
            
            _tradeRepository = tradeRepository;
            _analysisEngine = analysisEngine;
            _metadataManager = metadataManager;
            
            Widgets = new ObservableCollection<WidgetMetadata>();
            Charts = new ObservableCollection<PlotModel>();
            
            DataContext = this;
            
            LoadDashboard();
        }

        private async void LoadDashboard()
        {
            ShowProgress(true);
            
            try
            {
                // Load widgets from metadata
                var widgets = _metadataManager.GetAllWidgets();
                foreach (var widget in widgets.Where(w => w.IsVisible))
                {
                    Widgets.Add(widget);
                }

                // Load statistics
                await LoadStatistics();
                
                // Load charts
                await LoadCharts();
                
                // Load recent trades
                await LoadRecentTrades();
                
                // Load analysis
                await LoadAnalysis();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطا در بارگذاری داشبورد: {ex.Message}");
            }
            finally
            {
                ShowProgress(false);
            }
        }

        private async Task LoadStatistics()
        {
            var trades = await _tradeRepository.GetAllAsync();
            
            // Today's stats
            var todayTrades = trades.Where(t => t.EntryDate.Date == DateTime.Today).ToList();
            TodayTradesCount.Text = todayTrades.Count.ToString();
            TodayProfit.Text = todayTrades.Sum(t => t.Profit ?? 0).ToString("N0");
            
            // Week stats
            var weekStart = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
            var weekTrades = trades.Where(t => t.EntryDate >= weekStart).ToList();
            WeekTradesCount.Text = weekTrades.Count.ToString();
            WeekProfit.Text = weekTrades.Sum(t => t.Profit ?? 0).ToString("N0");
            
            // Month stats
            var monthTrades = trades.Where(t => t.EntryDate.Month == DateTime.Now.Month).ToList();
            MonthTradesCount.Text = monthTrades.Count.ToString();
            MonthProfit.Text = monthTrades.Sum(t => t.Profit ?? 0).ToString("N0");
            
            // Overall stats
            TotalTradesCount.Text = trades.Count().ToString();
            TotalProfit.Text = trades.Sum(t => t.Profit ?? 0).ToString("N0");
            WinRate.Text = $"{CalculateWinRate(trades):F1}%";
            AverageProfit.Text = trades.Average(t => t.Profit ?? 0).ToString("N0");
        }

        private async Task LoadCharts()
        {
            // Profit chart
            var profitChart = CreateProfitChart();
            Charts.Add(profitChart);
            
            // Win rate chart
            var winRateChart = CreateWinRateChart();
            Charts.Add(winRateChart);
            
            // Strategy performance chart
            var strategyChart = await CreateStrategyChart();
            Charts.Add(strategyChart);
            
            // Symbol distribution chart
            var symbolChart = await CreateSymbolChart();
            Charts.Add(symbolChart);
        }

        private PlotModel CreateProfitChart()
        {
            var model = new PlotModel
            {
                Title = "روند سود/زیان",
                Background = OxyColors.White
            };

            var lineSeries = new LineSeries
            {
                Title = "سود تجمعی",
                Color = OxyColors.Blue,
                StrokeThickness = 2
            };

            // Add sample data
            var trades = _tradeRepository.GetAllAsync().Result
                .OrderBy(t => t.EntryDate).ToList();
            
            double cumulativeProfit = 0;
            foreach (var trade in trades)
            {
                cumulativeProfit += (double)(trade.Profit ?? 0);
                lineSeries.Points.Add(new DataPoint(
                    DateTimeAxis.ToDouble(trade.EntryDate), 
                    cumulativeProfit));
            }

            model.Series.Add(lineSeries);
            return model;
        }

        private PlotModel CreateWinRateChart()
        {
            var model = new PlotModel
            {
                Title = "نرخ برد ماهانه",
                Background = OxyColors.White
            };

            var barSeries = new ColumnSeries
            {
                Title = "نرخ برد",
                FillColor = OxyColors.Green
            };

            // Calculate monthly win rates
            var trades = _tradeRepository.GetAllAsync().Result;
            var monthlyGroups = trades.GroupBy(t => new { t.EntryDate.Year, t.EntryDate.Month });
            
            foreach (var group in monthlyGroups.OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month))
            {
                var winRate = CalculateWinRate(group.ToList());
                barSeries.Items.Add(new ColumnItem(winRate));
            }

            model.Series.Add(barSeries);
            return model;
        }

        private async Task<PlotModel> CreateStrategyChart()
        {
            var model = new PlotModel
            {
                Title = "عملکرد استراتژی‌ها",
                Background = OxyColors.White
            };

            var pieSeries = new PieSeries
            {
                StrokeThickness = 2,
                InsideLabelPosition = 0.8,
                AngleSpan = 360,
                StartAngle = 0
            };

            var analysis = await _analysisEngine.AnalyzeStrategies();
            foreach (var strategy in analysis.StrategyStatistics.Take(5))
            {
                pieSeries.Slices.Add(new PieSlice(
                    strategy.Key, 
                    (double)strategy.Value.TotalProfit));
            }

            model.Series.Add(pieSeries);
            return model;
        }

        private async Task<PlotModel> CreateSymbolChart()
        {
            var model = new PlotModel
            {
                Title = "توزیع نمادها",
                Background = OxyColors.White
            };

            var barSeries = new BarSeries
            {
                Title = "تعداد معاملات",
                FillColor = OxyColors.Orange
            };

            var trades = await _tradeRepository.GetAllAsync();
            var symbolGroups = trades.GroupBy(t => t.Symbol)
                .OrderByDescending(g => g.Count())
                .Take(10);

            foreach (var group in symbolGroups)
            {
                barSeries.Items.Add(new BarItem(group.Count()));
            }

            model.Series.Add(barSeries);
            return model;
        }

        private async Task LoadRecentTrades()
        {
            var trades = await _tradeRepository.GetAllAsync();
            var recentTrades = trades
                .OrderByDescending(t => t.EntryDate)
                .Take(10)
                .ToList();

            RecentTradesGrid.ItemsSource = recentTrades;
        }

        private async Task LoadAnalysis()
        {
            // Day of week analysis
            var dayAnalysis = await _analysisEngine.AnalyzeDayOfWeekPerformance();
            DayAnalysisPanel.Children.Clear();
            
            foreach (var recommendation in dayAnalysis.Recommendations.Take(3))
            {
                var card = CreateRecommendationCard(recommendation);
                DayAnalysisPanel.Children.Add(card);
            }

            // Strategy analysis
            var strategyAnalysis = await _analysisEngine.AnalyzeStrategies();
            StrategyAnalysisPanel.Children.Clear();
            
            foreach (var recommendation in strategyAnalysis.Recommendations.Take(3))
            {
                var card = CreateRecommendationCard(recommendation);
                StrategyAnalysisPanel.Children.Add(card);
            }

            // Emotional analysis
            var emotionalAnalysis = await _analysisEngine.AnalyzeEmotions();
            EmotionalAnalysisPanel.Children.Clear();
            
            foreach (var recommendation in emotionalAnalysis.Recommendations.Take(3))
            {
                var card = CreateRecommendationCard(recommendation);
                EmotionalAnalysisPanel.Children.Add(card);
            }
        }

        private Card CreateRecommendationCard(AnalysisRecommendation recommendation)
        {
            var card = new Card
            {
                Margin = new Thickness(4),
                Padding = new Thickness(12)
            };

            var stack = new StackPanel();
            
            var title = new TextBlock
            {
                Text = recommendation.Title,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 8)
            };

            var description = new TextBlock
            {
                Text = recommendation.Description,
                TextWrapping = TextWrapping.Wrap
            };

            var confidence = new ProgressBar
            {
                Value = recommendation.ConfidenceLevel * 100,
                Height = 4,
                Margin = new Thickness(0, 8, 0, 0)
            };

            stack.Children.Add(title);
            stack.Children.Add(description);
            stack.Children.Add(confidence);
            
            card.Content = stack;
            return card;
        }

        private double CalculateWinRate(IEnumerable<Trade> trades)
        {
            var total = trades.Count();
            if (total == 0) return 0;
            
            var wins = trades.Count(t => t.IsWin);
            return (double)wins / total * 100;
        }

        private void ShowProgress(bool show)
        {
            ProgressBar.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnRefresh(object sender, RoutedEventArgs e)
        {
            LoadDashboard();
        }

        private void OnCustomizeDashboard(object sender, RoutedEventArgs e)
        {
            var dialog = new DashboardCustomizeDialog(_metadataManager);
            if (dialog.ShowDialog() == true)
            {
                LoadDashboard();
            }
        }
    }
}
// پایان کد