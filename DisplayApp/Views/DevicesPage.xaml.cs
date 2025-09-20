using System.Collections;
using DisplayApp.ViewModels;
using DisplayLogic.Models;

namespace DisplayApp.Views;

/// <summary>
/// Represents a page that displays all devices within a specific area.
/// Users can view devices, navigate to individual device control pages, 
/// and reassign devices to different areas.
/// </summary>
/// <remarks>
/// This page implements <see cref="IQueryAttributable"/> to receive navigation
/// parameters from other pages, specifically the selected <see cref="Area"/> and
/// a list of <see cref="MqttDevice"/> instances in that area. The page binds to
/// <see cref="DevicesPageViewModel"/> for all UI interactions and device management logic.
/// </remarks>
public partial class DevicesPage : ContentPage, IQueryAttributable
{
    /// <summary>
    /// The area currently displayed on the page. Used for filtering the device list.
    /// </summary>
    private Area? _currentArea;

    /// <summary>
    /// Initializes a new instance of the <see cref="DevicesPage"/> class.
    /// Sets up the page's BindingContext and subscribes to device-area change events.
    /// </summary>
    /// <param name="vm">
    /// The <see cref="DevicesPageViewModel"/> instance that provides device and area data,
    /// as well as commands for navigation and device reassignment.
    /// </param>
    public DevicesPage(DevicesPageViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;

        // Refresh the device list whenever the ViewModel signals a device-area change
        vm.DeviceAreaChanged += () =>
        {
            RefreshDeviceList(); // RefreshDeviceList uses _currentArea to filter
        };
    }

    /// <summary>
    /// Handles the selection of a device from the collection view.
    /// Navigates to the <see cref="DeviceControlPage"/> for the selected device.
    /// </summary>
    /// <param name="sender">The collection view that raised the event.</param>
    /// <param name="e">Event data containing the currently selected item.</param>
    private async void OnDeviceSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection is IList selectionList && selectionList.Count > 0 && selectionList[0] is MqttDevice selectedDevice)
        {
            try
            {
                await Shell.Current.GoToAsync(nameof(DeviceControlPage), new Dictionary<string, object>
                {
                    ["device"] = selectedDevice
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Navigation error: {ex.Message}");
                await DisplayAlert("Error", $"Failed to navigate to device control: {ex.Message}", "OK");
            }

            // Optional: Deselect item
            ((CollectionView)sender).SelectedItem = null;
        }
    }

    /// <summary>
    /// Handles the "Options" button click for a device.
    /// Allows the user to reassign the device to a different area via an action sheet.
    /// </summary>
    /// <param name="sender">The button that was clicked.</param>
    /// <param name="e">Event data (not used).</param>
    private async void OnDeviceOptionsClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn || btn.BindingContext is not MqttDevice device)
        {
            return;
        }

        if (BindingContext is not DevicesPageViewModel vm)
        {
            return;
        }

        // Build list of area names correctly
        string[] areaNames = [.. vm.Areas
            .Where(a => { return !string.Equals(a.area_id, device.area_id, StringComparison.OrdinalIgnoreCase); })
            .Select(a => { return a.name; })
        ];

        if (areaNames.Length == 0)
        {
            await DisplayAlert("No areas", "There are no areas to assign.", "OK");
            return;
        }

        // Show menu
        string action = await DisplayActionSheet($"Assign {device.name} to:", "Cancel", null, areaNames);
        if (string.IsNullOrEmpty(action) || action == "Cancel")
        {
            return;
        }

        // Find the selected area by name and execute the VM command
        Area? area = vm.Areas.FirstOrDefault(a =>
        {
            return a.name == action;
        });
        if (area == null)
        {
            return;
        }

        (MqttDevice device, Area area) tuple = (device, area);
        if (vm.AssignAreaCommand.CanExecute(tuple))
        {
            vm.AssignAreaCommand.Execute(tuple);
        }
    }

    /// <summary>
    /// Refreshes the device list displayed in the CollectionView based on the current area.
    /// Optionally accepts a list of devices to filter, otherwise uses the ViewModel's device list.
    /// </summary>
    /// <param name="devices">Optional list of devices to display.</param>
    private void RefreshDeviceList(IEnumerable<MqttDevice>? devices = null)
    {
        if (_currentArea == null)
        {
            return;
        }

        IEnumerable<MqttDevice> source = devices ?? ((DevicesPageViewModel)BindingContext).Devices;

        var filtered = source
            .Where(d =>
            {
                return string.Equals(d.area_id, _currentArea.area_id, StringComparison.OrdinalIgnoreCase);
            })
            .ToList();

        DevicesCollection.ItemsSource = filtered;

        RoomNameLabel.Text = filtered.Count > 0
            ? $"Devices in {_currentArea.name}"
            : $"Devices in {_currentArea.name} (No devices found)";
    }

    /// <summary>
    /// Receives query parameters from navigation and applies them to the page.
    /// Expects "area" and "devices" keys to initialize the current area and device list.
    /// </summary>
    /// <param name="query">Dictionary containing query attributes.</param>
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        try
        {
            if (query.TryGetValue("area", out object? areaObj) && areaObj is Area area &&
                query.TryGetValue("devices", out object? devicesObj) && devicesObj is List<MqttDevice> devices)
            {
                _currentArea = area; // <-- save current area

                RefreshDeviceList(devices);
            }
            else
            {
                RoomNameLabel.Text = "Invalid area or devices";
                DevicesCollection.ItemsSource = new List<MqttDevice>();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ApplyQueryAttributes error: {ex.Message}");
            RoomNameLabel.Text = "Error loading devices";
            DevicesCollection.ItemsSource = new List<MqttDevice>();
        }
    }

    /// <summary>
    /// Handles the "Back" button click.
    /// Navigates back to the previous page in the navigation stack.
    /// 
    /// Its a back button, what else would it do?
    /// 
    /// </summary>
    /// <param name="sender">The button that was clicked.</param>
    /// <param name="e">Event arguments.</param>
    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

}