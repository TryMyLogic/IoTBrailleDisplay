using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using DisplayApp.Services;
using DisplayApp.Views;
using DisplayLogic.Models;
using DisplayLogic.Services;
using DisplayLogic.SharedInterfaces;

namespace DisplayApp.ViewModels;

public class MainPageViewModel
{
    public ObservableCollection<Area> Areas { get; set; } = [];
    public ObservableCollection<MqttDevice> Devices { get; set; } = [];
    public ICommand OpenAreaCommand { get; }
    private readonly HomeAssistantWebSocketClient _haClient;
    private bool _isDataLoaded;
    private readonly ILoadingService _loadingService;
    private readonly IUserNotifier _notifier;


    public MainPageViewModel(HomeAssistantWebSocketClient haClient, ILoadingService loadingService, IUserNotifier notifier)
    {
        _haClient = haClient;
        _loadingService = loadingService;
        _notifier = notifier;
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
            // Show loading
            await _loadingService.ShowAsync("Loading data...");

            //Use a cancellation token with timeout for safety
            //using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await Task.Delay(3000);
            await LoadRealDataAsync();

            _isDataLoaded = true;
            ((Command)OpenAreaCommand).ChangeCanExecute();

            await _notifier.NotifyAsync("Data loaded successfully!", "Info");
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("WebSocket connection timed out. Loading mock data.");
            LoadMockData();
            _isDataLoaded = true;
            ((Command)OpenAreaCommand).ChangeCanExecute();

            await _notifier.NotifyAsync("Could not load live data. Using mock data.", "Warning");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading real data: {ex.Message}");
            LoadMockData();
            _isDataLoaded = true;
            ((Command)OpenAreaCommand).ChangeCanExecute();

            await _notifier.NotifyAsync("Error loading live data. Using mock data.", "Error");
        }
        finally
        {
            await _loadingService.HideAsync();
        }
    }

    private async Task LoadRealDataAsync()
    {
        if (!_haClient.IsConnected)
        {
            await _haClient.ConnectAsync();
            await _notifier.NotifyAsync("Connected to Home Assistant WebSocket", "Success");
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

        try
        {
            // Grab the latest devices
            List<MqttDevice> devicesCopy = _haClient.Devices ?? [];

            await Shell.Current.GoToAsync(nameof(DevicesPage), true, new Dictionary<string, object>
        {
            { "area", area },
            { "devices", devicesCopy }
        });
        }
        catch (Exception ex)
        {
            await _notifier.NotifyAsync("Navigation error", ex.Message);
        }
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
