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

        public async Task ReceiveTextAsync()
        {
            if (!_connectionStrategy.IsConnected)
                return;
            string text = await _connectionStrategy.ReceiveTextAsync();
            TextReceived?.Invoke(this, EventArgs.Empty);
        }

        public async Task SendTextAsync(string text)
        {
            if (!_connectionStrategy.IsConnected)
                return;
            await _connectionStrategy.SendTextAsync(text);
        }
    }
}