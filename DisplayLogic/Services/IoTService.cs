using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DisplayLogic.Services
{
    public class IoTService
    {
        private readonly IIoTDevice _iotDevice;
        private readonly IBrailleDisplay _brailleDisplay;
        private readonly ILogger<IoTService> _logger;

        public IoTService(IIoTDevice iotDevice, IBrailleDisplay brailleDisplay, ILogger<IoTService>? logger = null)
        {
            _iotDevice = iotDevice ?? throw new ArgumentNullException(nameof(iotDevice));
            _brailleDisplay = brailleDisplay ?? throw new ArgumentNullException(nameof(brailleDisplay));
            _logger = logger ?? NullLogger<IoTService>.Instance;
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
