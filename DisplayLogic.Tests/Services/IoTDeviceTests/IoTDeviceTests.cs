using DisplayLogic.Services;

namespace DisplayLogic.Tests.Services.IoTDeviceTests
{
    public class IoTDeviceTests : IClassFixture<MqttTestFixture>
    {
        private readonly MqttTestFixture _fixture;
        private const string _testBroker = "test.mosquitto.org";
        private const string _testRestEndpoint = "http://localhost/api";

        public IoTDeviceTests(MqttTestFixture fixture)
        {
            _fixture = fixture;
        }

        [SkippableFact]
        public async Task Should_Connect_To_Mqtt_Broker()
        {
            Skip.IfNot(_fixture.IsMqttAvailable, "MQTT broker not available");

            IoTDevice device = new(_testBroker, _testRestEndpoint);
            await device.ConnectAsync();
            Assert.True(device.IsConnected);
        }

        [SkippableFact]
        public async Task Should_Publish_And_Receive_Message()
        {
            Skip.IfNot(_fixture.IsMqttAvailable, "MQTT broker not available");

            string topic = "iot/test/message";
            string expectedPayload = "Hello World";

            IoTDevice device = new(_testBroker, _testRestEndpoint);
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
            Skip.IfNot(_fixture.IsMqttAvailable, "MQTT broker not available");

            IoTDevice device = new(_testBroker, _testRestEndpoint);
            await device.ConnectAsync();
            await device.DisconnectAsync();
            Assert.False(device.IsConnected);
        }

        [SkippableFact]
        public async Task Publish_Should_Throw_If_Not_Connected()
        {
            IoTDevice device = new(_testBroker, _testRestEndpoint);

            _ = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            {
                return device.PublishAsync("iot/test", "payload");
            });
        }
    }
}
