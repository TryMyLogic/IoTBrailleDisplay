namespace DisplayLogic.Services
{

    /// <summary>
    /// Defines the abstraction for platform specific connection strategies
    /// used to communicate with the braille display device
    /// </summary>

    public interface IConnectionStrategy
    {
        /// <summary>
        /// Gets a value to indicate if the strategy is connected to the device
        /// </summary>
        bool IsConnected { get; }
        /// <summary>
        /// Establishes connection using the current strategy
        /// </summary>
        /// <returns><c>true</c> if the connection is successful, otherwise returns <c>false</c></returns>
        Task<bool> ConnectAsync();
        /// <summary>
        /// Sends text to the device using the current connection.
        /// </summary>
        /// <param name="text">Text to send to the device</param>
        Task SendTextAsync(string text);
        /// <summary>
        /// Returns the text received from the device using the current connection
        /// </summary>
        /// <returns>Text received from current connection</returns>
        Task<string> ReceiveTextAsync();
        /// <summary>
        /// Closes the connection to the device
        /// </summary>
        /// <returns><c>true</c> if the disconnect was successful; otherwise, <c>false</c></returns>
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