using DisplayLogic.Services;
using Serilog;

namespace DisplayLogic.Tests.Mocks
{
    public class MockBluetoothConnection : IConnectionStrategy
    {
        private readonly bool _connectSucceed;
        private string? _receivedText;
        public bool ThrowOnConnect { get; set; }
        public bool ThrowOnDisconnect { get; set; }
        public bool ThrowOnSend { get; set; }
        public bool ThrowOnReceive { get; set; }

        public MockBluetoothConnection(bool connectSucceed = true)
        {
            _connectSucceed = connectSucceed;
            IsConnected = false;
            Log.Debug($"MockBluetoothConnection initialized. Connection success simulation: {connectSucceed}");
        }
        public bool IsConnected { get; private set; }
        public Task<bool> ConnectAsync()
        {
            if (ThrowOnConnect)
                throw new InvalidOperationException("Simulated exception in ConnectAsync");
            IsConnected = _connectSucceed;
            if (_connectSucceed)
            {
                Log.Information("MockBluetoothConnection: Simulated connection successful.");
            }
            else
            {
                Log.Warning("MockBluetoothConnection: Simulated connection failed.");
            }
            return Task.FromResult(_connectSucceed);
        }
        public Task<bool> DisconnectAsync()
        {
            if (ThrowOnDisconnect)
                throw new InvalidOperationException("Simulated exception in DisconnectAsync");
            IsConnected = false;
            Log.Information("MockBluetoothConnection: Simulated disconection.");
            return Task.FromResult(true);
        }
        public void ForceDisconnect()
        {
            IsConnected = false;
            Log.Warning("MockBluetoothConnection: Force disconnection triggered.");
        }

        public Task<string> ReceiveTextAsync()
        {
            if (ThrowOnReceive)
                throw new InvalidOperationException("Simulated exception in ReceiveTextAsync");
            string text = _receivedText ?? string.Empty;
            Log.Information($"MockBluetoothConnection: Received text: {text}");
            return Task.FromResult(text);
        }

        public Task SendTextAsync(string text)
        {
            if (ThrowOnSend)
                throw new InvalidOperationException("Simulated exception in SendTextAsync");
            _receivedText = text;
            Log.Information($"MockBluetoothConnection: Sent text: {text}");
            return Task.CompletedTask;
        }
    }
}
