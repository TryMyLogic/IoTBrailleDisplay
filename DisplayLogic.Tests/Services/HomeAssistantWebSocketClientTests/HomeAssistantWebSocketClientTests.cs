using System.Text.Json;
using DisplayLogic.Models;
using DisplayLogic.Services;
using DisplayLogic.SharedInterfaces;
using DisplayLogic.Tests.SharedTestItems;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Serilog.Events;
using Serilog.Sinks.InMemory;

namespace DisplayLogic.Tests.Services.HomeAssistantWebSocketClientTests
{
    public class HomeAssistantWebSocketClientTests : IDisposable
    {
        private readonly IWebSocketClient _webSocketClient;
        private readonly IUserNotifier _userNotifier;
        private readonly ILogger<HomeAssistantWebSocketClient> _logger;
        private readonly string _accessToken = "test-token";
        private readonly HomeAssistantWebSocketClient _homeAssistantWebSocketClient;

        private InMemorySink? _memorySink;
        private ILogger<HomeAssistantWebSocketClient>? _memoryLogger;
        private ILoggerFactory? _memoryLoggerFactory;

        public HomeAssistantWebSocketClientTests()
        {
            _webSocketClient = Substitute.For<IWebSocketClient>();
            _userNotifier = Substitute.For<IUserNotifier>();
            _logger = Substitute.For<ILogger<HomeAssistantWebSocketClient>>();
            _homeAssistantWebSocketClient = new HomeAssistantWebSocketClient(_webSocketClient, _userNotifier, _accessToken, _logger);
        }

        private void InitializeMemorySinkLogger()
        {
            (_memoryLogger, _memorySink, _memoryLoggerFactory) = SharedFunctions.CreateMemorySinkLogger<HomeAssistantWebSocketClient>();
        }

        [Fact]
        public void Constructor_NullWebSocketClient_ThrowsArgumentNullException()
        {
            _ = Assert.Throws<ArgumentNullException>(() =>
            {
                return new HomeAssistantWebSocketClient(null!, _userNotifier, _accessToken);
            });
        }

        [Fact]
        public void Constructor_NullNotifier_ThrowsArgumentNullException()
        {
            _ = Assert.Throws<ArgumentNullException>(() =>
            {
                return new HomeAssistantWebSocketClient(_webSocketClient, null!, _accessToken);
            });
        }

        [Fact]
        public void Constructor_NullAccessToken_ThrowsArgumentNullException()
        {
            _ = Assert.Throws<ArgumentNullException>(() =>
            {
                return new HomeAssistantWebSocketClient(_webSocketClient, _userNotifier, null!);
            });
        }

        [Fact]
        public void Constructor_NullLogger_DoesNotThrow()
        {
            // Arrange

            // Act & Assert
            Exception exception = Record.Exception(() =>
            {
                return new HomeAssistantWebSocketClient(_webSocketClient, _userNotifier, _accessToken, null!);
            });
            Assert.Null(exception);
        }

        // Not worth testing StartAsync and StopAsync as they are currently WebSocketClient wrappers

        [Fact]
        public async Task ConnectAsync_InitializationTimesOut_ThrowsTimeoutException()
        {
            // Arrange
            _ = _webSocketClient.StartAsync().Returns(Task.CompletedTask);
            InitializeMemorySinkLogger();
            HomeAssistantWebSocketClient client = new(_webSocketClient, _userNotifier, _accessToken, _memoryLogger);

            // Act
            Exception? caughtException = null;
            try
            {
                await client.ConnectAsync();
            }
            catch (Exception ex)
            {
                caughtException = ex;
            }

            // Assert
            Assert.NotNull(caughtException);
            _ = Assert.IsType<TimeoutException>(caughtException);
            Assert.Equal("Initialization timed out", caughtException.Message);
            SharedFunctions.AssertSingleLogEvent(_memorySink, LogEventLevel.Error, "Initialization timed out");
            Assert.False(_homeAssistantWebSocketClient.IsConnected, "Client should not be connected after a timeout occurs");

            await _userNotifier.Received(1).NotifyAsync("WebSocket", "Waiting for authentication...");

            // Cleanup
            _memoryLoggerFactory?.Dispose();
        }

        [Fact]
        public async Task UpdateDeviceAreaAsync_InvalidDeviceId_ThrowsInvalidOperationException()
        {
            // Arrange
            InitializeMemorySinkLogger();
            HomeAssistantWebSocketClient client = new(_webSocketClient, _userNotifier, _accessToken, _memoryLogger);
            string invalidDeviceId = "non_existent_device";
            string areaId = "valid_area_id";

            // Act
            Exception? caughtException = null;
            try
            {
                await client.UpdateDeviceAreaAsync(invalidDeviceId, areaId);
            }
            catch (Exception ex)
            {
                caughtException = ex;
            }

            // Assert
            Assert.NotNull(caughtException);
            _ = Assert.IsType<InvalidOperationException>(caughtException);
            Assert.Equal($"Device with id '{invalidDeviceId}' not found in registry.", caughtException.Message);
            // SharedFunctions.AssertSingleLogEvent(_memorySink, LogEventLevel.Error, $"Device with id '{invalidDeviceId}' not found in registry.");
            await _webSocketClient.DidNotReceive().SendAsync(Arg.Any<string>());

            // Cleanup
            _memoryLoggerFactory?.Dispose();
        }

        [Fact]
        public async Task UpdateDeviceAreaAsync_ValidDeviceIdAndAreaId_SendsCommandAndUpdatesDevice()
        {
            // Arrange
            InitializeMemorySinkLogger();
            HomeAssistantWebSocketClient client = new(_webSocketClient, _userNotifier, _accessToken, _memoryLogger);

            // Set up a device in the Devices list with all required properties
            var device = new MqttDevice
            {
                id = "valid_device_id",
                name = "Test Device",
                area_id = null,
                default_manufacturer = "Default Manufacturer",
                default_model = "Default Model",
                default_name = "Default Name",
                manufacturer = "Test Manufacturer",
                model = "Test Model",
                identifiers = []
            };
            client.GetType().GetProperty("Devices")!.SetValue(client, new List<MqttDevice> { device });

            string validDeviceId = "valid_device_id";
            string areaId = "new_area_id";

            string? sentPayload = null;
            _ = _webSocketClient.SendAsync(Arg.Do<string>(payload =>
            {
                sentPayload = payload;
            })).Returns(Task.CompletedTask);

            // Act
            await client.UpdateDeviceAreaAsync(validDeviceId, areaId);

            Assert.NotNull(sentPayload);
            JsonElement sentCommand = JsonSerializer.Deserialize<JsonDocument>(sentPayload!)!.RootElement;
            int commandId = sentCommand.GetProperty("id").GetInt32();

            _webSocketClient.PayloadReceived += Raise.Event<Action<string>>(JsonSerializer.Serialize(new
            {
                type = "result",
                id = commandId,
                success = true,
                result = new { id = validDeviceId, area_id = areaId }
            }));

            // Assert
            Assert.Equal("config/device_registry/update", sentCommand.GetProperty("type").GetString());
            Assert.Equal(validDeviceId, sentCommand.GetProperty("device_id").GetString());
            Assert.Equal(areaId, sentCommand.GetProperty("area_id").GetString());

            // Ensure areaid is updated
            Assert.Equal(areaId, device.area_id);

            //SharedFunctions.AssertLogEventContainsMessage(_memorySink, LogEventLevel.Information, $"UpdateDeviceAsync called with uniqueId: {validDeviceId}, areaId: {areaId}", expectedMatchCount: 1);
            SharedFunctions.AssertLogEventContainsMessage(_memorySink, LogEventLevel.Information, "Sending device area update:", expectedMatchCount: 1);

            await _userNotifier.Received(1).NotifyAsync("WebSocket", $"Device update result for ID {commandId}: Device {validDeviceId} updated to area {areaId}.");

            // Cleanup
            _memoryLoggerFactory?.Dispose();
        }

        [Fact]
        public async Task StopAsync_WhenCalled_StopsWebSocketAndUpdatesState()
        {
            // Arrange
            InitializeMemorySinkLogger();
            HomeAssistantWebSocketClient client = new(_webSocketClient, _userNotifier, _accessToken, _memoryLogger);

            // Using reflection here is an exception as IsConnected is guareenteed to stay.
            // Would not recommend for most scenarios
            client.GetType().GetProperty("IsConnected")!.SetValue(client, true);
            _ = _webSocketClient.StopAsync().Returns(Task.CompletedTask);

            // Act
            await client.StopAsync();

            // Assert
            Assert.False(client.IsConnected, "Client should not be connected after StopAsync");
            SharedFunctions.AssertLogEventContainsMessage(_memorySink, LogEventLevel.Information, "Stopping WebSocket connection", expectedMatchCount: 1);
            SharedFunctions.AssertLogEventContainsMessage(_memorySink, LogEventLevel.Information, "WebSocket connection stopped", expectedMatchCount: 1);
            await _webSocketClient.Received(1).StopAsync();

            // Cleanup
            _memoryLoggerFactory?.Dispose();
        }

        public void Dispose()
        {
            _memoryLoggerFactory?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
