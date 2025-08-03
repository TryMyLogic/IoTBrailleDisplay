using DisplayLogic.Models;
using DisplayLogic.Services;
using DisplayLogic.SharedInterfaces;
using DisplayLogic.Tests.SharedTestItems;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Serilog.Events;
using Serilog.Sinks.InMemory;
using Xunit.Abstractions;

namespace DisplayLogic.Tests.Services.HomeAssistantWebSocketClientTests
{
    public class HomeAssistantWebSocketClientIntegrationTests : IClassFixture<ServiceFixture>, IDisposable
    {
        // Only writes logs to console. Does not store for inspection. Use memory sink for that
        private readonly ILogger<HomeAssistantWebSocketClient> _testLogger;
        private readonly ILoggerFactory _loggerFactory;

        // Ensure you run InitializeMemorySinkLogger in each test you want to use it and dispose of at the end.
        private InMemorySink? _memorySink;
        private ILogger<HomeAssistantWebSocketClient>? _memoryLogger;
        private ILoggerFactory? _memoryLoggerFactory;

        private readonly bool _shouldSkipHATests;

        private readonly IUserNotifier _userNotifier;
        private readonly string _wsUrl;
        private readonly string _accessToken;
        private WebSocketClient? _webSocketClient;
        private HomeAssistantWebSocketClient? _homeAssistantWebSocketClient;

        public HomeAssistantWebSocketClientIntegrationTests(ServiceFixture fixture, ITestOutputHelper output)
        {
            (ILogger<HomeAssistantWebSocketClient> logger, ILoggerFactory loggerFactory) = SharedFunctions.CreateTestLogger<HomeAssistantWebSocketClient>(output);
            _testLogger = logger;
            _loggerFactory = loggerFactory;

            _shouldSkipHATests = !fixture.CanConnectToHomeAssistant;

            _userNotifier = Substitute.For<IUserNotifier>();
            _wsUrl = "ws://localhost:8123/api/websocket";
            _accessToken = ServiceFixture.HomeAssistantAccessToken;
        }

        private void InitializeMemorySinkLogger()
        {
            (ILogger<HomeAssistantWebSocketClient> memoryLogger, InMemorySink memorySink, ILoggerFactory loggerFactory) = SharedFunctions.CreateMemorySinkLogger<HomeAssistantWebSocketClient>();
            _memoryLogger = memoryLogger;
            _memorySink = memorySink;
            _memoryLoggerFactory = loggerFactory;
        }

        [SkippableFact]
        public async Task ConnectAsync_WithValidConnection_CompletesInitialization()
        {
            // Removed skip wrapping. Does not seem to be required
            Skip.If(_shouldSkipHATests, "Home Assistant is not available. Skipping this test");

            // Arrange
            InitializeMemorySinkLogger();
            _webSocketClient = new(_wsUrl, _userNotifier);
            _homeAssistantWebSocketClient = new(_webSocketClient, _userNotifier, _accessToken, _memoryLogger);

            // Act
            try
            {
                Task connectTask = _homeAssistantWebSocketClient.ConnectAsync();
                Task completed = await Task.WhenAny(connectTask, Task.Delay(45000)); // Extended timeout for diagnostics compared to normal timeout

                if (completed != connectTask)
                {
                    Assert.Fail("Test timed out waiting for ConnectAsync to complete after 45 seconds.");
                }

                await connectTask; // Propagate any exceptions
            }
            catch (Exception ex)
            {
                Assert.Fail($"Error caught while connecting to the broker: {ex.Message}");
            }

            // Assert
            _testLogger.LogInformation("IsConnected: {IsConnected}, Devices Count: {DevicesCount}, Areas Count: {AreasCount}", _homeAssistantWebSocketClient.IsConnected, _homeAssistantWebSocketClient.Devices?.Count ?? 0, _homeAssistantWebSocketClient.Areas?.Count ?? 0);

            SharedFunctions.AssertLogEventContainsMessage(_memorySink, LogEventLevel.Information, "Sending auth command", expectedMatchCount: 1);
            SharedFunctions.AssertLogEventContainsMessage(_memorySink, LogEventLevel.Information, "Sending device registry command", expectedMatchCount: 1);
            SharedFunctions.AssertLogEventContainsMessage(_memorySink, LogEventLevel.Information, "Sending area registry command", expectedMatchCount: 1);
            SharedFunctions.AssertLogEventContainsMessage(_memorySink, LogEventLevel.Information, "Authentication successful, waiting for registry data", expectedMatchCount: 1);

            Assert.True(_homeAssistantWebSocketClient.IsConnected, "Client did not connect successfully.");
            Assert.NotNull(_homeAssistantWebSocketClient.Devices);
            Assert.NotEmpty(_homeAssistantWebSocketClient.Devices);
            Assert.NotNull(_homeAssistantWebSocketClient.Areas);
            Assert.NotEmpty(_homeAssistantWebSocketClient.Areas);

            // Cleanup
            try
            {
                await _homeAssistantWebSocketClient.StopAsync();
            }
            catch (Exception ex)
            {
                Assert.Fail($"Error stopping WebSocket client during test cleanup: {ex.Message}");
            }

            _memoryLoggerFactory?.Dispose();
        }

        [SkippableFact]
        public async Task ConnectAsync_WithInvalidToken_TriggersAuthFailure()
        {
            Skip.If(_shouldSkipHATests, "Home Assistant is not available. Skipping this test");

            // Arrange
            InitializeMemorySinkLogger();
            _webSocketClient = new(_wsUrl, _userNotifier);
            _homeAssistantWebSocketClient = new(_webSocketClient, _userNotifier, "invalid_token", _memoryLogger);

            // Act
            Exception? caughtException = null;
            try
            {
                Task connectTask = _homeAssistantWebSocketClient.ConnectAsync();
                Task completed = await Task.WhenAny(connectTask, Task.Delay(45000)); // Extended timeout for diagnostics compared to normal timeout

                if (completed != connectTask)
                {
                    Assert.Fail("Test timed out waiting for ConnectAsync to complete after 45 seconds.");
                }

                await connectTask; // Propagate any exceptions
            }
            catch (Exception ex)
            {
                caughtException = ex;
            }

            // Assert
            Assert.NotNull(caughtException);
            _ = Assert.IsType<InvalidOperationException>(caughtException);
            Assert.Equal("WebSocket auth failed: Invalid token", caughtException.Message);
            SharedFunctions.AssertSingleLogEvent(_memorySink, LogEventLevel.Error, "WebSocket auth failed: Invalid token");
            Assert.False(_homeAssistantWebSocketClient.IsConnected, "Client should not be connected after auth failure");

            // Cleanup
            try
            {
                await _homeAssistantWebSocketClient.StopAsync();
            }
            catch (Exception ex)
            {
                Assert.Fail($"Error stopping WebSocket client during test cleanup: {ex.Message}");
            }

            _memoryLoggerFactory?.Dispose();
        }

        public void Dispose()
        {
            _loggerFactory?.Dispose();
            _memoryLoggerFactory?.Dispose();
            GC.SuppressFinalize(this);
        }

    }
}
