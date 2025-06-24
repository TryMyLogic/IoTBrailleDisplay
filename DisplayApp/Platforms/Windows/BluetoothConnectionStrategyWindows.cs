using DisplayLogic.Services;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace DisplayApp.Platforms.Windows
{
    public class BluetoothConnectionStrategyWindows : IConnectionStrategy
    {
        private BluetoothDevice? _device;
        public bool IsConnected => _device != null;
        public async Task<bool> ConnectAsync()
        {
            try
            {
                string selector = BluetoothDevice.GetDeviceSelector();
                DeviceInformationCollection devices = await DeviceInformation.FindAllAsync(selector);
                DeviceInformation? targetDevice = devices.FirstOrDefault();
                if (targetDevice == null)
                    return false;
                _device = await BluetoothDevice.FromIdAsync(targetDevice.Id);
                return _device != null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Windows Bluetooth failed: {ex.Message}");
                return false;
            }
        }
        public Task<bool> DisconnectAsync()
        {
            _device?.Dispose();
            _device = null;
            return Task.FromResult(true);
        }
    }
}