using WaterFilterCBZ.Models;
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

    // --- Two-tier value validation (RC-008 / SRS-C-003) --------------------------

    private static SensorDisplayInfo PhSensor()
        => new("0x03", null, SensorParameterRegistry.ForSensorId("0x03")); // operating 5.0–7.0, physical 0–14

    [Fact]
    public void NoAcceptedReadings_ReportsZeroMinMax_NotSentinels()
    {
        // A brand-new sensor, and one whose only sample was rejected, must not leak
        // the double.MaxValue/double.MinValue initialization sentinels to the UI.
        var fresh = new SensorDisplayInfo("0x03", null, SensorParameterRegistry.ForSensorId("0x03"));
        Assert.Equal(0.0, fresh.MinValue);
        Assert.Equal(0.0, fresh.MaxValue);

        fresh.AddValue(99.0); // outside physical range → rejected, ReadingCount stays 0
        Assert.Equal(0, fresh.ReadingCount);
        Assert.Equal(0.0, fresh.MinValue);
        Assert.Equal(0.0, fresh.MaxValue);
    }

    [Fact]
    public void WithoutParameter_AllValuesAreAcceptedAsNormal()
    {
        var sensor = new SensorDisplayInfo("0x99"); // no parameter mapping

        sensor.AddValue(123456);

        Assert.Equal(SensorValidationState.Normal, sensor.ValidationState);
        Assert.Equal(123456, sensor.CurrentValue);
    }

    [Fact]
    public void ValueWithinSpec_IsNormal_AndUpdatesValue()
    {
        var sensor = PhSensor();

        sensor.AddValue(6.5);

        Assert.Equal(SensorValidationState.Normal, sensor.ValidationState);
        Assert.Equal(6.5, sensor.CurrentValue);
        Assert.Equal(1, sensor.ReadingCount);
    }

    [Fact]
    public void PlausibleOutOfSpecValue_IsFlagged_ButStillDisplayed()
    {
        var sensor = PhSensor();

        sensor.AddValue(9.0); // within physical 0–14 but above operating 7.0

        Assert.Equal(SensorValidationState.OutOfSpec, sensor.ValidationState);
        Assert.Equal(9.0, sensor.CurrentValue);
        Assert.Equal(1, sensor.ReadingCount);
    }

    [Fact]
    public void ImplausibleValue_IsRejected_AndLastGoodValueIsKept()
    {
        var sensor = PhSensor();
        sensor.AddValue(6.5); // good

        sensor.AddValue(99.0); // outside physical range → rejected

        Assert.Equal(SensorValidationState.Invalid, sensor.ValidationState);
        Assert.Equal(6.5, sensor.CurrentValue);   // last good value preserved
        Assert.Equal(1, sensor.ReadingCount);     // rejected value not counted
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void NonFiniteValue_IsRejected(double bad)
    {
        var sensor = PhSensor();
        sensor.AddValue(6.0);

        sensor.AddValue(bad);

        Assert.Equal(SensorValidationState.Invalid, sensor.ValidationState);
        Assert.Equal(6.0, sensor.CurrentValue);
    }

    [Fact]
    public void RejectedValue_StillClearsStaleState()
    {
        var sensor = PhSensor();
        sensor.AddValue(6.0);
        sensor.EvaluateStaleness(sensor.LastSampleAtUtc.AddSeconds(10));
        Assert.True(sensor.IsStale);

        sensor.AddValue(99.0); // implausible, rejected — but a sample did arrive

        Assert.False(sensor.IsStale);
        Assert.Equal(SensorValidationState.Invalid, sensor.ValidationState);
    }

    [Fact]
    public void OutOfSpec_RecoversToNormal_WhenValueReturnsToSpec()
    {
        var sensor = PhSensor();
        sensor.AddValue(9.0);
        Assert.Equal(SensorValidationState.OutOfSpec, sensor.ValidationState);

        sensor.AddValue(6.0);

        Assert.Equal(SensorValidationState.Normal, sensor.ValidationState);
    }

    [Fact]
    public void CurrentValueText_IncludesUnit_WhenParameterKnown()
    {
        var sensor = PhSensor();
        sensor.AddValue(6.0);

        Assert.Contains("pH", sensor.CurrentValueText);
        Assert.Equal("Conductivity (0x01)", new SensorDisplayInfo("0x01", null, SensorParameterRegistry.ForSensorId("0x01")).DisplayName);
    }
}
