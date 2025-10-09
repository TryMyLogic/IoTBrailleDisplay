using System.Diagnostics;
using DisplayApp.Services;
using DisplayLogic.Models;
using DisplayLogic.Services;
using DisplayLogic.SharedInterfaces;
using MQTTnet.Exceptions;

namespace DisplayApp.Views;

/// <summary>
/// Represents the page for controlling a single IoT device.
/// Provides device-specific controls such as power toggle, brightness, or temperature adjustments.
/// Supports real-time updates via MQTT subscriptions and communicates with <see cref="IIoTDevice"/>.
/// </summary>
[QueryProperty(nameof(Device), "device")]
public partial class DeviceControlPage : ContentPage, IQueryAttributable
{
    /// <summary>
    /// The IoT backend service for publishing and subscribing to MQTT topics.
    /// </summary>
    private readonly IIoTDevice _iotDevice;

    /// <summary>
    /// The device being controlled on this page.
    /// Setting this property triggers the UI to load and subscribes to device state updates.
    /// </summary>
    private MqttDevice _device;

    /// <summary>
    /// Service for showing/hiding global loading overlays.
    /// </summary>
    private readonly ILoadingService _loadingService;

    /// <summary>
    /// Service for displaying notifications or alerts to the user.
    /// </summary>
    private readonly IUserNotifier _notifier;


    /// <summary>
    /// Gets or sets the device that this page will control.
    /// When set, the UI is initialized and device state subscriptions are started.
    /// </summary>
    public MqttDevice Device
    {
        get => _device;
        set
        {
            _device = value;
            LoadDevice(); // Load UI when device is set
            _ = SubscribeToDeviceStateAsync(); // Start subscription asynchronously
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceControlPage"/> class.
    /// Sets up services and prepares the page for device control.
    /// </summary>
    /// <param name="iotDevice">The MQTT device service used to publish and subscribe messages.</param>
    /// <param name="loadingService">Service to show and hide loading indicators.</param>
    /// <param name="notifier">Service for user notifications.</param>
    /// <exception cref="ArgumentNullException">Thrown if any required service is null.</exception>
    public DeviceControlPage(IIoTDevice iotDevice, ILoadingService loadingService, IUserNotifier notifier)
    {
        InitializeComponent();
        _iotDevice = iotDevice ?? throw new ArgumentNullException(nameof(iotDevice));
        _loadingService = loadingService ?? throw new ArgumentNullException(nameof(loadingService));
        _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));

        // Handle case where device is set before constructor completes
        if (_device != null)
        {
            LoadDevice();
            _ = SubscribeToDeviceStateAsync();
        }
    }

    /// <summary>
    /// Applies query attributes passed during navigation. Maps the "device" parameter to the <see cref="Device"/> property.
    /// </summary>
    /// <param name="query">Dictionary containing query attributes.</param>
    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        try
        {
            if (query.TryGetValue("device", out object? deviceObj) && deviceObj is MqttDevice device)
            {
                await _loadingService.ShowAsync("Opening device...");
                _device = device;
                try
                {

                    LoadDevice();
                }
                finally
                {
                    await _loadingService.HideAsync();
                }


                await SubscribeToDeviceStateAsync();
            }
            else
            {
                DeviceNameLabel.Text = "No device selected";
            }
        }
        catch (Exception ex)
        {
            DeviceNameLabel.Text = "Error loading device";
            System.Diagnostics.Debug.WriteLine($"ApplyQueryAttributes error: {ex.Message}");
            await DisplayAlert("Error", $"Failed to load device: {ex.Message}", "OK");
        }
    }

    /// <summary>
    /// Populates UI elements based on the current device.
    /// Creates power buttons, sliders, and informational labels.
    /// </summary>
    private void LoadDevice()
    {
        if (_device == null)
        {
            DeviceNameLabel.Text = "No device selected";
            return;
        }

        DeviceNameLabel.Text = _device.name ?? _device.model ?? "Unknown Device";
        LeftPane.Children.Clear();
        RightPane.Children.Clear();

        bool isShellySwitch = _device.manufacturer?.Contains("Shelly", StringComparison.OrdinalIgnoreCase) == true &&
                             _device.model?.Contains("Shelly 1PM", StringComparison.OrdinalIgnoreCase) == true;

        LeftPane.Children.Add(BuildPowerButton());
        RightPane.Children.Add(BuildControlSlider(isShellySwitch));

        if (isShellySwitch)
        {
            RightPane.Children.Add(BuildGenericInfo());
        }
        else
        {
            RightPane.Children.Add(BuildGenericInfo());
        }

        LoadingIndicator.IsRunning = true;
        LoadingIndicator.IsVisible = true;
    }

    /// <summary>
    /// Builds a power toggle button for the device.
    /// Includes optimistic UI updates and publishes MQTT messages.
    /// </summary>
    /// <returns>A <see cref="Frame"/> containing the power button UI.</returns>
    private Frame BuildPowerButton()
    {
        Image icon = new()
        {
            Source = "power_off_btn.png", // Default to off
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Aspect = Aspect.AspectFill
        };

        Frame frame = new()
        {
            BackgroundColor = Color.FromRgb(30, 29, 45),
            CornerRadius = 100,
            HeightRequest = 200,
            WidthRequest = 200,
            BorderColor = Color.FromRgb(255, 0, 0), // Red for off
            HasShadow = true,
            Content = icon,
            Padding = 0
        };

        TapGestureRecognizer tap = new();
        tap.Tapped += async (_, _) =>
        {
            try
            {
                string deviceId = _device.identifiers?.FirstOrDefault()?[1] ?? string.Empty;
                if (string.IsNullOrEmpty(deviceId))
                {
                    await DisplayAlert("Error", "Device ID not found", "OK");
                    return;
                }

                string topic = $"shellies/{deviceId}/relay/0";
                string payload = icon.Source?.ToString()?.Contains("power_off_btn.png") == true ? "on" : "off";
                await EnsureConnectedAndPublishAsync(topic, payload);

                // Update UI immediately (optimistic update)
                icon.Source = payload == "on" ? "power_on_btn.png" : "power_off_btn.png";
                frame.BorderColor = payload == "on" ? Color.FromRgb(166, 120, 226) : Color.FromRgb(255, 0, 0);

                System.Diagnostics.Debug.WriteLine($"Published MQTT: {topic} = {payload}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MQTT publish error: {ex.Message}");
                await DisplayAlert("Error", $"Failed to control device: {ex.Message}", "OK");
            }
        };
        frame.GestureRecognizers.Add(tap);

        return frame;
    }

    /// <summary>
    /// Ensures that the MQTT client is connected before publishing a message.
    /// </summary>
    /// <param name="topic">MQTT topic to publish to.</param>
    /// <param name="payload">Payload to publish.</param>
    private async Task EnsureConnectedAndPublishAsync(string topic, string payload)
    {
        if (!_iotDevice.IsConnected)
        {
            try
            {
                await _iotDevice.ConnectAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Connection retry error: {ex.Message}");
                throw; // Re-throw to be caught by the caller
            }
        }
        await _iotDevice.PublishAsync(topic, payload);
    }

    /// <summary>
    /// Subscribes to device state topics for power, brightness, and temperature.
    /// Updates the UI in real-time when device state changes occur.
    /// </summary>
    private async Task SubscribeToDeviceStateAsync()
    {
        const int maxRetries = 5;
        const int delayMs = 1000;

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                string deviceId = _device?.identifiers?.FirstOrDefault()?[1] ?? string.Empty;
                if (string.IsNullOrEmpty(deviceId))
                {
                    System.Diagnostics.Debug.WriteLine("Cannot subscribe: Device ID not found");
                    return;
                }

                string powerTopic = $"shellies/{deviceId}/relay/0";
                string brightnessTopic = $"shellies/{deviceId}/dimmer/brightness";
                string tempTopic = $"shellies/{deviceId}/temp/set"; // Adjust based on your device

                // Subscribe to power state persistently
                await _iotDevice.SubscribePersistentAsync(powerTopic, async state =>
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        if (LeftPane.Children.OfType<Frame>().FirstOrDefault()?.Content is Image powerButton)
                        {
                            powerButton.Source = state == "on" ? "power_on_btn.png" : "power_off_btn.png";
                            if (powerButton.Parent is Frame frame)
                            {
                                frame.BorderColor = state == "on" ? Color.FromRgb(166, 120, 226) : Color.FromRgb(255, 0, 0);
                            }
                        }
                    });
                });

                // Subscribe to brightness persistently
                await _iotDevice.SubscribePersistentAsync(brightnessTopic, async brightness =>
                {
                    await (MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        if (LeftPane.Children.OfType<VerticalStackLayout>().FirstOrDefault()?.Children.OfType<Slider>().FirstOrDefault() is Slider slider &&
                            LeftPane.Children.OfType<VerticalStackLayout>().FirstOrDefault()?.Children.OfType<Label>().FirstOrDefault() is Label label)
                        {
                            if (int.TryParse(brightness, out int value))
                            {
                                slider.Value = value;
                                label.Text = $"Brightness: {value}%";
                            }
                        }
                    }));
                });

                // Subscribe to temperature persistently
                await _iotDevice.SubscribePersistentAsync(tempTopic, async temp =>
                {
                    await (MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        if (RightPane.Children.OfType<VerticalStackLayout>().FirstOrDefault()?.Children.OfType<Frame>().FirstOrDefault()?.Content is Label tempLabel &&
                            int.TryParse(temp, out int value))
                        {
                            tempLabel.Text = $"{value}°";
                        }
                    }));
                });

                LoadingIndicator.IsRunning = false;
                LoadingIndicator.IsVisible = false;
                return;
            }
            catch (MqttClientNotConnectedException ex)
            {
                System.Diagnostics.Debug.WriteLine($"MQTT subscribe error (attempt {attempt + 1}/{maxRetries}): {ex.Message}");
                if (attempt == maxRetries - 1)
                {
                    await DisplayAlert("Error", $"Failed to subscribe to device state after {maxRetries} attempts: {ex.Message}", "OK");
                }
                if (attempt < maxRetries - 1)
                {
                    await Task.Delay(delayMs);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MQTT subscribe error: {ex.Message}");
                await DisplayAlert("Error", $"Failed to subscribe to device state: {ex.Message}", "OK");
                LoadingIndicator.IsRunning = false;
                LoadingIndicator.IsVisible = false;
                return;
            }
        }

        LoadingIndicator.IsRunning = false;
        LoadingIndicator.IsVisible = false;
    }

    /// <summary>
    /// Builds a slider control for brightness or temperature adjustment depending on device type.
    /// </summary>
    /// <param name="isShellySwitch">Indicates whether the device supports brightness control.</param>
    /// <returns>A <see cref="VerticalStackLayout"/> containing a slider and label.</returns>
    private VerticalStackLayout BuildControlSlider(bool isShellySwitch)
    {
        var infoLabel = new Label
        {
            Text = isShellySwitch ? "Brightness" : "--",
            TextColor = Color.FromRgb(166, 120, 226),
            HorizontalOptions = LayoutOptions.Center
        };

        var slider = new Slider
        {
            Minimum = 0,
            Maximum = 100,
            Value = 50,
            WidthRequest = 220
        };
        slider.ValueChanged += async (_, e) =>
        {
            infoLabel.Text = isShellySwitch ? $"Brightness: {e.NewValue:0}%" : $"{e.NewValue:0}°";
            try
            {
                string deviceId = _device?.identifiers?.FirstOrDefault()?[1] ?? string.Empty;
                if (string.IsNullOrEmpty(deviceId))
                {
                    await DisplayAlert("Error", "Device ID not found", "OK");
                    return;
                }

                string topic = isShellySwitch ? $"shellies/{deviceId}/dimmer/brightness" : $"shellies/{deviceId}/temp/set";
                string payload = ((int)e.NewValue).ToString();
                await EnsureConnectedAndPublishAsync(topic, payload);

                System.Diagnostics.Debug.WriteLine($"Published MQTT: {topic} = {payload}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MQTT control publish error: {ex.Message}");
                await DisplayAlert("Error", $"Failed to set control value: {ex.Message}", "OK");
            }
        };

        return new VerticalStackLayout
        {
            HorizontalOptions = LayoutOptions.Center,
            Children = { infoLabel, slider }
        };
    }

    /// <summary>
    /// Builds a generic label for informational purposes when the device has no special controls.
    /// </summary>
    /// <returns>A <see cref="Label"/> displaying placeholder text.</returns>
    private static Label BuildGenericInfo()
    {
        return new Label
        {
            Text = "No special controls available",
            FontSize = 16,
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.Center
        };
    }

    /// <summary>
    /// Handles the back button click event.
    /// Navigates back to the previous page in the navigation stack.
    ///
    /// Its a back button, what more do you want from me?
    /// 
    /// </summary>
    private async void OnBackClicked(object sender, EventArgs e)
    {
        Debug.WriteLine("Back button clicked");
        await Shell.Current.GoToAsync("..");
    }
}