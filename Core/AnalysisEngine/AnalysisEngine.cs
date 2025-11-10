// ابتدای فایل: Core/AnalysisEngine/AnalysisEngine.cs
// مسیر: /Core/AnalysisEngine/AnalysisEngine.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Serilog;
using TradingJournal.Data;
using TradingJournal.Data.Models;

namespace TradingJournal.Core.AnalysisEngine
{
    public enum AnalysisType
    {
        Performance,
        Risk,
        Pattern,
        Psychology,
        Strategy,
        TimeAnalysis,
        Correlation
    }

    public class AnalysisResult
    {
        public string AnalysisId { get; set; } = Guid.NewGuid().ToString();
        public AnalysisType Type { get; set; }
        public DateTime AnalysisDate { get; set; } = DateTime.Now;
        public Dictionary<string, object> Metrics { get; set; } = new();
        public List<Insight> Insights { get; set; } = new();
        public List<Recommendation> Recommendations { get; set; } = new();
        public Dictionary<string, ChartData> Charts { get; set; } = new();
        public TimeSpan AnalysisTime { get; set; }
    }

    public class Insight
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Severity { get; set; } = "Info"; // Info, Warning, Critical
        public double ConfidenceLevel { get; set; }
        public Dictionary<string, object> Data { get; set; } = new();
    }

    public class Recommendation
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ActionType { get; set; } = string.Empty;
        public int Priority { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    public class ChartData
    {
        public string ChartType { get; set; } = string.Empty;
        public List<DataPoint> DataPoints { get; set; } = new();
        public Dictionary<string, object> Options { get; set; } = new();
    }

    public class DataPoint
    {
        public string Label { get; set; } = string.Empty;
        public double Value { get; set; }
        public DateTime? Date { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public interface IAnalysisEngine
    {
        Task<AnalysisResult> AnalyzePerformanceAsync(DateTime? fromDate = null, DateTime? toDate = null);
        Task<AnalysisResult> AnalyzeRiskAsync();
        Task<AnalysisResult> AnalyzePatternsAsync();
        Task<AnalysisResult> AnalyzePsychologyAsync();
        Task<AnalysisResult> AnalyzeStrategyAsync(string? strategy = null);
        Task<AnalysisResult> AnalyzeTimeAsync();
        Task<AnalysisResult> RunComprehensiveAnalysisAsync();
        Task<List<Trade>> GetBestTradesAsync(int count = 10);
        Task<List<Trade>> GetWorstTradesAsync(int count = 10);
        Task<Dictionary<string, double>> GetKeyMetricsAsync();
    }

    public class AnalysisEngine : IAnalysisEngine
    {
        private readonly DatabaseContext _dbContext;

        public AnalysisEngine()
        {
            _dbContext = new DatabaseContext();
        }

        public async Task<AnalysisResult> AnalyzePerformanceAsync(DateTime? fromDate = null, DateTime? toDate = null)
        {
            var startTime = DateTime.Now;
            var result = new AnalysisResult { Type = AnalysisType.Performance };

            try
            {
                var query = _dbContext.Trades.Where(t => t.Status == TradeStatus.Closed);
                
                if (fromDate.HasValue)
                    query = query.Where(t => t.EntryDate >= fromDate.Value);
                if (toDate.HasValue)
                    query = query.Where(t => t.EntryDate <= toDate.Value);

                var trades = await query.ToListAsync();

                if (trades.Any())
                {
                    // Calculate performance metrics
                    result.Metrics["TotalTrades"] = trades.Count;
                    result.Metrics["WinningTrades"] = trades.Count(t => t.ProfitLoss > 0);
                    result.Metrics["LosingTrades"] = trades.Count(t => t.ProfitLoss < 0);
                    result.Metrics["WinRate"] = CalculateWinRate(trades);
                    result.Metrics["TotalProfit"] = trades.Sum(t => t.ProfitLoss ?? 0);
                    result.Metrics["AverageProfit"] = trades.Where(t => t.ProfitLoss > 0).Average(t => t.ProfitLoss ?? 0);
                    result.Metrics["AverageLoss"] = trades.Where(t => t.ProfitLoss < 0).Average(t => t.ProfitLoss ?? 0);
                    result.Metrics["ProfitFactor"] = CalculateProfitFactor(trades);
                    result.Metrics["Expectancy"] = CalculateExpectancy(trades);
                    result.Metrics["MaxDrawdown"] = await CalculateMaxDrawdownAsync(trades);
                    result.Metrics["SharpeRatio"] = CalculateSharpeRatio(trades);
                    result.Metrics["RecoveryFactor"] = CalculateRecoveryFactor(trades);

                    // Generate insights
                    GeneratePerformanceInsights(result, trades);

                    // Generate recommendations
                    GeneratePerformanceRecommendations(result, trades);

                    // Generate charts
                    result.Charts["ProfitOverTime"] = GenerateProfitOverTimeChart(trades);
                    result.Charts["WinRateByMonth"] = GenerateWinRateByMonthChart(trades);
                    result.Charts["ProfitDistribution"] = GenerateProfitDistributionChart(trades);
                }

                result.AnalysisTime = DateTime.Now - startTime;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "خطا در تحلیل عملکرد");
            }

            return result;
        }

        public async Task<AnalysisResult> AnalyzeRiskAsync()
        {
            var startTime = DateTime.Now;
            var result = new AnalysisResult { Type = AnalysisType.Risk };

            try
            {
                var trades = await _dbContext.Trades
                    .Where(t => t.Status == TradeStatus.Closed)
                    .ToListAsync();

                if (trades.Any())
                {
                    // Risk metrics
                    result.Metrics["AverageRiskPercent"] = trades.Average(t => t.RiskPercent ?? 0);
                    result.Metrics["MaxRiskPercent"] = trades.Max(t => t.RiskPercent ?? 0);
                    result.Metrics["AverageRiskReward"] = CalculateAverageRiskReward(trades);
                    result.Metrics["MaxConsecutiveLosses"] = CalculateMaxConsecutiveLosses(trades);
                    result.Metrics["MaxLossInDay"] = await CalculateMaxLossInDayAsync(trades);
                    result.Metrics["RiskAdjustedReturn"] = CalculateRiskAdjustedReturn(trades);
                    result.Metrics["VaR95"] = CalculateValueAtRisk(trades, 0.95);
                    result.Metrics["CVaR95"] = CalculateConditionalValueAtRisk(trades, 0.95);

                    // Generate risk insights
                    GenerateRiskInsights(result, trades);

                    // Generate risk recommendations
                    GenerateRiskRecommendations(result, trades);

                    // Risk charts
                    result.Charts["RiskDistribution"] = GenerateRiskDistributionChart(trades);
                    result.Charts["DrawdownChart"] = await GenerateDrawdownChartAsync(trades);
                }

                result.AnalysisTime = DateTime.Now - startTime;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "خطا در تحلیل ریسک");
            }

            return result;
        }

        public async Task<AnalysisResult> AnalyzePatternsAsync()
        {
            var startTime = DateTime.Now;
            var result = new AnalysisResult { Type = AnalysisType.Pattern };

            try
            {
                var trades = await _dbContext.Trades.ToListAsync();

                // Pattern detection
                var patterns = new Dictionary<string, int>();
                
                // Day of week patterns
                var dayPatterns = trades
                    .GroupBy(t => t.EntryDate.DayOfWeek)
                    .Select(g => new
                    {
                        Day = g.Key,
                        Count = g.Count(),
                        WinRate = CalculateWinRate(g.ToList()),
                        AvgProfit = g.Average(t => t.ProfitLoss ?? 0)
                    });

                foreach (var pattern in dayPatterns)
                {
                    result.Metrics[$"Day_{pattern.Day}_WinRate"] = pattern.WinRate;
                    result.Metrics[$"Day_{pattern.Day}_AvgProfit"] = pattern.AvgProfit;
                }

                // Time of day patterns
                var hourPatterns = trades
                    .GroupBy(t => t.EntryDate.Hour)
                    .Select(g => new
                    {
                        Hour = g.Key,
                        Count = g.Count(),
                        WinRate = CalculateWinRate(g.ToList()),
                        AvgProfit = g.Average(t => t.ProfitLoss ?? 0)
                    });

                // Symbol patterns
                var symbolPatterns = trades
                    .GroupBy(t => t.Symbol)
                    .Select(g => new
                    {
                        Symbol = g.Key,
                        Count = g.Count(),
                        WinRate = CalculateWinRate(g.ToList()),
                        AvgProfit = g.Average(t => t.ProfitLoss ?? 0)
                    });

                // Generate pattern insights
                if (dayPatterns.Any())
                {
                    var bestDay = dayPatterns.OrderByDescending(p => p.WinRate).First();
                    result.Insights.Add(new Insight
                    {
                        Title = "بهترین روز معاملاتی",
                        Description = $"{bestDay.Day} با نرخ برد {bestDay.WinRate:P}",
                        Category = "Pattern",
                        ConfidenceLevel = 0.8
                    });
                }

                result.AnalysisTime = DateTime.Now - startTime;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "خطا در تحلیل الگوها");
            }

            return result;
        }

        public async Task<AnalysisResult> AnalyzePsychologyAsync()
        {
            var startTime = DateTime.Now;
            var result = new AnalysisResult { Type = AnalysisType.Psychology };

            try
            {
                var trades = await _dbContext.Trades
                    .Where(t => t.EntryEmotion != null || t.ExitEmotion != null)
                    .ToListAsync();

                if (trades.Any())
                {
                    // Emotion analysis
                    var emotionGroups = trades
                        .Where(t => t.EntryEmotion.HasValue)
                        .GroupBy(t => t.EntryEmotion!.Value)
                        .Select(g => new
                        {
                            Emotion = g.Key,
                            Count = g.Count(),
                            WinRate = CalculateWinRate(g.ToList()),
                            AvgProfit = g.Average(t => t.ProfitLoss ?? 0)
                        });

                    foreach (var group in emotionGroups)
                    {
                        result.Metrics[$"Emotion_{group.Emotion}_WinRate"] = group.WinRate;
                        result.Metrics[$"Emotion_{group.Emotion}_Count"] = group.Count;
                    }

                    // Confidence level analysis
                    var confidenceTrades = trades.Where(t => t.ConfidenceLevel.HasValue);
                    if (confidenceTrades.Any())
                    {
                        var avgConfidence = confidenceTrades.Average(t => t.ConfidenceLevel!.Value);
                        result.Metrics["AverageConfidence"] = avgConfidence;

                        var highConfidenceTrades = confidenceTrades.Where(t => t.ConfidenceLevel >= 7);
                        var lowConfidenceTrades = confidenceTrades.Where(t => t.ConfidenceLevel < 5);

                        result.Metrics["HighConfidenceWinRate"] = CalculateWinRate(highConfidenceTrades.ToList());
                        result.Metrics["LowConfidenceWinRate"] = CalculateWinRate(lowConfidenceTrades.ToList());
                    }

                    // Generate psychology insights
                    GeneratePsychologyInsights(result, trades);
                }

                result.AnalysisTime = DateTime.Now - startTime;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "خطا در تحلیل روانشناسی");
            }

            return result;
        }

        public async Task<AnalysisResult> AnalyzeStrategyAsync(string? strategy = null)
        {
            var startTime = DateTime.Now;
            var result = new AnalysisResult { Type = AnalysisType.Strategy };

            try
            {
                var query = _dbContext.Trades.AsQueryable();
                
                if (!string.IsNullOrEmpty(strategy))
                {
                    query = query.Where(t => t.Strategy == strategy);
                }

                var trades = await query.ToListAsync();

                if (trades.Any())
                {
                    // Strategy performance by type
                    var strategies = trades
                        .Where(t => !string.IsNullOrEmpty(t.Strategy))
                        .GroupBy(t => t.Strategy!)
                        .Select(g => new
                        {
                            Strategy = g.Key,
                            Count = g.Count(),
                            WinRate = CalculateWinRate(g.ToList()),
                            TotalProfit = g.Sum(t => t.ProfitLoss ?? 0),
                            AvgProfit = g.Average(t => t.ProfitLoss ?? 0),
                            ProfitFactor = CalculateProfitFactor(g.ToList())
                        })
                        .OrderByDescending(s => s.WinRate);

                    foreach (var s in strategies)
                    {
                        result.Metrics[$"Strategy_{s.Strategy}_WinRate"] = s.WinRate;
                        result.Metrics[$"Strategy_{s.Strategy}_ProfitFactor"] = s.ProfitFactor;
                        result.Metrics[$"Strategy_{s.Strategy}_Count"] = s.Count;
                    }

                    // Best performing strategy
                    if (strategies.Any())
                    {
                        var best = strategies.First();
                        result.Insights.Add(new Insight
                        {
                            Title = "بهترین استراتژی",
                            Description = $"{best.Strategy} با نرخ برد {best.WinRate:P}",
                            Category = "Strategy",
                            ConfidenceLevel = 0.9,
                            Data = new Dictionary<string, object>
                            {
                                ["Strategy"] = best.Strategy,
                                ["WinRate"] = best.WinRate,
                                ["ProfitFactor"] = best.ProfitFactor
                            }
                        });
                    }
                }

                result.AnalysisTime = DateTime.Now - startTime;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "خطا در تحلیل استراتژی");
            }

            return result;
        }

        public async Task<AnalysisResult> AnalyzeTimeAsync()
        {
            var startTime = DateTime.Now;
            var result = new AnalysisResult { Type = AnalysisType.TimeAnalysis };

            try
            {
                var trades = await _dbContext.Trades.ToListAsync();

                if (trades.Any())
                {
                    // Monthly analysis
                    var monthlyData = trades
                        .GroupBy(t => new { t.EntryDate.Year, t.EntryDate.Month })
                        .Select(g => new
                        {
                            Month = $"{g.Key.Year}-{g.Key.Month:D2}",
                            Count = g.Count(),
                            Profit = g.Sum(t => t.ProfitLoss ?? 0),
                            WinRate = CalculateWinRate(g.ToList())
                        })
                        .OrderBy(m => m.Month);

                    // Weekly analysis
                    var weeklyData = trades
                        .GroupBy(t => CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(
                            t.EntryDate, 
                            CalendarWeekRule.FirstFourDayWeek, 
                            DayOfWeek.Monday))
                        .Select(g => new
                        {
                            Week = g.Key,
                            Count = g.Count(),
                            Profit = g.Sum(t => t.ProfitLoss ?? 0),
                            WinRate = CalculateWinRate(g.ToList())
                        });

                    // Session analysis (Asian, European, American)
                    var sessionData = trades.Select(t => new
                    {
                        Trade = t,
                        Session = GetTradingSession(t.EntryDate)
                    })
                    .GroupBy(t => t.Session)
                    .Select(g => new
                    {
                        Session = g.Key,
                        Count = g.Count(),
                        WinRate = CalculateWinRate(g.Select(x => x.Trade).ToList())
                    });

                    foreach (var session in sessionData)
                    {
                        result.Metrics[$"Session_{session.Session}_WinRate"] = session.WinRate;
                        result.Metrics[$"Session_{session.Session}_Count"] = session.Count;
                    }

                    // Generate time-based charts
                    result.Charts["MonthlyProfits"] = new ChartData
                    {
                        ChartType = "line",
                        DataPoints = monthlyData.Select(m => new DataPoint
                        {
                            Label = m.Month,
                            Value = (double)m.Profit
                        }).ToList()
                    };
                }

                result.AnalysisTime = DateTime.Now - startTime;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "خطا در تحلیل زمانی");
            }

            return result;
        }

        public async Task<AnalysisResult> RunComprehensiveAnalysisAsync()
        {
            var result = new AnalysisResult { Type = AnalysisType.Performance };

            // Run all analyses
            var tasks = new[]
            {
                AnalyzePerformanceAsync(),
                AnalyzeRiskAsync(),
                AnalyzePatternsAsync(),
                AnalyzePsychologyAsync(),
                AnalyzeStrategyAsync(),
                AnalyzeTimeAsync()
            };

            var results = await Task.WhenAll(tasks);

            // Combine all results
            foreach (var analysis in results)
            {
                foreach (var metric in analysis.Metrics)
                {
                    result.Metrics[metric.Key] = metric.Value;
                }

                result.Insights.AddRange(analysis.Insights);
                result.Recommendations.AddRange(analysis.Recommendations);

                foreach (var chart in analysis.Charts)
                {
                    result.Charts[chart.Key] = chart.Value;
                }
            }

            return result;
        }

        // Helper methods
        private double CalculateWinRate(List<Trade> trades)
        {
            if (!trades.Any()) return 0;
            
            var closedTrades = trades.Where(t => t.Status == TradeStatus.Closed).ToList();
            if (!closedTrades.Any()) return 0;
            
            var wins = closedTrades.Count(t => t.ProfitLoss > 0);
            return (double)wins / closedTrades.Count * 100;
        }

        private double CalculateProfitFactor(List<Trade> trades)
        {
            var gains = trades.Where(t => t.ProfitLoss > 0).Sum(t => t.ProfitLoss ?? 0);
            var losses = Math.Abs(trades.Where(t => t.ProfitLoss < 0).Sum(t => t.ProfitLoss ?? 0));
            
            return losses == 0 ? gains > 0 ? double.MaxValue : 0 : (double)(gains / losses);
        }

        private double CalculateExpectancy(List<Trade> trades)
        {
            if (!trades.Any()) return 0;
            
            var winRate = CalculateWinRate(trades) / 100;
            var avgWin = trades.Where(t => t.ProfitLoss > 0).DefaultIfEmpty().Average(t => t?.ProfitLoss ?? 0);
            var avgLoss = Math.Abs(trades.Where(t => t.ProfitLoss < 0).DefaultIfEmpty().Average(t => t?.ProfitLoss ?? 0));
            
            return (double)((winRate * avgWin) - ((1 - winRate) * avgLoss));
        }

        private double CalculateSharpeRatio(List<Trade> trades)
        {
            if (!trades.Any()) return 0;
            
            var returns = trades.Select(t => (double)(t.ProfitLoss ?? 0)).ToList();
            var avgReturn = returns.Average();
            var stdDev = CalculateStandardDeviation(returns);
            
            return stdDev == 0 ? 0 : avgReturn / stdDev;
        }

        private double CalculateStandardDeviation(List<double> values)
        {
            if (!values.Any()) return 0;
            
            var avg = values.Average();
            var sumOfSquares = values.Sum(v => Math.Pow(v - avg, 2));
            return Math.Sqrt(sumOfSquares / values.Count);
        }

        private async Task<double> CalculateMaxDrawdownAsync(List<Trade> trades)
        {
            if (!trades.Any()) return 0;
            
            var orderedTrades = trades.OrderBy(t => t.EntryDate).ToList();
            var cumulative = 0m;
            var peak = 0m;
            var maxDrawdown = 0m;
            
            foreach (var trade in orderedTrades)
            {
                cumulative += trade.ProfitLoss ?? 0;
                
                if (cumulative > peak)
                    peak = cumulative;
                
                var drawdown = peak - cumulative;
                if (drawdown > maxDrawdown)
                    maxDrawdown = drawdown;
            }
            
            return (double)maxDrawdown;
        }

        private double CalculateRecoveryFactor(List<Trade> trades)
        {
            var totalProfit = trades.Sum(t => t.ProfitLoss ?? 0);
            var maxDrawdown = CalculateMaxDrawdownAsync(trades).Result;
            
            return maxDrawdown == 0 ? 0 : (double)(totalProfit / (decimal)maxDrawdown);
        }

        private double CalculateAverageRiskReward(List<Trade> trades)
        {
            var validTrades = trades.Where(t => 
                t.StopLoss.HasValue && 
                t.TakeProfit.HasValue && 
                t.EntryPrice != 0
            );
            
            if (!validTrades.Any()) return 0;
            
            return validTrades.Average(t =>
            {
                var risk = Math.Abs((double)(t.EntryPrice - t.StopLoss!.Value));
                var reward = Math.Abs((double)(t.TakeProfit!.Value - t.EntryPrice));
                return risk == 0 ? 0 : reward / risk;
            });
        }

        private int CalculateMaxConsecutiveLosses(List<Trade> trades)
        {
            var orderedTrades = trades.OrderBy(t => t.EntryDate).ToList();
            var maxConsecutive = 0;
            var currentConsecutive = 0;
            
            foreach (var trade in orderedTrades)
            {
                if (trade.ProfitLoss < 0)
                {
                    currentConsecutive++;
                    maxConsecutive = Math.Max(maxConsecutive, currentConsecutive);
                }
                else
                {
                    currentConsecutive = 0;
                }
            }
            
            return maxConsecutive;
        }

        private async Task<decimal> CalculateMaxLossInDayAsync(List<Trade> trades)
        {
            var dailyLosses = trades
                .GroupBy(t => t.EntryDate.Date)
                .Select(g => g.Sum(t => t.ProfitLoss ?? 0))
                .Where(loss => loss < 0);
            
            return dailyLosses.Any() ? dailyLosses.Min() : 0;
        }

        private double CalculateRiskAdjustedReturn(List<Trade> trades)
        {
            var avgReturn = trades.Average(t => t.ProfitLoss ?? 0);
            var avgRisk = trades.Average(t => t.RiskAmount ?? 0);
            
            return avgRisk == 0 ? 0 : (double)(avgReturn / avgRisk);
        }

        private double CalculateValueAtRisk(List<Trade> trades, double confidenceLevel)
        {
            if (!trades.Any()) return 0;
            
            var losses = trades
                .Select(t => (double)(t.ProfitLoss ?? 0))
                .Where(p => p < 0)
                .OrderBy(l => l)
                .ToList();
            
            if (!losses.Any()) return 0;
            
            var index = (int)Math.Floor(losses.Count * (1 - confidenceLevel));
            return Math.Abs(losses[Math.Min(index, losses.Count - 1)]);
        }

        private double CalculateConditionalValueAtRisk(List<Trade> trades, double confidenceLevel)
        {
            var var95 = CalculateValueAtRisk(trades, confidenceLevel);
            
            var losses = trades
                .Select(t => (double)(t.ProfitLoss ?? 0))
                .Where(p => p < -var95)
                .ToList();
            
            return losses.Any() ? Math.Abs(losses.Average()) : var95;
        }

        private string GetTradingSession(DateTime dateTime)
        {
            var hour = dateTime.Hour;
            
            if (hour >= 0 && hour < 8)
                return "Asian";
            else if (hour >= 8 && hour < 16)
                return "European";
            else
                return "American";
        }

        private void GeneratePerformanceInsights(AnalysisResult result, List<Trade> trades)
        {
            var winRate = (double)result.Metrics["WinRate"];
            
            if (winRate < 40)
            {
                result.Insights.Add(new Insight
                {
                    Title = "نرخ برد پایین",
                    Description = $"نرخ برد شما {winRate:F2}% است که کمتر از حد مطلوب است",
                    Category = "Performance",
                    Severity = "Warning",
                    ConfidenceLevel = 0.9
                });
            }
            else if (winRate > 60)
            {
                result.Insights.Add(new Insight
                {
                    Title = "نرخ برد عالی",
                    Description = $"نرخ برد شما {winRate:F2}% است که عملکرد خوبی را نشان می‌دهد",
                    Category = "Performance",
                    Severity = "Info",
                    ConfidenceLevel = 0.9
                });
            }
        }

        private void GeneratePerformanceRecommendations(AnalysisResult result, List<Trade> trades)
        {
            var profitFactor = (double)result.Metrics["ProfitFactor"];
            
            if (profitFactor < 1.5)
            {
                result.Recommendations.Add(new Recommendation
                {
                    Title = "بهبود نسبت سود به زیان",
                    Description = "سعی کنید نسبت ریسک به ریوارد را در معاملات خود افزایش دهید",
                    ActionType = "ImproveProfitFactor",
                    Priority = 1
                });
            }
        }

        private void GenerateRiskInsights(AnalysisResult result, List<Trade> trades)
        {
            var avgRisk = (double)result.Metrics["AverageRiskPercent"];
            
            if (avgRisk > 3)
            {
                result.Insights.Add(new Insight
                {
                    Title = "ریسک بالا",
                    Description = $"میانگین ریسک شما {avgRisk:F2}% است که بالاتر از حد توصیه شده است",
                    Category = "Risk",
                    Severity = "Warning",
                    ConfidenceLevel = 0.85
                });
            }
        }

        private void GenerateRiskRecommendations(AnalysisResult result, List<Trade> trades)
        {
            var maxConsecutiveLosses = (int)result.Metrics["MaxConsecutiveLosses"];
            
            if (maxConsecutiveLosses > 5)
            {
                result.Recommendations.Add(new Recommendation
                {
                    Title = "مدیریت ضررهای متوالی",
                    Description = "پس از 3 ضرر متوالی، استراحت کنید یا حجم معاملات را کاهش دهید",
                    ActionType = "ManageConsecutiveLosses",
                    Priority = 1
                });
            }
        }

        private void GeneratePsychologyInsights(AnalysisResult result, List<Trade> trades)
        {
            if (result.Metrics.ContainsKey("Emotion_Fearful_WinRate"))
            {
                var fearfulWinRate = (double)result.Metrics["Emotion_Fearful_WinRate"];
                
                if (fearfulWinRate < 30)
                {
                    result.Insights.Add(new Insight
                    {
                        Title = "تأثیر منفی ترس",
                        Description = "معاملاتی که با ترس انجام می‌دهید نتایج ضعیفی دارند",
                        Category = "Psychology",
                        Severity = "Warning",
                        ConfidenceLevel = 0.8
                    });
                }
            }
        }

        private ChartData GenerateProfitOverTimeChart(List<Trade> trades)
        {
            var orderedTrades = trades.OrderBy(t => t.EntryDate).ToList();
            var cumulative = 0m;
            
            return new ChartData
            {
                ChartType = "line",
                DataPoints = orderedTrades.Select(t =>
                {
                    cumulative += t.ProfitLoss ?? 0;
                    return new DataPoint
                    {
                        Date = t.EntryDate,
                        Value = (double)cumulative,
                        Label = t.EntryDate.ToString("yyyy-MM-dd")
                    };
                }).ToList()
            };
        }

        private ChartData GenerateWinRateByMonthChart(List<Trade> trades)
        {
            var monthlyData = trades
                .GroupBy(t => new { t.EntryDate.Year, t.EntryDate.Month })
                .Select(g => new DataPoint
                {
                    Label = $"{g.Key.Year}-{g.Key.Month:D2}",
                    Value = CalculateWinRate(g.ToList())
                })
                .OrderBy(d => d.Label)
                .ToList();

            return new ChartData
            {
                ChartType = "bar",
                DataPoints = monthlyData
            };
        }

        private ChartData GenerateProfitDistributionChart(List<Trade> trades)
        {
            var distribution = new List<DataPoint>();
            var ranges = new[] { -1000, -500, -100, 0, 100, 500, 1000, double.MaxValue };
            
            for (int i = 0; i < ranges.Length - 1; i++)
            {
                var min = ranges[i];
                var max = ranges[i + 1];
                var count = trades.Count(t => 
                    t.ProfitLoss >= (decimal)min && 
                    t.ProfitLoss < (decimal)max
                );
                
                distribution.Add(new DataPoint
                {
                    Label = max == double.MaxValue ? $">{min}" : $"{min} to {max}",
                    Value = count
                });
            }

            return new ChartData
            {
                ChartType = "bar",
                DataPoints = distribution
            };
        }

        private ChartData GenerateRiskDistributionChart(List<Trade> trades)
        {
            var riskData = trades
                .Where(t => t.RiskPercent.HasValue)
                .GroupBy(t => Math.Floor(t.RiskPercent!.Value))
                .Select(g => new DataPoint
                {
                    Label = $"{g.Key}%",
                    Value = g.Count()
                })
                .OrderBy(d => d.Label)
                .ToList();

            return new ChartData
            {
                ChartType = "bar",
                DataPoints = riskData
            };
        }

        private async Task<ChartData> GenerateDrawdownChartAsync(List<Trade> trades)
        {
            var orderedTrades = trades.OrderBy(t => t.EntryDate).ToList();
            var cumulative = 0m;
            var peak = 0m;
            
            var drawdownPoints = new List<DataPoint>();
            
            foreach (var trade in orderedTrades)
            {
                cumulative += trade.ProfitLoss ?? 0;
                
                if (cumulative > peak)
                    peak = cumulative;
                
                var drawdown = peak > 0 ? (peak - cumulative) / peak * 100 : 0;
                
                drawdownPoints.Add(new DataPoint
                {
                    Date = trade.EntryDate,
                    Value = (double)drawdown,
                    Label = trade.EntryDate.ToString("yyyy-MM-dd")
                });
            }

            return new ChartData
            {
                ChartType = "area",
                DataPoints = drawdownPoints
            };
        }

        public async Task<List<Trade>> GetBestTradesAsync(int count = 10)
        {
            return await _dbContext.Trades
                .Where(t => t.Status == TradeStatus.Closed)
                .OrderByDescending(t => t.ProfitLoss)
                .Take(count)
                .ToListAsync();
        }

        public async Task<List<Trade>> GetWorstTradesAsync(int count = 10)
        {
            return await _dbContext.Trades
                .Where(t => t.Status == TradeStatus.Closed)
                .OrderBy(t => t.ProfitLoss)
                .Take(count)
                .ToListAsync();
        }

        public async Task<Dictionary<string, double>> GetKeyMetricsAsync()
        {
            var metrics = new Dictionary<string, double>();
            
            var trades = await _dbContext.Trades
                .Where(t => t.Status == TradeStatus.Closed)
                .ToListAsync();

            if (trades.Any())
            {
                metrics["TotalTrades"] = trades.Count;
                metrics["WinRate"] = CalculateWinRate(trades);
                metrics["ProfitFactor"] = CalculateProfitFactor(trades);
                metrics["TotalProfit"] = (double)trades.Sum(t => t.ProfitLoss ?? 0);
                metrics["AverageProfit"] = (double)trades.Average(t => t.ProfitLoss ?? 0);
                metrics["SharpeRatio"] = CalculateSharpeRatio(trades);
            }

            return metrics;
        }
    }
}

// پایان فایل: Core/AnalysisEngine/AnalysisEngine.cs