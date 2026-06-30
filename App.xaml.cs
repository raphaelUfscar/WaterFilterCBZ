using System.Configuration;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using WaterFilterCBZ.Models;
using WaterFilterCBZ.Services;
using Serilog;

namespace WaterFilterCBZ
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    // Application bootstrap / composition root: no unit-testable logic.
    // Excluded from unit-test coverage (IEC 62304 traceability).
    [ExcludeFromCodeCoverage]
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Initialize logging
            LoggingService.ConfigureLogging();
            Log.Information("WaterFilterCBZ application starting");

            // Apply user-configurable sensor ranges (RC-008 / SRS-C-003); missing or invalid
            // values fall back to the built-in defaults.
            SensorParameterRegistry.Configure(SensorRangeConfigLoader.Load());
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.Information("WaterFilterCBZ application exiting");
            LoggingService.CloseAndFlush();
            base.OnExit(e);
        }
    }

}
