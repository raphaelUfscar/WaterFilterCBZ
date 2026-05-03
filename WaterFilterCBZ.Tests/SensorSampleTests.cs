using WaterFilterCBZ.Models;

namespace WaterFilterCBZ.Tests;

public class SensorSampleTests
{
    [Fact]
    public void TryParseCsv_WithValidInvariantLine_ReturnsSample()
    {
        var success = SensorSample.TryParseCsv("ph,1714766400,7.25", out var sample);

        Assert.True(success);
        Assert.NotNull(sample);
        Assert.Equal("ph", sample.SensorId);
        Assert.Equal(7.25, sample.Value);
        Assert.Equal(
            DateTimeOffset.FromUnixTimeSeconds(1714766400).LocalDateTime,
            sample.Timestamp);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ph,1714766400")]
    [InlineData("ph,not-a-timestamp,7.25")]
    [InlineData("ph,1714766400,not-a-number")]
    public void TryParseCsv_WithInvalidLine_ReturnsFalse(string line)
    {
        var success = SensorSample.TryParseCsv(line, out var sample);

        Assert.False(success);
        Assert.Null(sample);
    }

    [Fact]
    public void TryParseCsv_UsesInvariantCultureForDecimalValues()
    {
        var success = SensorSample.TryParseCsv("flow,1714766400,12.5", out var sample);

        Assert.True(success);
        Assert.NotNull(sample);
        Assert.Equal(12.5, sample.Value);
    }
}
