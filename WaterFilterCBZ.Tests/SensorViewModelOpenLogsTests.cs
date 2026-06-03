using WaterFilterCBZ.ViewModels;
using WaterFilterCBZ.Services;

namespace WaterFilterCBZ.Tests;

public class SensorViewModelOpenLogsTests
{
    [Fact]
    public void OpenLogsCommand_IsNotNull()
    {
        var vm = new SensorViewModel();

        Assert.NotNull(vm.OpenLogsCommand);
    }

    [Fact]
    public void OpenLogsCommand_CanAlwaysExecute()
    {
        var vm = new SensorViewModel();

        Assert.True(vm.OpenLogsCommand.CanExecute(null));
    }

    [Fact]
    public void OpenLogsCommand_Execute_SetsStatusMessageWithLogDirectory()
    {
        var vm = new SensorViewModel(openLogDirectory: () => { });

        vm.OpenLogsCommand.Execute(null);

        Assert.Equal($"Opened logs: {LoggingService.LogDirectory}", vm.StatusMessage);
    }

    [Fact]
    public void OpenLogsCommand_Execute_RaisesPropertyChangedForStatusMessage()
    {
        var vm = new SensorViewModel(openLogDirectory: () => { });
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.OpenLogsCommand.Execute(null);

        Assert.Contains(nameof(vm.StatusMessage), changed);
    }

    [Fact]
    public void OpenLogsCommand_Execute_WhenOpenFails_SetsStatusMessageToFallback()
    {
        var vm = new SensorViewModel(openLogDirectory: () => throw new InvalidOperationException("simulated failure"));

        vm.OpenLogsCommand.Execute(null);

        Assert.Equal($"Log folder: {LoggingService.LogDirectory}", vm.StatusMessage);
    }

    [Fact]
    public void OpenLogsCommand_Execute_WhenOpenFails_DoesNotThrow()
    {
        var vm = new SensorViewModel(openLogDirectory: () => throw new Exception("simulated failure"));

        var ex = Record.Exception(() => vm.OpenLogsCommand.Execute(null));

        Assert.Null(ex);
    }
}
