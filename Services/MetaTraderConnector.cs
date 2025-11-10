// ابتدای فایل: Services/MetaTraderConnector.cs
// مسیر: /Services/MetaTraderConnector.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using TradingJournal.Data;
using TradingJournal.Data.Models;

namespace TradingJournal.Services
{
    public enum ConnectionStatus
    {
        Disconnected,
        Connecting,
        Connected,
        Error
    }

    public enum MessageType
    {
        Handshake,
        TradeOpen,
        TradeClose,
        TradeModify,
        AccountInfo,
        MarketData,
        History,
        Error,
        Heartbeat
    }

    public class MT4Trade
    {
        public int Ticket { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public int Type { get; set; } // 0=Buy, 1=Sell
        public double Lots { get; set; }
        public double OpenPrice { get; set; }
        public double ClosePrice { get; set; }
        public double StopLoss { get; set; }
        public double TakeProfit { get; set; }
        public DateTime OpenTime { get; set; }
        public DateTime? CloseTime { get; set; }
        public double Commission { get; set; }
        public double Swap { get; set; }
        public double Profit { get; set; }
        public string Comment { get; set; } = string.Empty;
        public int MagicNumber { get; set; }
    }

    public class AccountInfo
    {
        public int AccountNumber { get; set; }
        public string AccountName { get; set; } = string.Empty;
        public double Balance { get; set; }
        public double Equity { get; set; }
        public double Margin { get; set; }
        public double FreeMargin { get; set; }
        public int Leverage { get; set; }
        public string Currency { get; set; } = string.Empty;
    }

    public interface IMetaTraderConnector
    {
        ConnectionStatus Status { get; }
        event EventHandler<ConnectionStatus>? ConnectionStatusChanged;
        event EventHandler<MT4Trade>? TradeReceived;
        event EventHandler<AccountInfo>? AccountInfoReceived;
        
        Task<bool> ConnectAsync(string connectionType, string connectionString);
        Task DisconnectAsync();
        Task<List<MT4Trade>> GetOpenTradesAsync();
        Task<List<MT4Trade>> GetHistoryAsync(DateTime from, DateTime to);
        Task<AccountInfo?> GetAccountInfoAsync();
        Task<bool> SendCommandAsync(string command, JObject parameters);
    }

    public class MetaTraderConnector : IMetaTraderConnector, IDisposable
    {
        private ConnectionStatus _status = ConnectionStatus.Disconnected;
        private TcpListener? _tcpListener;
        private NamedPipeServerStream? _pipeServer;
        private Thread? _listenThread;
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly DatabaseContext _dbContext;
        private readonly object _lock = new object();
        private DateTime _lastHeartbeat = DateTime.Now;
        private System.Timers.Timer? _heartbeatTimer;

        public ConnectionStatus Status
        {
            get => _status;
            private set
            {
                if (_status != value)
                {
                    _status = value;
                    ConnectionStatusChanged?.Invoke(this, value);
                }
            }
        }

        public event EventHandler<ConnectionStatus>? ConnectionStatusChanged;
        public event EventHandler<MT4Trade>? TradeReceived;
        public event EventHandler<AccountInfo>? AccountInfoReceived;

        public MetaTraderConnector()
        {
            _dbContext = new DatabaseContext();
        }

        public async Task<bool> ConnectAsync(string connectionType, string connectionString)
        {
            try
            {
                Status = ConnectionStatus.Connecting;
                _cancellationTokenSource = new CancellationTokenSource();

                switch (connectionType.ToLower())
                {
                    case "tcp":
                        await StartTcpListenerAsync(connectionString);
                        break;
                    
                    case "pipe":
                        await StartNamedPipeAsync(connectionString);
                        break;
                    
                    case "file":
                        await StartFileWatcherAsync(connectionString);
                        break;
                    
                    default:
                        throw new NotSupportedException($"Connection type '{connectionType}' not supported");
                }

                // Start heartbeat monitoring
                StartHeartbeatMonitor();

                Status = ConnectionStatus.Connected;
                Log.Information($"متصل شد به MetaTrader از طریق {connectionType}");
                
                NotificationService.Instance.Show(
                    "اتصال MetaTrader",
                    "اتصال با موفقیت برقرار شد",
                    NotificationType.Success
                );

                return true;
            }
            catch (Exception ex)
            {
                Status = ConnectionStatus.Error;
                Log.Error(ex, "خطا در اتصال به MetaTrader");
                
                NotificationService.Instance.Show(
                    "خطای اتصال",
                    ex.Message,
                    NotificationType.Error
                );
                
                return false;
            }
        }

        private async Task StartTcpListenerAsync(string connectionString)
        {
            // Parse connection string (format: "127.0.0.1:12345")
            var parts = connectionString.Split(':');
            var ipAddress = IPAddress.Parse(parts[0]);
            var port = int.Parse(parts[1]);

            _tcpListener = new TcpListener(ipAddress, port);
            _tcpListener.Start();

            _listenThread = new Thread(async () =>
            {
                while (!_cancellationTokenSource!.Token.IsCancellationRequested)
                {
                    try
                    {
                        var tcpClient = await _tcpListener.AcceptTcpClientAsync();
                        _ = Task.Run(() => HandleTcpClientAsync(tcpClient, _cancellationTokenSource.Token));
                    }
                    catch (Exception ex)
                    {
                        if (!_cancellationTokenSource.Token.IsCancellationRequested)
                        {
                            Log.Error(ex, "خطا در پذیرش اتصال TCP");
                        }
                    }
                }
            })
            {
                IsBackground = true
            };
            _listenThread.Start();

            await Task.CompletedTask;
        }

        private async Task HandleTcpClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            try
            {
                using (client)
                {
                    var stream = client.GetStream();
                    var buffer = new byte[4096];

                    while (client.Connected && !cancellationToken.IsCancellationRequested)
                    {
                        var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                        
                        if (bytesRead > 0)
                        {
                            var message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                            await ProcessMessageAsync(message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "خطا در پردازش کلاینت TCP");
            }
        }

        private async Task StartNamedPipeAsync(string pipeName)
        {
            _listenThread = new Thread(async () =>
            {
                while (!_cancellationTokenSource!.Token.IsCancellationRequested)
                {
                    try
                    {
                        _pipeServer = new NamedPipeServerStream(
                            pipeName,
                            PipeDirection.InOut,
                            NamedPipeServerStream.MaxAllowedServerInstances,
                            PipeTransmissionMode.Message,
                            PipeOptions.Asynchronous
                        );

                        await _pipeServer.WaitForConnectionAsync(_cancellationTokenSource.Token);

                        using var reader = new StreamReader(_pipeServer);
                        while (_pipeServer.IsConnected && !_cancellationTokenSource.Token.IsCancellationRequested)
                        {
                            var message = await reader.ReadLineAsync();
                            if (!string.IsNullOrEmpty(message))
                            {
                                await ProcessMessageAsync(message);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (!_cancellationTokenSource.Token.IsCancellationRequested)
                        {
                            Log.Error(ex, "خطا در Named Pipe");
                        }
                    }
                }
            })
            {
                IsBackground = true
            };
            _listenThread.Start();

            await Task.CompletedTask;
        }

        private async Task StartFileWatcherAsync(string watchPath)
        {
            var watcher = new FileSystemWatcher
            {
                Path = watchPath,
                Filter = "*.json",
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
            };

            watcher.Created += async (sender, e) =>
            {
                await Task.Delay(100); // Wait for file to be written completely
                await ProcessFileAsync(e.FullPath);
            };

            watcher.Changed += async (sender, e) =>
            {
                await Task.Delay(100);
                await ProcessFileAsync(e.FullPath);
            };

            watcher.EnableRaisingEvents = true;

            await Task.CompletedTask;
        }

        private async Task ProcessFileAsync(string filePath)
        {
            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                await ProcessMessageAsync(json);
                
                // Delete processed file
                File.Delete(filePath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"خطا در پردازش فایل: {filePath}");
            }
        }

        private async Task ProcessMessageAsync(string message)
        {
            try
            {
                var messageObj = JObject.Parse(message);
                var messageType = messageObj["type"]?.ToString();
                
                _lastHeartbeat = DateTime.Now;

                switch (messageType?.ToLower())
                {
                    case "trade_open":
                    case "trade_close":
                    case "trade_modify":
                        await ProcessTradeMessageAsync(messageObj);
                        break;
                    
                    case "account_info":
                        await ProcessAccountInfoAsync(messageObj);
                        break;
                    
                    case "history":
                        await ProcessHistoryAsync(messageObj);
                        break;
                    
                    case "heartbeat":
                        // Just update last heartbeat time
                        break;
                    
                    case "error":
                        ProcessError(messageObj);
                        break;
                    
                    default:
                        Log.Warning($"Unknown message type: {messageType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "خطا در پردازش پیام");
            }
        }

        private async Task ProcessTradeMessageAsync(JObject message)
        {
            try
            {
                var trade = ParseTrade(message["trade"] as JObject);
                if (trade == null) return;

                // Map to internal Trade model
                var internalTrade = await GetOrCreateTradeAsync(trade.Ticket.ToString());
                
                internalTrade.Symbol = trade.Symbol;
                internalTrade.Direction = trade.Type == 0 ? TradeDirection.Buy : TradeDirection.Sell;
                internalTrade.Volume = (decimal)trade.Lots;
                internalTrade.EntryPrice = (decimal)trade.OpenPrice;
                internalTrade.EntryDate = trade.OpenTime;
                internalTrade.StopLoss = (decimal)trade.StopLoss;
                internalTrade.TakeProfit = (decimal)trade.TakeProfit;
                internalTrade.MetaTraderTicket = trade.Ticket.ToString();
                internalTrade.MagicNumber = trade.MagicNumber;
                
                if (trade.CloseTime.HasValue && trade.CloseTime.Value != DateTime.MinValue)
                {
                    internalTrade.ExitDate = trade.CloseTime;
                    internalTrade.ExitPrice = (decimal)trade.ClosePrice;
                    internalTrade.Status = TradeStatus.Closed;
                    internalTrade.Commission = (decimal)trade.Commission;
                    internalTrade.Swap = (decimal)trade.Swap;
                    internalTrade.ProfitLoss = (decimal)trade.Profit;
                    
                    // Calculate profit/loss percentage
                    if (internalTrade.EntryPrice != 0)
                    {
                        var pips = Math.Abs(internalTrade.ExitPrice.Value - internalTrade.EntryPrice);
                        var multiplier = trade.Symbol.Contains("JPY") ? 100 : 10000;
                        internalTrade.ProfitLossPercent = (pips / internalTrade.EntryPrice) * 100;
                    }
                }
                else
                {
                    internalTrade.Status = TradeStatus.Open;
                }

                await _dbContext.SaveChangesAsync();
                
                // Raise event
                TradeReceived?.Invoke(this, trade);
                
                // Send notification
                var action = trade.CloseTime.HasValue ? "بسته شد" : "باز شد";
                NotificationService.Instance.Show(
                    "معامله جدید",
                    $"معامله {trade.Symbol} {action}",
                    NotificationType.Trade
                );

                Log.Information($"معامله دریافت شد: {trade.Ticket} - {trade.Symbol}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "خطا در پردازش معامله");
            }
        }

        private async Task<Trade> GetOrCreateTradeAsync(string ticket)
        {
            var trade = await _dbContext.Trades
                .FirstOrDefaultAsync(t => t.MetaTraderTicket == ticket);
            
            if (trade == null)
            {
                trade = new Trade
                {
                    MetaTraderTicket = ticket
                };
                _dbContext.Trades.Add(trade);
            }
            
            return trade;
        }

        private MT4Trade? ParseTrade(JObject? tradeObj)
        {
            if (tradeObj == null) return null;

            try
            {
                return new MT4Trade
                {
                    Ticket = tradeObj["ticket"]?.Value<int>() ?? 0,
                    Symbol = tradeObj["symbol"]?.ToString() ?? "",
                    Type = tradeObj["type"]?.Value<int>() ?? 0,
                    Lots = tradeObj["lots"]?.Value<double>() ?? 0,
                    OpenPrice = tradeObj["openPrice"]?.Value<double>() ?? 0,
                    ClosePrice = tradeObj["closePrice"]?.Value<double>() ?? 0,
                    StopLoss = tradeObj["stopLoss"]?.Value<double>() ?? 0,
                    TakeProfit = tradeObj["takeProfit"]?.Value<double>() ?? 0,
                    OpenTime = ParseDateTime(tradeObj["openTime"]?.ToString()),
                    CloseTime = ParseNullableDateTime(tradeObj["closeTime"]?.ToString()),
                    Commission = tradeObj["commission"]?.Value<double>() ?? 0,
                    Swap = tradeObj["swap"]?.Value<double>() ?? 0,
                    Profit = tradeObj["profit"]?.Value<double>() ?? 0,
                    Comment = tradeObj["comment"]?.ToString() ?? "",
                    MagicNumber = tradeObj["magicNumber"]?.Value<int>() ?? 0
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "خطا در تجزیه داده معامله");
                return null;
            }
        }

        private DateTime ParseDateTime(string? dateStr)
        {
            if (string.IsNullOrEmpty(dateStr))
                return DateTime.MinValue;
            
            // Try parsing MT4/MT5 datetime format
            if (DateTime.TryParse(dateStr, out var date))
                return date;
            
            // Try Unix timestamp
            if (long.TryParse(dateStr, out var timestamp))
                return DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;
            
            return DateTime.MinValue;
        }

        private DateTime? ParseNullableDateTime(string? dateStr)
        {
            if (string.IsNullOrEmpty(dateStr) || dateStr == "0")
                return null;
            
            var date = ParseDateTime(dateStr);
            return date == DateTime.MinValue ? null : date;
        }

        private async Task ProcessAccountInfoAsync(JObject message)
        {
            try
            {
                var accountInfo = new AccountInfo
                {
                    AccountNumber = message["accountNumber"]?.Value<int>() ?? 0,
                    AccountName = message["accountName"]?.ToString() ?? "",
                    Balance = message["balance"]?.Value<double>() ?? 0,
                    Equity = message["equity"]?.Value<double>() ?? 0,
                    Margin = message["margin"]?.Value<double>() ?? 0,
                    FreeMargin = message["freeMargin"]?.Value<double>() ?? 0,
                    Leverage = message["leverage"]?.Value<int>() ?? 0,
                    Currency = message["currency"]?.ToString() ?? ""
                };

                AccountInfoReceived?.Invoke(this, accountInfo);
                
                Log.Information($"اطلاعات حساب دریافت شد: {accountInfo.AccountNumber}");
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "خطا در پردازش اطلاعات حساب");
            }
        }

        private async Task ProcessHistoryAsync(JObject message)
        {
            try
            {
                var trades = message["trades"] as JArray;
                if (trades == null) return;

                foreach (JObject tradeObj in trades)
                {
                    await ProcessTradeMessageAsync(new JObject { ["trade"] = tradeObj });
                }
                
                Log.Information($"تاریخچه دریافت شد: {trades.Count} معامله");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "خطا در پردازش تاریخچه");
            }
        }

        private void ProcessError(JObject message)
        {
            var error = message["error"]?.ToString() ?? "Unknown error";
            Log.Error($"خطا از MetaTrader: {error}");
            
            NotificationService.Instance.Show(
                "خطای MetaTrader",
                error,
                NotificationType.Error
            );
        }

        private void StartHeartbeatMonitor()
        {
            _heartbeatTimer = new System.Timers.Timer(30000); // Check every 30 seconds
            _heartbeatTimer.Elapsed += (sender, e) =>
            {
                var timeSinceLastHeartbeat = DateTime.Now - _lastHeartbeat;
                if (timeSinceLastHeartbeat.TotalMinutes > 2)
                {
                    Status = ConnectionStatus.Disconnected;
                    Log.Warning("اتصال MetaTrader قطع شد (عدم دریافت heartbeat)");
                    
                    NotificationService.Instance.Show(
                        "قطع اتصال",
                        "اتصال MetaTrader قطع شد",
                        NotificationType.Warning
                    );
                }
            };
            _heartbeatTimer.Start();
        }

        public async Task DisconnectAsync()
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                
                _tcpListener?.Stop();
                _pipeServer?.Close();
                _heartbeatTimer?.Stop();
                
                _listenThread?.Join(5000);
                
                Status = ConnectionStatus.Disconnected;
                
                Log.Information("اتصال MetaTrader قطع شد");
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "خطا در قطع اتصال");
            }
        }

        public async Task<List<MT4Trade>> GetOpenTradesAsync()
        {
            // Send request to MT4/MT5
            await SendCommandAsync("GET_OPEN_TRADES", new JObject());
            
            // For now, return empty list
            // In real implementation, wait for response
            return new List<MT4Trade>();
        }

        public async Task<List<MT4Trade>> GetHistoryAsync(DateTime from, DateTime to)
        {
            var parameters = new JObject
            {
                ["from"] = from.ToString("yyyy-MM-dd HH:mm:ss"),
                ["to"] = to.ToString("yyyy-MM-dd HH:mm:ss")
            };
            
            await SendCommandAsync("GET_HISTORY", parameters);
            
            // For now, return empty list
            // In real implementation, wait for response
            return new List<MT4Trade>();
        }

        public async Task<AccountInfo?> GetAccountInfoAsync()
        {
            await SendCommandAsync("GET_ACCOUNT_INFO", new JObject());
            
            // For now, return null
            // In real implementation, wait for response
            return null;
        }

        public async Task<bool> SendCommandAsync(string command, JObject parameters)
        {
            try
            {
                var message = new JObject
                {
                    ["command"] = command,
                    ["parameters"] = parameters,
                    ["timestamp"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };
                
                // Send via appropriate channel (TCP, Pipe, or File)
                // Implementation depends on connection type
                
                Log.Information($"دستور ارسال شد: {command}");
                
                return await Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"خطا در ارسال دستور: {command}");
                return false;
            }
        }

        public void Dispose()
        {
            DisconnectAsync().Wait();
            _heartbeatTimer?.Dispose();
            _pipeServer?.Dispose();
            _cancellationTokenSource?.Dispose();
            _dbContext?.Dispose();
        }
    }
}

// پایان فایل: Services/MetaTraderConnector.cs