using DisplayLogic.Services;
using DisplayLogic.Tests.Mocks;
using MQTTnet;

namespace DisplayLogic.Tests.Services.BluetoothFunctionalityTests
{
    public class BrailleDisplayTests
    {
        private bool IsMqttAvailable(string host, int port)
        {
            try
            {
                using var client = new System.Net.Sockets.TcpClient();
                Task task = client.ConnectAsync(host, port);
                return task.Wait(500);
            }
            catch
            {
                return false;
            }
        }
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

        [SkippableFact]
        public async Task BrailleDisplay_SendAndReceiveText_Mqtt_Integration_Works()
        {
            Skip.IfNot(IsMqttAvailable("localhost", 1883), "MQTT broker not running on localhost:1883");
            //Arrange
            IMqttClient client = new MqttClientFactory().CreateMqttClient();
            MqttClientOptions options = new MqttClientOptionsBuilder()
                .WithTcpServer("localhost", 1883)
                .WithClientId("TestClient")
                .Build();

            var strategy = new MqttConnectionStrategy(client);
            BrailleDisplay braille = new(strategy);

            await braille.ConnectAsync();

            //Act
            const string message = "Integration Test";

            await braille.SendTextAsync(message);

            IMqttClient responder = new MqttClientFactory().CreateMqttClient();
            await responder.ConnectAsync(options);
            MqttApplicationMessage msg = new MqttApplicationMessageBuilder()
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