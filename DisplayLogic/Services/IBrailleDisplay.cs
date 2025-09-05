namespace DisplayLogic.Services
{
    /// <summary>
    /// Defines the contract for interacting with a braille display device.
    /// </summary>
    public interface IBrailleDisplay
    {
        /// <summary>
        /// Indicates if the IoT device is connected
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Establishes connection with braille display
        /// </summary>
        Task ConnectAsync();

        /// <summary>
        /// Closes connection to the braille display
        /// </summary>
        Task DisconnectAsync();

        /// <summary>
        /// Sends text to be displayed on braille display
        /// </summary>
        /// <param name="text">Text to display on the device</param>
        Task SendTextAsync(string text);

        /// <summary>
        /// Receives text from the braille display to interact with IoT via MAUI app
        /// </summary>
        /// <returns> A <see cref="string"/>containing text received from device</returns>
        Task<string> ReceiveTextAsync();

        /// <summary>
        /// Used to alert other areas of code
        /// </summary>
        event EventHandler? TextReceived;
    }
}
