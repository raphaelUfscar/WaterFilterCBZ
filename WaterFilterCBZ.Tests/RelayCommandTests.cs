using WaterFilterCBZ.ViewModels;

namespace WaterFilterCBZ.Tests;

public class RelayCommandTests
{
    [Fact]
    public void CanExecute_WithoutPredicate_ReturnsTrue()
    {
        var command = new RelayCommand(() => { });

        Assert.True(command.CanExecute(null));
    }

    [Fact]
    public void CanExecute_UsesPredicate()
    {
        var canExecute = false;
        var command = new RelayCommand(() => { }, () => canExecute);

        Assert.False(command.CanExecute(null));

        canExecute = true;

        Assert.True(command.CanExecute(null));
    }

    [Fact]
    public void Execute_RunsAction()
    {
        var executed = false;
        var command = new RelayCommand(() => executed = true);

        command.Execute(null);

        Assert.True(executed);
    }

    [Fact]
    public void GenericExecute_PassesParameter()
    {
        string? received = null;
        var command = new RelayCommand<string>(value => received = value);

        command.Execute("COM4");

        Assert.Equal("COM4", received);
    }

    [Fact]
    public void Constructor_WithNullAction_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new RelayCommand(null!));
        Assert.Throws<ArgumentNullException>(() => new RelayCommand<string>(null!));
    }
}
