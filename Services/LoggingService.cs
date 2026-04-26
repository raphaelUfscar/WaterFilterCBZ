using Serilog;
using Serilog.Events;
using System.IO;

namespace WaterFilterCBZ.Services
{
    /// <summary>
    /// Centralizes Serilog configuration for the application.
    /// </summary>
    public static class LoggingService
    {
        public static void ConfigureLogging()
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WaterFilterCBZ",
                "logs");

            Directory.CreateDirectory(logPath);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Debug(outputTemplate: "[{Timestamp:HH:mm:ss.fff}] [{Level}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(
                    Path.Combine(logPath, "app-.txt"),
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                    restrictedToMinimumLevel: LogEventLevel.Information)
                .CreateLogger();

            Log.Information("Logging initialized. Log files: {LogPath}", logPath);
        }

        public static void CloseAndFlush()
        {
            Log.CloseAndFlush();
        }
    }
}
