namespace DisplayLogic.Services
{
    /// <summary>
    /// Defines a WebSocket client for connecting to a server, sending and receiving text messages, 
    /// and notifying users of events.
    /// </summary>
    /// <remarks>
    /// Implementations of this interface should provide functionality for establishing a WebSocket connection, 
    /// handling UTF-8 text messages, and managing connection lifecycle. 
    /// WebSockets enable bidirectional communication over a single TCP connection.
    /// </remarks>
    public interface IWebSocketClient
    {
        /// <summary>
        /// Event invoked when a text message is received from the WebSocket server.
        /// </summary>
        /// <remarks>
        /// Triggered when a UTF-8 encoded text message is received. 
        /// Subscribers should handle the payload asynchronously if needed.
        /// </remarks>
        event Action<string>? PayloadReceived;

        /// <summary>
        /// Connects to the WebSocket server and starts listening for messages.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <exception cref="UriFormatException">Thrown if the WebSocket URL is not a valid URI.</exception>
        /// <exception cref="ArgumentNullException">Thrown if the WebSocket URL is null or empty.</exception>
        /// <exception cref="Exception">Thrown for unexpected errors during connection or message listener setup.</exception>
        /// <remarks>
        /// Establishes a WebSocket connection to the specified URL and begins receiving messages in a background task. 
        /// The connection remains open until <see cref="StopAsync"/> is called.
        /// </remarks>
        Task StartAsync();

        /// <summary>
        /// Stops the WebSocket connection and cleans up resources.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <exception cref="Exception">Thrown for unexpected errors during shutdown or cleanup.</exception>
        /// <remarks>
        /// Closes the WebSocket connection if open, cancels any ongoing message listening, 
        /// and disposes of resources. Should be called when the client is no longer needed.
        /// </remarks>
        Task StopAsync();

        /// <summary>
        /// Sends a text message to the WebSocket server.
        /// </summary>
        /// <param name="payload">The text message to send. Must not be null or empty.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the WebSocket is not connected.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="payload"/> is null.</exception>
        /// <exception cref="EncoderFallbackException">
        /// Thrown if <paramref name="payload"/> contains characters that cannot be encoded as UTF-8.
        /// </exception>
        /// <remarks>
        /// Encodes the <paramref name="payload"/> as UTF-8 and sends it over the WebSocket connection. 
        /// Requires an active WebSocket connection established via <see cref="StartAsync"/>.
        /// </remarks>
        Task SendAsync(string payload);
    }
}
