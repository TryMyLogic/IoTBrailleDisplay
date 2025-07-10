using DisplayLogic.Services;

namespace DisplayApp.Tests.Mocks
{
    public class MockBluetoothConnection : IConnectionStrategy
    {
        private readonly bool _connectSucceed;

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
            throw new NotImplementedException();
        }

        public Task SendTextAsync(string text)
        {
            throw new NotImplementedException();
        }
    }
}
