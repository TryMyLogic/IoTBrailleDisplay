using System.Net.WebSockets;
using System.Text;
using DisplayLogic.SharedInterfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DisplayLogic.Services
{
    public class WebSocketClient(
        string wsUrl,
        IUserNotifier notifier,
        ILogger<WebSocketClient>? logger = null) : IWebSocketClient
    {
        private readonly ClientWebSocket _socket = new();
        private readonly string _wsUrl = wsUrl ?? throw new ArgumentNullException(nameof(wsUrl));
        private readonly ILogger<WebSocketClient>? _logger = logger ?? NullLogger<WebSocketClient>.Instance;
        private readonly IUserNotifier _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
        private Task? _messageListenerTask;
        private readonly CancellationTokenSource _messageListenerCts = new();

        public event Action<string>? PayloadReceived;

        public async Task StartAsync()
        {
            try
            {
                await _socket.ConnectAsync(new Uri(_wsUrl), CancellationToken.None);
                _logger?.LogInformation($"WebSocket connected to URL: {_wsUrl}.");
                await _notifier.NotifyAsync("WebSocket", "WebSocket connected.");

                // Start ReceiveMessagesAsync as a background task to not block the main thread
                _messageListenerTask = Task.Run(() =>
                {
                    return ReceiveMessagesAsync(_messageListenerCts.Token);
                }, _messageListenerCts.Token);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to start WebSocket connection: {ex.Message}");
                throw; // Whatever class uses this underlying should handle this exception accordingly
            }
        }

        public async Task StopAsync()
        {
            try
            {
                _logger?.LogInformation("Stopping WebSocket connection...");
                _messageListenerCts.Cancel();
                if (_socket.State == WebSocketState.Open)
                {
                    await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closing", CancellationToken.None);
                    _logger?.LogInformation("WebSocket closed gracefully.");
                }
                if (_messageListenerTask != null)
                {
                    await _messageListenerTask;
                }
                await _notifier.NotifyAsync("WebSocket", "WebSocket stopped.");
                _logger?.LogInformation("WebSocket stopped.");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error stopping WebSocket: {ex.Message}.");
                await _notifier.NotifyAsync("WebSocket", $"Error stopping WebSocket: {ex.Message}");
            }
            finally
            {
                _socket.Dispose();
                _messageListenerCts.Dispose();
            }
        }

        private async Task ReceiveMessagesAsync(CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[4096];
            MemoryStream messageStream = new();

            try
            {
                while (_socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                {
                    messageStream.SetLength(0);
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                        messageStream.Write(buffer, 0, result.Count);
                    } while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        string payload = Encoding.UTF8.GetString(messageStream.ToArray());
                        await HandleMessageAsync(payload);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error in receive loop: {ex.Message}.");
                await _notifier.NotifyAsync("WebSocket", $"Error in receive loop: {ex.Message}");
            }
        }

        public async Task SendAsync(string payload)
        {
            if (_socket.State != WebSocketState.Open)
            {
                throw new InvalidOperationException("WebSocket not connected.");
            }

            byte[] bytes = Encoding.UTF8.GetBytes(payload);
            await _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            await LogAsync($"Sent: {payload}");
        }

        private async Task HandleMessageAsync(string payload)
        {
            await LogAsync(payload);
            PayloadReceived?.Invoke(payload);
        }

        private async Task LogAsync(string message)
        {
            _logger?.LogInformation("WebSocket Payload: {message}", message);
            await _notifier.NotifyAsync("WebSocket Payload", message);
        }
    }
}