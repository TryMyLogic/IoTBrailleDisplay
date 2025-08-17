using DisplayLogic.Services;
using DisplayLogic.Tests.Mocks;
using DisplayLogic.Tests.SharedTestItems;
using Microsoft.Extensions.Logging;
using Serilog.Sinks.InMemory;



namespace DisplayLogic.Tests.Services.BluetoothFunctionalityTests
{
    public class BrailleDisplayTests
    {
        private readonly ILogger<BrailleDisplay>? _testLogger;
        private readonly ILoggerFactory? _loggerFactory;

        private InMemorySink? _memorySink;
        private ILogger<BrailleDisplay>? _memoryLogger;
        private ILoggerFactory? _memoryLoggerFactory;

        private void InitializeMemorySinkLogger()
        {
            (_memoryLogger, _memorySink, _memoryLoggerFactory) = SharedFunctions.CreateMemorySinkLogger<BrailleDisplay>();
        }

        [Fact]
        public async Task ConnectAsync_ShouldSetIsConnected_WhenMockSucceeds()
        {
            InitializeMemorySinkLogger();
            ILogger<MockBluetoothConnection> mockLogger = _memoryLoggerFactory!.CreateLogger<MockBluetoothConnection>();
            var mock = new MockBluetoothConnection(logger: mockLogger, connectSucceed: true);
            var braille = new BrailleDisplay(mock, _memoryLogger!);

            //Act
            await braille.ConnectAsync();

            //Assert
            Assert.True(braille.IsConnected);
        }

        [Fact]
        public async Task ConnectAsync_ShouldNotSetIsConnected_WhenMockFails()
        {
            InitializeMemorySinkLogger();
            ILogger<MockBluetoothConnection> mockLogger = _memoryLoggerFactory!.CreateLogger<MockBluetoothConnection>();
            var mock = new MockBluetoothConnection(logger: mockLogger, connectSucceed: false);
            var braille = new BrailleDisplay(mock, _memoryLogger!);

            //Act
            await braille.ConnectAsync();

            //Assert
            Assert.False(braille.IsConnected);
        }

        [Fact]
        public async Task BrailleDisplay_Integration_SendAndReceiveText_WorksCorrectly()
        {
            InitializeMemorySinkLogger();
            ILogger<MockBluetoothConnection> mockLogger = _memoryLoggerFactory!.CreateLogger<MockBluetoothConnection>();
            var mock = new MockBluetoothConnection(logger: mockLogger);
            var braille = new BrailleDisplay(mock, _memoryLogger!);
            bool eventTriggered = false;
            braille.TextReceived += (s, e) => eventTriggered = true;

            string message = "MQTT connection test string";

            //Act
            await braille.ConnectAsync();
            await braille.SendTextAsync(message);
            await braille.ReceiveTextAsync();

            //Assert
            Assert.True(mock.IsConnected);
            Assert.True(eventTriggered);
            string received = await mock.ReceiveTextAsync();
            Assert.Equal(message, received);

        }

        [Fact]
        public async Task DisconnectAsync_ShouldSetIsConnectedFalse()
        {
            //Arrange
            InitializeMemorySinkLogger();
            ILogger<MockBluetoothConnection> mockLogger = _memoryLoggerFactory!.CreateLogger<MockBluetoothConnection>();
            var mock = new MockBluetoothConnection(logger: mockLogger, connectSucceed: true);
            var braille = new BrailleDisplay(mock, _memoryLogger!);

            //Act
            await braille.ConnectAsync();
            await braille.DisconnectAsync();

            //Assert
            Assert.False(braille.IsConnected);
        }
        [Fact]
        public async Task BrailleDisplay_Log_ConnectAsync_Success_ShouldLogInfo()
        {
            //Arrange
            InMemorySink.Instance.Dispose();
            InitializeMemorySinkLogger();
            ILogger<MockBluetoothConnection> mockLogger = _memoryLoggerFactory!.CreateLogger<MockBluetoothConnection>();
            MockBluetoothConnection mock = new(logger: mockLogger, connectSucceed: true);
            BrailleDisplay brailleDisplay = new(mock, _memoryLogger!);

            //Act
            await brailleDisplay.ConnectAsync();

            //Assert
            bool hasLogEntry = _memorySink!
                .LogEvents
                .Any(log => log.RenderMessage().Contains("BrailleDisplay connected successfully."));

            Assert.True(hasLogEntry, "Expected log message not found.");
        }
        [Fact]
        public async Task BrailleDisplay_Log_Disconnect_ShouldLogInfo()
        {
            InMemorySink.Instance.Dispose();

            //Arrange
            InitializeMemorySinkLogger();
            ILogger<MockBluetoothConnection> mockLogger = _memoryLoggerFactory!.CreateLogger<MockBluetoothConnection>();
            MockBluetoothConnection mock = new(logger: mockLogger, connectSucceed: true);
            BrailleDisplay brailleDisplay = new(mock, _memoryLogger!);

            //Act
            await brailleDisplay.ConnectAsync();
            await brailleDisplay.DisconnectAsync();

            //Assert
            bool hasLogEntry = _memorySink!
                .LogEvents
                .Any(log => log.RenderMessage().Contains("BrailleDisplay disconnected successfully."));

            Assert.True(hasLogEntry, "Expected disconnect log message not found.");

        }
        [Fact]
        public async Task BrailleDisplay_Log_Send_ShouldLogSentText()
        {
            InMemorySink.Instance.Dispose();

            //Arrange
            InitializeMemorySinkLogger();
            ILogger<MockBluetoothConnection> mockLogger = _memoryLoggerFactory!.CreateLogger<MockBluetoothConnection>();
            MockBluetoothConnection mock = new(logger: mockLogger, connectSucceed: true);
            BrailleDisplay brailleDisplay = new(mock, _memoryLogger!);

            //Act
            await brailleDisplay.ConnectAsync();

            string testMessage = "Test Message";
            await brailleDisplay.SendTextAsync(testMessage);

            //Assert
            bool hasLogEntry = _memorySink!
                .LogEvents
                .Any(log => log.Level == Serilog.Events.LogEventLevel.Information && log.RenderMessage().Contains("BrailleDisplay sent text:") && log.RenderMessage().Contains(testMessage));
            Assert.True(hasLogEntry, "Expected send log message not found.");
        }
        [Fact]
        public async Task BrailleDisplay_Log_Receive_ShouldLogReceivedText()
        {
            InMemorySink.Instance.Dispose();

            //Arrange
            InitializeMemorySinkLogger();
            ILogger<MockBluetoothConnection> mockLogger = _memoryLoggerFactory!.CreateLogger<MockBluetoothConnection>();
            MockBluetoothConnection mock = new(logger: mockLogger, connectSucceed: true);
            BrailleDisplay brailleDisplay = new(mock, _memoryLogger!);

            //Act
            await brailleDisplay.ConnectAsync();

            string testMessage = "Received Message";
            await mock.SendTextAsync(testMessage);
            await brailleDisplay.ReceiveTextAsync();

            //Assert
            bool hasLogEntry = _memorySink!
                .LogEvents
                .Any(log => log.Level == Serilog.Events.LogEventLevel.Information && log.RenderMessage().Contains("BrailleDisplay received text:") && log.RenderMessage().Contains(testMessage));

            Assert.True(hasLogEntry, "Expected send log message not found.");

        }
        [Fact]
        public async Task BrailleDisplay_Log_ConnectAsync_Failure_ShouldLogWarning()
        {
            InMemorySink.Instance.Dispose();

            //Arrange
            InitializeMemorySinkLogger();
            ILogger<MockBluetoothConnection> mockLogger = _memoryLoggerFactory!.CreateLogger<MockBluetoothConnection>();
            MockBluetoothConnection mock = new(logger: mockLogger, connectSucceed: false);
            BrailleDisplay brailleDisplay = new(mock, _memoryLogger!);

            //Act
            await brailleDisplay.ConnectAsync();

            //Assert
            bool hasLogEntry = _memorySink!
                .LogEvents
                .Any(log => log.RenderMessage().Contains("BrailleDisplay failed to connect."));

            Assert.True(hasLogEntry, "Expected warning log message for failed connection not found.");
        }
        [Fact]
        public async Task BrailleDisplay_Log_ConnectAsync_Exception_ShouldLogError()
        {
            InMemorySink.Instance.Dispose();

            //Arrange
            InitializeMemorySinkLogger();
            ILogger<MockBluetoothConnection> mockLogger = _memoryLoggerFactory!.CreateLogger<MockBluetoothConnection>();
            MockBluetoothConnection mock = new(logger: mockLogger)
            {
                ThrowOnConnect = true
            };
            BrailleDisplay brailleDisplay = new(mock, _memoryLogger!);

            //Act
            await brailleDisplay.ConnectAsync();

            //Assert
            bool hasLogEntry = _memorySink!
                .LogEvents
                .Any(log => log.RenderMessage().Contains("Exception occurred during BrailleDisplay.ConnectAsync.") && log.Level == Serilog.Events.LogEventLevel.Error);

            Assert.True(hasLogEntry, "Expected error log message for ConnectAsync exception not found.");
        }
        [Fact]
        public async Task BrailleDisplay_Log_SendTextAsync_NotConnected_ShouldLogWarning()
        {
            InMemorySink.Instance.Dispose();

            //Arrange
            InitializeMemorySinkLogger();
            ILogger<MockBluetoothConnection> mockLogger = _memoryLoggerFactory!.CreateLogger<MockBluetoothConnection>();
            MockBluetoothConnection mock = new(logger: mockLogger, connectSucceed: false);
            BrailleDisplay brailleDisplay = new(mock, _memoryLogger!);

            //Act
            await brailleDisplay.SendTextAsync("Test Message");

            //Assert
            bool hasLogEntry = _memorySink!
                .LogEvents
                .Any(log => log.RenderMessage().Contains("Attempted to send text while BrailleDisplay is not connected.") && log.Level == Serilog.Events.LogEventLevel.Warning);

            Assert.True(hasLogEntry, "Expected warning log message for failed connection not found.");
        }
        [Fact]
        public async Task BrailleDisplay_Log_SendTextAsync_Exception_ShouldLogError()
        {
            InMemorySink.Instance.Dispose();

            //Arrange
            InitializeMemorySinkLogger();
            ILogger<MockBluetoothConnection> mockLogger = _memoryLoggerFactory!.CreateLogger<MockBluetoothConnection>();
            MockBluetoothConnection mock = new(logger: mockLogger)
            {
                ThrowOnSend = true
            };
            BrailleDisplay brailleDisplay = new(mock, _memoryLogger!);

            //Act
            await brailleDisplay.ConnectAsync();
            await brailleDisplay.SendTextAsync("Faulty message");

            //Assert
            bool hasLogEntry = _memorySink!
                .LogEvents
                .Any(log => log.RenderMessage().Contains("Exception occured during BrailleDisplay.SendTextAsync.") && log.Level == Serilog.Events.LogEventLevel.Error);

            Assert.True(hasLogEntry, "Expected warning log message for failed connection not found.");
        }
        [Fact]
        public async Task BrailleDisplay_Log_ReceiveTextAsync_Exception_ShouldLogError()
        {
            InMemorySink.Instance.Dispose();

            //Arrange
            InitializeMemorySinkLogger();
            ILogger<MockBluetoothConnection> mockLogger = _memoryLoggerFactory!.CreateLogger<MockBluetoothConnection>();
            MockBluetoothConnection mock = new(logger: mockLogger)
            {
                ThrowOnReceive = true
            };
            BrailleDisplay brailleDisplay = new(mock, _memoryLogger!);

            //Act
            await brailleDisplay.ConnectAsync();
            try
            {
                await brailleDisplay.ReceiveTextAsync();
            }
            catch (InvalidOperationException)
            {

            }


            //Assert
            bool hasLogEntry = _memorySink!
                .LogEvents
                .Any(log => log.RenderMessage().Contains("Exception occurred during BrailleDisplay.ReceiveTextAsync.") && log.Level == Serilog.Events.LogEventLevel.Error);

            Assert.True(hasLogEntry, "Expected warning log message for failed connection not found.");
        }
    }
}