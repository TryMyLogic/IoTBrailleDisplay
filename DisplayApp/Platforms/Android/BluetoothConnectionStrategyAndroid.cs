using System.Text;
using Android.Bluetooth;
using Android.Content;
using DisplayLogic.Services;
using Serilog;

namespace DisplayApp.Platforms.Android
{
    public class BluetoothConnectionStrategyAndroid : IConnectionStrategy
    {
        private BluetoothAdapter? _adapter;
        private BluetoothSocket? _bluetoothSocket = null!;

        private static readonly Java.Util.UUID uuid = Java.Util.UUID.FromString("00001101-0000-1000-8000-00805F9B34FB")!;

        public bool IsConnected => _bluetoothSocket?.IsConnected ?? false;

        private void EnsureConnected()
        {
            if (_bluetoothSocket == null || !_bluetoothSocket.IsConnected || _bluetoothSocket.InputStream == null || _bluetoothSocket.OutputStream == null)
            {
                throw new InvalidOperationException("Bluetooth socket is not connected or has no valid stream.");
            }
        }
        public async Task<bool> ConnectAsync()
        {
            try
            {

                global::Android.App.Activity? context = Platform.CurrentActivity;

                if (context == null)
                {
                    Log.Warning("Context is null");
                }

                BluetoothManager? bluetoothManager = (BluetoothManager?)context?.GetSystemService(Context.BluetoothService);

                if (bluetoothManager == null)
                {
                    Log.Warning("BluetoothManager is not available");
                    return false;
                }

                _adapter = bluetoothManager?.Adapter;
                if (_adapter == null || !_adapter.IsEnabled)
                {
                    Log.Warning("Bluetooth adapter is not available or not enabled");
                    return false;
                }

                BluetoothDevice? device = _adapter.BondedDevices?.FirstOrDefault();
                if (device == null)
                {
                    Log.Warning("No Paired Bluetooth devices found");
                    return false;
                }

                _bluetoothSocket = device.CreateInsecureRfcommSocketToServiceRecord(uuid);
                if (_bluetoothSocket == null)
                {
                    Log.Error("Failed to create Bluetooth socket");
                    return false;
                }
                await _bluetoothSocket.ConnectAsync();
                if (!_bluetoothSocket.IsConnected)
                {
                    Log.Error("Failed to connect to Bluetooth socket");
                    return false;
                }
                return _bluetoothSocket.IsConnected;
            }

            catch (Exception ex)
            {
                Log.Error($"Android Bluetooth failed {ex.Message}");
                return false;
            }
        }
        //for both send and receive, EnsureConnected will ensure that the passed values are not null for _bluetoothSocket and Input- and Output stream
        public async Task SendTextAsync(string text)
        {
            try
            {
                EnsureConnected();
                byte[] buffer = Encoding.UTF8.GetBytes(text);
                await _bluetoothSocket!.OutputStream!.WriteAsync(buffer);
                Log.Information($"Sending text via Bluetooth: {text}.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error sending text over Bluetooth.");
                throw;
            }
        }

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
                Log.Error(ex, "Error receiving text over Bluetooth.");
                throw;
            }
        }

        public Task<bool> DisconnectAsync()
        {
            try
            {
                _bluetoothSocket?.Close();
                _bluetoothSocket = null;
                Log.Information("Disconnecting Bluetooth device.");
                return Task.FromResult(true);
            }

            catch (Exception ex)
            {
                Log.Error(ex, "Error during Bluetooth disconnect");
                return Task.FromResult(false);
            }
        }
    }
}