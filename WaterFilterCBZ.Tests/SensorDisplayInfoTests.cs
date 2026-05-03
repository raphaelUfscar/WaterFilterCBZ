using WaterFilterCBZ.ViewModels;

namespace WaterFilterCBZ.Tests;

public class SensorDisplayInfoTests
{
    [Fact]
    public void AddValue_UpdatesCurrentMinMaxAverageAndCount()
    {
        var sensor = new SensorDisplayInfo("0x01");

        sensor.AddValue(10);
        sensor.AddValue(4);
        sensor.AddValue(7);

        Assert.Equal(7, sensor.CurrentValue);
        Assert.Equal(4, sensor.MinValue);
        Assert.Equal(10, sensor.MaxValue);
        Assert.Equal(7, sensor.AvgValue);
        Assert.Equal(3, sensor.ReadingCount);
    }

    [Fact]
    public void AddValue_RaisesPropertyChangedForDerivedValues()
    {
        var sensor = new SensorDisplayInfo("0x01");
        var changedProperties = new List<string?>();
        sensor.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        sensor.AddValue(10);

        Assert.Contains(nameof(SensorDisplayInfo.CurrentValue), changedProperties);
        Assert.Contains(nameof(SensorDisplayInfo.MinValue), changedProperties);
        Assert.Contains(nameof(SensorDisplayInfo.MaxValue), changedProperties);
        Assert.Contains(nameof(SensorDisplayInfo.AvgValue), changedProperties);
        Assert.Contains(nameof(SensorDisplayInfo.ReadingCount), changedProperties);
    }

    [Fact]
    public void LastUpdate_RaisesPropertyChangedWhenChanged()
    {
        var sensor = new SensorDisplayInfo("0x01");
        string? changedProperty = null;
        var timestamp = new DateTime(2026, 5, 3, 12, 0, 0);
        sensor.PropertyChanged += (_, args) => changedProperty = args.PropertyName;

        sensor.LastUpdate = timestamp;

        Assert.Equal(timestamp, sensor.LastUpdate);
        Assert.Equal(nameof(SensorDisplayInfo.LastUpdate), changedProperty);
    }
}
