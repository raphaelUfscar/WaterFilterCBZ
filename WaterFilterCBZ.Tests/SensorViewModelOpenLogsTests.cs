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
    public void OpenLogsCommand_Execute_SetsStatusMessageToLogDirectory()
    {
        var vm = new SensorViewModel();

        vm.OpenLogsCommand.Execute(null);

        Assert.Contains(LoggingService.LogDirectory, vm.StatusMessage);
    }
}
