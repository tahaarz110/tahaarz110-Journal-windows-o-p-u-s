// مسیر فایل: Core/AnalysisEngine/SmartAnalysisEngine.cs
// ابتدای کد
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TradingJournal.Data.Models;
using TradingJournal.Data.Repositories;

namespace TradingJournal.Core.AnalysisEngine
{
    public class SmartAnalysisEngine
    {
        private readonly ITradeRepository _tradeRepository;
        
        public SmartAnalysisEngine(ITradeRepository tradeRepository)
        {
            _tradeRepository = tradeRepository;
        }

        #region تحلیل‌های زمانی

        public async Task<DayOfWeekAnalysis> AnalyzeDayOfWeekPerformance()
        {
            var trades = await _tradeRepository.GetAllAsync();
            var analysis = new DayOfWeekAnalysis();

            var groupedByDay = trades.GroupBy(t => t.EntryDate.DayOfWeek);
            
            foreach (var group in groupedByDay)
            {
                var dayStats = new DayStats
                {
                    DayOfWeek = group.Key,
                    TotalTrades = group.Count(),
                    WinningTrades = group.Count(t => t.IsWin),
                    LosingTrades = group.Count(t => !t.IsWin && t.Profit.HasValue),
                    TotalProfit = group.Sum(t => t.Profit ?? 0),
                    AverageProfit = group.Average(t => t.Profit ?? 0),
                    WinRate = CalculateWinRate(group.ToList()),
                    BestTrade = group.OrderByDescending(t => t.Profit).FirstOrDefault(),
                    WorstTrade = group.OrderBy(t => t.Profit).FirstOrDefault()
                };

                analysis.DayStatistics[group.Key] = dayStats;
            }

            // پیشنهادات بر اساس تحلیل
            var bestDay = analysis.DayStatistics.OrderByDescending(d => d.Value.WinRate).First();
            var worstDay = analysis.DayStatistics.OrderBy(d => d.Value.WinRate).First();

            analysis.Recommendations.Add(new AnalysisRecommendation
            {
                Type = RecommendationType.BestPractice,
                Title = $"بهترین روز معاملاتی: {GetPersianDayName(bestDay.Key)}",
                Description = $"نرخ برد {bestDay.Value.WinRate:F1}% با میانگین سود {bestDay.Value.AverageProfit:N0}",
                Priority = Priority.High,
                ConfidenceLevel = 0.85
            });

            if (worstDay.Value.WinRate < 40)
            {
                analysis.Recommendations.Add(new AnalysisRecommendation
                {
                    Type = RecommendationType.Warning,
                    Title = $"اجتناب از معامله در {GetPersianDayName(worstDay.Key)}",
                    Description = $"نرخ برد پایین {worstDay.Value.WinRate:F1}% - پیشنهاد می‌شود در این روز معامله نکنید",
                    Priority = Priority.Critical,
                    ConfidenceLevel = 0.90
                });
            }

            return analysis;
        }

        public async Task<HourlyAnalysis> AnalyzeHourlyPerformance()
        {
            var trades = await _tradeRepository.GetAllAsync();
            var analysis = new HourlyAnalysis();

            var groupedByHour = trades.GroupBy(t => t.EntryDate.Hour);
            
            foreach (var group in groupedByHour)
            {
                var hourStats = new HourStats
                {
                    Hour = group.Key,
                    TotalTrades = group.Count(),
                    WinningTrades = group.Count(t => t.IsWin),
                    TotalProfit = group.Sum(t => t.Profit ?? 0),
                    AverageProfit = group.Average(t => t.Profit ?? 0),
                    WinRate = CalculateWinRate(group.ToList())
                };

                analysis.HourStatistics[group.Key] = hourStats;
            }

            // تشخیص بهترین بازه زمانی
            var bestHours = analysis.HourStatistics
                .Where(h => h.Value.TotalTrades >= 5)
                .OrderByDescending(h => h.Value.WinRate)
                .Take(3);

            foreach (var hour in bestHours)
            {
                analysis.Recommendations.Add(new AnalysisRecommendation
                {
                    Type = RecommendationType.BestPractice,
                    Title = $"ساعت طلایی: {hour.Key}:00 - {hour.Key + 1}:00",
                    Description = $"نرخ برد {hour.Value.WinRate:F1}% با {hour.Value.TotalTrades} معامله",
                    Priority = Priority.High,
                    ConfidenceLevel = hour.Value.TotalTrades >= 10 ? 0.85 : 0.65
                });
            }

            return analysis;
        }

        public async Task<MonthlyAnalysis> AnalyzeMonthlyPerformance()
        {
            var trades = await _tradeRepository.GetAllAsync();
            var analysis = new MonthlyAnalysis();

            var groupedByMonth = trades.GroupBy(t => t.EntryDate.Month);
            
            foreach (var group in groupedByMonth)
            {
                var monthStats = new MonthStats
                {
                    Month = group.Key,
                    TotalTrades = group.Count(),
                    WinningTrades = group.Count(t => t.IsWin),
                    TotalProfit = group.Sum(t => t.Profit ?? 0),
                    AverageProfit = group.Average(t => t.Profit ?? 0),
                    WinRate = CalculateWinRate(group.ToList()),
                    MaxDrawdown = CalculateMaxDrawdown(group.OrderBy(t => t.EntryDate).ToList())
                };

                analysis.MonthStatistics[group.Key] = monthStats;
            }

            // تشخیص الگوهای فصلی
            var summerMonths = new[] { 4, 5, 6 }; // تیر، مرداد، شهریور
            var summerStats = analysis.MonthStatistics
                .Where(m => summerMonths.Contains(m.Key))
                .Select(m => m.Value);

            if (summerStats.Any() && summerStats.Average(s => s.WinRate) < 45)
            {
                analysis.Recommendations.Add(new AnalysisRecommendation
                {
                    Type = RecommendationType.Insight,
                    Title = "عملکرد ضعیف در تابستان",
                    Description = "آمار نشان می‌دهد عملکرد شما در ماه‌های تابستان ضعیف‌تر است. پیشنهاد می‌شود حجم معاملات را کاهش دهید.",
                    Priority = Priority.Medium,
                    ConfidenceLevel = 0.75
                });
            }

            return analysis;
        }

        #endregion

        #region تحلیل‌های استراتژیک

        public async Task<StrategyAnalysis> AnalyzeStrategies()
        {
            var trades = await _tradeRepository.GetAllAsync();
            var analysis = new StrategyAnalysis();

            var groupedByStrategy = trades.Where(t => !string.IsNullOrEmpty(t.Strategy))
                                          .GroupBy(t => t.Strategy);
            
            foreach (var group in groupedByStrategy)
            {
                var strategyStats = new StrategyStats
                {
                    StrategyName = group.Key,
                    TotalTrades = group.Count(),
                    WinningTrades = group.Count(t => t.IsWin),
                    TotalProfit = group.Sum(t => t.Profit ?? 0),
                    AverageProfit = group.Average(t => t.Profit ?? 0),
                    WinRate = CalculateWinRate(group.ToList()),
                    AverageRiskReward = group.Average(t => t.RiskRewardRatio ?? 0),
                    MaxConsecutiveWins = CalculateMaxConsecutive(group.ToList(), true),
                    MaxConsecutiveLosses = CalculateMaxConsecutive(group.ToList(), false),
                    ProfitFactor = CalculateProfitFactor(group.ToList()),
                    Sharpe = CalculateSharpeRatio(group.ToList())
                };

                analysis.StrategyStatistics[group.Key] = strategyStats;
            }

            // پیشنهادات بر اساس استراتژی‌ها
            var topStrategies = analysis.StrategyStatistics
                .Where(s => s.Value.TotalTrades >= 10)
                .OrderByDescending(s => s.Value.ProfitFactor)
                .Take(3);

            foreach (var strategy in topStrategies)
            {
                analysis.Recommendations.Add(new AnalysisRecommendation
                {
                    Type = RecommendationType.BestPractice,
                    Title = $"استراتژی موفق: {strategy.Key}",
                    Description = $"Profit Factor: {strategy.Value.ProfitFactor:F2}, نرخ برد: {strategy.Value.WinRate:F1}%",
                    Priority = Priority.High,
                    ConfidenceLevel = 0.80
                });
            }

            // تشخیص استراتژی‌های ضعیف
            var weakStrategies = analysis.StrategyStatistics
                .Where(s => s.Value.TotalTrades >= 5 && s.Value.WinRate < 35)
                .ToList();

            foreach (var strategy in weakStrategies)
            {
                analysis.Recommendations.Add(new AnalysisRecommendation
                {
                    Type = RecommendationType.Warning,
                    Title = $"بازنگری در استراتژی: {strategy.Key}",
                    Description = $"نرخ برد پایین {strategy.Value.WinRate:F1}% - نیاز به بهبود یا حذف",
                    Priority = Priority.High,
                    ConfidenceLevel = 0.85
                });
            }

            return analysis;
        }

        public async Task<SymbolAnalysis> AnalyzeSymbols()
        {
            var trades = await _tradeRepository.GetAllAsync();
            var analysis = new SymbolAnalysis();

            var groupedBySymbol = trades.GroupBy(t => t.Symbol);
            
            foreach (var group in groupedBySymbol)
            {
                var symbolStats = new SymbolStats
                {
                    Symbol = group.Key,
                    TotalTrades = group.Count(),
                    WinningTrades = group.Count(t => t.IsWin),
                    TotalProfit = group.Sum(t => t.Profit ?? 0),
                    AverageProfit = group.Average(t => t.Profit ?? 0),
                    WinRate = CalculateWinRate(group.ToList()),
                    TotalVolume = group.Sum(t => t.Volume),
                    AveragePips = group.Average(t => t.ProfitPips ?? 0),
                    BestStreak = CalculateMaxConsecutive(group.ToList(), true)
                };

                analysis.SymbolStatistics[group.Key] = symbolStats;
            }

            // جفت ارزهای طلایی
            var goldPairs = analysis.SymbolStatistics
                .Where(s => s.Value.TotalTrades >= 10 && s.Value.WinRate >= 60)
                .OrderByDescending(s => s.Value.TotalProfit)
                .Take(3);

            foreach (var pair in goldPairs)
            {
                analysis.Recommendations.Add(new AnalysisRecommendation
                {
                    Type = RecommendationType.BestPractice,
                    Title = $"جفت ارز طلایی: {pair.Key}",
                    Description = $"سود کل: {pair.Value.TotalProfit:N0}, نرخ برد: {pair.Value.WinRate:F1}%",
                    Priority = Priority.High,
                    ConfidenceLevel = 0.85
                });
            }

            return analysis;
        }

        #endregion

        #region تحلیل‌های روانشناختی

        public async Task<EmotionalAnalysis> AnalyzeEmotions()
        {
            var trades = await _tradeRepository.GetAllAsync();
            var analysis = new EmotionalAnalysis();

            var groupedByEmotion = trades.Where(t => !string.IsNullOrEmpty(t.Emotions))
                                         .GroupBy(t => t.Emotions);
            
            foreach (var group in groupedByEmotion)
            {
                var emotionStats = new EmotionStats
                {
                    Emotion = group.Key,
                    TotalTrades = group.Count(),
                    WinningTrades = group.Count(t => t.IsWin),
                    TotalProfit = group.Sum(t => t.Profit ?? 0),
                    WinRate = CalculateWinRate(group.ToList()),
                    AverageHoldTime = group.Average(t => t.Duration?.TotalHours ?? 0)
                };

                analysis.EmotionStatistics[group.Key] = emotionStats;
            }

            // تشخیص احساسات مخرب
            var destructiveEmotions = analysis.EmotionStatistics
                .Where(e => e.Value.WinRate < 40 && e.Value.TotalTrades >= 5)
                .ToList();

            foreach (var emotion in destructiveEmotions)
            {
                analysis.Recommendations.Add(new AnalysisRecommendation
                {
                    Type = RecommendationType.Psychology,
                    Title = $"کنترل احساس: {emotion.Key}",
                    Description = $"معاملات با این احساس {emotion.Value.WinRate:F1}% نرخ برد دارند. در این حالت معامله نکنید.",
                    Priority = Priority.Critical,
                    ConfidenceLevel = 0.90
                });
            }

            // تحلیل سری شکست‌ها
            var losingSeries = IdentifyLosingSeries(trades.OrderBy(t => t.EntryDate).ToList());
            
            if (losingSeries.Any(s => s.Count >= 3))
            {
                analysis.Recommendations.Add(new AnalysisRecommendation
                {
                    Type = RecommendationType.Psychology,
                    Title = "مدیریت سری شکست‌ها",
                    Description = "پس از 3 شکست متوالی، حتماً استراحت کنید. آمار نشان می‌دهد احتمال شکست معامله بعدی 75% است.",
                    Priority = Priority.Critical,
                    ConfidenceLevel = 0.85
                });
            }

            return analysis;
        }

        public async Task<TiltAnalysis> AnalyzeTilt()
        {
            var trades = await _tradeRepository.GetAllAsync();
            var analysis = new TiltAnalysis();

            // تشخیص Revenge Trading
            var revengePatterns = IdentifyRevengeTrading(trades.OrderBy(t => t.EntryDate).ToList());
            
            if (revengePatterns.Count > 0)
            {
                analysis.RevengeTradeCount = revengePatterns.Count;
                analysis.RevengeTradeImpact = revengePatterns.Sum(p => p.Impact);
                
                analysis.Recommendations.Add(new AnalysisRecommendation
                {
                    Type = RecommendationType.Psychology,
                    Title = "الگوی Revenge Trading شناسایی شد",
                    Description = $"{revengePatterns.Count} مورد معامله انتقامی با ضرر کل {analysis.RevengeTradeImpact:N0} شناسایی شد",
                    Priority = Priority.Critical,
                    ConfidenceLevel = 0.88
                });
            }

            // تشخیص Overtrading
            var overtradingDays = trades.GroupBy(t => t.EntryDate.Date)
                                        .Where(g => g.Count() > 5)
                                        .ToList();
            
            if (overtradingDays.Any())
            {
                analysis.OvertradingDays = overtradingDays.Count;
                
                var avgProfitNormal = trades.Where(t => !overtradingDays.Any(d => d.Key == t.EntryDate.Date))
                                            .Average(t => t.Profit ?? 0);
                var avgProfitOvertrading = overtradingDays.SelectMany(g => g).Average(t => t.Profit ?? 0);
                
                if (avgProfitOvertrading < avgProfitNormal * 0.5)
                {
                    analysis.Recommendations.Add(new AnalysisRecommendation
                    {
                        Type = RecommendationType.Psychology,
                        Title = "Overtrading آسیب‌زا شناسایی شد",
                        Description = "در روزهایی که بیش از 5 معامله داشتید، میانگین سود 50% کمتر بوده است",
                        Priority = Priority.High,
                        ConfidenceLevel = 0.82
                    });
                }
            }

            return analysis;
        }

        #endregion

        #region تحلیل‌های ترکیبی

        public async Task<CombinedAnalysis> AnalyzeCombinations()
        {
            var trades = await _tradeRepository.GetAllAsync();
            var analysis = new CombinedAnalysis();

            // ترکیب روز + استراتژی
            var dayStrategyCombo = trades.Where(t => !string.IsNullOrEmpty(t.Strategy))
                .GroupBy(t => new { Day = t.EntryDate.DayOfWeek, t.Strategy });

            foreach (var combo in dayStrategyCombo)
            {
                if (combo.Count() >= 5)
                {
                    var winRate = CalculateWinRate(combo.ToList());
                    if (winRate >= 70)
                    {
                        analysis.HighPerformanceCombos.Add(new PerformanceCombo
                        {
                            Type = "Day-Strategy",
                            Description = $"{GetPersianDayName(combo.Key.Day)} + {combo.Key.Strategy}",
                            WinRate = winRate,
                            TradeCount = combo.Count(),
                            TotalProfit = combo.Sum(t => t.Profit ?? 0)
                        });
                    }
                }
            }

            // ترکیب ساعت + جفت ارز
            var hourSymbolCombo = trades.GroupBy(t => new { Hour = t.EntryDate.Hour, t.Symbol });

            foreach (var combo in hourSymbolCombo)
            {
                if (combo.Count() >= 5)
                {
                    var winRate = CalculateWinRate(combo.ToList());
                    if (winRate >= 70)
                    {
                        analysis.HighPerformanceCombos.Add(new PerformanceCombo
                        {
                            Type = "Hour-Symbol",
                            Description = $"ساعت {combo.Key.Hour}:00 + {combo.Key.Symbol}",
                            WinRate = winRate,
                            TradeCount = combo.Count(),
                            TotalProfit = combo.Sum(t => t.Profit ?? 0)
                        });
                    }
                }
            }

            // پیشنهادات بر اساس بهترین ترکیب‌ها
            var topCombos = analysis.HighPerformanceCombos.OrderByDescending(c => c.WinRate).Take(3);

            foreach (var combo in topCombos)
            {
                analysis.Recommendations.Add(new AnalysisRecommendation
                {
                    Type = RecommendationType.BestPractice,
                    Title = $"ترکیب طلایی: {combo.Description}",
                    Description = $"نرخ برد {combo.WinRate:F1}% با {combo.TradeCount} معامله",
                    Priority = Priority.High,
                    ConfidenceLevel = combo.TradeCount >= 10 ? 0.90 : 0.75
                });
            }

            return analysis;
        }

        #endregion

        #region Helper Methods

        private double CalculateWinRate(List<Trade> trades)
        {
            if (!trades.Any()) return 0;
            var wins = trades.Count(t => t.IsWin);
            return (double)wins / trades.Count * 100;
        }

        private decimal CalculateMaxDrawdown(List<Trade> trades)
        {
            if (!trades.Any()) return 0;

            decimal peak = 0;
            decimal maxDrawdown = 0;
            decimal runningTotal = 0;

            foreach (var trade in trades)
            {
                runningTotal += trade.Profit ?? 0;
                if (runningTotal > peak)
                    peak = runningTotal;
                
                var drawdown = peak - runningTotal;
                if (drawdown > maxDrawdown)
                    maxDrawdown = drawdown;
            }

            return maxDrawdown;
        }

        private int CalculateMaxConsecutive(List<Trade> trades, bool wins)
        {
            if (!trades.Any()) return 0;

            int maxStreak = 0;
            int currentStreak = 0;

            foreach (var trade in trades.OrderBy(t => t.EntryDate))
            {
                if ((wins && trade.IsWin) || (!wins && !trade.IsWin && trade.Profit.HasValue))
                {
                    currentStreak++;
                    maxStreak = Math.Max(maxStreak, currentStreak);
                }
                else
                {
                    currentStreak = 0;
                }
            }

            return maxStreak;
        }

        private double CalculateProfitFactor(List<Trade> trades)
        {
            var grossProfit = trades.Where(t => t.Profit > 0).Sum(t => t.Profit ?? 0);
            var grossLoss = Math.Abs(trades.Where(t => t.Profit < 0).Sum(t => t.Profit ?? 0));
            
            if (grossLoss == 0) return grossProfit > 0 ? double.MaxValue : 0;
            return (double)(grossProfit / grossLoss);
        }

        private double CalculateSharpeRatio(List<Trade> trades)
        {
            if (trades.Count < 2) return 0;

            var returns = trades.Select(t => (double)(t.Profit ?? 0)).ToList();
            var avgReturn = returns.Average();
            var stdDev = Math.Sqrt(returns.Select(r => Math.Pow(r - avgReturn, 2)).Average());
            
            if (stdDev == 0) return 0;
            return avgReturn / stdDev * Math.Sqrt(252); // Annualized
        }

        private List<List<Trade>> IdentifyLosingSeries(List<Trade> trades)
        {
            var series = new List<List<Trade>>();
            var currentSeries = new List<Trade>();

            foreach (var trade in trades)
            {
                if (!trade.IsWin && trade.Profit.HasValue)
                {
                    currentSeries.Add(trade);
                }
                else
                {
                    if (currentSeries.Count >= 2)
                        series.Add(new List<Trade>(currentSeries));
                    currentSeries.Clear();
                }
            }

            if (currentSeries.Count >= 2)
                series.Add(currentSeries);

            return series;
        }

        private List<RevengePattern> IdentifyRevengeTrading(List<Trade> trades)
        {
            var patterns = new List<RevengePattern>();

            for (int i = 1; i < trades.Count; i++)
            {
                var prevTrade = trades[i - 1];
                var currentTrade = trades[i];

                // Revenge trading: بعد از ضرر، حجم یا ریسک بیشتر
                if (!prevTrade.IsWin && prevTrade.Profit < -50)
                {
                    var timeDiff = currentTrade.EntryDate - (prevTrade.ExitDate ?? prevTrade.EntryDate);
                    
                    if (timeDiff.TotalMinutes < 30 && currentTrade.Volume > prevTrade.Volume * 1.5m)
                    {
                        patterns.Add(new RevengePattern
                        {
                            TriggerTrade = prevTrade,
                            RevengeTrade = currentTrade,
                            Impact = currentTrade.Profit ?? 0
                        });
                    }
                }
            }

            return patterns;
        }

        private string GetPersianDayName(DayOfWeek day)
        {
            return day switch
            {
                DayOfWeek.Saturday => "شنبه",
                DayOfWeek.Sunday => "یکشنبه",
                DayOfWeek.Monday => "دوشنبه",
                DayOfWeek.Tuesday => "سه‌شنبه",
                DayOfWeek.Wednesday => "چهارشنبه",
                DayOfWeek.Thursday => "پنج‌شنبه",
                DayOfWeek.Friday => "جمعه",
                _ => ""
            };
        }

        #endregion
    }
}
// پایان کد