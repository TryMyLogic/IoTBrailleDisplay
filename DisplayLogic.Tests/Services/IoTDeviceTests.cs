using System.Net;
using DisplayLogic.Services;
using DisplayLogic.Tests.TestUtils;

namespace DisplayLogic.Tests.Services
{
    public class IoTDeviceTests
    {
        private const string TestBroker = "test.mosquitto.org";
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

        [Fact]
        public void Should_Map_Mqtt_Topic_To_Rest_Endpoint()
        {
            IoTDevice device = new(TestBroker, TestRestEndpoint);
            string topic = "iot/devices/status";

            System.Reflection.MethodInfo? method = typeof(IoTDevice).GetMethod("MapTopicToEndpoint", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(method);
            string? restUrl = method.Invoke(device, [topic]) as string;
            Assert.NotNull(restUrl);

            Assert.Equal("http://localhost/api/devices-status", restUrl);
        }

        [Fact]
        public async Task Should_Fallback_To_Rest_When_Mqtt_Unavailable()
        {
            // Inject fake REST client
            FakeHttpHandler fakeHandler = new(HttpStatusCode.OK, "Fallback successful");
            HttpClient httpClient = new(fakeHandler);

            // Use an unreachable MQTT broker to simulate fallback
            IoTDevice device = new("invalid-broker", TestRestEndpoint, httpClient: httpClient);

            System.Reflection.MethodInfo? method = typeof(IoTDevice).GetMethod("MapTopicToEndpoint", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(method);
            string? restUrl = method.Invoke(device, ["iot/device/test"])!.ToString();

            string response = await httpClient.GetStringAsync(restUrl);
            Assert.Equal("Fallback successful", response);
        }

        [SkippableFact]
        public async Task Should_Disconnect_From_Mqtt()
        {
            IoTDevice device = new(TestBroker, TestRestEndpoint);
            await device.ConnectAsync();

            await device.DisconnectAsync();

            Assert.False(device.IsConnected);
        }

        [Fact]
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