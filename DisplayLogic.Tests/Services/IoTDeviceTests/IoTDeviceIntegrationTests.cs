using DisplayLogic.Services;
using DisplayLogic.Tests.SharedTestItems;
using Xunit.Abstractions;

namespace DisplayLogic.Tests.Services.IoTDeviceTests
{
    public class IoTDeviceIntegrationTests : IClassFixture<ServiceFixture>
    {
        private const string TestBroker = "localhost";
        private const string TestRestEndpoint = "http://localhost/api";

        private readonly bool _shouldSkipMQTTests;
        public IoTDeviceIntegrationTests(ServiceFixture fixture, ITestOutputHelper output)
        {
            _shouldSkipMQTTests = !fixture.CanConnectToMQTTBroker;
        }

        [SkippableFact]
        public async Task Should_Connect_To_Mqtt_Broker()
        {
            Skip.If(_shouldSkipMQTTests, "MQTT broker not available");

            IoTDevice device = new(TestBroker, TestRestEndpoint);
            await device.ConnectAsync();
            Assert.True(device.IsConnected);
        }

        [SkippableFact]
        public async Task Should_Publish_And_Receive_Message()
        {
            Skip.If(_shouldSkipMQTTests, "MQTT broker not available");

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
            Skip.If(_shouldSkipMQTTests, "MQTT broker not available");

            IoTDevice device = new(TestBroker, TestRestEndpoint);
            await device.ConnectAsync();
            await device.DisconnectAsync();
            Assert.False(device.IsConnected);
        }

    }
}
