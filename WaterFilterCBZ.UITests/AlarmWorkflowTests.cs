using Xunit;

namespace WaterFilterCBZ.UITests;

/// <summary>
/// End-to-end UI Automation tests for the safety alarms (RC-002 stale supervision and
/// RC-008 two-tier value validation). Each test drives a specific simulator scenario over the
/// virtual COM pair and asserts both the on-screen alarm indicator (by AutomationId) and the
/// corresponding audit entry in the rolling log file.
///
/// Requires the interactive environment in WaterFilterCBZ.UITests/README.md; self-skips otherwise.
/// </summary>
[Collection("UI")] // serialize: only one app instance should own the COM port at a time
public sealed class AlarmWorkflowTests : UiAutomationTestBase
{
    [SkippableFact]
    public void OutOfSpecScenario_RaisesOutOfSpecIndicatorAndLogsIt()
    {
        SkipIfEnvironmentMissing();

        // All sensors emit values above their operating band but within the physical range:
        // plausible, so charted and flagged OutOfSpec (tier 2) rather than rejected.
        StartSimulator(scenario: "out-of-spec");
        LaunchApp();

        var logOffset = LogFileLength();
        ConnectThroughUi();

        Assert.True(
            WaitUntil(() => AnyVisible("SensorOutOfSpecIndicator"), DataWait),
            $"Expected an OUT OF SPEC indicator to appear. Simulator output:{Environment.NewLine}{SimulatorOutput}");

        AssertLoggedAfter("out-of-spec stream", logOffset, "is out of operating spec");
    }

    [SkippableFact]
    public void InvalidScenario_RaisesInvalidIndicatorIncrementsRejectedAndLogsIt()
    {
        SkipIfEnvironmentMissing();

        // All sensors emit physically implausible values (beyond the physical range): rejected
        // at tier 1, the last good value preserved and the rejected count incremented.
        StartSimulator(scenario: "invalid");
        LaunchApp();

        var logOffset = LogFileLength();
        ConnectThroughUi();

        Assert.True(
            WaitUntil(() => AnyVisible("SensorInvalidIndicator"), DataWait),
            $"Expected an INVALID indicator to appear. Simulator output:{Environment.NewLine}{SimulatorOutput}");

        Assert.True(
            WaitUntil(() => MaxRejectedCount() > 0, DataWait),
            "Expected the rejected-sample count to climb above zero.");

        AssertLoggedAfter("invalid stream", logOffset, "implausible");
    }

    [SkippableFact]
    public void StaleScenario_RaisesStaleIndicatorAfterFeedGoesSilentAndLogsIt()
    {
        SkipIfEnvironmentMissing();

        // The simulator streams normal frames briefly, then goes silent (port stays open), so the
        // app's 5 s stale supervision fires while still connected.
        StartSimulator(scenario: "stale");
        LaunchApp();

        var logOffset = LogFileLength();
        ConnectThroughUi(); // succeeds during the warm-up burst

        Assert.True(
            WaitUntil(() => AnyVisible("SensorStaleIndicator"), StaleWait),
            $"Expected a STALE indicator after the feed went silent. Simulator output:{Environment.NewLine}{SimulatorOutput}");

        // The connection stays open even though data stopped; staleness is a data alarm, not a disconnect.
        Assert.Contains("Connected", ConnectionStatus, StringComparison.OrdinalIgnoreCase);

        AssertLoggedAfter("stale supervision", logOffset, "data is stale");
    }

    [SkippableFact]
    public void CorruptedFrameInjection_KeepsStreamingValidSamples()
    {
        SkipIfEnvironmentMissing();

        // Normal values with a corrupted-checksum frame injected periodically. The parser must
        // drop the bad frames and keep decoding the valid ones — the app stays connected and the
        // sample count keeps climbing, with no value-level INVALID alarm (frame-level corruption
        // is discarded, not surfaced as an implausible value).
        StartSimulator(scenario: "normal", injectErrors: "checksum");
        LaunchApp();

        ConnectThroughUi();

        var countAfterConnect = SampleCount;
        Assert.True(
            WaitUntil(() => SampleCount > countAfterConnect + 5, DataWait),
            $"Expected the sample count to keep climbing past corrupted frames (was {countAfterConnect}). " +
            $"Simulator output:{Environment.NewLine}{SimulatorOutput}");

        Assert.Contains("Connected", ConnectionStatus, StringComparison.OrdinalIgnoreCase);
        Assert.False(AnyVisible("SensorInvalidIndicator"), "Frame corruption must not surface as a value-level INVALID alarm.");
    }
}
