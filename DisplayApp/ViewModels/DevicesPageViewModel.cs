using System.Windows.Input;
using DisplayApp.Services;
using DisplayApp.Views;
using DisplayLogic.Models;
using DisplayLogic.SharedInterfaces;

namespace DisplayApp.ViewModels
{
    public class DevicesPageViewModel
    {
        public ICommand OpenDeviceCommand { get; }
        private readonly IUserNotifier _notifier;

        public DevicesPageViewModel(IUserNotifier notifier)
        {
            _notifier = notifier;

            OpenDeviceCommand = new Command<MqttDevice>(async device =>
            {
                if (device == null)
                {
                    return;
                }

                await Shell.Current.GoToAsync(
                    nameof(DeviceControlPage),
                    true,
                    new Dictionary<string, object> { ["device"] = device }
                );

            });
        }
    }
}
