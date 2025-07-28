using DisplayLogic.Services;

namespace DisplayLogic.Tests.SharedTestItems
{
    public class ServiceFixture : IAsyncLifetime
    {
        public bool CanConnectToHomeAssistant { get; private set; }
        public bool CanConnectToMQTTBroker { get; private set; }
        public bool CanConnectToBrailleDisplay { get; private set; }
        public static string HomeAssistantAccessToken { get; private set; } = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiI1NGVhNTA4NDU5MjM0ZDhkYWY4ZDEzZjZmM2NjOTU4YSIsImlhdCI6MTc1MDg3MDkwNiwiZXhwIjoyMDY2MjMwOTA2fQ.Yh61tJ9jU1Y - mbOBtDy76B4L1w2lUw2nQVBypWqGpvE";
        public static string WsURL { get; private set; } = "ws://localhost:8123/api/websocket";
        public ServiceFixture() { }

        public async Task InitializeAsync()
        {
            CanConnectToHomeAssistant = await TestConnectionToHomeAssistant();
            // CanConnectToMQTTBroker = await TestConnectionToMQTTBroker();
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        private static async Task<bool> TestConnectionToHomeAssistant()
        {
            //try
            //{
            //    IUserNotifier notifier = NSubstitute.Substitute.For<IUserNotifier>();
            //    WebSocketClient webSocketClient = new(WsURL, notifier);

            //    HomeAssistantWebSocketClient client = new(webSocketClient, notifier, HomeAssistantAccessToken);

            //    TaskCompletionSource<bool> tcs = new();

            //    client.DeviceRegistryReceivedAndProcessed += x =>
            //    {
            //        _ = tcs.TrySetResult(true);
            //    };

            //    // StartAsync loops until it recieves a message. As such, it can cause hanging and needs to be dealt with accordingly
            //    Task startTask = client.StartAsync();

            //    // Wait for device registry OR timeout after 5s
            //    Task completed = await Task.WhenAny(tcs.Task, Task.Delay(5000));

            //    await client.StopAsync();

            //    return completed == tcs.Task && tcs.Task.Result;
            //}
            //catch
            //{
            //    return false;
            //}
            return false;
        }

        private static async Task<bool> TestConnectionToMQTTBroker()
        {
            try
            {
                string mqttBroker = "localhost";
                string restEndpoint = "http://localhost:8123"; // Not used for MQTT, but required by constructor

                IoTDevice device = new(mqttBroker, restEndpoint);

                await device.ConnectAsync();
                await device.DisconnectAsync();

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TestConnectionToBraille()
        {
            throw new NotImplementedException();
        }


    }
}
