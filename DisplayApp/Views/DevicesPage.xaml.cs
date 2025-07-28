using DisplayLogic.Models;

namespace DisplayApp.Views;

public partial class DevicesPage : ContentPage, IQueryAttributable
{
    public DevicesPage()
    {
        InitializeComponent();

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
                        return d.area_id == area.area_id;
                    })
                    .ToList();

                DevicesCollection.ItemsSource = filtered;
            }
        }
    }
}