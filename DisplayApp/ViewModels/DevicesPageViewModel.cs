using System.Collections.ObjectModel;
using System.Windows.Input;
using DisplayApp.Views;
using DisplayLogic.Models;
using DisplayLogic.Services;
using DisplayLogic.SharedInterfaces;

namespace DisplayApp.ViewModels
{
    public class DevicesPageViewModel
    {
        public ICommand OpenDeviceCommand { get; }
        public ICommand ShowAreaPickerCommand { get; }
        private readonly IUserNotifier _notifier;
        public ObservableCollection<MqttDevice> Devices { get; } = [];
        public ObservableCollection<Area> Areas { get; } = [];
        public ICommand AssignAreaCommand { get; }
        private readonly IHomeAssistantWebSocketClient _haClient;
        public event Action? DeviceAreaChanged;
        public Area? CurrentArea { get; private set; }

        private void RaiseDeviceAreaChanged()
        {
            DeviceAreaChanged?.Invoke();
        }

        public DevicesPageViewModel(IUserNotifier notifier, IHomeAssistantWebSocketClient haClient)
        {
            _notifier = notifier;
            _haClient = haClient;

            // Load devices + areas from HA
            Devices = new ObservableCollection<MqttDevice>(_haClient.Devices ?? []);
            Areas = new ObservableCollection<Area>(_haClient.Areas ?? []);

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

            // Assign device to a new area
            AssignAreaCommand = new Command<(MqttDevice device, Area area)>(async tuple =>
            {
                await _haClient.UpdateDeviceAreaAsync(tuple.device.id, tuple.area.area_id);

                // Update local device
                tuple.device.area_id = tuple.area.area_id;

                await _notifier.NotifyAsync("Device Updated", $"{tuple.device.name} moved to {tuple.area.name}");

                // Refresh the UI: remove device from current collection if needed
                _ = Devices.Remove(tuple.device);

                RaiseDeviceAreaChanged();
            });

            // Command for showing the area picker
            ShowAreaPickerCommand = new Command<MqttDevice>(async device =>
            {
                if (device == null || Areas.Count == 0)
                {
                    return;
                }

                // Build list of area names
                string[] areaNames = [.. Areas.Select(a =>
                {
                    return a.name;
                })];

                // Show the action sheet
                string selected = await Application.Current.MainPage.DisplayActionSheet(
                    $"Assign {device.name} to:", "Cancel", null, areaNames);

                if (string.IsNullOrEmpty(selected) || selected == "Cancel")
                {
                    return;
                }

                // Find the selected area
                Area? area = Areas.FirstOrDefault(a =>
                {
                    return a.name == selected;
                });
                if (area != null && AssignAreaCommand.CanExecute((device, area)))
                {
                    AssignAreaCommand.Execute((device, area));
                }
            });

        }

        public void LoadForArea(Area area)
        {
            CurrentArea = area;

            Devices.Clear();
            foreach (MqttDevice? d in _haClient.Devices.Where(d =>
            {
                return string.Equals(string.IsNullOrEmpty(d.area_id) ? "unassigned" : d.area_id,
                                              area.area_id,
                                              StringComparison.OrdinalIgnoreCase);
            }))
            {
                Devices.Add(d);
            }
        }
    }
}
