using WaterFilterCBZ.Models;
using WaterFilterCBZ.ViewModels;

namespace WaterFilterCBZ.Tests;

/// <summary>
/// Verifies that <see cref="SensorViewModel"/> drives the defined monitoring-state taxonomy
/// (RC-010 / SRS-C-006) from its connection and data-quality inputs. Runs headless; the
/// state derivation does not depend on a WPF dispatcher.
/// </summary>
public class SensorViewModelMonitoringStateTests
{
    private static SensorViewModel NewViewModel() => new(() => { });

    private static SensorSample Sample(string id, double value)
        => new() { SensorId = id, Value = value, Timestamp = DateTime.Now };

    [Fact]
    public void DefaultState_IsDisconnected()
        => Assert.Equal(MonitoringState.Disconnected, NewViewModel().MonitoringState);

    [Fact]
    public void Connected_NoIssues_IsHealthy()
    {
        var vm = NewViewModel();

        vm.UpdateConnectionStatus(true, "COM1");

        Assert.Equal(MonitoringState.ConnectedHealthy, vm.MonitoringState);
        Assert.Equal("Connected — healthy", vm.MonitoringStateLabel);
    }

    [Fact]
    public void Disconnect_ReturnsToDisconnected()
    {
        var vm = NewViewModel();
        vm.UpdateConnectionStatus(true, "COM1");

        vm.UpdateConnectionStatus(false);

        Assert.Equal(MonitoringState.Disconnected, vm.MonitoringState);
    }

    [Fact]
    public void InvalidSample_WhileConnected_IsInvalidValue()
    {
        var vm = NewViewModel();
        vm.UpdateConnectionStatus(true, "COM1");

        vm.AddSample(Sample("0x03", 99.0)); // pH physical range 0..14 -> implausible, rejected

        Assert.Equal(MonitoringState.InvalidValue, vm.MonitoringState);
    }

    [Fact]
    public void ValidSample_WhileConnected_StaysHealthy()
    {
        var vm = NewViewModel();
        vm.UpdateConnectionStatus(true, "COM1");

        vm.AddSample(Sample("0x03", 6.5)); // in spec

        Assert.Equal(MonitoringState.ConnectedHealthy, vm.MonitoringState);
    }

    [Fact]
    public void ProcessingFault_SetsProcessingFaultState()
    {
        var vm = NewViewModel();
        vm.UpdateConnectionStatus(true, "COM1");

        vm.NotifyProcessingFault("boom");

        Assert.Equal(MonitoringState.ProcessingFault, vm.MonitoringState);
    }

    [Fact]
    public void DeviceMismatch_WhileConnected_SetsDeviceMismatchState()
    {
        var vm = NewViewModel();
        vm.UpdateConnectionStatus(true, "COM1");

        vm.NotifyDeviceMismatch(true);

        Assert.Equal(MonitoringState.DeviceMismatch, vm.MonitoringState);
    }

    [Fact]
    public void ParserError_WhileConnected_SetsParserErrorState()
    {
        var vm = NewViewModel();
        vm.UpdateConnectionStatus(true, "COM1");

        vm.NotifyParserError(true);

        Assert.Equal(MonitoringState.ParserError, vm.MonitoringState);
    }

    [Fact]
    public void Reconnect_ClearsParserAndMismatch_BackToHealthy()
    {
        var vm = NewViewModel();
        vm.UpdateConnectionStatus(true, "COM1");
        vm.NotifyParserError(true);
        vm.NotifyDeviceMismatch(true);

        vm.UpdateConnectionStatus(true, "COM1"); // fresh successful connect clears degraded flags

        Assert.False(vm.HasParserError);
        Assert.False(vm.HasDeviceMismatch);
        Assert.Equal(MonitoringState.ConnectedHealthy, vm.MonitoringState);
    }

    [Fact]
    public void ClearData_AfterInvalid_ReturnsToHealthy()
    {
        var vm = NewViewModel();
        vm.UpdateConnectionStatus(true, "COM1");
        vm.AddSample(Sample("0x03", 99.0));
        Assert.Equal(MonitoringState.InvalidValue, vm.MonitoringState);

        vm.ClearAllData();

        Assert.Equal(MonitoringState.ConnectedHealthy, vm.MonitoringState);
    }
}
