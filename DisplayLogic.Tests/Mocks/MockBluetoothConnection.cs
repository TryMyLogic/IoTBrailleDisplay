using DisplayLogic.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DisplayLogic.Tests.Mocks
{
    public class MockBluetoothConnection : IConnectionStrategy
    {
        private readonly bool _connectSucceed;
        private readonly ILogger<MockBluetoothConnection> _logger;
        private string? _receivedText;
        public bool ThrowOnConnect { get; set; }
        public bool ThrowOnDisconnect { get; set; }
        public bool ThrowOnSend { get; set; }
        public bool ThrowOnReceive { get; set; }

        public MockBluetoothConnection(ILogger<MockBluetoothConnection>? logger = null, bool connectSucceed = true)
        {
            _connectSucceed = connectSucceed;
            IsConnected = false;
            _logger = logger ?? NullLogger<MockBluetoothConnection>.Instance;
            _logger.LogDebug($"MockBluetoothConnection initialized. Connection success simulation: {connectSucceed}");
        }
        public bool IsConnected { get; private set; }
        public Task<bool> ConnectAsync()
        {
            if (ThrowOnConnect)
                throw new InvalidOperationException("Simulated exception in ConnectAsync");
            IsConnected = _connectSucceed;
            if (_connectSucceed)
            {
                _logger.LogInformation("MockBluetoothConnection: Simulated connection successful.");
            }
            else
            {
                _logger.LogWarning("MockBluetoothConnection: Simulated connection failed.");
            }
            return Task.FromResult(_connectSucceed);
        }
        public Task<bool> DisconnectAsync()
        {
            if (ThrowOnDisconnect)
                throw new InvalidOperationException("Simulated exception in DisconnectAsync");
            IsConnected = false;
            _logger.LogInformation("MockBluetoothConnection: Simulated disconection.");
            return Task.FromResult(true);
        }
        public void ForceDisconnect()
        {
            IsConnected = false;
            _logger.LogWarning("MockBluetoothConnection: Force disconnection triggered.");
        }

        public Task<string> ReceiveTextAsync()
        {
            if (ThrowOnReceive)
                throw new InvalidOperationException("Simulated exception in ReceiveTextAsync");
            string text = _receivedText ?? string.Empty;
            _logger.LogInformation($"MockBluetoothConnection: Received text: {text}");
            return Task.FromResult(text);
        }

        public Task SendTextAsync(string text)
        {
            if (ThrowOnSend)
                throw new InvalidOperationException("Simulated exception in SendTextAsync");
            _receivedText = text;
            _logger.LogInformation($"MockBluetoothConnection: Sent text: {text}");
            return Task.CompletedTask;
        }
    }
}