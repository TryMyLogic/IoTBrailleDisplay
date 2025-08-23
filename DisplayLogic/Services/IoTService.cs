using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DisplayLogic.Services
{
    public class IoTService(IIoTDevice iotDevice, IHomeAssistantWebSocketClient webSocketClient, IBrailleDisplay? brailleDisplay = null, ILogger<IoTService>? logger = null)
    {
        private readonly IIoTDevice _iotDevice = iotDevice ?? throw new ArgumentNullException(nameof(iotDevice));
        private readonly IBrailleDisplay? _brailleDisplay = brailleDisplay;
        private readonly IHomeAssistantWebSocketClient _webSocketClient = webSocketClient ?? throw new ArgumentNullException(nameof(webSocketClient));
        private readonly ILogger<IoTService> _logger = logger ?? NullLogger<IoTService>.Instance;

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

        private async Task ExecuteCommandAsync(IoTCommand command)
        {
            _logger.LogInformation($"Executing IoT command. Request topic:{command.RequestTopic}, Response Topic: {command.ResponseTopic}");
            // View existing state
            string prePublishResponse = await _iotDevice.SubscribeAsync(command.ResponseTopic);
            _logger.LogDebug($"PrePublishResponse: {prePublishResponse}");

            await _iotDevice.PublishAsync(command.RequestTopic, command.RequestPayload);
            _logger.LogInformation($"Published to topic {command.RequestTopic} with payload {command.RequestPayload}.");

            // View new state
            string response = await _iotDevice.SubscribeAsync(command.ResponseTopic);
            _logger.LogDebug($"Post-publish response received: {command.ResponseTopic}.");

            object result = await command.ProcessResponseAsync(response);

            if (result is string text)
            {
                _logger.LogInformation($"Processed response into text: {text}.");
                Console.WriteLine($"Sending text to Braille Display: {text}");
                // await _brailleDisplay.SendTextAsync(text);
            }
            else
            {
                _logger.LogError($"Braile Display expects string responses but received type: {result.GetType()}");
                throw new InvalidDataException("Braille Display expects string responses");
            }
        }

        public async Task FlipShellySwitch(string uniqueId, bool turnOn)
        {
            // Added try catch for logging purposes
            _logger.LogInformation($"Flipping Shelly switch with unique ID {uniqueId} to {turnOn}.");
            try
            {
                await ExecuteCommandAsync(new ShellySwitchRelayCommand(uniqueId, turnOn));
                _logger.LogInformation($"Successfully flipped Shelly switch {uniqueId} to {turnOn}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error flipping Shelly switch {uniqueId} to {turnOn}, {ex.Message}");
                throw;
            }

        }

    }
}
