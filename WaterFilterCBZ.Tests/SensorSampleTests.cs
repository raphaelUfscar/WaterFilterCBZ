using System.Globalization;
using WaterFilterCBZ.Models;

namespace WaterFilterCBZ.Tests;

public class SensorSampleTests
{
    [Fact]
    public void ToString_FormatsIdValueAndTimestamp()
    {
        var sample = new SensorSample
        {
            SensorId = "0x01",
            Value = 3.14159,
            Timestamp = new DateTime(2026, 6, 6, 13, 45, 7)
        };

        // Pin the culture so the F2 decimal separator is deterministic on any agent.
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            Assert.Equal("0x01: 3.14 @ 13:45:07", sample.ToString());
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Defaults_AreEmptyIdAndZeroValue()
    {
        var sample = new SensorSample();

        Assert.Equal(string.Empty, sample.SensorId);
        Assert.Equal(0.0, sample.Value);
    }
}
