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
}
