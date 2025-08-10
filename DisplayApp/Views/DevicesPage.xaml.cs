using System.Collections;
using DisplayApp.ViewModels;
using DisplayLogic.Models;

namespace DisplayApp.Views;

public partial class DevicesPage : ContentPage, IQueryAttributable
{
    public DevicesPage()
    {
        InitializeComponent();
        BindingContext = new DevicesPageViewModel();
    }

    private async void OnDeviceSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection is IList selectionList && selectionList.Count > 0 && selectionList[0] is MqttDevice selectedDevice)
        {
            // Navigate to control page with selected device
            await Shell.Current.GoToAsync(nameof(DeviceControlPage), new Dictionary<string, object>
            {
                ["device"] = selectedDevice
            });

            // Optional: Deselect item
            ((CollectionView)sender).SelectedItem = null;
        }
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("area", out object? areaObj) && query.TryGetValue("devices", out object? devicesObj))
        {
            if (areaObj is Area area && devicesObj is List<MqttDevice> devices)
            {
                RoomNameLabel.Text = $"Devices in {area.name}";

                var filtered = devices
                .Where(d =>
                {
                    return string.Equals(d.area_id, area.area_id, StringComparison.OrdinalIgnoreCase);
                })
                .ToList();
                DevicesCollection.ItemsSource = devices;
            }
        }
    }
}