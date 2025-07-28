using System.Collections.ObjectModel;
using System.Windows.Input;
using DisplayApp.Views;
using DisplayLogic.Models;

namespace DisplayApp.ViewModels;

public class MainPageViewModel
{
    public ObservableCollection<Area> Areas { get; set; } = [];
    public ObservableCollection<MqttDevice> Devices { get; set; } = [];

    public ICommand OpenAreaCommand { get; }

    public MainPageViewModel()
    {
        LoadMockData(); // For testing, replace later with real data

        OpenAreaCommand = new Command<Area>(async (area) =>
        {
            if (area != null)
            {
                // Copy devices to a list
                var devicesCopy = Devices.ToList();

                // Navigate to DevicesPage, passing area and devices
                await Shell.Current.GoToAsync(nameof(DevicesPage), true, new Dictionary<string, object>
                    {
                        { "area", area },
                        { "devices", devicesCopy }
                    });
            }
        });
    }

    private void LoadMockData()
    {
        Areas.Add(new Area { area_id = "kitchen", name = "Kitchen" });
        Areas.Add(new Area { area_id = "living_room", name = "Living Room" });

        Devices.Add(new MqttDevice
        {
            id = "device1",
            name = "Kitchen Light",
            area_id = "kitchen",
            manufacturer = "Philips",
            model = "Hue",
            default_manufacturer = "Generic",
            default_model = "ModelA",
            default_name = "Light A",
            identifiers = [["mqtt", "kitchen_light"]]
        });
    }
}
