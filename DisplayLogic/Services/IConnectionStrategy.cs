namespace DisplayLogic.Services
{
    // Using this file as the main interface. Platform specific connection strategies have been added to their separate platform folders
    public interface IConnectionStrategy
    {
        bool IsConnected { get; }
        Task<bool> ConnectAsync();
        Task SendTextAsync(string text);
        Task<string> ReceiveTextAsync();
        Task<bool> DisconnectAsync();
    }

    //Leaving WiredConnectionStrategy here for later use. Bluetooth connection strategies have been moved
    public class WiredConnectionStrategy : IConnectionStrategy
    {
        public bool IsConnected => throw new NotImplementedException();

        public Task<bool> ConnectAsync()
        {
            throw new NotImplementedException();
        }

        public Task SendTextAsync(string text)
        {
            throw new NotImplementedException();
        }

        public Task<string> ReceiveTextAsync()
        {
            throw new NotImplementedException();
        }

        public Task<bool> DisconnectAsync()
        {
            throw new NotImplementedException();
        }
    }
}