using DisplayLogic.Services;

namespace DisplayLogic.Tests.Mocks
{
    public class MockBluetoothConnection : IConnectionStrategy
    {
        private readonly bool _connectSucceed;
        private string? _receivedText;

        public MockBluetoothConnection(bool connectSucceed = true)
        {
            _connectSucceed = connectSucceed;
            IsConnected = false;
        }
        public bool IsConnected { get; private set; }
        public Task<bool> ConnectAsync()
        {
            IsConnected = _connectSucceed;
            return Task.FromResult(_connectSucceed);
        }
        public Task<bool> DisconnectAsync()
        {
            IsConnected = false;
            return Task.FromResult(true);
        }
        public void ForceDisconnect()
        {
            IsConnected = false;
        }

        public Task<string> ReceiveTextAsync()
        {
            return Task.FromResult(_receivedText ?? string.Empty);
        }

        public Task SendTextAsync(string text)
        {
            _receivedText = text;
            return Task.CompletedTask;
        }
    }
}
