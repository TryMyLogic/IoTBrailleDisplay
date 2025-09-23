using System.Collections.ObjectModel;
using System.Windows.Input;
using DisplayApp.Views;
using DisplayLogic.Models;
using DisplayLogic.Services;
using DisplayLogic.SharedInterfaces;

namespace DisplayApp.ViewModels
{
    /// <summary>
    /// ViewModel for the <see cref="DevicesPage"/> in the MAUI application.
    /// Manages devices, allows reassigning devices to different Areas, and handles navigation to the device control page.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This ViewModel adheres to the MVVM pattern, exposing ObservableCollections for data binding
    /// and ICommand implementations for user interactions. It interacts with the Home Assistant WebSocket
    /// client (<see cref="IHomeAssistantWebSocketClient"/>) to retrieve and update device and area information.
    /// </para>
    /// <para>
    /// Key responsibilities include:
    /// <list type="bullet">
    /// <item>Displaying a list of devices and areas.</item>
    /// <item>Handling device selection to navigate to <see cref="DeviceControlPage"/>.</item>
    /// <item>Allowing users to reassign a device to a new Area.</item>
    /// <item>Raising events (<see cref="DeviceAreaChanged"/>) when a device's area changes to update dependent UI elements.</item>
    /// </list>
    /// </para>
    /// </remarks>
    public class DevicesPageViewModel
    {
        /// <summary>
        /// Command executed when the user selects a device to open its control page.
        /// </summary>
        public ICommand OpenDeviceCommand { get; }

        /// <summary>
        /// Command to show an area picker for reassigning a device.
        /// </summary>
        public ICommand ShowAreaPickerCommand { get; }

        /// <summary>
        /// Command executed when the user assigns a device to a new area.
        /// </summary>
        public ICommand AssignAreaCommand { get; }

        /// <summary>
        /// Collection of devices displayed on the DevicesPage.
        /// Observable for UI updates.
        /// </summary>
        public ObservableCollection<MqttDevice> Devices { get; } = [];

        /// <summary>
        /// Collection of available Areas.
        /// Used for assigning devices to different Areas.
        /// </summary>
        public ObservableCollection<Area> Areas { get; } = [];

        /// <summary>
        /// Event triggered when a device's area is updated.
        /// Used to notify other parts of the UI that may need to refresh.
        /// </summary>
        public event Action? DeviceAreaChanged;

        private readonly IUserNotifier _notifier;
        private readonly IHomeAssistantWebSocketClient _haClient;

        /// <summary>
        /// Raises the <see cref="DeviceAreaChanged"/> event.
        /// </summary>
        private void RaiseDeviceAreaChanged()
        {
            DeviceAreaChanged?.Invoke();
        }

        /// <summary>
        /// Initializes a new instance of <see cref="DevicesPageViewModel"/>.
        /// </summary>
        /// <param name="notifier">Service for showing user notifications.</param>
        /// <param name="haClient">Home Assistant WebSocket client for retrieving devices and areas.</param>
        /// <remarks>
        /// Initializes the device and area collections from the HA client.
        /// Sets up commands for navigation and device reassignment.
        /// </remarks>
        public DevicesPageViewModel(IUserNotifier notifier, IHomeAssistantWebSocketClient haClient)
        {
            _notifier = notifier;
            _haClient = haClient;

            // Load devices + areas from HA
            Devices = new ObservableCollection<MqttDevice>(_haClient.Devices ?? []);
            Areas = new ObservableCollection<Area>(_haClient.Areas ?? []);

            // Command to navigate to the DeviceControlPage for a selected device
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

            // Command to display the area picker for assigning a device to a new area
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
    }
}
