using System.Diagnostics;
using DisplayLogic.Models;
using DisplayLogic.Services;
using MQTTnet.Exceptions;

namespace DisplayApp.Views;

[QueryProperty(nameof(Device), "device")]
public partial class DeviceControlPage : ContentPage, IQueryAttributable
{
    private readonly IoTDevice _iotDevice;
    private MqttDevice _device;


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

    public DeviceControlPage(IoTDevice iotDevice)
    {
        InitializeComponent();
        _iotDevice = iotDevice ?? throw new ArgumentNullException(nameof(iotDevice));
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
                _device = device;
                LoadDevice();
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
        RightPane.Children.Add(BuildBrightnessSlider());

        if (isShellySwitch)
        {
            RightPane.Children.Add(DeviceControlPage.BuildGenericInfo());
        }
        else
        {
            RightPane.Children.Add(DeviceControlPage.BuildGenericInfo());
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

                string stateTopic = $"shellies/{deviceId}/relay/0";
                string state = await _iotDevice.SubscribeAsync(stateTopic);

                if (LeftPane.Children.OfType<Frame>().FirstOrDefault()?.Content is Image powerButton)
                {
                    powerButton.Source = state == "on" ? "power_on_btn.png" : "power_off_btn.png";
                    if (powerButton.Parent is Frame frame)
                    {
                        frame.BorderColor = state == "on" ? Color.FromRgb(166, 120, 226) : Color.FromRgb(255, 0, 0);
                    }
                }

                System.Diagnostics.Debug.WriteLine($"Subscribed to MQTT topic: {stateTopic}, State: {state}");
                LoadingIndicator.IsRunning = false;
                LoadingIndicator.IsVisible = false;
                return;
            }
            catch (MqttClientNotConnectedException ex)
            {
                System.Diagnostics.Debug.WriteLine($"MQTT subscribe error (attempt {attempt + 1}/{maxRetries}): {ex.Message}");
                if (attempt == maxRetries - 1) // Only show alert on last failure
                {
                    await DisplayAlert("Error", $"Failed to subscribe to device state after {maxRetries} attempts: {ex.Message}", "OK");
                }
                if (attempt < maxRetries - 1)
                {
                    await Task.Delay(delayMs); // Wait before retrying
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
    }

    private VerticalStackLayout BuildBrightnessSlider()
    {
        var label = new Label
        {
            Text = "Brightness",
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
            label.Text = $"Brightness: {e.NewValue:0}%";
            try
            {
                string deviceId = _device?.identifiers?.FirstOrDefault()?[1] ?? string.Empty;
                if (string.IsNullOrEmpty(deviceId))
                {
                    await DisplayAlert("Error", "Device ID not found", "OK");
                    return;
                }

                string topic = $"shellies/{deviceId}/dimmer/brightness";
                string payload = ((int)e.NewValue).ToString();
                await EnsureConnectedAndPublishAsync(topic, payload);

                System.Diagnostics.Debug.WriteLine($"Published MQTT: {topic} = {payload}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MQTT brightness publish error: {ex.Message}");
                await DisplayAlert("Error", $"Failed to set brightness: {ex.Message}", "OK");
            }
        };

        return new VerticalStackLayout
        {
            HorizontalOptions = LayoutOptions.Center,
            Children = { label, slider }
        };
    }

    private static VerticalStackLayout BuildTempDial()
    {
        var dial = new Frame
        {
            BackgroundColor = Color.FromRgb(38, 37, 57), // #262539
            CornerRadius = 100,
            HeightRequest = 160,
            WidthRequest = 160,
            HasShadow = true,
            Content = new Label
            {
                Text = "20°",
                FontAttributes = FontAttributes.Bold,
                FontSize = 22,
                TextColor = Color.FromRgb(166, 120, 226),
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            }
        };

        var minus = new Label { Text = "–", FontSize = 28, TextColor = dial.BorderColor };
        var plus = new Label { Text = "+", FontSize = 28, TextColor = dial.BorderColor };

        // Simple tap gestures for demo purposes
        minus.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(() =>
            {
                DeviceControlPage.ChangeTemp(dial, -1);
            })
        });
        plus.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(() =>
            {
                DeviceControlPage.ChangeTemp(dial, +1);
            })
        });

        return new VerticalStackLayout
        {
            HorizontalOptions = LayoutOptions.Center,
            Spacing = 20,
            Children =
            {
                dial,
                new HorizontalStackLayout
                {
                    HorizontalOptions = LayoutOptions.Center,
                    Spacing = 40,
                    Children = { minus, new Label { Text="Auto", TextColor=dial.BorderColor }, plus }
                }
            }
        };
    }

    private static void ChangeTemp(Frame dial, int delta)
    {
        if (dial.Content is Label lbl &&
            int.TryParse(lbl.Text.Replace("°", ""), out int t))
        {
            t = Math.Clamp(t + delta, 10, 30);
            lbl.Text = $"{t}°";
        }
    }

    private static Label BuildGenericInfo()
    {
        return new Label
        {
            Text = "No special controls available.",
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
    }
}