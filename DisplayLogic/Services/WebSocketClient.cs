using System.Net.WebSockets;
using System.Text;
using DisplayLogic.SharedInterfaces;

namespace DisplayLogic.Services
{
    public class WebSocketClient : IWebSocketClient
    {
        private readonly ClientWebSocket _socket;
        private readonly string _wsUrl;
        private readonly string _logFilePath;
        private readonly IUserNotifier _notifier;

        public event Action<string>? PayloadReceived;

        public WebSocketClient(
            string wsUrl,
            IUserNotifier notifier,
            string logFilePath)
        {
            _wsUrl = wsUrl ?? throw new ArgumentNullException(nameof(wsUrl));
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
            _socket = new ClientWebSocket();
            _logFilePath = logFilePath ?? throw new ArgumentNullException(nameof(logFilePath));
        }

        public async Task StartAsync()
        {
            try
            {
                await _socket.ConnectAsync(new Uri(_wsUrl), CancellationToken.None);
                _ = Task.Run(ReceiveMessagesAsync);
            }
            catch (Exception ex)
            {
                await LogAsync($"Connection error: {ex.Message}");
            }
        }

        public async Task StopAsync()
        {
            if (_socket.State == WebSocketState.Open)
            {
                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closing", CancellationToken.None);
            }
            _socket.Dispose();
        }

        private async Task ReceiveMessagesAsync()
        {
            byte[] buffer = new byte[4096];
            MemoryStream ms = new();

            while (_socket.State == WebSocketState.Open)
            {
                ms.SetLength(0);
                WebSocketReceiveResult result;
                do
                {
                    result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string payload = Encoding.UTF8.GetString(ms.ToArray());
                    await HandleMessageAsync(payload);
                }
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
            string line = $"{DateTime.UtcNow:o}: {message}{Environment.NewLine}";
            await File.AppendAllTextAsync(_logFilePath, line);
            await _notifier.NotifyAsync("WebSocket Payload", message);
        }
    }
}