using DisplayLogic.Services;
using DisplayLogic.Tests.Mocks;
using DisplayLogic.Tests.SharedTestItems;
using Microsoft.Extensions.Logging;
using Serilog.Sinks.InMemory;


namespace DisplayLogic.Tests.Services.BluetoothFunctionalityTests
{
    public class BluetoothConnectionIntegrationTests
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
        public async Task BrailleDisplay_Integration_SendAndReceiveText_WorksCorrectly()
        {
            InitializeMemorySinkLogger();
            ILogger<MockBluetoothConnection> mockLogger = _memoryLoggerFactory!.CreateLogger<MockBluetoothConnection>();
            var mock = new MockBluetoothConnection(logger: mockLogger);
            var braille = new BrailleDisplay(mock, _memoryLogger!);
            bool eventTriggered = false;
            braille.TextReceived += (s, e) =>
            {
                eventTriggered = true;
            };

            string message = "MQTT connection test string";

            //Act
            await braille.ConnectAsync();
            await braille.SendTextAsync(message);
            _ = await braille.ReceiveTextAsync();

            //Assert
            Assert.True(mock.IsConnected);
            Assert.True(eventTriggered);
            string received = await mock.ReceiveTextAsync();
            Assert.Equal(message, received);

        }
    }
}
