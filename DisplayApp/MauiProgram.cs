using DisplayApp.Services;
using DisplayApp.Views;
using DisplayLogic.Services;
using DisplayLogic.SharedInterfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DisplayApp
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            MauiAppBuilder builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // Register services
            //builder.Services.AddTransient<IUserINotifier, PopupNotifier>(); // Simple notifier for now (logs to console/output)
            builder.Services.AddTransient<IUserNotifier, PopupNotifier>();
            builder.Services.AddSingleton<ILoadingService, LoadingService>();
            builder.Services.AddSingleton<IWebSocketClient>(sp =>
            {
                return new WebSocketClient("ws://localhost:8123/api/websocket", sp.GetRequiredService<IUserNotifier>());
            });
            builder.Services.AddSingleton<HomeAssistantWebSocketClient>(sp =>
            {
                return new HomeAssistantWebSocketClient(
                                    sp.GetRequiredService<IWebSocketClient>(),
                                    sp.GetRequiredService<IUserNotifier>(),
                                    "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJkNmU0NjZkZDZmNzQ0OGNmYjI2NmFlMzBjNTc4ZWM5MiIsImlhdCI6MTc1NDc0NzgzMSwiZXhwIjoyMDcwMTA3ODMxfQ.zqueo1YB8Az69HUtID-1Gfxso690VXUHPMB8qJnuNfo" // Get from HA Profile > Long-Lived Access Tokens
                                );
            });
            builder.Services.AddSingleton<IoTDevice>(sp =>
            {
                var iotDevice = new IoTDevice(
                                    mqttBroker: "localhost", // e.g., same as HA if integrated
                                    restEndpoint: "http://localhost:8123/api" // Fallback REST
                                );
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await iotDevice.ConnectAsync();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Background ConnectAsync error: {ex.Message}");
                    }
                });
                return iotDevice;
            });
            builder.Services.AddSingleton<DisplayApp.ViewModels.MainPageViewModel>();
            builder.Services.AddSingleton<MainPage>();
            builder.Services.AddTransient<DeviceControlPage>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }

    }
    public class ConsoleUserNotifier : DisplayLogic.SharedInterfaces.IUserNotifier
    {
        public async Task NotifyAsync(string title, string message)
        {
            Console.WriteLine($"{title}: {message}");
            await Task.CompletedTask;
        }
    }


}
