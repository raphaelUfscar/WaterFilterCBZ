using WaterFilterCBZ.Services;

namespace WaterFilterCBZ.Tests;

public class LoggingServiceTests
{
    [Fact]
    public void LogDirectory_IsUnderAppData()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        Assert.StartsWith(appData, LoggingService.LogDirectory, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LogDirectory_EndsWithWaterFilterCBZLogs()
    {
        var expected = Path.Combine("WaterFilterCBZ", "logs");

        Assert.EndsWith(expected, LoggingService.LogDirectory, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LogDirectory_ReturnsSamePathOnEveryCall()
    {
        var first = LoggingService.LogDirectory;
        var second = LoggingService.LogDirectory;

        Assert.Equal(first, second);
    }

    [Fact]
    public void LogDirectory_IsAbsolutePath()
    {
        Assert.True(Path.IsPathRooted(LoggingService.LogDirectory));
    }

    [Fact]
    public void OpenLogDirectory_CreatesDirectoryIfMissing()
    {
        LoggingService.OpenLogDirectory();

        Assert.True(Directory.Exists(LoggingService.LogDirectory));
    }

    [Fact]
    public void ConfigureLogging_CreatesLogDirectoryAndInstallsLogger()
    {
        LoggingService.ConfigureLogging();

        Assert.True(Directory.Exists(LoggingService.LogDirectory));
        // The global logger is now configured; writing through it must not throw.
        var ex = Record.Exception(() => Serilog.Log.Information("logging-service-test"));
        Assert.Null(ex);

        LoggingService.CloseAndFlush();
    }
}
