using DisplayLogic.Services;
using DisplayLogic.SharedInterfaces;
using DisplayLogic.Tests.SharedTestItems;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Serilog.Events;
using Serilog.Sinks.InMemory;
using Xunit.Abstractions;

namespace DisplayLogic.Tests.Services.WebSocketClientTests
{
    public class WebSocketClientIntegrationTests : IClassFixture<ServiceFixture>, IDisposable
    {
        private readonly IUserNotifier _substituteNotifier;
        private readonly string _wsUrl = "ws://localhost:8123/api/websocket";
        private readonly bool _shouldSkipHATests;

        // Ensure you run InitializeMemorySinkLogger in each test you want to use it and dispose of at the end.
        private InMemorySink? _memorySink;
        private ILogger<WebSocketClient>? _memoryLogger;
        private ILoggerFactory? _memoryLoggerFactory;

        public WebSocketClientIntegrationTests(ServiceFixture fixture, ITestOutputHelper output)
        {
            _shouldSkipHATests = !fixture.CanConnectToHomeAssistant;
            _substituteNotifier = Substitute.For<IUserNotifier>();
            _ = _substituteNotifier.NotifyAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(Task.CompletedTask);
        }

        private void InitializeMemorySinkLogger()
        {
            (_memoryLogger, _memorySink, _memoryLoggerFactory) = SharedFunctions.CreateMemorySinkLogger<WebSocketClient>();
        }

        [SkippableFact]
        public async Task StartAsync_WithValidUrl_ConnectsAndLogs()
        {
            Skip.If(_shouldSkipHATests, "Home Assistant is not available. Skipping this test");

            // Arrange
            InitializeMemorySinkLogger();
            WebSocketClient client = new(_wsUrl, _substituteNotifier, _memoryLogger);

            // Act
            try
            {
                Task connectTask = client.StartAsync();
                Task completed = await Task.WhenAny(connectTask, Task.Delay(45000)); // Extended timeout for diagnostics compared to normal timeout
                if (completed != connectTask)
                {
                    Assert.Fail("Test timed out waiting for StartAsync to complete after 45 seconds.");
                }
                await connectTask; // Propagate any exceptions
            }
            catch (Exception ex)
            {
                Assert.Fail($"Error connecting WebSocket: {ex.Message}");
            }

            // Assert
            await _substituteNotifier.Received(1).NotifyAsync("WebSocket", "WebSocket connected.");
            SharedFunctions.AssertLogEventContainsMessage(_memorySink, LogEventLevel.Information, "WebSocket connected to URL", expectedMatchCount: 1);
            SharedFunctions.AssertLogEventContainsMessage(_memorySink, LogEventLevel.Information, "WebSocket Payload: WebSocket connected.", expectedMatchCount: 1);

            // Cleanup
            try
            {
                await client.StopAsync();
            }
            catch (Exception ex)
            {
                Assert.Fail($"Error stopping WebSocket client during test cleanup: {ex.Message}");
            }

            _memoryLoggerFactory?.Dispose();
        }

        [SkippableFact]
        public async Task SendAsync_WithConnectedClient_SendsAndLogsPayload()
        {
            Skip.If(_shouldSkipHATests, "Home Assistant is not available. Skipping this test");

            // Arrange
            InitializeMemorySinkLogger();
            WebSocketClient client = new(_wsUrl, _substituteNotifier, _memoryLogger);
            string payload = "test payload";

            try
            {
                Task connectTask = client.StartAsync();
                Task completed = await Task.WhenAny(connectTask, Task.Delay(45000));
                if (completed != connectTask)
                {
                    Assert.Fail("Test timed out waiting for StartAsync to complete after 45 seconds.");
                }
                await connectTask;
            }
            catch (Exception ex)
            {
                Assert.Fail($"Error connecting WebSocket: {ex.Message}");
            }

            // Act
            try
            {
                Task sendTask = client.SendAsync(payload);
                Task completed = await Task.WhenAny(sendTask, Task.Delay(45000));
                if (completed != sendTask)
                {
                    Assert.Fail("Test timed out waiting for SendAsync to complete after 45 seconds.");
                }
                await sendTask;
            }
            catch (Exception ex)
            {
                Assert.Fail($"Error sending payload: {ex.Message}");
            }

            // Assert
            // await _substituteNotifier.Received(1).NotifyAsync("WebSocket Payload", $"Sent: {payload}");
            SharedFunctions.AssertLogEventContainsMessage(_memorySink, LogEventLevel.Information, $"Sent: {payload}", expectedMatchCount: 1);

            // Cleanup
            try
            {
                await client.StopAsync();
            }
            catch (Exception ex)
            {
                Assert.Fail($"Error stopping WebSocket client during test cleanup: {ex.Message}");
            }

            _memoryLoggerFactory?.Dispose();
        }

        [SkippableFact]
        public async Task StopAsync_WithConnectedClient_ClosesAndLogs()
        {
            Skip.If(_shouldSkipHATests, "Home Assistant WebSocket server is not available. Skipping this test");

            // Arrange
            InitializeMemorySinkLogger();
            WebSocketClient client = new(_wsUrl, _substituteNotifier, _memoryLogger);

            try
            {
                Task connectTask = client.StartAsync();
                Task completed = await Task.WhenAny(connectTask, Task.Delay(45000));
                if (completed != connectTask)
                {
                    Assert.Fail("Test timed out waiting for StartAsync to complete after 45 seconds.");
                }
                await connectTask;
            }
            catch (Exception ex)
            {
                Assert.Fail($"Error connecting WebSocket: {ex.Message}");
            }

            // Act
            try
            {
                Task stopTask = client.StopAsync();
                Task completed = await Task.WhenAny(stopTask, Task.Delay(45000));
                if (completed != stopTask)
                {
                    Assert.Fail("Test timed out waiting for StopAsync to complete after 45 seconds.");
                }
                await stopTask;
            }
            catch (Exception ex)
            {
                Assert.Fail($"Error stopping WebSocket: {ex.Message}");
            }

            // Assert
            await _substituteNotifier.Received(1).NotifyAsync("WebSocket", "WebSocket stopped.");

            // Note: Home Assistant server closes the connection immediately after StartAsync due to missing authentication,
            // so _socket.State is not Open, and "WebSocket closed gracefully" is not logged.
            // This is covered by HomeAssistantWebSocketClientIntegrationTests, which handles authentication.
            //SharedFunctions.AssertLogEventContainsMessage(_memorySink, LogEventLevel.Information, "WebSocket closed gracefully.", expectedMatchCount: 1);

            SharedFunctions.AssertLogEventContainsMessage(_memorySink, LogEventLevel.Information, "WebSocket Payload: WebSocket stopped.", expectedMatchCount: 1);
            SharedFunctions.AssertLogEventContainsMessage(_memorySink, LogEventLevel.Error, "Error stopping WebSocket", expectedMatchCount: 0);

            // Cleanup
            _memoryLoggerFactory?.Dispose();
        }

        // Similarly to StopAsync, no integration test for the payload as that is HomeAssistantIntegrationTests domain. 

        public void Dispose()
        {
            _memoryLoggerFactory?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
