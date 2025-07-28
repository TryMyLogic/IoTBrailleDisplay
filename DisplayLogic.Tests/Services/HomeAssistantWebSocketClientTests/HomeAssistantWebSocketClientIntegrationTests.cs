//using DisplayLogic.Models;
//using DisplayLogic.Services;
//using DisplayLogic.SharedInterfaces;
//using DisplayLogic.Tests.SharedTestItems;
//using Microsoft.Extensions.Logging;
//using NSubstitute;
//using Serilog.Events;
//using Serilog.Sinks.InMemory;
//using Xunit.Abstractions;

//namespace DisplayLogic.Tests.Services.HomeAssistantWebSocketClientTests
//{
//    public class HomeAssistantWebSocketClientIntegrationTests : IClassFixture<ServiceFixture>, IDisposable
//    {
//        // Only writes logs to console. Does not store for inspection. Use memory sink for that
//        private readonly ILogger<HomeAssistantWebSocketClient> _testLogger;
//        private readonly ILoggerFactory _loggerFactory;

//        // Ensure you run InitializeMemorySinkLogger in each test you want to use it and dispose of at the end.
//        private InMemorySink? _memorySink;
//        private ILogger<HomeAssistantWebSocketClient>? _memoryLogger;
//        private ILoggerFactory? _memoryLoggerFactory;

//        private readonly bool _shouldSkipHATests;

//        private readonly IUserNotifier _userNotifier;
//        private readonly string _wsUrl;
//        private readonly string _accessToken;
//        private WebSocketClient? _webSocketClient;
//        private HomeAssistantWebSocketClient? _homeAssistantWebSocketClient;

//        public HomeAssistantWebSocketClientIntegrationTests(ServiceFixture fixture, ITestOutputHelper output)
//        {
//            (ILogger<HomeAssistantWebSocketClient> logger, ILoggerFactory loggerFactory) = SharedFunctions.CreateTestLogger<HomeAssistantWebSocketClient>(output);
//            _testLogger = logger;
//            _loggerFactory = loggerFactory;

//            _shouldSkipHATests = !fixture.CanConnectToHomeAssistant;

//            _userNotifier = Substitute.For<IUserNotifier>();
//            _wsUrl = "ws://localhost:8123/api/websocket";
//            _accessToken = ServiceFixture.HomeAssistantAccessToken;
//        }

//        private void InitializeMemorySinkLogger()
//        {
//            (ILogger<HomeAssistantWebSocketClient> memoryLogger, InMemorySink memorySink, ILoggerFactory loggerFactory) = SharedFunctions.CreateMemorySinkLogger<HomeAssistantWebSocketClient>();
//            _memoryLogger = memoryLogger;
//            _memorySink = memorySink;
//            _memoryLoggerFactory = loggerFactory;
//        }

//        [SkippableFact]
//        public async Task StartAsync_WithValidConnection_TriggersAuthAndDeviceRegistry()
//        {
//            // You can directly use Skip.If, however since this task is async - it was wrapped in an if statement to wait for completion
//            if (_shouldSkipHATests)
//            {
//                Skip.If(true, "Home Assistant is not available. Skipping this test");
//            }

//            // Arrange
//            InitializeMemorySinkLogger();
//            _webSocketClient = new(_wsUrl, _userNotifier);
//            _homeAssistantWebSocketClient = new(_webSocketClient, _userNotifier, _accessToken, _memoryLogger);

//            bool deviceRegistryReceived = false;
//            List<MqttDevice>? receivedDeviceRegistry = null;
//            _homeAssistantWebSocketClient.DeviceRegistryReceivedAndProcessed += (mqttDevices) =>
//            {
//                deviceRegistryReceived = true;
//                receivedDeviceRegistry = mqttDevices;
//            };

//            // Act
//            Task startTask = _homeAssistantWebSocketClient.StartAsync();

//            try
//            {
//                // Wait for auth success or fail fast on invalid token
//                await _homeAssistantWebSocketClient.WaitForAuthCompletionAsync();
//            }
//            catch (Exception ex)
//            {
//                await _homeAssistantWebSocketClient.StopAsync();
//                Assert.Fail($"{ex.Message}");
//            }

//            Task waitTask = Task.Run(async () =>
//            {
//                while (!deviceRegistryReceived)
//                {
//                    await Task.Delay(50);
//                }
//            });

//            Task completed = await Task.WhenAny(waitTask, Task.Delay(30000));
//            await _homeAssistantWebSocketClient.StopAsync();

//            if (completed != waitTask)
//            {
//                Assert.Fail("Timed out waiting for DeviceRegistryReceived event.");
//            }

//            // Assert logs
//            SharedFunctions.AssertLogEventContainsMessage(_memorySink, LogEventLevel.Information, "Sending auth command", debugLogger: _testLogger, expectedMatchCount: 1);
//            SharedFunctions.AssertLogEventContainsMessage(_memorySink, LogEventLevel.Information, "Sending device registry command", debugLogger: _testLogger, expectedMatchCount: 1);

//            Assert.True(deviceRegistryReceived, "DeviceRegistryReceivedAndProcessed event was not triggered.");
//            Assert.NotNull(receivedDeviceRegistry);
//            Assert.NotEmpty(receivedDeviceRegistry);

//            _memoryLoggerFactory?.Dispose();
//        }

//        [SkippableFact]
//        public async Task StartAsync_WithInvalidToken_TriggersAuthFailure()
//        {
//            _testLogger.LogInformation("Should skip: {ShouldSkip}", _shouldSkipHATests);
//            Skip.If(_shouldSkipHATests, "Home Assistant is not available. Skipping this test");

//            // Arrange
//            InitializeMemorySinkLogger();

//            _webSocketClient = new WebSocketClient(_wsUrl, _userNotifier, null);
//            _homeAssistantWebSocketClient = new HomeAssistantWebSocketClient(_webSocketClient, _userNotifier, "invalid_token", _memoryLogger);

//            // Act
//            Task startTask = _homeAssistantWebSocketClient.StartAsync();
//            Exception? caughtException = null;
//            try
//            {
//                await _homeAssistantWebSocketClient.WaitForAuthCompletionAsync();
//            }
//            catch (Exception ex)
//            {
//                caughtException = ex;
//            }
//            await _homeAssistantWebSocketClient.StopAsync();

//            // Assert
//            Assert.NotNull(caughtException);
//            _ = Assert.IsType<InvalidOperationException>(caughtException);
//            Assert.Equal("WebSocket auth failed: Invalid token.", caughtException.Message);
//            await _userNotifier.Received(1).NotifyAsync("Error", Arg.Is<string>(s => s.Contains("WebSocket auth failed: Invalid token")));
//            SharedFunctions.AssertSingleLogEvent(_memorySink, LogEventLevel.Error, "WebSocket auth failed: Invalid token");

//            _memoryLoggerFactory?.Dispose();
//        }

//        public void Dispose()
//        {
//            _loggerFactory?.Dispose();
//            _memoryLoggerFactory?.Dispose();
//            GC.SuppressFinalize(this);
//        }

//    }
//}
