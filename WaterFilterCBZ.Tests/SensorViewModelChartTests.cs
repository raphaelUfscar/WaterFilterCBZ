using System.Reflection;
using OxyPlot.Series;
using WaterFilterCBZ.Models;
using WaterFilterCBZ.ViewModels;

namespace WaterFilterCBZ.Tests;

/// <summary>
/// Exercises SensorViewModel paths that are normally marshalled onto the WPF dispatcher
/// (chart updates) plus the connection commands and status. These run headless: the private
/// UpdateChartForSensor is invoked directly via reflection, bypassing the dispatcher gate.
/// </summary>
public class SensorViewModelChartTests
{
    private static SensorViewModel NewViewModel() => new(() => { });

    private static SensorSample Sample(string id, double value)
        => new() { SensorId = id, Value = value, Timestamp = DateTime.Now };

    private static SensorDisplayInfo Display(string id)
        => new(id, TimeSpan.FromSeconds(5), SensorParameterRegistry.ForSensorId(id));

    private static void UpdateChart(SensorViewModel vm, SensorDisplayInfo info, SensorSample sample)
        => typeof(SensorViewModel)
            .GetMethod("UpdateChartForSensor", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(vm, new object[] { info, sample });

    // ----- UpdateChartForSensor -------------------------------------------

    [Fact]
    public void UpdateChartForSensor_AddsLineSeriesWithPoint_ToFirstPlot()
    {
        var vm = NewViewModel();
        var info = Display("0x01");

        UpdateChart(vm, info, Sample("0x01", 1.5));

        var series = Assert.IsType<LineSeries>(Assert.Single(vm.PlotModel1.Series));
        Assert.Single(series.Points);
        Assert.Equal("Sensor 0x01", vm.PlotModel1.Title);
    }

    [Fact]
    public void UpdateChartForSensor_AppendsPointsForSameSensor()
    {
        var vm = NewViewModel();
        var info = Display("0x01");

        UpdateChart(vm, info, Sample("0x01", 1.0));
        UpdateChart(vm, info, Sample("0x01", 2.0));

        var series = (LineSeries)vm.PlotModel1.Series.Single();
        Assert.Equal(2, series.Points.Count);
    }

    [Fact]
    public void UpdateChartForSensor_RoutesDifferentSensorsToDifferentPlots()
    {
        var vm = NewViewModel();

        UpdateChart(vm, Display("0x01"), Sample("0x01", 1.0));
        UpdateChart(vm, Display("0x02"), Sample("0x02", 2.0));

        Assert.Single(vm.PlotModel1.Series);
        Assert.Single(vm.PlotModel2.Series);
    }

    [Fact]
    public void UpdateChartForSensor_CapsSeriesAtThreeHundredPoints()
    {
        var vm = NewViewModel();
        var info = Display("0x01");

        for (int i = 0; i < 305; i++)
            UpdateChart(vm, info, Sample("0x01", i));

        var series = (LineSeries)vm.PlotModel1.Series.Single();
        Assert.Equal(300, series.Points.Count);
    }

    [Fact]
    public void UpdateChartForSensor_IgnoresSensorsBeyondFourPlots()
    {
        var vm = NewViewModel();

        // Five distinct sensors; only four plots exist, the fifth must be ignored.
        var ex = Record.Exception(() =>
        {
            UpdateChart(vm, Display("0x01"), Sample("0x01", 1));
            UpdateChart(vm, Display("0x02"), Sample("0x02", 2));
            UpdateChart(vm, Display("0x03"), Sample("0x03", 3));
            UpdateChart(vm, Display("0x7A"), Sample("0x7A", 4));
            UpdateChart(vm, Display("0x7B"), Sample("0x7B", 5)); // fifth -> ignored
        });

        Assert.Null(ex);
        Assert.Single(vm.PlotModel1.Series);
        Assert.Single(vm.PlotModel4.Series);
    }

    // ----- UpdateConnectionStatus -----------------------------------------

    [Fact]
    public void UpdateConnectionStatus_Connected_SetsStatusStrings()
    {
        var vm = NewViewModel();

        vm.UpdateConnectionStatus(true, "COM3");

        Assert.True(vm.IsConnected);
        Assert.Equal("Connected (COM3)", vm.ConnectionStatus);
        Assert.Equal("Listening for sensor data...", vm.StatusMessage);
    }

    [Fact]
    public void UpdateConnectionStatus_Disconnected_SetsStatusStrings()
    {
        var vm = NewViewModel();
        vm.UpdateConnectionStatus(true, "COM3");

        vm.UpdateConnectionStatus(false);

        Assert.False(vm.IsConnected);
        Assert.Equal("Disconnected", vm.ConnectionStatus);
        Assert.Equal("No connection", vm.StatusMessage);
    }

    // ----- Connect / Disconnect commands ----------------------------------

    [Fact]
    public void ConnectCommand_CanExecute_ReflectsConnectionAndSelectedPort()
    {
        var vm = NewViewModel();
        vm.SelectedPort = "COM4";
        Assert.True(vm.ConnectCommand.CanExecute(null));

        vm.UpdateConnectionStatus(true, "COM4");
        Assert.False(vm.ConnectCommand.CanExecute(null));

        vm.UpdateConnectionStatus(false);
        vm.SelectedPort = "   ";
        Assert.False(vm.ConnectCommand.CanExecute(null));
    }

    [Fact]
    public void DisconnectCommand_CanExecute_OnlyWhenConnected()
    {
        var vm = NewViewModel();
        Assert.False(vm.DisconnectCommand.CanExecute(null));

        vm.UpdateConnectionStatus(true, "COM4");
        Assert.True(vm.DisconnectCommand.CanExecute(null));
    }

    [Fact]
    public void ConnectCommand_Execute_WithValidPort_RaisesConnectionStatusChanged()
    {
        var vm = NewViewModel();
        vm.SelectedPort = "COM4";
        var raised = false;
        vm.ConnectionStatusChanged += () => raised = true;

        vm.ConnectCommand.Execute(null);

        Assert.True(raised);
    }

    [Fact]
    public void ConnectCommand_Execute_WithBlankPort_SetsStatusAndDoesNotRaise()
    {
        var vm = NewViewModel();
        vm.SelectedPort = "   ";
        var raised = false;
        vm.ConnectionStatusChanged += () => raised = true;

        vm.ConnectCommand.Execute(null);

        Assert.False(raised);
        Assert.Equal("Please select a COM port", vm.StatusMessage);
    }

    [Fact]
    public void DisconnectCommand_Execute_RaisesDisconnectionStatusChanged()
    {
        var vm = NewViewModel();
        var raised = false;
        vm.DisconnectionStatusChanged += () => raised = true;

        vm.DisconnectCommand.Execute(null);

        Assert.True(raised);
    }
}
