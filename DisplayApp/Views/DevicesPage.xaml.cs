using System.Collections;
using DisplayApp.ViewModels;
using DisplayLogic.Models;

namespace DisplayApp.Views;

public partial class DevicesPage : ContentPage, IQueryAttributable
{
    public DevicesPage()
    {
        InitializeComponent();
        BindingContext = this;
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

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        try
        {
            if (query.TryGetValue("area", out object? areaObj) && areaObj is Area area &&
                query.TryGetValue("devices", out object? devicesObj) && devicesObj is List<MqttDevice> devices)
            {
                RoomNameLabel.Text = string.IsNullOrEmpty(area.name) ? "Unknown Area" : $"Devices in {area.name}";

                var filtered = devices
                    .Where(d =>
                    {
                        return string.Equals(d.area_id, area.area_id, StringComparison.OrdinalIgnoreCase);
                    })
                    .ToList();

                DevicesCollection.ItemsSource = filtered;

                if (filtered.Count == 0)
                {
                    RoomNameLabel.Text += " (No devices found)";
                }
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
}