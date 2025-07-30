using DisplayLogic.Services;

namespace DisplayLogic.Tests.Services.IoTDeviceTests
{
    public class IoTDeviceTests
    {
        private const string TestBroker = "localhost";
        private const string TestRestEndpoint = "http://localhost/api";

        private static readonly bool s_mqttAvailable = IsMqttBrokerAvailable();

        private static bool IsMqttBrokerAvailable()
        {
            try
            {
                using var client = new System.Net.Sockets.TcpClient();
                client.Connect(TestBroker, 1883);
                return false;
            }
            catch
            {
                return true;
            }
        }

        [SkippableFact]
        public async Task Should_Connect_To_Mqtt_Broker()
        {
            Skip.If(s_mqttAvailable, "MQTT broker not available");

            IoTDevice device = new(TestBroker, TestRestEndpoint);
            await device.ConnectAsync();
            Assert.True(device.IsConnected);
        }

        [SkippableFact]
        public async Task Should_Publish_And_Receive_Message()
        {
            Skip.If(s_mqttAvailable, "MQTT broker not available");

            string topic = "iot/test/message";
            string expectedPayload = "Hello World";

            IoTDevice device = new(TestBroker, TestRestEndpoint);
            await device.ConnectAsync();

            Task<string> subscribeTask = device.SubscribeAsync(topic);
            await Task.Delay(2000);
            await device.PublishAsync(topic, expectedPayload);

            string received = await subscribeTask.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(expectedPayload, received);
        }

        [SkippableFact]
        public async Task Should_Disconnect_From_Mqtt()
        {
            Skip.If
                (s_mqttAvailable, "MQTT broker not available");

            IoTDevice device = new(TestBroker, TestRestEndpoint);
            await device.ConnectAsync();
            await device.DisconnectAsync();
            Assert.False(device.IsConnected);
        }

        [SkippableFact]
        public async Task Publish_Should_Throw_If_Not_Connected()
        {
            IoTDevice device = new(TestBroker, TestRestEndpoint);

            _ = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            {
                return device.PublishAsync("iot/test", "payload");
            });
        }
    }
}
