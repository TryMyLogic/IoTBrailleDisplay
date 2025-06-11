namespace DisplayLogic.Services
{
    // This is platform specific code. Must move to platform folder
    internal interface IConnectionStrategy
    {
        bool IsConnected { get; }
        Task<bool> ConnectAsync();
        Task<bool> DisconnectAsync();
    }

    // Below 2 are examples. Actual implementation will be like: WindowsWiredConnectionStrategy. Must be moved to appropriate platform specific folder
    public class WiredConnectionStrategy : IConnectionStrategy
    {
        public bool IsConnected => throw new NotImplementedException();

        public Task<bool> ConnectAsync()
        {
            throw new NotImplementedException();
        }

        public Task<bool> DisconnectAsync()
        {
            throw new NotImplementedException();
        }
    }

    public class BluetoothConnectionStrategy : IConnectionStrategy
    {
        public bool IsConnected => throw new NotImplementedException();

        public Task<bool> ConnectAsync()
        {
            throw new NotImplementedException();
        }

        public Task<bool> DisconnectAsync()
        {
            throw new NotImplementedException();
        }
    }
}
