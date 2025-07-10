using DisplayLogic.Tests.Mocks;
using DisplayLogic.Services;
using MQTTnet;

namespace DisplayLogic.Tests.Services.BluetoothFunctionalityTests
{
    public class BrailleDisplayTests
    {
        [Fact]
        public async Task ConnectAsync_ShouldSetIsConnected_WhenMockSucceeds()
        {
            var mock = new MockBluetoothConnection(connectSucceed: true);
            var braille = new BrailleDisplay(mock);

            //Act
            await braille.ConnectAsync();

            //Assert
            Assert.True(braille.IsConnected);
        }

        [Fact]
        public async Task ConnectAsync_ShouldNotSetIsConnected_WhenMockFails()
        {
            var mock = new MockBluetoothConnection(connectSucceed: false);
            var braille = new BrailleDisplay(mock);

            //Act
            await braille.ConnectAsync();

            //Assert
            Assert.False(braille.IsConnected);
        }

        [Fact]
        public async Task BrailleDisplay_Integration_SendAndReceiveText_WorksCorrectly()
        {
            var mock = new MockBluetoothConnection();
            var braille = new BrailleDisplay(mock);
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
        public async Task BrailleDisplay_SendAndReceiveText_Mqtt_Integration_Works()
        {
            //Arrange
            var client = new MqttClientFactory().CreateMqttClient();
            var options = new MqttClientOptionsBuilder()
                .WithTcpServer("localhost", 1883)
                .WithClientId("TestClient")
                .Build();

            var strategy = new MqttConnectionStrategy(client);
            BrailleDisplay braille = new BrailleDisplay(strategy);

            await braille.ConnectAsync();

            //Act
            const string message = "Integration Test";

            await braille.SendTextAsync(message);

            var responder = new MqttClientFactory().CreateMqttClient();
            await responder.ConnectAsync(options);
            var msg = new MqttApplicationMessageBuilder()
                .WithTopic("braille/receive")
                .WithPayload(message)
                .Build();
            await responder.PublishAsync(msg, CancellationToken.None);

            await Task.Delay(200);

            //Assert
            string received = await braille.ReceiveTextAsync();
            Assert.Equal(message, received);
        }

        [Fact]
        public async Task DisconnectAsync_ShouldSetIsConnectedFalse()
        {
            var mock = new MockBluetoothConnection(connectSucceed: true);
            var braille = new BrailleDisplay(mock);

            //Act
            await braille.ConnectAsync();
            await braille.DisconnectAsync();

            //Assert
            Assert.False(braille.IsConnected);
        }
    }
}