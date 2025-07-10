using System.Text;
using Android.Bluetooth;
using Android.Content;
using DisplayLogic.Services;

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
                    System.Diagnostics.Debug.WriteLine("Context is null");
                }

                BluetoothManager? bluetoothManager = (BluetoothManager?)context?.GetSystemService(Context.BluetoothService);

                if (bluetoothManager == null)
                {
                    System.Diagnostics.Debug.WriteLine("BluetoothManager is not available");
                    return false;
                }

                _adapter = bluetoothManager?.Adapter;
                if (_adapter == null || !_adapter.IsEnabled)
                {
                    System.Diagnostics.Debug.WriteLine("Bluetooth adapter is not available or not enabled");
                    return false;
                }

                BluetoothDevice? device = _adapter.BondedDevices?.FirstOrDefault();
                if (device == null)
                {
                    System.Diagnostics.Debug.WriteLine("No Paired Bluetooth devices found");
                    return false;
                }

                _bluetoothSocket = device.CreateInsecureRfcommSocketToServiceRecord(uuid);
                if (_bluetoothSocket == null)
                {
                    System.Diagnostics.Debug.WriteLine("Failed to create Bluetooth socket");
                    return false;
                }
                await _bluetoothSocket.ConnectAsync();
                if (!_bluetoothSocket.IsConnected)
                {
                    System.Diagnostics.Debug.WriteLine("Failed to connect to Bluetooth socket");
                    return false;
                }
                return _bluetoothSocket.IsConnected;
            }

            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Android Bluetooth failed {ex.Message}");
                return false;
            }
        }
        //for both send and receive, EnsureConnected will ensure that the passed values are not null for _bluetoothSocket and Input- and Output stream
        public async Task SendTextAsync(string text)
        {
            EnsureConnected();
            byte[] buffer = Encoding.UTF8.GetBytes(text);
            await _bluetoothSocket!.OutputStream!.WriteAsync(buffer);
        }

        public async Task<string> ReceiveTextAsync()
        {
            EnsureConnected();
            byte[] buffer = new byte[1024];
            int bytesRead = await _bluetoothSocket!.InputStream!.ReadAsync(buffer);
            return Encoding.UTF8.GetString(buffer, 0, bytesRead);
        }

        public Task<bool> DisconnectAsync()
        {
            try
            {
                _bluetoothSocket?.Close();
                _bluetoothSocket = null;
                return Task.FromResult(true);
            }

            catch
            {
                return Task.FromResult(false);
            }
        }
    }
}