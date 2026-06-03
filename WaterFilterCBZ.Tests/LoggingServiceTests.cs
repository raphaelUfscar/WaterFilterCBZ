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
    public void OpenLogDirectory_CreatesDirectoryIfMissing()
    {
        // Verify the call ensures the directory exists without asserting on Process.Start side-effects.
        LoggingService.OpenLogDirectory();

        Assert.True(Directory.Exists(LoggingService.LogDirectory));
    }
}
