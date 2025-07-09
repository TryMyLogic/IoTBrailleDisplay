namespace DisplayLogic.Services
{
    public interface IWebSocketClient
    {
        event Action<string>? PayloadReceived;

        Task StartAsync();
        Task StopAsync();
        Task SendAsync(string payload);
    }
}
