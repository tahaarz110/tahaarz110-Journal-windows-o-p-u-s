// مسیر فایل: Core/AnalysisEngine/Models/AnalysisModels.cs
// ابتدای کد
using System;
using System.Collections.Generic;
using TradingJournal.Data.Models;

namespace TradingJournal.Core.AnalysisEngine.Models
{
    // مدل‌های تحلیل روزانه
    public class DayOfWeekAnalysis
    {
        public Dictionary<DayOfWeek, DayStats> DayStatistics { get; set; }
        public List<AnalysisRecommendation> Recommendations { get; set; }
        public DateTime AnalysisDate { get; set; }

        public DayOfWeekAnalysis()
        {
            DayStatistics = new Dictionary<DayOfWeek, DayStats>();
            Recommendations = new List<AnalysisRecommendation>();
            AnalysisDate = DateTime.Now;
        }
    }

    public class DayStats
    {
        public DayOfWeek DayOfWeek { get; set; }
        public int TotalTrades { get; set; }
        public int WinningTrades { get; set; }
        public int LosingTrades { get; set; }
        public decimal TotalProfit { get; set; }
        public decimal AverageProfit { get; set; }
        public double WinRate { get; set; }
        public Trade BestTrade { get; set; }
        public Trade WorstTrade { get; set; }
    }

    // مدل‌های تحلیل ساعتی
    public class HourlyAnalysis
    {
        public Dictionary<int, HourStats> HourStatistics { get; set; }
        public List<AnalysisRecommendation> Recommendations { get; set; }
        public List<TimeRange> BestTradingHours { get; set; }

        public HourlyAnalysis()
        {
            HourStatistics = new Dictionary<int, HourStats>();
            Recommendations = new List<AnalysisRecommendation>();
            BestTradingHours = new List<TimeRange>();
        }
    }

    public class HourStats
    {
        public int Hour { get; set; }
        public int TotalTrades { get; set; }
        public int WinningTrades { get; set; }
        public decimal TotalProfit { get; set; }
        public decimal AverageProfit { get; set; }
        public double WinRate { get; set; }
    }

    public class TimeRange
    {
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public double AverageWinRate { get; set; }
    }

    // مدل‌های تحلیل ماهانه
    public class MonthlyAnalysis
    {
        public Dictionary<int, MonthStats> MonthStatistics { get; set; }
        public List<AnalysisRecommendation> Recommendations { get; set; }
        public List<SeasonalPattern> SeasonalPatterns { get; set; }

        public MonthlyAnalysis()
        {
            MonthStatistics = new Dictionary<int, MonthStats>();
            Recommendations = new List<AnalysisRecommendation>();
            SeasonalPatterns = new List<SeasonalPattern>();
        }
    }

    public class MonthStats
    {
        public int Month { get; set; }
        public int TotalTrades { get; set; }
        public int WinningTrades { get; set; }
        public decimal TotalProfit { get; set; }
        public decimal AverageProfit { get; set; }
        public double WinRate { get; set; }
        public decimal MaxDrawdown { get; set; }
    }

    public class SeasonalPattern
    {
        public string Season { get; set; }
        public List<int> Months { get; set; }
        public double AverageWinRate { get; set; }
        public string Description { get; set; }
    }

    // مدل‌های تحلیل استراتژی
    public class StrategyAnalysis
    {
        public Dictionary<string, StrategyStats> StrategyStatistics { get; set; }
        public List<AnalysisRecommendation> Recommendations { get; set; }
        public List<StrategyCorrelation> Correlations { get; set; }

        public StrategyAnalysis()
        {
            StrategyStatistics = new Dictionary<string, StrategyStats>();
            Recommendations = new List<AnalysisRecommendation>();
            Correlations = new List<StrategyCorrelation>();
        }
    }

    public class StrategyStats
    {
        public string StrategyName { get; set; }
        public int TotalTrades { get; set; }
        public int WinningTrades { get; set; }
        public decimal TotalProfit { get; set; }
        public decimal AverageProfit { get; set; }
        public double WinRate { get; set; }
        public decimal AverageRiskReward { get; set; }
        public int MaxConsecutiveWins { get; set; }
        public int MaxConsecutiveLosses { get; set; }
        public double ProfitFactor { get; set; }
        public double Sharpe { get; set; }
    }

    public class StrategyCorrelation
    {
        public string Strategy1 { get; set; }
        public string Strategy2 { get; set; }
        public double Correlation { get; set; }
    }

    // مدل‌های تحلیل نماد
    public class SymbolAnalysis
    {
        public Dictionary<string, SymbolStats> SymbolStatistics { get; set; }
        public List<AnalysisRecommendation> Recommendations { get; set; }
        public List<SymbolRanking> TopSymbols { get; set; }

        public SymbolAnalysis()
        {
            SymbolStatistics = new Dictionary<string, SymbolStats>();
            Recommendations = new List<AnalysisRecommendation>();
            TopSymbols = new List<SymbolRanking>();
        }
    }

    public class SymbolStats
    {
        public string Symbol { get; set; }
        public int TotalTrades { get; set; }
        public int WinningTrades { get; set; }
        public decimal TotalProfit { get; set; }
        public decimal AverageProfit { get; set; }
        public double WinRate { get; set; }
        public decimal TotalVolume { get; set; }
        public double AveragePips { get; set; }
        public int BestStreak { get; set; }
    }

    public class SymbolRanking
    {
        public string Symbol { get; set; }
        public int Rank { get; set; }
        public double Score { get; set; }
    }

    // مدل‌های تحلیل روانشناسی
    public class EmotionalAnalysis
    {
        public Dictionary<string, EmotionStats> EmotionStatistics { get; set; }
        public List<AnalysisRecommendation> Recommendations { get; set; }
        public EmotionalState CurrentState { get; set; }

        public EmotionalAnalysis()
        {
            EmotionStatistics = new Dictionary<string, EmotionStats>();
            Recommendations = new List<AnalysisRecommendation>();
        }
    }

    public class EmotionStats
    {
        public string Emotion { get; set; }
        public int TotalTrades { get; set; }
        public int WinningTrades { get; set; }
        public decimal TotalProfit { get; set; }
        public double WinRate { get; set; }
        public double AverageHoldTime { get; set; }
    }

    public class EmotionalState
    {
        public string State { get; set; }
        public double ConfidenceLevel { get; set; }
        public string Recommendation { get; set; }
    }

    // مدل‌های تحلیل Tilt
    public class TiltAnalysis
    {
        public int RevengeTradeCount { get; set; }
        public decimal RevengeTradeImpact { get; set; }
        public int OvertradingDays { get; set; }
        public List<TiltEvent> TiltEvents { get; set; }
        public List<AnalysisRecommendation> Recommendations { get; set; }

        public TiltAnalysis()
        {
            TiltEvents = new List<TiltEvent>();
            Recommendations = new List<AnalysisRecommendation>();
        }
    }

    public class TiltEvent
    {
        public DateTime Date { get; set; }
        public TiltType Type { get; set; }
        public decimal Impact { get; set; }
        public string Description { get; set; }
    }

    public enum TiltType
    {
        RevengeTrading,
        Overtrading,
        FearBased,
        GreedBased,
        FOMO
    }

    public class RevengePattern
    {
        public Trade TriggerTrade { get; set; }
        public Trade RevengeTrade { get; set; }
        public decimal Impact { get; set; }
    }

    // مدل‌های تحلیل ترکیبی
    public class CombinedAnalysis
    {
        public List<PerformanceCombo> HighPerformanceCombos { get; set; }
        public List<PerformanceCombo> LowPerformanceCombos { get; set; }
        public List<AnalysisRecommendation> Recommendations { get; set; }

        public CombinedAnalysis()
        {
            HighPerformanceCombos = new List<PerformanceCombo>();
            LowPerformanceCombos = new List<PerformanceCombo>();
            Recommendations = new List<AnalysisRecommendation>();
        }
    }

    public class PerformanceCombo
    {
        public string Type { get; set; }
        public string Description { get; set; }
        public double WinRate { get; set; }
        public int TradeCount { get; set; }
        public decimal TotalProfit { get; set; }
    }

    // مدل پیشنهاد
    public class AnalysisRecommendation
    {
        public RecommendationType Type { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public Priority Priority { get; set; }
        public double ConfidenceLevel { get; set; }
        public DateTime CreatedDate { get; set; }
        public List<string> ActionItems { get; set; }

        public AnalysisRecommendation()
        {
            CreatedDate = DateTime.Now;
            ActionItems = new List<string>();
        }
    }

    public enum RecommendationType
    {
        BestPractice,
        Warning,
        Insight,
        Psychology,
        RiskManagement,
        Opportunity
    }

    public enum Priority
    {
        Low,
        Medium,
        High,
        Critical
    }
}
// پایان کد