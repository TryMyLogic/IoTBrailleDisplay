using DisplayLogic.Services;

namespace DisplayLogic.Tests.Services.IoTDeviceTests
{
    public class IoTDeviceTests
    {
        private readonly string _testBroker = Environment.GetEnvironmentVariable("MQTT_BROKER") ?? "localhost";
        private readonly string _testRestEndpoint = Environment.GetEnvironmentVariable("REST_ENDPOINT") ?? "http://localhost/api";

        [SkippableFact]
        public async Task Should_Connect_To_Mqtt_Broker()
        {
            try
            {
                IoTDevice device = new(_testBroker, _testRestEndpoint);
                await device.ConnectAsync();
                Assert.True(device.IsConnected);
            }
            catch (Exception ex) when (ex.Message.Contains("Could not connect to MQTT broker"))
            {
                throw new SkipException($"MQTT broker is not available: {ex.Message}");
            }
        }

        [SkippableFact]
        public async Task Should_Publish_And_Receive_Message()
        {
            try
            {
                string topic = "iot/test/message";
                string expectedPayload = "Hello World";

                IoTDevice device = new(_testBroker, _testRestEndpoint);
                await device.ConnectAsync();

                // Start subscription first
                Task<string> subscribeTask = device.SubscribeAsync(topic);

                // Publish after a delay
                await Task.Delay(2000); // Increased for CI reliability
                await device.PublishAsync(topic, expectedPayload);

                // Wait with timeout
                string received = await subscribeTask.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Equal(expectedPayload, received);
            }
            catch (Exception ex) when (ex.Message.Contains("Could not connect to MQTT broker"))
            {
                throw new SkipException($"MQTT broker is not available: {ex.Message}");
            }
        }

        [SkippableFact]
        public async Task Should_Disconnect_From_Mqtt()
        {
            try
            {
                IoTDevice device = new(_testBroker, _testRestEndpoint);
                await device.ConnectAsync();
                await device.DisconnectAsync();
                Assert.False(device.IsConnected);
            }
            catch (Exception ex) when (ex.Message.Contains("Could not connect to MQTT broker"))
            {
                throw new SkipException($"MQTT broker is not available: {ex.Message}");
            }
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
