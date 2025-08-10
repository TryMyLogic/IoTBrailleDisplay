using System.Collections.ObjectModel;
using System.Windows.Input;
using DisplayApp.Views;
using DisplayLogic.Models;
using DisplayLogic.Services;

namespace DisplayApp.ViewModels;

public class MainPageViewModel
{
    public ObservableCollection<Area> Areas { get; set; } = [];
    public ObservableCollection<MqttDevice> Devices { get; set; } = [];
    public ICommand OpenAreaCommand { get; }
    private readonly HomeAssistantWebSocketClient _haClient;
    private bool _isDataLoaded;

    public MainPageViewModel(HomeAssistantWebSocketClient haClient)
    {
        _haClient = haClient;
        OpenAreaCommand = new Command<Area>(
               async (area) =>
               {
                   await OpenAreaAsync(area);
               },
               (area) =>
               {
                   return _isDataLoaded && area != null // Only enabled after data is loaded
                   ;
               });

        // Start data loading
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        if (_haClient == null)
        {
            System.Diagnostics.Debug.WriteLine("HomeAssistantWebSocketClient not registered");
            LoadMockData();
            _isDataLoaded = true;
            ((Command)OpenAreaCommand).ChangeCanExecute();
            return;
        }

        try
        {
            await LoadRealDataAsync();
            _isDataLoaded = true;
            ((Command)OpenAreaCommand).ChangeCanExecute();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading real data: {ex.Message}");
            LoadMockData();
            _isDataLoaded = true;
            ((Command)OpenAreaCommand).ChangeCanExecute();
        }
    }

    private async Task LoadRealDataAsync()
    {
        if (!_haClient.IsConnected)
        {
            await _haClient.ConnectAsync();
            System.Diagnostics.Debug.WriteLine("Connected to Home Assistant WebSocket");
        }

        Areas.Clear();
        Devices.Clear();

        List<Area> areas = _haClient.Areas ?? [];
        foreach (Area area in areas)
        {
            Areas.Add(area);
        }

        List<MqttDevice> devices = _haClient.Devices ?? [];
        foreach (MqttDevice device in devices)
        {
            Devices.Add(device);
        }

        System.Diagnostics.Debug.WriteLine($"Loaded {Areas.Count} areas and {Devices.Count} devices");
        System.Diagnostics.Debug.WriteLine("=== AREAS ===");
        foreach (Area a in Areas)
        {
            System.Diagnostics.Debug.WriteLine($"Area: {a.area_id} - {a.name}");
        }

        System.Diagnostics.Debug.WriteLine("=== DEVICES ===");
        foreach (MqttDevice d in Devices)
        {
            System.Diagnostics.Debug.WriteLine($"Device: {d.id}, Name: {d.name}, AreaId: {d.area_id}");
        }
    }

    private async Task OpenAreaAsync(Area area)
    {
        if (area == null)
        {
            return;
        }

        // Always grab the most up-to-date devices from the client
        List<MqttDevice> devicesCopy = _haClient.Devices ?? [];

        await Shell.Current.GoToAsync(nameof(DevicesPage), true, new Dictionary<string, object>
            {
                { "area", area },
                { "devices", devicesCopy }
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
