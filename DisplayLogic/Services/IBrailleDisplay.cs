namespace DisplayLogic.Services
{
    internal interface IBrailleDisplay
    {
        // Indicates if the IoT device is connected
        bool IsConnected { get; }

        // Establishes connection with braille display
        Task ConnectAsync();

        // Closes connection to the braille display
        Task DisconnectAsync();

        // Sends text to be displayed on braille display
        Task SendTextAsync(string text);

        // Receives text from the braille display to interact with IoT via MAUI app
        Task ReceiveTextAsync();

        // Used to alert other areas of code
        event EventHandler? TextReceived;
    }
}
