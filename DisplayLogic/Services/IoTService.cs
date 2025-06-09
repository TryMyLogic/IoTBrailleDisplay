namespace DisplayLogic.Services
{
    internal class IoTService
    {
        private readonly IIoTDevice _iotDevice;
        private readonly IBrailleDisplay _brailleDisplay;

        public IoTService(IIoTDevice iotDevice, IBrailleDisplay brailleDisplay)
        {
            _iotDevice = iotDevice ?? throw new ArgumentNullException(nameof(iotDevice));
            _brailleDisplay = brailleDisplay ?? throw new ArgumentNullException(nameof(brailleDisplay));
        }

        private async Task ExecuteCommandAsync(IoTCommand command)
        {
            throw new NotImplementedException();
        }

        public async Task GetDevicesAsync()
        {
            await ExecuteCommandAsync(new GetDevicesCommand());
        }
    }
}
