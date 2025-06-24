using Android.Bluetooth;
using Android.Content;
using DisplayLogic.Services;

namespace DisplayApp.Platforms.Android
{
    public class BluetoothConnectionStrategyAndroid : IConnectionStrategy
    {
        private BluetoothAdapter? _adapter;
        private BluetoothSocket? _bluetoothSocket;
        public bool IsConnected => _bluetoothSocket?.IsConnected ?? false;
        public async Task<bool> ConnectAsync()
        {
            try
            {

                global::Android.App.Activity? context = Platform.CurrentActivity;

                if (context == null)
                {
                    System.Diagnostics.Debug.WriteLine("Context is null");
                }
                //Getting a warning here for possible null value. I've been trying for hours to solve it, but I've been unable to. I've added null checks to prevent potential future crashes, but this might cause issues in the future

                var bluetoothManager = (BluetoothManager?)context.GetSystemService(Context.BluetoothService);

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

                //UUID is required for MAUI communication with Android Device. Subject to change, however, the UUID present is the universal standard for communication with Raspberry Pi
                var uuid = Java.Util.UUID.FromString("00001101-0000-1000-8000-00805F9B34FB");
                _bluetoothSocket = device.CreateInsecureRfcommSocketToServiceRecord(uuid);
                await _bluetoothSocket.ConnectAsync();
                return _bluetoothSocket.IsConnected;
            }

            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Android Bluetooth failed {ex.Message}");
                return false;
            }
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