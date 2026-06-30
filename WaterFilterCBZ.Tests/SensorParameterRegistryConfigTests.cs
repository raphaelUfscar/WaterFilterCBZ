using WaterFilterCBZ.Models;

namespace WaterFilterCBZ.Tests;

/// <summary>
/// Exercises user-configurable sensor ranges with per-field fallback to defaults and
/// consistency validation (RC-008 / SRS-C-003). Each test restores defaults on dispose so the
/// shared <see cref="SensorParameterRegistry"/> singleton is left clean for other tests.
/// </summary>
public sealed class SensorParameterRegistryConfigTests : IDisposable
{
    public void Dispose() => SensorParameterRegistry.ResetToDefaults();

    private static SensorRangeOverride Override(
        double? opMin = null, double? opMax = null, double? phMin = null, double? phMax = null)
        => new() { OperatingMin = opMin, OperatingMax = opMax, PhysicalMin = phMin, PhysicalMax = phMax };

    [Fact]
    public void Configure_Null_LeavesDefaultsInPlace()
    {
        SensorParameterRegistry.Configure(null);

        var ph = SensorParameterRegistry.ForSensorId("0x03")!;
        var def = SensorParameterRegistry.DefaultForSensorId("0x03")!;
        Assert.Equal(def.OperatingMin, ph.OperatingMin);
        Assert.Equal(def.OperatingMax, ph.OperatingMax);
    }

    [Fact]
    public void Configure_OverridesOnlyProvidedFields_KeepsDefaultForTheRest()
    {
        var def = SensorParameterRegistry.DefaultForSensorId("0x03")!; // pH operating 5..7, physical 0..14

        SensorParameterRegistry.Configure(new Dictionary<string, SensorRangeOverride>
        {
            ["0x03"] = Override(opMin: 6.0, opMax: 8.0), // tune operating band only
        });

        var ph = SensorParameterRegistry.ForSensorId("0x03")!;
        Assert.Equal(6.0, ph.OperatingMin);
        Assert.Equal(8.0, ph.OperatingMax);
        Assert.Equal(def.PhysicalMin, ph.PhysicalMin);   // unchanged
        Assert.Equal(def.PhysicalMax, ph.PhysicalMax);   // unchanged
        Assert.Equal(def.Name, ph.Name);
        Assert.Equal(def.Unit, ph.Unit);
    }

    [Fact]
    public void Configure_AppliedOverride_ChangesValidationOutcome()
    {
        // Default pH physical max is 14; raise it so a previously-Invalid value becomes OutOfSpec.
        SensorParameterRegistry.Configure(new Dictionary<string, SensorRangeOverride>
        {
            ["0x03"] = Override(phMax: 100.0),
        });

        var ph = SensorParameterRegistry.ForSensorId("0x03")!;
        Assert.Equal(SensorValidationState.OutOfSpec, ph.Classify(20.0)); // was Invalid under default
    }

    [Theory]
    [InlineData(5.0, 4.0, 0.0, 14.0)]   // operatingMin > operatingMax
    [InlineData(-1.0, 7.0, 0.0, 14.0)]  // operatingMin < physicalMin
    [InlineData(5.0, 7.0, 0.0, 6.0)]    // operatingMax > physicalMax
    public void Configure_InconsistentOverride_IsRejected_DefaultKept(
        double opMin, double opMax, double phMin, double phMax)
    {
        var def = SensorParameterRegistry.DefaultForSensorId("0x03")!;

        SensorParameterRegistry.Configure(new Dictionary<string, SensorRangeOverride>
        {
            ["0x03"] = Override(opMin, opMax, phMin, phMax),
        });

        var ph = SensorParameterRegistry.ForSensorId("0x03")!;
        Assert.Equal(def.OperatingMin, ph.OperatingMin);
        Assert.Equal(def.OperatingMax, ph.OperatingMax);
        Assert.Equal(def.PhysicalMin, ph.PhysicalMin);
        Assert.Equal(def.PhysicalMax, ph.PhysicalMax);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Configure_NonFiniteBound_IsRejected_DefaultKept(double bad)
    {
        var def = SensorParameterRegistry.DefaultForSensorId("0x01")!;

        SensorParameterRegistry.Configure(new Dictionary<string, SensorRangeOverride>
        {
            ["0x01"] = Override(opMax: bad),
        });

        Assert.Equal(def.OperatingMax, SensorParameterRegistry.ForSensorId("0x01")!.OperatingMax);
    }

    [Fact]
    public void Configure_UnknownSensorId_IsIgnored()
    {
        SensorParameterRegistry.Configure(new Dictionary<string, SensorRangeOverride>
        {
            ["0x09"] = Override(opMin: 1.0, opMax: 2.0),
        });

        Assert.Null(SensorParameterRegistry.ForSensorId("0x09"));
    }

    [Fact]
    public void Configure_ThenReset_RestoresDefaults()
    {
        SensorParameterRegistry.Configure(new Dictionary<string, SensorRangeOverride>
        {
            ["0x01"] = Override(opMax: 1.1),
        });
        Assert.Equal(1.1, SensorParameterRegistry.ForSensorId("0x01")!.OperatingMax);

        SensorParameterRegistry.ResetToDefaults();

        var def = SensorParameterRegistry.DefaultForSensorId("0x01")!;
        Assert.Equal(def.OperatingMax, SensorParameterRegistry.ForSensorId("0x01")!.OperatingMax);
    }
}
