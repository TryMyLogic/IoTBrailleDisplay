using System.Collections;
using DisplayApp.ViewModels;
using DisplayLogic.Models;

namespace DisplayApp.Views;

public partial class DevicesPage : ContentPage, IQueryAttributable
{
    private Area? _currentArea;

    public DevicesPage(DevicesPageViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        vm.DeviceAreaChanged += () =>
        {
            RefreshDeviceList(); // RefreshDeviceList uses _currentArea to filter
        };
    }

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

    // Back button handler
    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

}