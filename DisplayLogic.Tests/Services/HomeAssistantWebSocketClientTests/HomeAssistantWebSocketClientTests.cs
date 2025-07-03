using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DisplayLogic.Services;
using DisplayLogic.SharedInterfaces;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DisplayLogic.Tests.Services.HomeAssistantWebSocketClientTests
{
    public class HomeAssistantWebSocketClientTests
    {
        private readonly IWebSocketClient _webSocketClient;
        private readonly IUserNotifier _notifier;
        private readonly ILogger<HomeAssistantWebSocketClient> _logger;
        private readonly string _accessToken = "test-token";
        private readonly HomeAssistantWebSocketClient _client;

        public HomeAssistantWebSocketClientTests()
        {
            _webSocketClient = Substitute.For<IWebSocketClient>();
            _notifier = Substitute.For<IUserNotifier>();
            _logger = Substitute.For<ILogger<HomeAssistantWebSocketClient>>();
            _client = new HomeAssistantWebSocketClient(_webSocketClient, _notifier, _accessToken, _logger);
        }

        [Fact]
        public void Constructor_NullWebSocketClient_ThrowsArgumentNullException()
        {
            _ = Assert.Throws<ArgumentNullException>(() =>
            {
                return new HomeAssistantWebSocketClient(null!, _notifier, _accessToken);
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
                return new HomeAssistantWebSocketClient(_webSocketClient, _notifier, null!);
            });
        }

        [Fact]
        public void Constructor_NullLogger_DoesNotThrow()
        {
            // Arrange

            // Act & Assert
            Exception exception = Record.Exception(() =>
            {
                return new HomeAssistantWebSocketClient(_webSocketClient, _notifier, _accessToken, null!);
            });
            Assert.Null(exception);
        }

        // Not worth testing StartAsync and StopAsync as they are currently WebSocketClient wrappers
    }
}
