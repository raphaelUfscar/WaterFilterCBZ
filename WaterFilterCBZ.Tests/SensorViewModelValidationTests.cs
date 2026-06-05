using WaterFilterCBZ.Models;
using WaterFilterCBZ.ViewModels;

namespace WaterFilterCBZ.Tests;

/// <summary>
/// Exercises the SensorViewModel sample-handling and freshness logic (AddSample,
/// LogValidationTransition, EvaluateSensorStaleness). These run headless: with no WPF
/// Application present, the Dispatcher-gated UI collection updates are skipped, but the
/// validation / staleness branches and SampleCount still execute.
/// </summary>
public class SensorViewModelValidationTests
{
    private static SensorSample Sample(string id, double value)
        => new() { SensorId = id, Value = value, Timestamp = DateTime.Now };

    private static SensorViewModel NewViewModel() => new(() => { });

    [Fact]
    public void AddSample_NullSample_IsIgnored()
    {
        var vm = NewViewModel();

        vm.AddSample(null!);

        Assert.Equal(0, vm.SampleCount);
    }

    [Fact]
    public void AddSample_CountsEverySample_IncludingRejected()
    {
        var vm = NewViewModel();

        vm.AddSample(Sample("0x01", 0.8));   // conductivity, in spec
        vm.AddSample(Sample("0x03", 99.0));  // pH, implausible -> rejected (still counted)

        Assert.Equal(2, vm.SampleCount);
    }

    [Fact]
    public void AddSample_DrivesAllValidationTransitions()
    {
        var vm = NewViewModel();

        // New sensor in spec (Normal), then out of spec, then back -> exercises the
        // OutOfSpec and Normal-recovery branches of LogValidationTransition.
        vm.AddSample(Sample("0x02", 22.0));  // Normal
        vm.AddSample(Sample("0x02", 45.0));  // -> OutOfSpec (transition logged)
        vm.AddSample(Sample("0x02", 23.0));  // -> Normal (transition logged)

        // Implausible value -> Invalid branch.
        vm.AddSample(Sample("0x03", 99.0));  // -> Invalid (transition logged)

        Assert.Equal(4, vm.SampleCount);
    }

    [Fact]
    public void AddSample_UnknownSensorId_IsAcceptedWithoutRangeValidation()
    {
        var vm = NewViewModel();

        vm.AddSample(Sample("0x7F", 999999.0)); // no parameter mapping -> no validation

        Assert.Equal(1, vm.SampleCount);
    }

    [Fact]
    public void EvaluateSensorStaleness_MarksRegisteredSensorsStale()
    {
        var vm = NewViewModel();
        // Zero threshold so any elapsed time since the last sample is "stale".
        var sensor = new SensorDisplayInfo("0x01", TimeSpan.Zero, SensorParameterRegistry.ForSensorId("0x01"));
        sensor.AddValue(0.8);
        vm.Sensors.Add(sensor);

        vm.EvaluateSensorStaleness();

        Assert.True(sensor.IsStale);
    }

    [Fact]
    public void EvaluateSensorStaleness_LogsRecovery_WhenSensorBecomesFreshAgain()
    {
        var vm = NewViewModel();
        var sensor = new SensorDisplayInfo("0x01", TimeSpan.FromSeconds(5), SensorParameterRegistry.ForSensorId("0x01"));
        sensor.AddValue(0.8);
        // Force it stale using a controlled future time...
        sensor.EvaluateStaleness(sensor.LastSampleAtUtc.AddSeconds(10));
        Assert.True(sensor.IsStale);
        vm.Sensors.Add(sensor);

        // ...then re-evaluate at "now" (within the 5 s threshold) -> transitions back to fresh.
        vm.EvaluateSensorStaleness();

        Assert.False(sensor.IsStale);
    }

    [Fact]
    public void EvaluateSensorStaleness_LeavesSensorsWithNoReadingsAlone()
    {
        var vm = NewViewModel();
        var sensor = new SensorDisplayInfo("0x01", TimeSpan.Zero, SensorParameterRegistry.ForSensorId("0x01"));
        vm.Sensors.Add(sensor); // never received a value

        vm.EvaluateSensorStaleness();

        Assert.False(sensor.IsStale);
    }

    [Fact]
    public void ClearAllData_ResetsSampleCount()
    {
        var vm = NewViewModel();
        vm.AddSample(Sample("0x01", 0.8));
        Assert.Equal(1, vm.SampleCount);

        vm.ClearAllData();

        Assert.Equal(0, vm.SampleCount);
        Assert.Empty(vm.Sensors);
    }
}
