using System.Collections.ObjectModel;
using System.Windows.Input;
using DisplayApp.Services;
using DisplayApp.Views;
using DisplayLogic.Models;
using DisplayLogic.Services;
using DisplayLogic.SharedInterfaces;

namespace DisplayApp.ViewModels;

/// <summary>
/// ViewModel for the <see cref="MainPage"/> in the MAUI application.
/// Manages the list of Areas and Devices, handles loading data from Home Assistant,
/// and coordinates navigation to device-specific pages.
/// </summary>
/// <remarks>
/// <para>
/// This ViewModel adheres to the MVVM pattern, exposing ObservableCollections for
/// data binding to the view and commands to handle user interaction.
/// </para>
/// <para>
/// It supports live data retrieval from a Home Assistant WebSocket client, with
/// fallback to mock data in case of connection failure or errors. Loading overlays
/// and user notifications are handled via injected services (<see cref="ILoadingService"/> and
/// <see cref="IUserNotifier"/>), keeping the ViewModel testable and decoupled from UI elements.
/// </para>
/// </remarks>
public class MainPageViewModel
{
    /// <summary>
    /// Collection of Areas (rooms) to be displayed in the UI.
    /// Bound to a ListView or CollectionView in the MainPage.
    /// </summary>
    public ObservableCollection<Area> Areas { get; set; } = [];

    /// <summary>
    /// Collection of devices available in the selected Area.
    /// Used for navigation and device display in the UI.
    /// </summary>
    public ObservableCollection<MqttDevice> Devices { get; set; } = [];

    /// <summary>
    /// Command invoked when a user selects an Area.
    /// Triggers navigation to the <see cref="DevicesPage"/> with devices for that Area.
    /// </summary>
    public ICommand OpenAreaCommand { get; }

    private readonly IHomeAssistantWebSocketClient _haClient;
    private bool _isDataLoaded;
    private readonly ILoadingService _loadingService;
    private readonly IUserNotifier _notifier;

    /// <summary>
    /// Initializes a new instance of <see cref="MainPageViewModel"/>.
    /// </summary>
    /// <param name="haClient">The Home Assistant WebSocket client for real-time data.</param>
    /// <param name="loadingService">Service to show/hide loading overlays.</param>
    /// <param name="notifier">Service to display user notifications.</param>
    public MainPageViewModel(IHomeAssistantWebSocketClient haClient, ILoadingService loadingService, IUserNotifier notifier)
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

    /// <summary>
    /// Initializes the ViewModel by loading data from Home Assistant.
    /// Falls back to mock data if the client is null or fails to connect.
    /// </summary>
    private async Task InitializeAsync()
    {
        if (_haClient == null)
        {
            LoadMockData();
            _isDataLoaded = true;
            ((Command)OpenAreaCommand).ChangeCanExecute();
            return;
        }

        try
        {
            // Show loading
            await _loadingService.ShowAsync("Loading data...");
            await LoadRealDataAsync();

            _isDataLoaded = true;
            ((Command)OpenAreaCommand).ChangeCanExecute();
        }
        catch (OperationCanceledException)
        {
            LoadMockData();
            _isDataLoaded = true;
            ((Command)OpenAreaCommand).ChangeCanExecute();

            await _notifier.NotifyAsync("Warning", "Could not load live data. Using mock data.");
        }
        catch (Exception ex)
        {
            LoadMockData();
            _isDataLoaded = true;
            ((Command)OpenAreaCommand).ChangeCanExecute();

            await _notifier.NotifyAsync("Error", "Error loading live data. Using mock data.");
        }
        finally
        {
            await _loadingService.HideAsync();
        }
    }

    /// <summary>
    /// Loads live data from the Home Assistant WebSocket client into the Areas and Devices collections.
    /// </summary>
    /// <returns>A task representing the asynchronous load operation.</returns>
    /// <remarks>
    /// Clears previous data before adding new items to ensure UI reflects the latest state.
    /// </remarks>
    private async Task LoadRealDataAsync()
    {
        if (!_haClient.IsConnected)
        {
            await _haClient.ConnectAsync();
        }

        Areas.Clear();
        Devices.Clear();

        List<Area> areas = _haClient.Areas ?? [];
        foreach (Area area in areas)
        {
            Areas.Add(area);
        }
    }

    /// <summary>
    /// Navigates to the <see cref="DevicesPage"/> for the selected Area.
    /// Passes the Area and the list of devices as navigation parameters.
    /// </summary>
    /// <param name="area">The selected Area to open.</param>
    /// <returns>A task representing the asynchronous navigation operation.</returns>
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

    /// <summary>
    /// Loads mock Areas and Devices for testing or fallback scenarios.
    /// </summary>
    /// <remarks>
    /// Provides basic UI data when live connection is unavailable.
    /// Ensures the app can still function and display meaningful content.
    /// </remarks>
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
