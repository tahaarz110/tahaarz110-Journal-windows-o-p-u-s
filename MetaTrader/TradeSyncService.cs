// 📁 MetaTrader/TradeSyncService.cs
// ===== شروع کد =====

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TradingJournal.Data;
using TradingJournal.Data.Models;

namespace TradingJournal.MetaTrader
{
    public class TradeSyncService
    {
        private readonly AppDbContext _context;
        private readonly MetaTraderService _mtService;
        
        public TradeSyncService(AppDbContext context)
        {
            _context = context;
            _mtService = new MetaTraderService();
            _mtService.TradeReceived += OnTradeReceived;
        }
        
        public async Task StartAsync()
        {
            await _mtService.StartAsync();
        }
        
        public async Task StopAsync()
        {
            await _mtService.StopAsync();
        }
        
        private async void OnTradeReceived(object sender, TradeReceivedEventArgs e)
        {
            try
            {
                await UpsertTrade(e.Trade);
            }
            catch (Exception ex)
            {
                // Log error
                Console.WriteLine($"Error syncing trade: {ex.Message}");
            }
        }
        
        private async Task UpsertTrade(MetaTraderTrade mtTrade)
        {
            // جستجو بر اساس Ticket
            var existingTrade = await _context.Trades
                .FirstOrDefaultAsync(t => t.MetaTraderTicket == mtTrade.Ticket);
            
            if (existingTrade != null)
            {
                // به‌روزرسانی معامله موجود
                UpdateTradeFromMT(existingTrade, mtTrade);
                _context.Trades.Update(existingTrade);
            }
            else
            {
                // ایجاد معامله جدید
                var newTrade = CreateTradeFromMT(mtTrade);
                _context.Trades.Add(newTrade);
            }
            
            await _context.SaveChangesAsync();
        }
        
        private Trade CreateTradeFromMT(MetaTraderTrade mtTrade)
        {
            return new Trade
            {
                Id = Guid.NewGuid(),
                MetaTraderTicket = mtTrade.Ticket,
                Symbol = mtTrade.Symbol,
                TradeType = mtTrade.Type,
                EntryTime = mtTrade.OpenTime,
                EntryPrice = mtTrade.OpenPrice,
                Volume = mtTrade.Volume,
                StopLoss = mtTrade.StopLoss,
                TakeProfit = mtTrade.TakeProfit,
                Commission = mtTrade.Commission,
                Swap = mtTrade.Swap,
                Profit = mtTrade.Profit,
                Comments = mtTrade.Comment,
                ExitTime = mtTrade.CloseTime,
                ExitPrice = mtTrade.ClosePrice,
                Status = mtTrade.Status == "CLOSED" ? TradeStatus.Closed : TradeStatus.Open,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                Source = "MetaTrader",
                
                // محاسبات خودکار
                RiskRewardRatio = CalculateRiskReward(mtTrade),
                NetProfit = mtTrade.Profit - mtTrade.Commission - mtTrade.Swap,
                ReturnPercentage = CalculateReturnPercentage(mtTrade)
            };
        }
        
        private void UpdateTradeFromMT(Trade trade, MetaTraderTrade mtTrade)
        {
            trade.Symbol = mtTrade.Symbol;
            trade.TradeType = mtTrade.Type;
            trade.EntryTime = mtTrade.OpenTime;
            trade.EntryPrice = mtTrade.OpenPrice;
            trade.Volume = mtTrade.Volume;
            trade.StopLoss = mtTrade.StopLoss;
            trade.TakeProfit = mtTrade.TakeProfit;
            trade.Commission = mtTrade.Commission;
            trade.Swap = mtTrade.Swap;
            trade.Profit = mtTrade.Profit;
            trade.ExitTime = mtTrade.CloseTime;
            trade.ExitPrice = mtTrade.ClosePrice;
            trade.Status = mtTrade.Status == "CLOSED" ? TradeStatus.Closed : TradeStatus.Open;
            trade.UpdatedAt = DateTime.Now;
            
            // محاسبات خودکار
            trade.RiskRewardRatio = CalculateRiskReward(mtTrade);
            trade.NetProfit = mtTrade.Profit - mtTrade.Commission - mtTrade.Swap;
            trade.ReturnPercentage = CalculateReturnPercentage(mtTrade);
        }
        
        private decimal CalculateRiskReward(MetaTraderTrade trade)
        {
            if (trade.StopLoss == 0 || trade.TakeProfit == 0)
                return 0;
            
            var risk = Math.Abs(trade.OpenPrice - trade.StopLoss);
            var reward = Math.Abs(trade.TakeProfit - trade.OpenPrice);
            
            return risk > 0 ? reward / risk : 0;
        }
        
        private decimal CalculateReturnPercentage(MetaTraderTrade trade)
        {
            if (trade.OpenPrice == 0)
                return 0;
            
            var pips = trade.Type == "BUY" 
                ? (trade.ClosePrice ?? trade.OpenPrice) - trade.OpenPrice
                : trade.OpenPrice - (trade.ClosePrice ?? trade.OpenPrice);
            
            return (pips / trade.OpenPrice) * 100;
        }
    }
}

// ===== پایان کد =====