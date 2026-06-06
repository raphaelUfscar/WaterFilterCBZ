using Xunit;

namespace WaterFilterCBZ.UITests;

/// <summary>
/// End-to-end UI Automation test that drives the real WPF window through the full
/// connection workflow — select port, connect, observe live data, open logs, clear,
/// disconnect — with the bundled Python simulator feeding frames over a virtual COM pair.
///
/// In addition to UI state, each command is verified to reach the rolling log file under
/// %AppData%\WaterFilterCBZ\logs (evidence for the logging/auditability requirements).
///
/// Requires an interactive desktop session and the environment described in
/// WaterFilterCBZ.UITests/README.md. Self-skips when that environment is absent.
/// </summary>
[Collection("UI")] // serialize: only one app instance should own the COM port at a time
public sealed class ConnectionWorkflowTests : UiAutomationTestBase
{
    [SkippableFact]
    public void FullConnectionWorkflow_ConnectsStreamsClearsAndDisconnects()
    {
        SkipIfEnvironmentMissing();

        // Arrange: the simulator owns the writer side of the pair, streaming all four sensors.
        StartSimulator(scenario: "normal");
        LaunchApp();

        // The app auto-connects on load. Drive an explicit disconnect so we can deterministically
        // select the app-side port and reconnect through the real UI.
        if (DisconnectButton.IsEnabled)
            ClickWhenEnabled(DisconnectButton, "Disconnect");
        Assert.True(
            WaitUntil(() => ConnectionStatus.Contains("Disconnected", StringComparison.OrdinalIgnoreCase), ShortWait),
            $"Expected 'Disconnected' but status was '{ConnectionStatus}'.");

        // Select the app-side port (the ComboBox enables immediately on disconnect).
        Assert.True(WaitUntil(() => PortSelector.IsEnabled, ShortWait), "Port selector never became enabled.");
        PortSelector.Select(TestEnvironment.AppPort);

        // Act + Assert: connect and confirm the status flips and frames start arriving.
        var connectLogOffset = LogFileLength();
        ClickWhenEnabled(ConnectButton, "Connect");
        Assert.True(
            WaitUntil(() => ConnectionStatus.Contains("Connected", StringComparison.OrdinalIgnoreCase), ShortWait),
            $"Expected 'Connected' but status was '{ConnectionStatus}'. Simulator output:{Environment.NewLine}{SimulatorOutput}");

        Assert.True(
            WaitUntil(() => SampleCount > 0, DataWait),
            $"No samples were received within {DataWait.TotalSeconds:N0}s. Simulator output:{Environment.NewLine}{SimulatorOutput}");

        // The simulator emits 4 sensors; at least one must register in the Active Sensors panel.
        Assert.True(WaitUntil(() => SensorCount >= 1, DataWait), "No sensors registered in the dashboard.");

        // The connection must be recorded in the log file.
        AssertLoggedAfter("Connect", connectLogOffset, "Connection status: Connected");

        // Open Logs: writes an audit entry (and opens the folder in the OS file browser).
        var openLogsOffset = LogFileLength();
        ClickWhenEnabled(OpenLogsButton, "Open Logs");
        Assert.True(
            WaitUntil(() => StatusMessage.Contains("Opened logs", StringComparison.OrdinalIgnoreCase), ShortWait),
            $"Expected an 'Opened logs' status but it was '{StatusMessage}'.");
        AssertLoggedAfter("Open Logs", openLogsOffset, "Opened log directory");

        // Clear Data resets the running totals and is recorded in the log.
        var clearLogOffset = LogFileLength();
        ClickWhenEnabled(ClearDataButton, "Clear Data");
        Assert.True(
            WaitUntil(() => StatusMessage.Contains("cleared", StringComparison.OrdinalIgnoreCase), ShortWait),
            $"Expected a 'Data cleared' status but it was '{StatusMessage}'.");
        AssertLoggedAfter("Clear Data", clearLogOffset, "All sensor data cleared");

        // Disconnect returns to the idle state and is recorded in the log.
        var disconnectLogOffset = LogFileLength();
        ClickWhenEnabled(DisconnectButton, "Disconnect");
        Assert.True(
            WaitUntil(() => ConnectionStatus.Contains("Disconnected", StringComparison.OrdinalIgnoreCase), ShortWait),
            $"Expected 'Disconnected' after disconnect but status was '{ConnectionStatus}'.");
        AssertLoggedAfter("Disconnect", disconnectLogOffset, "Connection status: Disconnected");
    }
}
