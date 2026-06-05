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

    // --- Stale-data detection (RC-002 / SRS-C-001) -------------------------------

    [Fact]
    public void NewSensorWithNoReadings_IsNotStale()
    {
        var sensor = new SensorDisplayInfo("0x01", TimeSpan.FromSeconds(5));

        // Even far in the future, a sensor that never produced a reading is not "stale".
        bool changed = sensor.EvaluateStaleness(DateTime.UtcNow.AddMinutes(10));

        Assert.False(changed);
        Assert.False(sensor.IsStale);
    }

    [Fact]
    public void Sensor_BecomesStale_AfterThresholdWithNoNewSample()
    {
        var sensor = new SensorDisplayInfo("0x01", TimeSpan.FromSeconds(5));
        sensor.AddValue(10);

        bool changed = sensor.EvaluateStaleness(sensor.LastSampleAtUtc.AddSeconds(6));

        Assert.True(changed);
        Assert.True(sensor.IsStale);
    }

    [Fact]
    public void Sensor_StaysFresh_WithinThreshold()
    {
        var sensor = new SensorDisplayInfo("0x01", TimeSpan.FromSeconds(5));
        sensor.AddValue(10);

        bool changed = sensor.EvaluateStaleness(sensor.LastSampleAtUtc.AddSeconds(4));

        Assert.False(changed);
        Assert.False(sensor.IsStale);
    }

    [Fact]
    public void StaleSensor_RecoversToFresh_WhenNewSampleArrives()
    {
        var sensor = new SensorDisplayInfo("0x01", TimeSpan.FromSeconds(5));
        sensor.AddValue(10);
        sensor.EvaluateStaleness(sensor.LastSampleAtUtc.AddSeconds(6));
        Assert.True(sensor.IsStale);

        sensor.AddValue(11);

        Assert.False(sensor.IsStale);
    }

    [Fact]
    public void EvaluateStaleness_ReturnsFalse_WhenStateUnchanged()
    {
        var sensor = new SensorDisplayInfo("0x01", TimeSpan.FromSeconds(5));
        sensor.AddValue(10);

        var staleTime = sensor.LastSampleAtUtc.AddSeconds(6);
        Assert.True(sensor.EvaluateStaleness(staleTime));   // transitions to stale
        Assert.False(sensor.EvaluateStaleness(staleTime));  // already stale: no change
    }

    [Fact]
    public void BecomingStale_RaisesPropertyChangedForIsStale()
    {
        var sensor = new SensorDisplayInfo("0x01", TimeSpan.FromSeconds(5));
        sensor.AddValue(10);
        var changedProperties = new List<string?>();
        sensor.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        sensor.EvaluateStaleness(sensor.LastSampleAtUtc.AddSeconds(6));

        Assert.Contains(nameof(SensorDisplayInfo.IsStale), changedProperties);
    }
}
