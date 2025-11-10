// 📁 MetaTrader/MetaTraderService.cs
// ===== شروع کد =====

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;

namespace TradingJournal.MetaTrader
{
    public class MetaTraderService
    {
        private IHost _host;
        private readonly int _port;
        private readonly string _apiKey;
        private CancellationTokenSource _cancellationTokenSource;
        
        public event EventHandler<TradeReceivedEventArgs> TradeReceived;
        
        public MetaTraderService(int port = 5000, string apiKey = "your-api-key")
        {
            _port = port;
            _apiKey = apiKey;
        }
        
        public async Task StartAsync()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            
            _host = Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseUrls($"http://localhost:{_port}");
                    webBuilder.Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapPost("/api/trades", HandleTradeData);
                        });
                    });
                })
                .Build();
            
            await _host.StartAsync(_cancellationTokenSource.Token);
        }
        
        public async Task StopAsync()
        {
            if (_host != null)
            {
                await _host.StopAsync();
                _host.Dispose();
            }
            _cancellationTokenSource?.Cancel();
        }
        
        private async Task HandleTradeData(HttpContext context)
        {
            // بررسی API Key
            var apiKey = context.Request.Headers["X-API-Key"];
            if (apiKey != _apiKey)
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Unauthorized");
                return;
            }
            
            try
            {
                // خواندن داده JSON
                using var reader = new System.IO.StreamReader(context.Request.Body);
                var json = await reader.ReadToEndAsync();
                
                var trades = JsonConvert.DeserializeObject<List<MetaTraderTrade>>(json);
                
                // پردازش معاملات
                foreach (var trade in trades)
                {
                    TradeReceived?.Invoke(this, new TradeReceivedEventArgs { Trade = trade });
                }
                
                context.Response.StatusCode = 200;
                await context.Response.WriteAsync("Success");
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync($"Error: {ex.Message}");
            }
        }
    }
    
    public class MetaTraderTrade
    {
        public int Ticket { get; set; }
        public string Symbol { get; set; }
        public string Type { get; set; }
        public DateTime OpenTime { get; set; }
        public decimal OpenPrice { get; set; }
        public decimal Volume { get; set; }
        public decimal StopLoss { get; set; }
        public decimal TakeProfit { get; set; }
        public decimal Commission { get; set; }
        public decimal Swap { get; set; }
        public decimal Profit { get; set; }
        public string Comment { get; set; }
        public int MagicNumber { get; set; }
        public DateTime? CloseTime { get; set; }
        public decimal? ClosePrice { get; set; }
        public string Status { get; set; }
    }
    
    public class TradeReceivedEventArgs : EventArgs
    {
        public MetaTraderTrade Trade { get; set; }
    }
}

// ===== پایان کد =====