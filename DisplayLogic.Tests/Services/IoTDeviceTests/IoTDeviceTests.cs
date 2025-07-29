using DisplayLogic.Services;

namespace DisplayLogic.Tests.Services.IoTDeviceTests
{
    public class IoTDeviceTests
    {
        private const string TestBroker = "mosquitto";
        private const string TestRestEndpoint = "http://localhost/api";

        [SkippableFact]
        public async Task Should_Connect_To_Mqtt_Broker()
        {
            IoTDevice device = new(TestBroker, TestRestEndpoint);
            await device.ConnectAsync();

            Assert.True(device.IsConnected);
        }

        [SkippableFact]
        public async Task Should_Publish_And_Receive_Message()
        {
            string topic = "iot/test/message";
            string expectedPayload = "Hello World";

            IoTDevice device = new(TestBroker, TestRestEndpoint);
            await device.ConnectAsync();

            // Use Task.Run to simulate publish delay
            _ = Task.Run(async () =>
            {
                await Task.Delay(1000);
                await device.PublishAsync(topic, expectedPayload);
            });

            string received = await device.SubscribeAsync(topic);
            Assert.Equal(expectedPayload, received);
        }

        [SkippableFact]
        public async Task Should_Disconnect_From_Mqtt()
        {
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
