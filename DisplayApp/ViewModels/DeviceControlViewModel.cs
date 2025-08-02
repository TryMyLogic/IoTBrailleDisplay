using System.ComponentModel;
using System.Runtime.CompilerServices;
using DisplayLogic.Models;

namespace DisplayApp.ViewModels
{
    public partial class DeviceControlViewModel : INotifyPropertyChanged
    {
        public required MqttDevice _device;

        public MqttDevice Device
        {
            get => _device;
            set
            {
                if (_device != value)
                {
                    _device = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
