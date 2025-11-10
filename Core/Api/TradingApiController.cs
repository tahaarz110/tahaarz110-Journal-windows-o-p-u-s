// مسیر فایل: Core/Api/TradingApiController.cs
// ابتدای کد
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using TradingJournal.Core.Api.Models;
using TradingJournal.Data.Models;
using TradingJournal.Data.Repositories;

namespace TradingJournal.Core.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class TradesController : ControllerBase
    {
        private readonly ITradeRepository _tradeRepository;
        private readonly ILogger<TradesController> _logger;

        public TradesController(ITradeRepository tradeRepository, ILogger<TradesController> logger)
        {
            _tradeRepository = tradeRepository;
            _logger = logger;
        }

        [HttpGet("ping")]
        public IActionResult Ping()
        {
            return Ok(new { status = "alive", timestamp = DateTime.UtcNow });
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> UpsertTrade([FromBody] MetaTraderTradeDto tradeDto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                // Check if trade exists
                var existingTrade = await _tradeRepository.FindAsync(t => 
                    t.Platform == tradeDto.Platform && 
                    t.AccountNumber == tradeDto.AccountNumber &&
                    t.PlatformTicket == tradeDto.Ticket.ToString());

                Trade trade;
                
                if (existingTrade != null && existingTrade.Any())
                {
                    // Update existing trade
                    trade = existingTrade.First();
                    UpdateTradeFromDto(trade, tradeDto);
                    await _tradeRepository.UpdateAsync(trade);
                    _logger.LogInformation($"Trade updated: {trade.Id}");
                }
                else
                {
                    // Create new trade
                    trade = CreateTradeFromDto(tradeDto);
                    await _tradeRepository.AddAsync(trade);
                    _logger.LogInformation($"Trade created: {trade.Id}");
                }

                // Handle screenshot if provided
                if (!string.IsNullOrEmpty(tradeDto.Screenshot))
                {
                    await ProcessScreenshot(trade.Id, tradeDto.Screenshot);
                }

                return Ok(new { 
                    success = true, 
                    tradeId = trade.Id,
                    message = existingTrade != null ? "Trade updated" : "Trade created"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing trade");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpPost("batch")]
        [Authorize]
        public async Task<IActionResult> UpsertTrades([FromBody] List<MetaTraderTradeDto> trades)
        {
            try
            {
                var results = new List<object>();

                foreach (var tradeDto in trades)
                {
                    try
                    {
                        // Process each trade
                        var result = await UpsertTrade(tradeDto);
                        results.Add(new { ticket = tradeDto.Ticket, success = true });
                    }
                    catch (Exception ex)
                    {
                        results.Add(new { ticket = tradeDto.Ticket, success = false, error = ex.Message });
                    }
                }

                return Ok(new { results });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing batch trades");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        private Trade CreateTradeFromDto(MetaTraderTradeDto dto)
        {
            return new Trade
            {
                Symbol = dto.Symbol,
                Type = dto.Type.ToLower() == "buy" ? TradeType.Buy : TradeType.Sell,
                EntryDate = DateTime.Parse(dto.EntryTime),
                EntryPrice = dto.EntryPrice,
                Volume = dto.Volume,
                StopLoss = dto.StopLoss > 0 ? dto.StopLoss : null,
                TakeProfit = dto.TakeProfit > 0 ? dto.TakeProfit : null,
                Platform = dto.Platform,
                AccountNumber = dto.AccountNumber,
                PlatformTicket = dto.Ticket.ToString(),
                Commission = dto.Commission,
                Swap = dto.Swap,
                Notes = dto.Comment,
                CreatedAt = DateTime.Now
            };
        }

        private void UpdateTradeFromDto(Trade trade, MetaTraderTradeDto dto)
        {
            if (dto.IsClosed && !trade.ExitDate.HasValue)
            {
                trade.ExitDate = DateTime.Now;
                trade.ExitPrice = dto.CurrentPrice;
                trade.Profit = dto.Profit;
            }
            else if (!dto.IsClosed)
            {
                // Update open position
                trade.Profit = dto.Profit;
                trade.Swap = dto.Swap;
                trade.Commission = dto.Commission;
            }

            trade.UpdatedAt = DateTime.Now;
        }

        private async Task ProcessScreenshot(int tradeId, string screenshotPath)
        {
            // Process and save screenshot
            // This would integrate with ImageManager
            await Task.CompletedTask;
        }
    }

    public class MetaTraderTradeDto
    {
        public long Ticket { get; set; }
        public string Symbol { get; set; }
        public string Type { get; set; }
        public decimal Volume { get; set; }
        public decimal EntryPrice { get; set; }
        public string EntryTime { get; set; }
        public decimal StopLoss { get; set; }
        public decimal TakeProfit { get; set; }
        public decimal CurrentPrice { get; set; }
        public decimal Profit { get; set; }
        public decimal Swap { get; set; }
        public decimal Commission { get; set; }
        public int Magic { get; set; }
        public string Comment { get; set; }
        public string Platform { get; set; }
        public string AccountNumber { get; set; }
        public bool IsClosed { get; set; }
        public string Screenshot { get; set; }
    }

    // Add to Trade model
    public partial class Trade
    {
        public string PlatformTicket { get; set; }
    }
}
// پایان کد