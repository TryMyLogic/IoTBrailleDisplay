using DisplayLogic.Services;
using DisplayLogic.SharedInterfaces;
using DisplayLogic.Tests.SharedTestItems;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Serilog.Events;
using Serilog.Sinks.InMemory;

namespace DisplayLogic.Tests.Services.WebSocketClientTests
{
    public class WebSocketClientTests : IDisposable
    {
        private readonly IUserNotifier _substituteNotifier;
        private readonly string _wsUrl = "ws://localhost:8123/api/websocket";

        // Ensure you run InitializeMemorySinkLogger in each test you want to use it and dispose of at the end.
        private InMemorySink? _memorySink;
        private ILogger<WebSocketClient>? _memoryLogger;
        private ILoggerFactory? _memoryLoggerFactory;

        public WebSocketClientTests()
        {
            _substituteNotifier = Substitute.For<IUserNotifier>();
            _ = _substituteNotifier.NotifyAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(Task.CompletedTask);
        }

        private void InitializeMemorySinkLogger()
        {
            (_memoryLogger, _memorySink, _memoryLoggerFactory) = SharedFunctions.CreateMemorySinkLogger<WebSocketClient>();
        }

        [Fact]
        public void Constructor_ValidParameters_CreatesInstance()
        {
            // Arrange

            // Act
            WebSocketClient? client = new(_wsUrl, _substituteNotifier);

            // Assert
            Assert.NotNull(client);
        }

        [Fact]
        public void Constructor_NullWsUrl_ThrowsArgumentNullException()
        {
            // Arrange

            // Act & Assert
            _ = Assert.Throws<ArgumentNullException>(() =>
            {
                return new WebSocketClient(null!, _substituteNotifier);
            });
        }

        [Fact]
        public void Constructor_NullNotifier_ThrowsArgumentNullException()
        {
            // Arrange

            // Act & Assert
            _ = Assert.Throws<ArgumentNullException>(() =>
            {
                return new WebSocketClient(_wsUrl, null!);
            });
        }

        [Fact]
        public void Constructor_NullLogger_DoesNotThrow()
        {
            // Arrange

            // Act & Assert
            Exception exception = Record.Exception(() =>
            {
                return new WebSocketClient(_wsUrl, _substituteNotifier, null);
            });
            Assert.Null(exception);
        }

        [Fact]
        public async Task StartAsync_ConnectionFails_ThrowsException()
        {
            // Arrange
            string InvalidWsUrl = "ws://unknownhost:8123/api/websocket";
            WebSocketClient client = new(InvalidWsUrl, _substituteNotifier);

            // Act && Assert
            Exception ex = await Assert.ThrowsAnyAsync<Exception>(client.StartAsync);
        }

        [Fact]
        public async Task SendAsync_WhenNotConnected_ThrowsInvalidOperationException()
        {
            // Arrange
            WebSocketClient client = new(_wsUrl, _substituteNotifier);
            string payload = string.Empty;

            // Act & Assert
            _ = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            {
                return client.SendAsync(payload);
            });
            await _substituteNotifier.DidNotReceive().NotifyAsync(Arg.Any<string>(), Arg.Any<string>());
        }

        [Fact]
        public async Task StopAsync_WhenNotConnected_NotifiesStopped()
        {
            // Arrange
            InitializeMemorySinkLogger();
            WebSocketClient client = new(_wsUrl, _substituteNotifier, _memoryLogger);

            // Act
            await client.StopAsync();

            // Assert
            await _substituteNotifier.Received(1).NotifyAsync("WebSocket", "WebSocket stopped.");
            SharedFunctions.AssertLogEventContainsMessage(_memorySink, LogEventLevel.Information, "WebSocket Payload: WebSocket stopped.");

            // Cleanup
            _memoryLoggerFactory?.Dispose();
        }

        // Cannot test exception notification as i cannot purposefully throw an exception

        [Fact]
        public async Task SendAsync_WhenNotConnected_DoesNotLogPayload()
        {
            // Arrange
            InitializeMemorySinkLogger();
            WebSocketClient client = new(_wsUrl, _substituteNotifier, _memoryLogger);
            string payload = "test payload";

            // Act
            _ = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await client.SendAsync(payload);
            });


            // Assert
            await _substituteNotifier.DidNotReceive().NotifyAsync("WebSocket Payload", Arg.Any<string>());
            SharedFunctions.AssertLogEventContainsMessage(_memorySink, LogEventLevel.Information, $"Sent: {payload}", expectedMatchCount: 0);

            // Cleanup
            _memoryLoggerFactory?.Dispose();
        }

        [Fact]
        public async Task StartAsync_ConnectionFails_LogsErrorAndThrows()
        {
            // Arrange
            InitializeMemorySinkLogger();
            string invalidWsUrl = "ws://unknownhost:8123/api/websocket";
            WebSocketClient client = new(invalidWsUrl, _substituteNotifier, _memoryLogger);

            // Act
            Exception? caughtException = null;
            try
            {
                await client.StartAsync();
            }
            catch (Exception ex)
            {
                caughtException = ex;
            }

            // Assert
            Assert.NotNull(caughtException);
            await _substituteNotifier.Received(0).NotifyAsync("WebSocket", "WebSocket connected.");
            SharedFunctions.AssertLogEventContainsMessage(_memorySink, LogEventLevel.Information, "WebSocket connected to URL", expectedMatchCount: 0);
            SharedFunctions.AssertLogEventContainsMessage(_memorySink, LogEventLevel.Error, "Failed to start WebSocket connection", expectedMatchCount: 1);

            // Cleanup
            _memoryLoggerFactory?.Dispose();
        }

        [Fact]
        public async Task StopAsync_WhenNotConnected_DoesNotNotifyError()
        {
            // Arrange
            InitializeMemorySinkLogger();
            WebSocketClient client = new(_wsUrl, _substituteNotifier, _memoryLogger);

            // Act
            await client.StopAsync();

            // Assert
            await _substituteNotifier.Received(1).NotifyAsync("WebSocket", "WebSocket stopped.");
            await _substituteNotifier.DidNotReceive().NotifyAsync("WebSocket", Arg.Is<string>(s => s.StartsWith("Error stopping WebSocket")));
            SharedFunctions.AssertLogEventContainsMessage(_memorySink, LogEventLevel.Information, "WebSocket Payload: WebSocket stopped.", expectedMatchCount: 1);
            SharedFunctions.AssertLogEventContainsMessage(_memorySink, LogEventLevel.Error, "Error stopping WebSocket", expectedMatchCount: 0);

            // Cleanup
            _memoryLoggerFactory?.Dispose();
        }

        [Fact]
        public async Task StopAsync_WhenNotConnected_DoesNotLogClosedGracefully()
        {
            // Arrange
            InitializeMemorySinkLogger();
            WebSocketClient client = new(_wsUrl, _substituteNotifier, _memoryLogger);

            // Act
            await client.StopAsync();

            // Assert
            await _substituteNotifier.Received(1).NotifyAsync("WebSocket", "WebSocket stopped.");
            SharedFunctions.AssertLogEventContainsMessage(_memorySink, LogEventLevel.Information, "WebSocket Payload: WebSocket stopped.", expectedMatchCount: 1);
            SharedFunctions.AssertLogEventContainsMessage(_memorySink, LogEventLevel.Information, "WebSocket closed gracefully.", expectedMatchCount: 0);

            // Cleanup
            _memoryLoggerFactory?.Dispose();
        }

        [Fact]
        public void SubscribingToPayloadEvent_BeforeConnection_DoesNotThrow()
        {
            // Arrange
            InitializeMemorySinkLogger();
            WebSocketClient client = new(_wsUrl, _substituteNotifier, _memoryLogger);
            bool eventTriggered = false;
            string? receivedPayload = null;

            // Act
            Exception? exception = Record.Exception(() =>
            {
                client.PayloadReceived += (payload) =>
                {
                    eventTriggered = true;
                    receivedPayload = payload;
                };
            });

            // Assert
            Assert.Null(exception);
            Assert.False(eventTriggered);
            Assert.Null(receivedPayload);
            SharedFunctions.AssertLogEventContainsMessage(_memorySink, LogEventLevel.Information, "WebSocket Payload", expectedMatchCount: 0);

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
