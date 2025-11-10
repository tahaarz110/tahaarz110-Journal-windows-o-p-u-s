// مسیر فایل: Data/Repositories/TradeRepository.cs
// ابتدای کد
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TradingJournal.Data.Models;

namespace TradingJournal.Data.Repositories
{
    public interface ITradeRepository
    {
        Task<Trade> GetByIdAsync(int id);
        Task<IEnumerable<Trade>> GetAllAsync();
        Task<IEnumerable<Trade>> FindAsync(Expression<Func<Trade, bool>> predicate);
        Task<Trade> AddAsync(Trade trade);
        Task<Trade> UpdateAsync(Trade trade);
        Task<bool> DeleteAsync(int id);
        Task<int> CountAsync(Expression<Func<Trade, bool>> predicate = null);
        IQueryable<Trade> GetQueryable();
        Task<bool> ExistsAsync(int id);
        Task SaveChangesAsync();
    }

    public class TradeRepository : ITradeRepository
    {
        private readonly TradingJournalContext _context;

        public TradeRepository(TradingJournalContext context)
        {
            _context = context;
        }

        public async Task<Trade> GetByIdAsync(int id)
        {
            return await _context.Trades
                .Include(t => t.Images)
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<IEnumerable<Trade>> GetAllAsync()
        {
            return await _context.Trades
                .Include(t => t.Images)
                .OrderByDescending(t => t.EntryDate)
                .ToListAsync();
        }

        public async Task<IEnumerable<Trade>> FindAsync(Expression<Func<Trade, bool>> predicate)
        {
            return await _context.Trades
                .Include(t => t.Images)
                .Where(predicate)
                .OrderByDescending(t => t.EntryDate)
                .ToListAsync();
        }

        public async Task<Trade> AddAsync(Trade trade)
        {
            trade.CreatedAt = DateTime.Now;
            _context.Trades.Add(trade);
            await _context.SaveChangesAsync();
            return trade;
        }

        public async Task<Trade> UpdateAsync(Trade trade)
        {
            trade.UpdatedAt = DateTime.Now;
            _context.Entry(trade).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return trade;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var trade = await _context.Trades.FindAsync(id);
            if (trade == null)
                return false;

            _context.Trades.Remove(trade);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<int> CountAsync(Expression<Func<Trade, bool>> predicate = null)
        {
            if (predicate == null)
                return await _context.Trades.CountAsync();
            
            return await _context.Trades.CountAsync(predicate);
        }

        public IQueryable<Trade> GetQueryable()
        {
            return _context.Trades.Include(t => t.Images);
        }

        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.Trades.AnyAsync(t => t.Id == id);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        // متدهای تحلیلی
        public async Task<decimal> GetTotalProfitAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.Trades.Where(t => t.Profit.HasValue);
            
            if (startDate.HasValue)
                query = query.Where(t => t.EntryDate >= startDate.Value);
            
            if (endDate.HasValue)
                query = query.Where(t => t.EntryDate <= endDate.Value);

            return await query.SumAsync(t => t.Profit ?? 0);
        }

        public async Task<double> GetWinRateAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.Trades.Where(t => t.Profit.HasValue);
            
            if (startDate.HasValue)
                query = query.Where(t => t.EntryDate >= startDate.Value);
            
            if (endDate.HasValue)
                query = query.Where(t => t.EntryDate <= endDate.Value);

            var totalTrades = await query.CountAsync();
            if (totalTrades == 0) return 0;

            var winningTrades = await query.CountAsync(t => t.Profit > 0);
            return (double)winningTrades / totalTrades * 100;
        }

        public async Task<Dictionary<string, int>> GetTradesByStrategyAsync()
        {
            return await _context.Trades
                .Where(t => !string.IsNullOrEmpty(t.Strategy))
                .GroupBy(t => t.Strategy)
                .Select(g => new { Strategy = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Strategy, x => x.Count);
        }

        public async Task<Dictionary<string, decimal>> GetProfitBySymbolAsync()
        {
            return await _context.Trades
                .Where(t => t.Profit.HasValue)
                .GroupBy(t => t.Symbol)
                .Select(g => new { Symbol = g.Key, TotalProfit = g.Sum(t => t.Profit ?? 0) })
                .ToDictionaryAsync(x => x.Symbol, x => x.TotalProfit);
        }
    }
}
// پایان کد