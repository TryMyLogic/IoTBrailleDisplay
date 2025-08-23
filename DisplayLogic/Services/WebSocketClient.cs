using System.Net.WebSockets;
using System.Text;
using DisplayLogic.SharedInterfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DisplayLogic.Services
{
    /// <summary>
    /// Implements a WebSocket client for connecting to a server, sending and receiving text messages, 
    /// and notifying users of events.
    /// </summary>
    /// <param name="wsUrl">
    /// WebSocket server URL (e.g., ws://localhost:8080). Must not be null or empty.
    /// </param>
    /// <param name="notifier">
    /// The notifier for sending user feedback (e.g., errors, status updates). Must not be null.
    /// </param>
    /// <param name="logger">
    /// The logger for diagnostics, using <see cref="ILogger{T}"/> with Serilog for flexibility.
    /// Optional and defaults to <see cref="NullLogger{T}"/> with no logging.
    /// </param>
    /// <remarks>
    /// Uses <see cref="ClientWebSocket"/> to establish a WebSocket connection, send and receive UTF-8 text messages, 
    /// and invoke the <see cref="PayloadReceived"/> event for incoming messages. 
    /// WebSockets provide a bidirectional communication protocol over a single TCP connection. 
    /// Relies on <see cref="IUserNotifier"/> for user feedback and <see cref="ILogger{T}"/> for logging. 
    /// Maintains internal state for connection and message handling.
    /// </remarks>
    public class WebSocketClient(
        string wsUrl,
        IUserNotifier notifier,
        ILogger<WebSocketClient>? logger = null) : IWebSocketClient
    {
        private readonly ClientWebSocket _socket = new();
        private readonly string _wsUrl = wsUrl ?? throw new ArgumentNullException(nameof(wsUrl));
        private readonly ILogger<WebSocketClient> _logger = logger ?? NullLogger<WebSocketClient>.Instance;
        private readonly IUserNotifier _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
        private Task? _messageListenerTask;
        private readonly CancellationTokenSource _messageListenerCts = new();

        /// <summary>
        /// Event invoked when a text message is received from the WebSocket server.
        /// </summary>
        /// <remarks>
        /// Triggered by <see cref="HandleMessageAsync"/> with the message payload decoded from UTF-8 bytes to a string. 
        /// Subscribers should handle the payload asynchronously if needed.
        /// </remarks>
        public event Action<string>? PayloadReceived;

        /// <summary>
        /// Connects to the WebSocket server and starts listening for messages.
        /// </summary>
        /// <returns><see cref="Task"/> for the asynchronous operation.</returns>
        /// <exception cref="UriFormatException">Thrown if <see cref="_wsUrl"/> is not a valid URI.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="wsUrl"/> is null.</exception>
        /// <exception cref="Exception">Thrown for unexpected errors during connection or message listener setup.</exception>
        /// <remarks>
        /// Establishes a connection to <see cref="_wsUrl"/>, starts a background task to receive messages, logs via <see cref="ILogger{T}"/>, and notifies via <see cref="IUserNotifier"/>.
        /// Connection remains open until <see cref="StopAsync"/> is called.
        /// </remarks>
        public async Task StartAsync()
        {
            try
            {
                await _socket.ConnectAsync(new Uri(_wsUrl), CancellationToken.None);
                _logger.LogInformation("WebSocket connected to URL: {URL}.", _wsUrl);
                await _notifier.NotifyAsync("WebSocket", "WebSocket connected.");
                _logger.LogInformation("WebSocket Payload: WebSocket connected.");

                // Start ReceiveMessagesAsync as a background task to not block the main thread
                _messageListenerTask = Task.Run(() =>
                {
                    return ReceiveMessagesAsync(_messageListenerCts.Token);
                }, _messageListenerCts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start WebSocket connection: {errMessage}", ex.Message);
                throw; // Whatever class uses this underlying should handle this exception accordingly
            }
        }

        /// <summary>
        /// Stops the WebSocket connection and cleans up resources.
        /// </summary>
        /// <returns><see cref="Task"/> for the asynchronous operation.</returns>
        /// <exception cref="Exception">Thrown for any unexpected errors during shutdown or cleanup.</exception>
        /// <remarks>
        /// Closes the connection if open, cancels the message listener, disposes resources, logs via <see cref="ILogger{T}"/>, 
        /// and notifies via <see cref="IUserNotifier"/>. Called after <see cref="StartAsync"/> when client is no longer required, to release resources.
        /// </remarks>
        public async Task StopAsync()
        {
            try
            {
                _logger.LogInformation("Stopping WebSocket connection...");
                _messageListenerCts.Cancel();
                _logger.LogDebug("WebSocketState: {state}", _socket.State);
                if (_socket.State == WebSocketState.Open)
                {
                    await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closing", CancellationToken.None);
                    _logger.LogInformation("WebSocket closed gracefully.");
                }
                if (_messageListenerTask != null)
                {
                    await _messageListenerTask;
                }
                await _notifier.NotifyAsync("WebSocket", "WebSocket stopped.");
                _logger.LogInformation("WebSocket Payload: WebSocket stopped.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping WebSocket: {errMessage}.", ex.Message);
                await _notifier.NotifyAsync("WebSocket", $"Error stopping WebSocket: {ex.Message}");
            }
            finally
            {
                _socket.Dispose();
                _messageListenerCts.Dispose();
            }
        }

        /// <summary>
        /// Listens for incoming WebSocket messages until cancelled or the connection closes.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the message listener programmatically.</param>
        /// <returns><see cref="Task"/> for the asynchronous operation.</returns>
        /// <exception cref="Exception">Thrown for any errors while messages are recieved, such as network failures</exception>
        /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken"/> is used.</exception>
        /// <remarks>
        /// Receives messages as UTF-8 and parses to string, invokes <see cref="PayloadReceived"/>"/>,
        /// logs via <see cref="ILogger{T}"/>, and notifies via <see cref="IUserNotifier"/>.
        /// Runs in a background task started by <see cref="StartAsync"/>. 
        /// Requires an open WebSocket connection.
        /// </remarks>
        private async Task ReceiveMessagesAsync(CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[4096];
            MemoryStream messageStream = new();

            try
            {
                while (_socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                {
                    messageStream.SetLength(0);
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                        messageStream.Write(buffer, 0, result.Count);
                    } while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        string payload = Encoding.UTF8.GetString(messageStream.ToArray());
                        await HandleMessageAsync(payload);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in receive loop: {errMessage}.", ex.Message);
                await _notifier.NotifyAsync("WebSocket", $"Error in receive loop: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends a text message to the WebSocket server.
        /// </summary>
        /// <param name="payload">text message to send. Must not be null or empty.</param>
        /// <returns><see cref="Task"/> for the asynchronous operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the WebSocket is not connected.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="payload"/> is null.</exception>
        /// <exception cref="EncoderFallbackException">
        /// Thrown if <paramref name="payload"/> contains characters that cannot be encoded as UTF-8.
        /// </exception>
        /// <remarks>
        /// Encodes <paramref name="payload"/> as a UTF-8 string and sends it via the WebSocket connection. 
        /// Logs the outgoing message via <see cref="ILogger{T}"/> and notifies via <see cref="IUserNotifier"/>. 
        /// Requires an open WebSocket connection.
        /// </remarks>
        public async Task SendAsync(string payload)
        {
            if (_socket.State != WebSocketState.Open)
            {
                throw new InvalidOperationException("WebSocket not connected.");
            }

            byte[] bytes = Encoding.UTF8.GetBytes(payload);
            await _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            await LogAsync($"Sent: {payload}");
        }

        /// <summary>
        /// Handles an incoming WebSocket message by invoking the <see cref="PayloadReceived"/> event.
        /// </summary>
        /// <param name="payload">Text message received. Must not be null.</param>
        /// <returns><see cref="Task"/> for the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="payload"/> is null.</exception>
        /// <remarks>
        /// Invokes <see cref="PayloadReceived"/> with <paramref name="payload"/>, logs via <see cref="ILogger{T}"/>, and notifies via <see cref="IUserNotifier"/>. 
        /// Called by <see cref="ReceiveMessagesAsync"/> as messages are received from the WebSocket server.
        /// </remarks>
        private async Task HandleMessageAsync(string payload)
        {
            await LogAsync(payload);
            PayloadReceived?.Invoke(payload);
        }

        /// <summary>
        /// Logs a message and notifies the user.
        /// </summary>
        /// <param name="message">Message to log and notify. Must not be null.</param>
        /// <returns><see cref="Task"/> for the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="message"/> is null.</exception>
        /// <remarks>
        /// Logs <paramref name="message"/> via <see cref="ILogger{T}"/> and notifies via <see cref="IUserNotifier"/>. 
        /// Used by <see cref="SendAsync"/> and <see cref="HandleMessageAsync"/> for consistent logging.
        /// </remarks>
        private async Task LogAsync(string message)
        {
            ArgumentNullException.ThrowIfNull(message);
            _logger.LogInformation("WebSocket Payload: {message}", message);
            await _notifier.NotifyAsync("WebSocket Payload", message);
        }
    }
}