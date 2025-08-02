using System.Diagnostics;
using DisplayLogic.Models;

namespace DisplayApp.Views;

[QueryProperty(nameof(Device), "device")]
public partial class DeviceControlPage : ContentPage
{
    private MqttDevice _device;

    public DeviceControlPage()
    {
        InitializeComponent();
        _device = new MqttDevice
        {
            id = Guid.NewGuid().ToString(),
            default_manufacturer = "Unknown",
            default_model = "Unknown",
            default_name = "Unknown Device",
            manufacturer = "Unknown",
            model = "Unknown"
        };
    }

    // ── 1.  bindable property for device ─────────────────────────────────
    public static readonly BindableProperty DeviceProperty =
        BindableProperty.Create(nameof(Device), typeof(MqttDevice), typeof(DeviceControlPage), null);

    public MqttDevice Device
    {
        get => _device;
        set
        {
            _device = value;
            LoadDevice();
        }
    }

    private void LoadDevice()
    {
        if (Device == null)
        {
            return;
        }

        DeviceNameLabel.Text = Device.name ?? Device.model ?? "Unknown device";
        LeftPane.Children.Clear();
        RightPane.Children.Clear();

        bool looksLikeLight = (Device.name ?? "").Contains("light", StringComparison.CurrentCultureIgnoreCase)
                              || (Device.model ?? "").Contains("hue", StringComparison.CurrentCultureIgnoreCase);

        bool looksLikeThermostat = (Device.name ?? "").Contains("thermo", StringComparison.CurrentCultureIgnoreCase)
                              || (Device.model ?? "").Contains("nest", StringComparison.CurrentCultureIgnoreCase)
                              || (Device.model ?? "").Contains("tstat", StringComparison.CurrentCultureIgnoreCase);

        LeftPane.Children.Add(DeviceControlPage.BuildPowerButton());

        if (looksLikeLight)
        {
            RightPane.Children.Add(DeviceControlPage.BuildBrightnessSlider());
        }
        else if (looksLikeThermostat)
        {
            RightPane.Children.Add(BuildTempDial());
        }
        else
        {
            RightPane.Children.Add(DeviceControlPage.BuildGenericInfo());
        }
    }

    // ───────────────── helper builders ────────────────────────────────────
    private static Frame BuildPowerButton()
    {
        Image icon = new()
        {
            Source = "power_off_btn.png",
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
            BorderColor = Color.FromRgb(255, 0, 0),
            HasShadow = true,
            Content = icon,
            Padding = 0,
        };

        TapGestureRecognizer tap = new();
        tap.Tapped += (_, _) =>
        {
            if (icon.Source?.ToString()?.Contains("power_off_btn.png") == true)
            {
                icon.Source = "power_on_btn.png";
                frame.BorderColor = Color.FromRgb(166, 120, 226);
            }
            else
            {
                icon.Source = "power_off_btn.png";
                frame.BorderColor = Color.FromRgb(255, 0, 0);
            }
        };
        frame.GestureRecognizers.Add(tap);

        return frame;
    }

    private static VerticalStackLayout BuildBrightnessSlider()
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
        slider.ValueChanged += (_, e) =>
        {
            label.Text = $"Brightness: {e.NewValue:0}%";
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

    private static void ChangeTemp(Frame dial, int delta)
    {
        if (dial.Content is Label lbl &&
            int.TryParse(lbl.Text.Replace("°", ""), out int t))
        {
            t = Math.Clamp(t + delta, 10, 30);
            lbl.Text = $"{t}°";
        }
    }

    // Back button handler
    private async void OnBackClicked(object sender, EventArgs e)
    {
        Debug.WriteLine("Back button clicked");
        await Shell.Current.GoToAsync("..");
    }
}