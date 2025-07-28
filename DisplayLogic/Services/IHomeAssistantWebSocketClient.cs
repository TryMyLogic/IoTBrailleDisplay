using DisplayLogic.Models;

namespace DisplayLogic.Services
{
    public interface IHomeAssistantWebSocketClient
    {
        List<MqttDevice> Devices { get; }
        List<Area> Areas { get; }
        bool IsConnected { get; }
        Task ConnectAsync();
        Task UpdateDeviceAreaAsync(string uniqueId, string areaId);
    }
}
