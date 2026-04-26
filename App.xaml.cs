using System.Configuration;
using System.Data;
using System.Windows;
using WaterFilterCBZ.Services;
using Serilog;

namespace WaterFilterCBZ
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Initialize logging
            LoggingService.ConfigureLogging();
            Log.Information("WaterFilterCBZ application starting");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.Information("WaterFilterCBZ application exiting");
            LoggingService.CloseAndFlush();
            base.OnExit(e);
        }
    }

}
