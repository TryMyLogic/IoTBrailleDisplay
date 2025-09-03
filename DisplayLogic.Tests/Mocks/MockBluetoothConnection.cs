using DisplayLogic.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DisplayLogic.Tests.Mocks
{
    /// <summary>
    /// Represents a mock bluetooth connection used for testing braille display
    /// </summary>
    public class MockBluetoothConnection : IConnectionStrategy
    {
        private readonly bool _connectSucceed;
        private readonly ILogger<MockBluetoothConnection> _logger;
        private string? _receivedText;
        /// <summary>
        /// Gets or sets a value indicating whether <see cref="ConnectAsync"/> should throw an exception.
        /// </summary>
        public bool ThrowOnConnect { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether <see cref="DisconnectAsync"/> should throw an exception.
        /// </summary>
        public bool ThrowOnDisconnect { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether <see cref="SendTextAsync(string)"/> should throw an exception.
        /// </summary>
        public bool ThrowOnSend { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether <see cref="ReceiveTextAsync"/> should throw an exception.
        /// </summary>
        public bool ThrowOnReceive { get; set; }

        /// <summary>
        /// Initializes a new instance of <see cref="MockBluetoothConnection"/> class.
        /// </summary>
        /// <param name="logger">Logger for diagnostic messages.</param>
        /// <param name="connectSucceed">Indicates whether simulated connections succeed.</param>
        public MockBluetoothConnection(ILogger<MockBluetoothConnection>? logger = null, bool connectSucceed = true)
        {
            _connectSucceed = connectSucceed;
            IsConnected = false;
            _logger = logger ?? NullLogger<MockBluetoothConnection>.Instance;
            _logger.LogDebug("MockBluetoothConnection initialized. Connection success simulation: {ConnectSucceed}", connectSucceed);
        }

        ///<inheritdoc/>
        public bool IsConnected { get; private set; }

        ///<inheritdoc/>
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

        ///<inheritdoc/>
        public Task<bool> DisconnectAsync()
        {
            if (ThrowOnDisconnect)
                throw new InvalidOperationException("Simulated exception in DisconnectAsync");
            IsConnected = false;
            _logger.LogInformation("MockBluetoothConnection: Simulated disconection.");
            return Task.FromResult(true);
        }

        /// <summary>
        /// Forces the mock connection to appear disconnected without invoking <see cref="DisconnectAsync"/>.
        /// </summary>
        public void ForceDisconnect()
        {
            IsConnected = false;
            _logger.LogWarning("MockBluetoothConnection: Force disconnection triggered.");
        }

        ///<inheritdoc/>
        public Task<string> ReceiveTextAsync()
        {
            if (ThrowOnReceive)
                throw new InvalidOperationException("Simulated exception in ReceiveTextAsync");
            string text = _receivedText ?? string.Empty;
            _logger.LogInformation("MockBluetoothConnection: Received text: {Text}", text);
            return Task.FromResult(text);
        }

        ///<inheritdoc/>
        public Task SendTextAsync(string text)
        {
            if (ThrowOnSend)
                throw new InvalidOperationException("Simulated exception in SendTextAsync");
            _receivedText = text;
            _logger.LogInformation("MockBluetoothConnection: Sent text: {Text}", text);
            return Task.CompletedTask;
        }
    }
}