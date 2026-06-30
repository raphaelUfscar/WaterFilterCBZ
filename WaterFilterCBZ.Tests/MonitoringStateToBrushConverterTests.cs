using System.Globalization;
using System.Windows.Media;
using WaterFilterCBZ.Models;

namespace WaterFilterCBZ.Tests;

/// <summary>
/// Verifies the monitoring-state → status-colour mapping (RC-010 / SRS-C-006): every state maps to
/// a distinct opaque brush, healthy is green, and degraded/fault states are non-green so they
/// cannot be mistaken for healthy.
/// </summary>
public class MonitoringStateToBrushConverterTests
{
    private readonly MonitoringStateToBrushConverter _converter = new();

    private SolidColorBrush Convert(MonitoringState state)
        => Assert.IsType<SolidColorBrush>(
            _converter.Convert(state, typeof(Brush), null, CultureInfo.InvariantCulture));

    [Theory]
    [InlineData(MonitoringState.Disconnected)]
    [InlineData(MonitoringState.Connecting)]
    [InlineData(MonitoringState.ConnectedHealthy)]
    [InlineData(MonitoringState.Stale)]
    [InlineData(MonitoringState.InvalidValue)]
    [InlineData(MonitoringState.ParserError)]
    [InlineData(MonitoringState.DeviceMismatch)]
    [InlineData(MonitoringState.ProcessingFault)]
    public void Convert_EveryState_ReturnsAnOpaqueBrush(MonitoringState state)
    {
        var brush = Convert(state);
        Assert.Equal(byte.MaxValue, brush.Color.A); // fully opaque
        Assert.True(brush.IsFrozen);
    }

    [Fact]
    public void Convert_Healthy_IsGreen()
    {
        var c = Convert(MonitoringState.ConnectedHealthy).Color;
        Assert.Equal(Color.FromRgb(0x27, 0xAE, 0x60), c);
    }

    [Theory]
    [InlineData(MonitoringState.Stale)]
    [InlineData(MonitoringState.InvalidValue)]
    [InlineData(MonitoringState.ParserError)]
    [InlineData(MonitoringState.DeviceMismatch)]
    [InlineData(MonitoringState.ProcessingFault)]
    public void Convert_DegradedOrFaultStates_AreNotGreen(MonitoringState state)
    {
        var healthy = Convert(MonitoringState.ConnectedHealthy).Color;
        Assert.NotEqual(healthy, Convert(state).Color);
    }

    [Fact]
    public void Convert_NonState_FallsBackToDisconnectedColour()
    {
        var brush = Assert.IsType<SolidColorBrush>(
            _converter.Convert("not a state", typeof(Brush), null, CultureInfo.InvariantCulture));
        Assert.Equal(Convert(MonitoringState.Disconnected).Color, brush.Color);
    }

    [Fact]
    public void ConvertBack_IsNotSupported()
        => Assert.Throws<NotSupportedException>(
            () => _converter.ConvertBack(Brushes.Red, typeof(MonitoringState), null, CultureInfo.InvariantCulture));
}
