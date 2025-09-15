using System.Diagnostics;
using System.Reflection;
using DisplayApp.Services;
using DisplayApp.ViewModels;
using DisplayApp.Views;
using DisplayLogic.Services;
using DisplayLogic.SharedInterfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
#if ANDROID
using DisplayApp.Platforms.Android;
#elif WINDOWS
using DisplayApp.Platforms.Windows;
#endif

namespace DisplayApp
{
    internal static class ServiceRegistry
    {
        public static IServiceCollection RegisterCustomServices(this IServiceCollection services)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using Stream? stream = assembly.GetManifestResourceStream("DisplayApp.appsettings.json"); // <-- Make sure namespace matches

            IConfiguration configuration = new ConfigurationBuilder()
                .AddJsonStream(stream!)
                .Build();

            // Get config values
            string webSocketProtocol = configuration["HomeAssistant:WebSocketProtocol"] ?? "wss"; // Assume websocket secure by default, similar to HTTPS
            string homeAssistantDomain = configuration["HomeAssistant:HomeAssistantDomain"] ?? throw new InvalidOperationException("HomeAssistantDomain not found in configuration.");
            string webSocketPort = configuration["HomeAssistant:WebSocketPort"] ?? throw new InvalidOperationException("WebSocketPort not found in configuration.");
            string mqttBrokerDomain = configuration["HomeAssistant:MqttBrokerDomain"] ?? throw new InvalidOperationException("MqttBrokerDomain not found in configuration.");
            string accessToken = configuration["HomeAssistant:AccessToken"] ?? throw new InvalidOperationException("AccessToken not found in configuration."); // Get from HA Profile > Long-Lived Access Tokens
            Log.Debug("This is a test debug message");
            Debug.WriteLine($"Protocol: {webSocketProtocol}");
            Debug.WriteLine($"HomeAssistantDomain: {homeAssistantDomain}");
            Debug.WriteLine($"WebSocketPort: {webSocketPort}");
            Debug.WriteLine($"MqttBrokerDomain: {mqttBrokerDomain}");
            Debug.WriteLine($"AccessToken: {accessToken}");

            // From config values, dynamically build what is required
            string haWebSocketUrl = $"{webSocketProtocol}://{homeAssistantDomain}:{webSocketPort}/api/websocket";

            // Setup logger
            string logsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            if (!Directory.Exists(logsDirectory))
            {
                Directory.CreateDirectory(logsDirectory);
            }
            string logFilePath = Path.Combine(logsDirectory, "RuntimeLog_.txt");

            Log.Logger = new LoggerConfiguration()
           .MinimumLevel.Debug()
           .WriteTo.File(logFilePath,
                  rollingInterval: RollingInterval.Day,
                  retainedFileCountLimit: 7,
                  fileSizeLimitBytes: 104857600, // 100MB
                  outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level}] {Message}{NewLine}{Exception}").ReadFrom.Configuration(configuration)
            .WriteTo.Debug(restrictedToMinimumLevel: LogEventLevel.Debug)
            .MinimumLevel.Override("Polly", LogEventLevel.Warning)
            .CreateLogger();

            // Global services
            services.AddLogging(builder =>
            {
                builder.AddSerilog(dispose: true);
            });

            // Global UI Related Services
            services.AddTransient<IUserNotifier, PopupNotifier>();
            services.AddSingleton<ILoadingService, LoadingService>();
            services.AddSingleton<MainPageViewModel>();
            services.AddSingleton<MainPage>();
            services.AddTransient<DeviceControlPage>();
            services.AddTransient<DevicesPageViewModel>();
            services.AddTransient<DevicesPage>();

            // Global Logic Related Services
            services.AddSingleton<IWebSocketClient>(sp =>
            {
                return new WebSocketClient(
                    haWebSocketUrl,
                    sp.GetRequiredService<IUserNotifier>(),
                    sp.GetRequiredService<ILogger<WebSocketClient>>()
                );
            });

            services.AddSingleton<IHomeAssistantWebSocketClient>(sp =>
            {
                return new HomeAssistantWebSocketClient(
                    sp.GetRequiredService<IWebSocketClient>(),
                    sp.GetRequiredService<IUserNotifier>(),
                    accessToken,
                    sp.GetRequiredService<ILogger<HomeAssistantWebSocketClient>>()
                );
            });

            services.AddSingleton<IIoTDevice>(sp =>
            {
                ILogger<IoTDevice> logger = sp.GetRequiredService<ILogger<IoTDevice>>();
                IoTDevice iotDevice = new(mqttBroker: mqttBrokerDomain, restEndpoint: "");

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await iotDevice.ConnectAsync();
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Background ConnectAsync error occurred: {ErrorMessage}", ex.Message);
                    }
                });

                return iotDevice;
            });

            services.AddSingleton<IBrailleDisplay, BrailleDisplay>();
            // Conditional compilation directives for platform specific services. (Note: Do not indent or it will not work)
#if ANDROID
            services.AddSingleton<IConnectionStrategy, BluetoothConnectionStrategyAndroid>();
#elif WINDOWS
            services.AddSingleton<IConnectionStrategy, BluetoothConnectionStrategyWindows>();
#elif IOS

#elif MACCATALYST

#else
            services.AddSingleton<IConnectionStrategy, MockBluetoothConnection>();
#endif

            return services;
        }
    }

}
