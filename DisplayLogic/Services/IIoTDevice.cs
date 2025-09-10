
namespace DisplayLogic.Services
{
    public interface IIoTDevice
    {
        // Indicates if the IoT device is connected
        bool IsConnected { get; }

        // Establishes connection to IoT system (MQTT broker or REST api)
        Task ConnectAsync();

        // Closes connection to IoT system. Disposes of resource accordingly
        Task DisconnectAsync();

        // Publishes a message to an IoT topic (MQTT) or endpoint (REST)
        Task PublishAsync(string topic, string payload);

        // Subscribes to an IoT topic (MQTT) or retrieves data (REST)
        Task<string> SubscribeAsync(string topic);
        Task SubscribePersistentAsync(string topic, Action<string> callback);
    }
}
