using System.Text;
using Android.Bluetooth;
using Android.Content;
using DisplayLogic.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DisplayApp.Platforms.Android
{
    /// <summary>
    /// Represents the Android-specific Bluetooth connection strategy for communicating
    /// with the braille display device.
    /// </summary>
    public class BluetoothConnectionStrategyAndroid : IConnectionStrategy
    {
        private BluetoothAdapter? _adapter;
        private BluetoothSocket? _bluetoothSocket = null!;

        private static readonly Java.Util.UUID uuid = Java.Util.UUID.FromString("00001101-0000-1000-8000-00805F9B34FB")!;
        private readonly ILogger<BluetoothConnectionStrategyAndroid>? _logger = null;

        ///<inheritdoc/>
        public bool IsConnected => _bluetoothSocket?.IsConnected ?? false;

        /// <summary>
        /// Initializes a new instance of the <see cref="BluetoothConnectionStrategyAndroid"/> class.
        /// </summary>
        /// <param name="logger">Logger for diagnostic messages.</param>
        public BluetoothConnectionStrategyAndroid(ILogger<BluetoothConnectionStrategyAndroid>? logger = null)
        {
            _logger = logger ?? NullLogger<BluetoothConnectionStrategyAndroid>.Instance;
        }

        /// <summary>
        /// Ensures that the Bluetooth socket and its streams are valid and connected.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the socket or streams are invalid or disconnected.</exception>
        private void EnsureConnected()
        {
            if (_bluetoothSocket == null || !_bluetoothSocket.IsConnected || _bluetoothSocket.InputStream == null || _bluetoothSocket.OutputStream == null)
            {
                throw new InvalidOperationException("Bluetooth socket is not connected or has no valid stream.");
            }
        }

        ///<inheritdoc/>
        public async Task<bool> ConnectAsync()
        {
            try
            {

                global::Android.App.Activity? context = Platform.CurrentActivity;

                if (context == null)
                {
                    _logger?.LogWarning("Context is null");
                }

                BluetoothManager? bluetoothManager = (BluetoothManager?)context?.GetSystemService(Context.BluetoothService);

                if (bluetoothManager == null)
                {
                    _logger?.LogWarning("BluetoothManager is not available");
                    return false;
                }

                _adapter = bluetoothManager?.Adapter;
                if (_adapter == null || !_adapter.IsEnabled)
                {
                    _logger?.LogWarning("Bluetooth adapter is not available or not enabled");
                    return false;
                }

                BluetoothDevice? device = _adapter.BondedDevices?.FirstOrDefault();
                if (device == null)
                {
                    _logger?.LogWarning("No Paired Bluetooth devices found");
                    return false;
                }

                _bluetoothSocket = device.CreateInsecureRfcommSocketToServiceRecord(uuid);
                if (_bluetoothSocket == null)
                {
                    _logger?.LogError("Failed to create Bluetooth socket");
                    return false;
                }
                await _bluetoothSocket.ConnectAsync();
                if (!_bluetoothSocket.IsConnected)
                {
                    _logger?.LogError("Failed to connect to Bluetooth socket");
                    return false;
                }
                return _bluetoothSocket.IsConnected;
            }

            catch (Exception ex)
            {
                _logger?.LogError("Android Bluetooth failed {Message}", ex.Message);
                return false;
            }
        }

        ///<inheritdoc/>
        public async Task SendTextAsync(string text)
        {
            try
            {
                EnsureConnected();
                byte[] buffer = Encoding.UTF8.GetBytes(text);
                await _bluetoothSocket!.OutputStream!.WriteAsync(buffer);
                _logger?.LogInformation($"Sending text via Bluetooth: {text}.");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error sending text over Bluetooth.");
                throw;
            }
        }

        ///<inheritdoc/>
        public async Task<string> ReceiveTextAsync()
        {
            try
            {
                EnsureConnected();
                byte[] buffer = new byte[1024];
                int bytesRead = await _bluetoothSocket!.InputStream!.ReadAsync(buffer);
                string received = Encoding.UTF8.GetString(buffer, 0, bytesRead);
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
                _bluetoothSocket?.Close();
                _bluetoothSocket = null;
                _logger?.LogInformation("Disconnecting Bluetooth device.");
                return Task.FromResult(true);
            }

            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during Bluetooth disconnect");
                return Task.FromResult(false);
            }
        }
    }
}