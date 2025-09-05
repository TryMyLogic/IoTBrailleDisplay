using System.Text;
using DisplayLogic.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace DisplayApp.Platforms.Windows
{
    /// <summary>
    /// Represents the Windows-specific Bluetooth connection strategy for communicating
    /// with the braille display device.
    /// </summary>
    public class BluetoothConnectionStrategyWindows : IConnectionStrategy
    {
        private BluetoothDevice? _device;
        private StreamSocket? _socket;
        private DataWriter? _writer;
        private DataReader? _reader;
        private static readonly Guid PiUuid = Guid.Parse("00001101-0000-1000-8000-00805F9B34FB");

        private bool _isConnected = false;
        private readonly ILogger<BluetoothConnectionStrategyWindows> _logger;

        /// <inheritdoc/>
        public bool IsConnected => _isConnected && _device != null && _socket != null && _writer != null && _reader != null;

        /// <summary>
        /// Initializes a new instance of the <see cref="BluetoothConnectionStrategyWindows"/> class.
        /// </summary>
        /// <param name="logger">Logger for diagnostic messages.</param>
        public BluetoothConnectionStrategyWindows(ILogger<BluetoothConnectionStrategyWindows>? logger = null)
        {
            _logger = logger ?? NullLogger<BluetoothConnectionStrategyWindows>.Instance;
        }

        ///<inheritdoc/>
        public async Task<bool> ConnectAsync()
        {
            try
            {
                string selector = BluetoothDevice.GetDeviceSelector();
                DeviceInformationCollection devices = await DeviceInformation.FindAllAsync(selector);
                DeviceInformation? targetDevice = devices.FirstOrDefault();

                if (targetDevice == null)
                {
                    _logger?.LogWarning("No Bluetooth devices found");
                    return false;
                }

                _device = await BluetoothDevice.FromIdAsync(targetDevice.Id);
                if (_device == null)
                {
                    _logger?.LogWarning("BluetoothDevice.FromIdAsync returned null");
                    return false;
                }

                RfcommDeviceServicesResult rfcommDeviceServices = await _device.GetRfcommServicesAsync();
                RfcommDeviceService? service = rfcommDeviceServices.Services.FirstOrDefault(s =>
                {
                    return s.ServiceId.Uuid == PiUuid;
                });
                if (service == null)
                {
                    _logger?.LogWarning("No matching Rfcomm service found.");
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
                _logger?.LogInformation("Bluetooth connection established succesfully.");
                return true;
            }
            catch (Exception ex)
            {
                _isConnected = false;
                _logger?.LogError($"Windows Bluetooth failed: {ex.Message}");
                return false;
            }
        }

        ///<inheritdoc/>
        public async Task SendTextAsync(string text)
        {
            try
            {
                if (_writer == null)
                    throw new InvalidOperationException("Bluetooth writer not initialized");
                byte[] data = Encoding.UTF8.GetBytes(text);
                _writer.WriteBytes(data);
                _ = await _writer.StoreAsync();
                _ = await _writer.FlushAsync();
                _logger?.LogInformation("Sending text via Bluetooth: {Text}", text);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error sending text via Bluetooth");
                throw;
            }
        }

        ///<inheritdoc/>
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
                _logger?.LogInformation("Received text via Bluetooth: {Received}", received);
                return received;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error receiving text over Bluetooth.");
                throw;

            }
        }

        ///<inheritdoc/>
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

                _logger?.LogInformation("Disconnecting Bluetooth device.");
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during Bluetooth disconnect.");
                return Task.FromResult(false);
            }

        }
    }
}