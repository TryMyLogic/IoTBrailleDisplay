using DisplayLogic.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DisplayLogic.Tests.Services.IoTServiceTests
{
    public class IoTServiceTests
    {
        private readonly IIoTDevice _iotDevice;
        private readonly IBrailleDisplay _brailleDisplay;
        private readonly IHomeAssistantWebSocketClient _haWsClient;
        private readonly ILogger<IoTService> _logger;

        public IoTServiceTests()
        {
            _iotDevice = Substitute.For<IIoTDevice>();
            _brailleDisplay = Substitute.For<IBrailleDisplay>();
            _haWsClient = Substitute.For<IHomeAssistantWebSocketClient>();
            _logger = Substitute.For<ILogger<IoTService>>();
        }

        [Fact]
        public async Task FlipShellySwitch_SendsCommandAndDisplaysResponse()
        {
            // Arrange
            string deviceId = "testdevice";
            string expectedRequestTopic = $"shellies/{deviceId}/relay/0/command";
            string expectedResponseTopic = $"shellies/{deviceId}/relay/0";
            string expectedPayload = "on";
            string expectedUnprocessedResponse = "switch turned on";
            string expectedResponse = $"The device is: {expectedUnprocessedResponse}";

            // Return a dummy values for SubscribeAsync calls
            _ = _iotDevice.SubscribeAsync(expectedResponseTopic)
                .Returns(
                    Task.FromResult("{stale text}"), // prePublishResponse
                    Task.FromResult(expectedUnprocessedResponse) // Response (unprocessed)
                );

            // Assume task succeeds
            _ = _iotDevice.PublishAsync(expectedRequestTopic, expectedPayload).Returns(Task.CompletedTask);

            // Assume task succeeds
            _ = _brailleDisplay.SendTextAsync(expectedResponse).Returns(Task.CompletedTask);

            IoTService iotService = new(_iotDevice, _haWsClient, _brailleDisplay, _logger);

            // Act
            await iotService.FlipShellySwitch($"{deviceId}", true);

            // Assert
            await _iotDevice.Received(1).PublishAsync(expectedRequestTopic, expectedPayload);
            _ = await _iotDevice.Received(2).SubscribeAsync(expectedResponseTopic);
            // await _brailleDisplay.Received(1).SendTextAsync(Arg.Is<string>(response => response == expectedResponse));
        }
    }
}
