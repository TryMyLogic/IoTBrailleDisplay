using DisplayLogic.Services.IoTCommand;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DisplayLogic.Services
{
    /// <summary>
    /// Manages IoT device interactions, WebSocket communication with Home Assistant, and optional Braille display output.
    /// </summary>
    /// <remarks>
    /// <param name="iotDevice">IoT device for MQTT communication. Must not be null.</param>
    /// <param name="webSocketClient">WebSocket client for Home Assistant. Must not be null.</param>
    /// <param name="brailleDisplay">
    /// Optional refreshable Braille display for tactile text output.
    /// May be null, as the app can also function with a screen reader.
    /// </param>
    /// <param name="logger">
    /// The logger for diagnostics, using <see cref="ILogger{T}"/> with Serilog for flexibility.
    /// Optional and defaults to <see cref="NullLogger{T}"/> with no logging.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="iotDevice"/> or <paramref name="webSocketClient"/> is null.</exception>
    /// Coordinates <see cref="IIoTDevice"/> for MQTT-based IoT operations, 
    /// <see cref="IHomeAssistantWebSocketClient"/> for Home Assistant integration, 
    /// and <see cref="IBrailleDisplay"/> for physical braille device output. 
    /// Logs via <see cref="ILogger{T}"/> and manages device connections.
    /// Initializes the service with the provided dependencies and logs creation.
    /// </remarks>
    public class IoTService(IIoTDevice iotDevice, IHomeAssistantWebSocketClient webSocketClient, IBrailleDisplay? brailleDisplay = null, ILogger<IoTService>? logger = null)
    {
        private readonly IIoTDevice _iotDevice = iotDevice ?? throw new ArgumentNullException(nameof(iotDevice));
        private readonly IBrailleDisplay? _brailleDisplay = brailleDisplay;
        private readonly IHomeAssistantWebSocketClient _webSocketClient = webSocketClient ?? throw new ArgumentNullException(nameof(webSocketClient));
        private readonly ILogger<IoTService> _logger = logger ?? NullLogger<IoTService>.Instance;

        /// <summary>
        /// Initializes the IoT service by connecting to the IoT device, Home Assistant WebSocket client and Braille Display (If any).
        /// </summary>
        /// <returns><see cref="Task"/> for the asynchronous operation.</returns>
        /// <exception cref="Exception">Thrown if connecting to <see cref="IIoTDevice"/>, <see cref="IBrailleDisplay"/>, or <see cref="IHomeAssistantWebSocketClient"/> fails.</exception>
        /// <remarks>
        /// Connects any unconnected components (<see cref="IIoTDevice"/>, <see cref="IBrailleDisplay"/>, <see cref="IHomeAssistantWebSocketClient"/>) and logs via <see cref="ILogger{T}"/>. 
        /// Ensures all components are ready for operation.
        /// </remarks>
        public async Task InitializeAsync()
        {
            _logger.LogInformation("Initializing IoTService...");
            if (!_iotDevice.IsConnected)
            {
                _logger.LogInformation("Connecting to IoT device...");
                await _iotDevice.ConnectAsync();
            }
            if (_brailleDisplay != null && !_brailleDisplay.IsConnected)
            {
                _logger.LogInformation("Connecting to Braille Display...");
                await _brailleDisplay.ConnectAsync();
            }
            if (!_webSocketClient.IsConnected)
            {
                _logger.LogInformation("Connecting to Home Assistant WebSocket client...");
                await _webSocketClient.ConnectAsync();
            }
            _logger.LogInformation("IoTService initialized successfully.");
        }

        /// <summary>
        /// Executes an IoT command via MQTT, processing the response for Braille display output (If any).
        /// This is a helper function to support multiple device commands like <see cref="FlipShellySwitch"/>.
        /// </summary>
        /// <param name="command">IoT command with request and response topics and payload. Must not be null.</param>
        /// <returns><see cref="Task"/> for the asynchronous operation.</returns>
        /// <exception cref="InvalidDataException">Thrown if the command response is not a string for Braille display output.</exception>
        /// <exception cref="Exception">Thrown if MQTT publish or subscribe operations fail.</exception>
        /// <remarks>
        /// Publishes <paramref name="command"/> to its request topic, subscribes to its response topic, processes the response, and sends string results to <see cref="IBrailleDisplay"/> if available. 
        /// Logs via <see cref="ILogger{T}"/>. Requires a connected <see cref="IIoTDevice"/>. 
        /// </remarks>
        private async Task ExecuteCommandAsync(IIoTCommand command)
        {
            _logger.LogInformation("Executing IoT command. Request topic:{RequestTopic}, Response Topic: {ResponseTopic}", command.RequestTopic, command.ResponseTopic);
            // View existing state
            string prePublishResponse = await _iotDevice.SubscribeAsync(command.ResponseTopic);
            _logger.LogDebug("PrePublishResponse: {PrePublishResponse}", prePublishResponse);

            await _iotDevice.PublishAsync(command.RequestTopic, command.RequestPayload);
            _logger.LogInformation("Published to topic {RequestTopic} with payload {RequestPayload}.", command.RequestTopic, command.RequestPayload);

            // View new state
            string response = await _iotDevice.SubscribeAsync(command.ResponseTopic);
            _logger.LogDebug("Post-publish response received: {ResponseTopic}.", command.ResponseTopic);

            object result = await command.ProcessResponseAsync(response);

            if (result is string text)
            {
                _logger.LogInformation("Processed response into text: {Text}.", text);
                Console.WriteLine($"Sending text to Braille Display: {text}");
                // await _brailleDisplay.SendTextAsync(text);
            }
            else
            {
                _logger.LogError("Braille Display expects string responses but received type: {Type}", result.GetType());
                throw new InvalidDataException("Braille Display expects string responses");
            }
        }

        /// <summary>
        /// Flips a Shelly switch to on or off via MQTT.
        /// </summary>
        /// <param name="uniqueId">Unique ID of the Shelly switch. Must not be null or empty.</param>
        /// <param name="turnOn">Set to true to turn the switch on or false to turn it off.</param>
        /// <returns><see cref="Task"/> for the asynchronous operation.</returns>
        /// <exception cref="InvalidDataException">Thrown if the command response is not a string.</exception>
        /// <exception cref="Exception">Thrown if MQTT operations fail.</exception>
        /// <remarks>
        /// Executes a <see cref="ShellySwitchRelayCommand"/> via <see cref="ExecuteCommandAsync"/>, logs via <see cref="ILogger{T}"/>, 
        /// and sends output to <see cref="IBrailleDisplay"/> if available. Requires a connected <see cref="IIoTDevice"/>. 
        /// </remarks>
        public async Task FlipShellySwitch(string uniqueId, bool turnOn)
        {
            _logger.LogInformation("Flipping Shelly switch with unique ID {UniqueId} to {TurnOn}.", uniqueId, turnOn);
            try
            {
                await ExecuteCommandAsync(new ShellySwitchRelayCommand(uniqueId, turnOn));
                _logger.LogInformation("Successfully flipped Shelly switch {UniqueId} to {TurnOn}", uniqueId, turnOn);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error flipping Shelly switch {UniqueId} to {TurnOn}, {Message}", uniqueId, turnOn, ex.Message);
                throw;
            }
        }
    }
}
