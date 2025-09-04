using System.Diagnostics;
using DisplayApp.Services;
using DisplayLogic.Models;
using DisplayLogic.Services;
using DisplayLogic.SharedInterfaces;
using MQTTnet.Exceptions;

namespace DisplayApp.Views;

[QueryProperty(nameof(Device), "device")]
public partial class DeviceControlPage : ContentPage, IQueryAttributable
{
    private readonly IoTDevice _iotDevice;
    private MqttDevice _device;
    private readonly ILoadingService _loadingService;
    private readonly IUserNotifier _notifier;


    // Map 'device' query parameter to Device property
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

    public DeviceControlPage(IoTDevice iotDevice, ILoadingService loadingService, IUserNotifier notifier)
    {
        InitializeComponent();
        _iotDevice = iotDevice ?? throw new ArgumentNullException(nameof(iotDevice));
        _loadingService = loadingService ?? throw new ArgumentNullException(nameof(loadingService));
        _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
        if (_device != null) // Handle case where device is set before constructor finishes
        {
            LoadDevice();
            _ = SubscribeToDeviceStateAsync();
        }
    }

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
                System.Diagnostics.Debug.WriteLine("Invalid device in query");
            }
        }
        catch (Exception ex)
        {
            DeviceNameLabel.Text = "Error loading device";
            System.Diagnostics.Debug.WriteLine($"ApplyQueryAttributes error: {ex.Message}");
            await DisplayAlert("Error", $"Failed to load device: {ex.Message}", "OK");
        }
    }

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
        //RightPane.Children.Add(BuildTempDial());

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
                await _iotDevice.SubscribePersistentAsync(powerTopic, state =>
                {
                    using (MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        if (LeftPane.Children.OfType<Frame>().FirstOrDefault()?.Content is Image powerButton)
                        {
                            powerButton.Source = state == "on" ? "power_on_btn.png" : "power_off_btn.png";
                            if (powerButton.Parent is Frame frame)
                            {
                                frame.BorderColor = state == "on" ? Color.FromRgb(166, 120, 226) : Color.FromRgb(255, 0, 0);
                            }
                        }
                    }))
                    {
                    }
                });

                // Subscribe to brightness persistently
                await _iotDevice.SubscribePersistentAsync(brightnessTopic, brightness =>
                {
                    using (MainThread.InvokeOnMainThreadAsync(() =>
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
                    }))
                    {
                    }
                });

                // Subscribe to temperature persistently
                await _iotDevice.SubscribePersistentAsync(tempTopic, temp =>
                {
                    using (MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        if (RightPane.Children.OfType<VerticalStackLayout>().FirstOrDefault()?.Children.OfType<Frame>().FirstOrDefault()?.Content is Label tempLabel &&
                            int.TryParse(temp, out int value))
                        {
                            tempLabel.Text = $"{value}°";
                        }
                    }))
                    {
                    }
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

    // Back button handler
    private async void OnBackClicked(object sender, EventArgs e)
    {
        Debug.WriteLine("Back button clicked");
        await Shell.Current.GoToAsync("..");
        //await _notifier.NotifyAsync("Navigation", "Returned to previous page");
    }
}