// 📁 MetaTrader/WebSocketService.cs
// ===== شروع کد =====

using System;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TradingJournal.MetaTrader
{
    public class WebSocketService
    {
        private HttpListener _httpListener;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly int _port;
        
        public event EventHandler<TradeReceivedEventArgs> TradeReceived;
        public event EventHandler<string> ClientConnected;
        public event EventHandler<string> ClientDisconnected;
        
        public WebSocketService(int port = 5001)
        {
            _port = port;
        }
        
        public async Task StartAsync()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add($"http://localhost:{_port}/ws/");
            _httpListener.Start();
            
            // Accept connections in background
            _ = Task.Run(async () => await AcceptWebSocketClients(_cancellationTokenSource.Token));
        }
        
        public void Stop()
        {
            _cancellationTokenSource?.Cancel();
            _httpListener?.Stop();
            _httpListener?.Close();
        }
        
        private async Task AcceptWebSocketClients(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var context = await _httpListener.GetContextAsync();
                    
                    if (context.Request.IsWebSocketRequest)
                    {
                        var wsContext = await context.AcceptWebSocketAsync(null);
                        _ = Task.Run(async () => await HandleWebSocketConnection(wsContext.WebSocket, cancellationToken));
                        
                        ClientConnected?.Invoke(this, context.Request.RemoteEndPoint.ToString());
                    }
                    else
                    {
                        context.Response.StatusCode = 400;
                        context.Response.Close();
                    }
                }
                catch (Exception ex) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"WebSocket Error: {ex.Message}");
                }
            }
        }
        
        private async Task HandleWebSocketConnection(WebSocket webSocket, CancellationToken cancellationToken)
        {
            var buffer = new ArraySegment<byte>(new byte[4096]);
            
            try
            {
                while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                {
                    var result = await webSocket.ReceiveAsync(buffer, cancellationToken);
                    
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer.Array, 0, result.Count);
                        ProcessMessage(message);
                        
                        // Send acknowledgment
                        var response = Encoding.UTF8.GetBytes("{\"status\":\"received\"}");
                        await webSocket.SendAsync(
                            new ArraySegment<byte>(response),
                            WebSocketMessageType.Text,
                            true,
                            cancellationToken
                        );
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "Closing",
                            cancellationToken
                        );
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WebSocket Connection Error: {ex.Message}");
            }
            finally
            {
                ClientDisconnected?.Invoke(this, "Client disconnected");
            }
        }
        
        private void ProcessMessage(string message)
        {
            try
            {
                var trade = JsonConvert.DeserializeObject<MetaTraderTrade>(message);
                TradeReceived?.Invoke(this, new TradeReceivedEventArgs { Trade = trade });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing message: {ex.Message}");
            }
        }
    }
}

// ===== پایان کد =====