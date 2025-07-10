using System.Text;
using DisplayLogic.Services;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace DisplayApp.Platforms.Windows
{
    public class BluetoothConnectionStrategyWindows : IConnectionStrategy
    {
        private BluetoothDevice? _device;
        private StreamSocket? _socket;
        private DataWriter? _writer;
        private DataReader? _reader;
        private static readonly Guid PiUuid = Guid.Parse("00001101-0000-1000-8000-00805F9B34FB");

        private bool _isConnected = false;
        public bool IsConnected => _isConnected && _device != null && _socket != null && _writer != null && _reader != null;
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
                if (_device == null)
                    return false;

                RfcommDeviceServicesResult rfcommDeviceServices = await _device.GetRfcommServicesAsync();
                RfcommDeviceService? service = rfcommDeviceServices.Services.FirstOrDefault(s => s.ServiceId.Uuid == PiUuid);
                if (service == null)
                    return false;

                _socket = new StreamSocket();
                await _socket.ConnectAsync(service.ConnectionHostName, service.ConnectionServiceName);

                _writer = new DataWriter(_socket.OutputStream);
                _reader = new DataReader(_socket.InputStream)
                {
                    InputStreamOptions = InputStreamOptions.Partial
                };

                _isConnected = true;
                return true;
            }
            catch (Exception ex)
            {
                _isConnected = false;
                System.Diagnostics.Debug.WriteLine($"Windows Bluetooth failed: {ex.Message}");
                return false;
            }
        }
        public async Task SendTextAsync(string text)
        {
            if (_writer == null)
                throw new InvalidOperationException("Bluetooth writer not initialized");
            byte[] data = Encoding.UTF8.GetBytes(text);
            _writer.WriteBytes(data);
            await _writer.StoreAsync();
            await _writer.FlushAsync();
        }
        public async Task<string> ReceiveTextAsync()
        {
            if (_reader == null)
                throw new InvalidOperationException("Bluetooth reader not initialized");
            const uint bufferSize = 1024;
            uint bytesRead = await _reader.LoadAsync(bufferSize);
            byte[] buffer = new byte[bytesRead];
            _reader.ReadBytes(buffer);
            return Encoding.UTF8.GetString(buffer);
        }
        public Task<bool> DisconnectAsync()
        {
            try
            {
                _writer?.DetachStream();
                _reader?.DetachStream();

                _writer?.Dispose();
                _reader?.Dispose();
                _socket?.Dispose();
                _device?.Dispose();

                _writer = null;
                _reader = null;
                _socket = null;
                _device = null;

                return Task.FromResult(true);
            }
            catch
            {
                return Task.FromResult(false);
            }

        }
    }
}