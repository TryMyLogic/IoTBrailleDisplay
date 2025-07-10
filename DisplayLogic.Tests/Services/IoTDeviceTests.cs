using DisplayLogic.Services;

namespace DisplayLogic.Tests.Services
{
    public class IoTDeviceTests
    {
        // private const string TestBroker = "test.mosquitto.org";
        // private const string TestRestEndpoint = "http://localhost/api";

        // [SkippableFact]
        // public async Task Should_Connect_To_Mqtt_Broker()
        // {
        //     IoTDevice device = new(TestBroker, TestRestEndpoint);
        //     await device.ConnectAsync();

        //     Assert.True(device.IsConnected);
        // }

        // [SkippableFact]
        // public async Task Should_Publish_And_Receive_Message()
        // {
        //     string topic = "iot/test/message";
        //     string expectedPayload = "Hello World";

        //     IoTDevice device = new(TestBroker, TestRestEndpoint);
        //     await device.ConnectAsync();

        //     // Use Task.Run to simulate publish delay
        //     _ = Task.Run(async () =>
        //     {
        //         await Task.Delay(1000);
        //         await device.PublishAsync(topic, expectedPayload);
        //     });

        //     string received = await device.SubscribeAsync(topic);
        //     Assert.Equal(expectedPayload, received);
        // }

        // [SkippableFact]
        // public async Task Should_Disconnect_From_Mqtt()
        // {
        //     IoTDevice device = new(TestBroker, TestRestEndpoint);
        //     await device.ConnectAsync();

        //     await device.DisconnectAsync();

        //     Assert.False(device.IsConnected);
        // }

        // [Fact]
        // public async Task Publish_Should_Throw_If_Not_Connected()
        // {
        //     IoTDevice device = new(TestBroker, TestRestEndpoint);

        //     _ = await Assert.ThrowsAsync<InvalidOperationException>(() =>
        //     {
        //         return device.PublishAsync("iot/test", "payload");
        //     });
        // }

        //Simple integration test to check if Home Assistant and MQTT are reachable
        [SkippableFact]
        public async Task Should_Connect_To_HomeAssistant_And_MQTT()
        {
            // Use localhost for both services to test the GitHub Actions containers
            IoTDevice mqttDevice = new("mosquitto", "http://mosquitto:8123");

            // Try connecting to MQTT
            await mqttDevice.ConnectAsync();
            Assert.True(mqttDevice.IsConnected);

            // Wait for Home Assistant (in case it’s still initializing)
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var success = false;

            for (int i = 0; i < 12; i++) // Retry for up to 60s
            {
                try
                {
                    var res = await httpClient.GetAsync("http://localhost:8123/.well-known/core");
                    if (res.IsSuccessStatusCode)
                    {
                        success = true;
                        break;
                    }
                }
                catch
                {
                    // ignored
                }

                await Task.Delay(5000);
            }

            Assert.True(success, "Home Assistant was not reachable after 60 seconds");
        }
    }
}