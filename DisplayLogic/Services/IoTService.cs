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
            // View existing state
            string prePublishResponse = await _iotDevice.SubscribeAsync(command.ResponseTopic);
            Console.WriteLine($"PrePublishResponse: {prePublishResponse}");

            await _iotDevice.PublishAsync(command.RequestTopic, command.RequestPayload);

            // View new state
            string response = await _iotDevice.SubscribeAsync(command.ResponseTopic);

            object result = await command.ProcessResponseAsync(response);

            if (result is string text)
            {
                Console.WriteLine($"Sending text to Braille Display: {text}");
                // await _brailleDisplay.SendTextAsync(text);
            }
            else
            {
                throw new InvalidDataException("Braille Display expects string responses");
            }
        }

        public async Task FlipShellySwitch(string uniqueId, bool turnOn)
        {
            await ExecuteCommandAsync(new ShellySwitchRelayCommand(uniqueId, turnOn));
        }

    }
}
