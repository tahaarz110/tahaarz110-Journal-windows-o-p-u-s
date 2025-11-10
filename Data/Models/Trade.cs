// مسیر فایل: Data/Models/Trade.cs
// ابتدای کد
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TradingJournal.Data.Models
{
    public class Trade
    {
        public int Id { get; set; }
        
        [Display(Name = "نماد")]
        public string Symbol { get; set; }
        
        [Display(Name = "نوع معامله")]
        public TradeType Type { get; set; }
        
        [Display(Name = "تاریخ ورود")]
        public DateTime EntryDate { get; set; }
        
        [Display(Name = "قیمت ورود")]
        public decimal EntryPrice { get; set; }
        
        [Display(Name = "حجم")]
        public decimal Volume { get; set; }
        
        [Display(Name = "تاریخ خروج")]
        public DateTime? ExitDate { get; set; }
        
        [Display(Name = "قیمت خروج")]
        public decimal? ExitPrice { get; set; }
        
        [Display(Name = "حد ضرر")]
        public decimal? StopLoss { get; set; }
        
        [Display(Name = "حد سود")]
        public decimal? TakeProfit { get; set; }
        
        [Display(Name = "سود/زیان")]
        public decimal? Profit { get; set; }
        
        [Display(Name = "سود/زیان (پیپ)")]
        public int? ProfitPips { get; set; }
        
        [Display(Name = "کمیسیون")]
        public decimal? Commission { get; set; }
        
        [Display(Name = "سوآپ")]
        public decimal? Swap { get; set; }
        
        [Display(Name = "استراتژی")]
        public string Strategy { get; set; }
        
        [Display(Name = "تایم‌فریم")]
        public string Timeframe { get; set; }
        
        [Display(Name = "احساسات")]
        public string Emotions { get; set; }
        
        [Display(Name = "یادداشت‌ها")]
        public string Notes { get; set; }
        
        [Display(Name = "برچسب‌ها")]
        public string Tags { get; set; }
        
        [Display(Name = "امتیاز")]
        public int? Rating { get; set; }
        
        [Display(Name = "ریسک به ریوارد")]
        public decimal? RiskRewardRatio { get; set; }
        
        [Display(Name = "درصد ریسک")]
        public decimal? RiskPercentage { get; set; }
        
        [Display(Name = "شماره حساب")]
        public string AccountNumber { get; set; }
        
        [Display(Name = "پلتفرم")]
        public string Platform { get; set; }
        
        [Display(Name = "تصاویر")]
        public virtual ICollection<TradeImage> Images { get; set; }
        
        [Display(Name = "فیلدهای سفارشی")]
        public string CustomFields { get; set; } // JSON
        
        [Display(Name = "تاریخ ایجاد")]
        public DateTime CreatedAt { get; set; }
        
        [Display(Name = "تاریخ آخرین ویرایش")]
        public DateTime? UpdatedAt { get; set; }
        
        // محاسبات خودکار
        public decimal? NetProfit => Profit - (Commission ?? 0) - (Swap ?? 0);
        public bool IsWin => Profit > 0;
        public bool IsOpen => ExitDate == null;
        public TimeSpan? Duration => ExitDate?.Subtract(EntryDate);
    }

    public enum TradeType
    {
        [Display(Name = "خرید")]
        Buy,
        [Display(Name = "فروش")]
        Sell
    }

    public class TradeImage
    {
        public int Id { get; set; }
        public int TradeId { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public string Description { get; set; }
        public ImageType Type { get; set; }
        public DateTime UploadedAt { get; set; }
        
        public virtual Trade Trade { get; set; }
    }

    public enum ImageType
    {
        [Display(Name = "چارت ورود")]
        EntryChart,
        [Display(Name = "چارت خروج")]
        ExitChart,
        [Display(Name = "تحلیل")]
        Analysis,
        [Display(Name = "سایر")]
        Other
    }
}
// پایان کد