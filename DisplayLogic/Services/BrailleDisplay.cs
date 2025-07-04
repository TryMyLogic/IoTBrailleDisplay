namespace DisplayLogic.Services
{
    public class BrailleDisplay : IBrailleDisplay
    {
        private readonly IConnectionStrategy _connectionStrategy;
        public BrailleDisplay(IConnectionStrategy connectionStrategy)
        {
            _connectionStrategy = connectionStrategy;
        }
        public bool IsConnected => _connectionStrategy.IsConnected;

        public event EventHandler? TextReceived;

        public async Task ConnectAsync()
        {
            _ = await _connectionStrategy.ConnectAsync();
        }

        public async Task DisconnectAsync()
        {
            _ = await _connectionStrategy.DisconnectAsync();
        }

        public Task ReceiveTextAsync()
        {
            throw new NotImplementedException();
        }

        public Task SendTextAsync(string text)
        {
            throw new NotImplementedException();
        }
    }
}
