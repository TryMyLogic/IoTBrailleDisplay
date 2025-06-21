using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;

namespace DisplayApp
{
    internal static class ServiceRegistry
    {
        public static IServiceCollection RegisterServices(this IServiceCollection services)
        {
            IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();

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

            // Conditional compilation directives for platform specific services. (Note: Do not indent or it will not work)
#if ANDROID

#elif WINDOWS

#elif IOS

#elif MACCATALYST

#else
            // Add defaults as fallback here. 
#endif

            return services;
        }
    }

}
