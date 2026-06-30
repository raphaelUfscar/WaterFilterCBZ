using System.ComponentModel;
using WaterFilterCBZ.ViewModels;

namespace WaterFilterCBZ.Tests;

/// <summary>
/// Exercises the processing-fault surfacing in <see cref="SensorViewModel"/>
/// (RC-009 / SRS-C-005 / HAZ-004). Runs headless: with no WPF Application present the
/// dispatcher-gated path falls through to direct execution on the calling thread.
/// </summary>
public class SensorViewModelProcessingFaultTests
{
    private static SensorViewModel NewViewModel() => new(() => { });

    [Fact]
    public void NotifyProcessingFault_SetsDegradedState()
    {
        var vm = NewViewModel();
        vm.UpdateConnectionStatus(true, "COM1");

        vm.NotifyProcessingFault("processing task fault: boom");

        Assert.True(vm.HasProcessingFault);
        Assert.False(vm.IsConnected);
        Assert.Equal("Processing fault", vm.ConnectionStatus);
        Assert.Contains("Reconnect", vm.StatusMessage);
    }

    [Fact]
    public void NotifyProcessingFault_WithoutDetail_StillProducesAReconnectMessage()
    {
        var vm = NewViewModel();

        vm.NotifyProcessingFault();

        Assert.True(vm.HasProcessingFault);
        Assert.Contains("Reconnect", vm.StatusMessage);
    }

    [Fact]
    public void NotifyProcessingFault_RaisesPropertyChangedForHasProcessingFault()
    {
        var vm = NewViewModel();
        var changed = new List<string?>();
        ((INotifyPropertyChanged)vm).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.NotifyProcessingFault("x");

        Assert.Contains(nameof(SensorViewModel.HasProcessingFault), changed);
    }

    [Fact]
    public void UpdateConnectionStatus_SuccessfulReconnect_ClearsProcessingFault()
    {
        var vm = NewViewModel();
        vm.NotifyProcessingFault("boom");
        Assert.True(vm.HasProcessingFault);

        vm.UpdateConnectionStatus(true, "COM1");

        Assert.False(vm.HasProcessingFault);
        Assert.True(vm.IsConnected);
        Assert.Equal("Connected (COM1)", vm.ConnectionStatus);
    }
}
