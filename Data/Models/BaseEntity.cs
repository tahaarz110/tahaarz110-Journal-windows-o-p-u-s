// ابتدای فایل: Data/Models/BaseEntity.cs - بخش 1
// مسیر: /Data/Models/BaseEntity.cs

using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace TradingJournal.Data.Models
{
    public abstract class BaseEntity
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
    }

    public class Trade : BaseEntity
    {
        public string Symbol { get; set; } = string.Empty;
        public string? Platform { get; set; }
        public DateTime EntryDate { get; set; }
        public DateTime? ExitDate { get; set; }
        public decimal EntryPrice { get; set; }
        public decimal? ExitPrice { get; set; }
        public decimal Volume { get; set; }
        public TradeDirection Direction { get; set; }
        public TradeStatus Status { get; set; } = TradeStatus.Open;
        public decimal? StopLoss { get; set; }
        public decimal? TakeProfit { get; set; }
        public decimal? Commission { get; set; }
        public decimal? Swap { get; set; }
        public decimal? ProfitLoss { get; set; }
        public decimal? ProfitLossPercent { get; set; }
        public decimal? RiskRewardRatio { get; set; }
        
        // استراتژی و تحلیل
        public string? Strategy { get; set; }
        public string? Setup { get; set; }
        public string? Timeframe { get; set; }
        public string? EntryReason { get; set; }
        public string? ExitReason { get; set; }
        
        // احساسات و روانشناسی
        public EmotionalState? EntryEmotion { get; set; }
        public EmotionalState? ExitEmotion { get; set; }
        public int? ConfidenceLevel { get; set; } // 1-10
        public string? PsychologyNotes { get; set; }
        
        // تحلیل فنی
        public string? TechnicalIndicators { get; set; }
        public string? ChartPattern { get; set; }
        public string? SupportResistance { get; set; }
        
        // مدیریت ریسک
        public decimal? RiskAmount { get; set; }
        public decimal? RiskPercent { get; set; }
        public decimal? AccountBalance { get; set; }
        
        // یادداشت‌ها و تصاویر
        public string? Notes { get; set; }
        public string? Lessons { get; set; }
        public string? Mistakes { get; set; }
        
        // تصاویر
        public virtual ICollection<TradeImage> Images { get; set; } = new List<TradeImage>();
        
        // فیلدهای پویا (JSON)
        public string? CustomFields { get; set; }
        
        // متاتریدر
        public string? MetaTraderTicket { get; set; }
        public int? MagicNumber { get; set; }
    }

    public class TradeImage : BaseEntity
    {
        public Guid TradeId { get; set; }
        public virtual Trade Trade { get; set; } = null!;
        public string ImagePath { get; set; } = string.Empty;
        public string? ThumbnailPath { get; set; }
        public string? Caption { get; set; }
        public ImageType ImageType { get; set; }
        public long FileSize { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
    }

// ابتدای بخش 2 فایل: Data/Models/BaseEntity.cs
// ادامه از بخش 1

    public class DynamicField : BaseEntity
    {
        public string FieldName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string DisplayNameFa { get; set; } = string.Empty;
        public FieldType FieldType { get; set; }
        public string? DefaultValue { get; set; }
        public bool IsRequired { get; set; }
        public bool IsVisible { get; set; } = true;
        public bool IsEditable { get; set; } = true;
        public int OrderIndex { get; set; }
        public string? GroupName { get; set; }
        public string? ValidationRules { get; set; } // JSON
        public string? Options { get; set; } // JSON for dropdown/radio options
        public string? Formula { get; set; } // for calculated fields
        public string? Metadata { get; set; } // Additional JSON metadata
    }

    public class TabConfiguration : BaseEntity
    {
        public string TabKey { get; set; } = string.Empty;
        public string TabName { get; set; } = string.Empty;
        public string TabNameFa { get; set; } = string.Empty;
        public string IconName { get; set; } = string.Empty;
        public TabType TabType { get; set; }
        public int OrderIndex { get; set; }
        public bool IsVisible { get; set; } = true;
        public bool IsEnabled { get; set; } = true;
        public bool IsCloseable { get; set; } = true;
        public string? Permissions { get; set; } // JSON
        public string? Configuration { get; set; } // JSON for tab-specific config
    }

    public class WidgetConfiguration : BaseEntity
    {
        public string WidgetKey { get; set; } = string.Empty;
        public string WidgetName { get; set; } = string.Empty;
        public string WidgetNameFa { get; set; } = string.Empty;
        public WidgetType WidgetType { get; set; }
        public string? TabKey { get; set; }
        public int Row { get; set; }
        public int Column { get; set; }
        public int RowSpan { get; set; } = 1;
        public int ColumnSpan { get; set; } = 1;
        public string? DataSource { get; set; } // Query or data source
        public string? Configuration { get; set; } // JSON widget config
        public bool IsVisible { get; set; } = true;
        public string? RefreshInterval { get; set; }
    }

    // Enums
    public enum TradeDirection
    {
        Buy,
        Sell
    }

    public enum TradeStatus
    {
        Open,
        Closed,
        Pending,
        Cancelled
    }

    public enum EmotionalState
    {
        Confident,
        Anxious,
        Fearful,
        Greedy,
        Neutral,
        Excited,
        Frustrated,
        Disciplined
    }

    public enum ImageType
    {
        EntryChart,
        ExitChart,
        Analysis,
        Setup,
        Result,
        Other
    }

    public enum FieldType
    {
        Text,
        Number,
        Decimal,
        Date,
        DateTime,
        Boolean,
        Dropdown,
        Radio,
        Checkbox,
        TextArea,
        Image,
        File,
        Formula,
        Reference
    }

    public enum TabType
    {
        TradeList,
        TradeForm,
        Dashboard,
        Report,
        Analysis,
        Settings,
        Plugin,
        Custom
    }

    public enum WidgetType
    {
        Table,
        Chart,
        Card,
        KPI,
        Form,
        Filter,
        Custom
    }
}

// پایان فایل: Data/Models/BaseEntity.cs