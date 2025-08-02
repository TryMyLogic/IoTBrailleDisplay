using System.Windows.Input;
using DisplayApp.Views;
using DisplayLogic.Models;

namespace DisplayApp.ViewModels
{
    public class DevicesPageViewModel
    {

        public ICommand OpenDeviceCommand { get; }

        public DevicesPageViewModel()
        {
            OpenDeviceCommand = new Command<MqttDevice>(async device =>
            {
                if (device != null)
                {
                    await Shell.Current.GoToAsync(nameof(DeviceControlPage), true,
                        new Dictionary<string, object> { ["device"] = device });
                }
            });
        }
    }
}
