using DisplayLogic.Services;
using DisplayLogic.SharedInterfaces;
using NSubstitute;

namespace DisplayLogic.Tests.Services.WebSocketClientTests
{
    public class WebSocketClientTests
    {
        private readonly IUserNotifier _substituteNotifier;
        private readonly string _wsUrl = "ws://localhost:8123/api/websocket";

        public WebSocketClientTests()
        {
            _substituteNotifier = Substitute.For<IUserNotifier>();
            _ = _substituteNotifier.NotifyAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(Task.CompletedTask);
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
        public async Task StartAsync_ConnectionFails_NotifiesError()
        {
            // Arrange
            string InvalidWsUrl = "ws://unknownhost:8123/api/websocket";
            string expectedMessage = "Connection error: Unable to connect to the remote server";
            WebSocketClient client = new(InvalidWsUrl, _substituteNotifier);

            // Act
            await client.StartAsync();

            // Assert
            await _substituteNotifier.Received(1).NotifyAsync("WebSocket Payload", Arg.Is<string>(message => message == expectedMessage));
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
    }
}
