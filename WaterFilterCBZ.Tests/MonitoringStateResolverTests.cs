using WaterFilterCBZ.Models;

namespace WaterFilterCBZ.Tests;

/// <summary>
/// Verifies the defined monitoring-state taxonomy and its deterministic derivation
/// (RC-010 / SRS-C-006): every state is reachable and the precedence ordering is correct.
/// </summary>
public class MonitoringStateResolverTests
{
    // Resolve(isConnected, isConnecting, hasProcessingFault, hasDeviceMismatch, hasParserError, anyInvalid, anyStale)

    [Fact]
    public void Disconnected_WhenNotConnectedAndNotConnecting()
        => Assert.Equal(MonitoringState.Disconnected,
            MonitoringStateResolver.Resolve(false, false, false, false, false, false, false));

    [Fact]
    public void Connecting_WhenNotConnectedButAttempting()
        => Assert.Equal(MonitoringState.Connecting,
            MonitoringStateResolver.Resolve(false, true, false, false, false, false, false));

    [Fact]
    public void Healthy_WhenConnectedAndNoIssues()
        => Assert.Equal(MonitoringState.ConnectedHealthy,
            MonitoringStateResolver.Resolve(true, false, false, false, false, false, false));

    [Fact]
    public void Stale_WhenConnectedAndAnyStale()
        => Assert.Equal(MonitoringState.Stale,
            MonitoringStateResolver.Resolve(true, false, false, false, false, false, anyStale: true));

    [Fact]
    public void InvalidValue_WhenConnectedAndAnyInvalid()
        => Assert.Equal(MonitoringState.InvalidValue,
            MonitoringStateResolver.Resolve(true, false, false, false, false, anyInvalidValue: true, anyStale: false));

    [Fact]
    public void ParserError_WhenConnectedAndParserError()
        => Assert.Equal(MonitoringState.ParserError,
            MonitoringStateResolver.Resolve(true, false, false, false, hasParserError: true, false, false));

    [Fact]
    public void DeviceMismatch_WhenConnectedAndMismatch()
        => Assert.Equal(MonitoringState.DeviceMismatch,
            MonitoringStateResolver.Resolve(true, false, false, hasDeviceMismatch: true, false, false, false));

    [Fact]
    public void ProcessingFault_WhenFaulted()
        => Assert.Equal(MonitoringState.ProcessingFault,
            MonitoringStateResolver.Resolve(false, false, hasProcessingFault: true, false, false, false, false));

    // ----- precedence ordering -------------------------------------------

    [Fact]
    public void ProcessingFault_TakesPrecedenceOverEverything()
        => Assert.Equal(MonitoringState.ProcessingFault,
            MonitoringStateResolver.Resolve(true, true, true, true, true, true, true));

    [Fact]
    public void NotConnected_BeatsConnectedConditions_ExceptFault()
        // Not connected with data-quality flags still set (stale flags from a prior session):
        // disconnected wins over invalid/stale because those are connected-only conditions.
        => Assert.Equal(MonitoringState.Disconnected,
            MonitoringStateResolver.Resolve(false, false, false, false, false, anyInvalidValue: true, anyStale: true));

    [Fact]
    public void DeviceMismatch_BeatsParserInvalidStale()
        => Assert.Equal(MonitoringState.DeviceMismatch,
            MonitoringStateResolver.Resolve(true, false, false, true, true, true, true));

    [Fact]
    public void ParserError_BeatsInvalidAndStale()
        => Assert.Equal(MonitoringState.ParserError,
            MonitoringStateResolver.Resolve(true, false, false, false, true, true, true));

    [Fact]
    public void InvalidValue_BeatsStale()
        => Assert.Equal(MonitoringState.InvalidValue,
            MonitoringStateResolver.Resolve(true, false, false, false, false, anyInvalidValue: true, anyStale: true));

    [Fact]
    public void Describe_CoversEveryState()
    {
        foreach (MonitoringState state in Enum.GetValues<MonitoringState>())
        {
            var label = MonitoringStateResolver.Describe(state);
            Assert.False(string.IsNullOrWhiteSpace(label));
        }
    }
}
