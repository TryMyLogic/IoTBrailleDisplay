using System.Text;
using DisplayLogic.Services;
using Serilog;
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
                {
                    Log.Warning("No Bluetooth devices found");
                    return false;
                }

                _device = await BluetoothDevice.FromIdAsync(targetDevice.Id);
                if (_device == null)
                {
                    Log.Warning("BluetoothDevice.FromIdAsync returned null");
                    return false;
                }

                RfcommDeviceServicesResult rfcommDeviceServices = await _device.GetRfcommServicesAsync();
                RfcommDeviceService? service = rfcommDeviceServices.Services.FirstOrDefault(s =>
                {
                    return s.ServiceId.Uuid == PiUuid;
                });
                if (service == null)
                {
                    Log.Warning("No matching Rfcomm service found.");
                    return false;
                }

                _socket = new StreamSocket();
                await _socket.ConnectAsync(service.ConnectionHostName, service.ConnectionServiceName);

                _writer = new DataWriter(_socket.OutputStream);
                _reader = new DataReader(_socket.InputStream)
                {
                    InputStreamOptions = InputStreamOptions.Partial
                };

                _isConnected = true;
                Log.Information("Bluetooth connection established succesfully.");
                return true;
            }
            catch (Exception ex)
            {
                _isConnected = false;
                Log.Error($"Windows Bluetooth failed: {ex.Message}");
                return false;
            }
        }
        public async Task SendTextAsync(string text)
        {
            try
            {
                if (_writer == null)
                    throw new InvalidOperationException("Bluetooth writer not initialized");
                byte[] data = Encoding.UTF8.GetBytes(text);
                _writer.WriteBytes(data);
                await _writer.StoreAsync();
                await _writer.FlushAsync();
                Log.Information($"Sending text via Bluetooth: {text}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error sending text via Bluetooth");
                throw;
            }
        }
        public async Task<string> ReceiveTextAsync()
        {
            try
            {
                if (_reader == null)
                    throw new InvalidOperationException("Bluetooth reader not initialized");
                const uint bufferSize = 1024;
                uint bytesRead = await _reader.LoadAsync(bufferSize);
                byte[] buffer = new byte[bytesRead];
                _reader.ReadBytes(buffer);
                string received = Encoding.UTF8.GetString(buffer);
                Log.Information($"Received text via Bluetooth: {received}");
                return received;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error receiving text over Bluetooth.");
                throw;

            }
        }
        public Task<bool> DisconnectAsync()
        {
            try
            {
                _ = (_writer?.DetachStream());
                _ = (_reader?.DetachStream());

                _writer?.Dispose();
                _reader?.Dispose();
                _socket?.Dispose();
                _device?.Dispose();

                _writer = null;
                _reader = null;
                _socket = null;
                _device = null;

                Log.Information("Disconnecting Bluetooth device.");
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during Bluetooth disconnect.");
                return Task.FromResult(false);
            }

        }
    }
}