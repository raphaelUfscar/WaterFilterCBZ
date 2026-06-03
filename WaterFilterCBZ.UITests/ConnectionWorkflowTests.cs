using System.Diagnostics;
using System.Drawing;
using System.IO;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.UIA3;
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
public sealed class ConnectionWorkflowTests : IDisposable
{
    private static readonly TimeSpan ShortWait = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DataWait = TimeSpan.FromSeconds(10);

    private SimulatorProcess? _simulator;
    private Application? _app;
    private UIA3Automation? _automation;
    private Window? _window;

    [SkippableFact]
    public void FullConnectionWorkflow_ConnectsStreamsClearsAndDisconnects()
    {
        var skip = TestEnvironment.GetSkipReason();
        Skip.If(skip is not null, skip ?? string.Empty);

        // Arrange: the simulator owns the writer side of the pair for this test's lifetime.
        _simulator = SimulatorProcess.Start(
            TestEnvironment.Python,
            TestEnvironment.ResolveSimulatorScript()!,
            TestEnvironment.SimPort);

        _app = Application.Launch(TestEnvironment.ResolveAppExe()!);
        _automation = new UIA3Automation();
        _window = _app.GetMainWindow(_automation, ShortWait)
                  ?? throw new InvalidOperationException("App main window did not appear.");

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
            $"Expected 'Connected' but status was '{ConnectionStatus}'. Simulator output:{Environment.NewLine}{_simulator.OutputSoFar}");

        Assert.True(
            WaitUntil(() => SampleCount > 0, DataWait),
            $"No samples were received within {DataWait.TotalSeconds:N0}s. Simulator output:{Environment.NewLine}{_simulator.OutputSoFar}");

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

    // --- Element accessors (located by AutomationId, so they are locale-independent) ---

    private Button ConnectButton => FindButton("ConnectButton");
    private Button DisconnectButton => FindButton("DisconnectButton");
    private Button ClearDataButton => FindButton("ClearDataButton");
    private Button OpenLogsButton => FindButton("OpenLogsButton");
    private ComboBox PortSelector => Find("PortSelector").AsComboBox();

    private string ConnectionStatus => Find("ConnectionStatusText").Name ?? string.Empty;
    private string StatusMessage => Find("StatusMessageText").Name ?? string.Empty;

    private int SampleCount
    {
        get
        {
            var raw = Find("SampleCountText").Name ?? "0";
            var digits = new string(raw.Where(char.IsDigit).ToArray());
            return int.TryParse(digits, out var n) ? n : 0;
        }
    }

    private int SensorCount =>
        _window!.FindAllDescendants(cf => cf.ByControlType(ControlType.Text))
            .Count(e => (e.Name ?? string.Empty).StartsWith("Sensor: 0x", StringComparison.OrdinalIgnoreCase));

    private AutomationElement Find(string automationId) =>
        _window!.FindFirstDescendant(cf => cf.ByAutomationId(automationId))
        ?? throw new InvalidOperationException($"Element '{automationId}' not found in the window.");

    private Button FindButton(string automationId) => Find(automationId).AsButton();

    // --- Helpers ---

    /// <summary>
    /// Clicks a button once it is enabled. The app's RelayCommand relies on
    /// CommandManager.RequerySuggested, so CanExecute is only re-evaluated on UI input —
    /// moving the mouse over the button nudges that requery before we invoke it.
    /// </summary>
    private static void ClickWhenEnabled(Button button, string name)
    {
        var timeout = ShortWait;
        var sw = Stopwatch.StartNew();
        Exception? last = null;
        while (sw.Elapsed < timeout)
        {
            try
            {
                var r = button.BoundingRectangle;
                Mouse.MoveTo(new Point(r.X + r.Width / 2, r.Y + r.Height / 2));
                if (button.IsEnabled)
                {
                    button.Invoke();
                    return;
                }
            }
            catch (Exception ex)
            {
                last = ex;
            }

            Thread.Sleep(150);
        }

        throw new TimeoutException($"Button '{name}' did not become enabled within {timeout.TotalSeconds:N0}s.", last);
    }

    /// <summary>
    /// Asserts that, after a command, a log entry containing <paramref name="expectedSubstring"/>
    /// was appended to the rolling log file beyond <paramref name="beforeOffset"/>. Polls because
    /// Serilog flushes asynchronously relative to the UI action.
    /// </summary>
    private void AssertLoggedAfter(string actionName, long beforeOffset, string expectedSubstring)
    {
        Assert.True(
            WaitUntil(() => ReadLogTail(beforeOffset).Contains(expectedSubstring, StringComparison.OrdinalIgnoreCase), ShortWait),
            $"Expected a log entry containing '{expectedSubstring}' after {actionName}, but it was not found.{Environment.NewLine}" +
            $"New log content since the action:{Environment.NewLine}{ReadLogTail(beforeOffset)}");
    }

    /// <summary>Newest rolling log file (app-*.txt) in the app's log directory, or null if none yet.</summary>
    private static string? CurrentLogFile()
    {
        if (!Directory.Exists(TestEnvironment.LogDirectory))
            return null;

        return new DirectoryInfo(TestEnvironment.LogDirectory)
            .GetFiles("app-*.txt")
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .FirstOrDefault()?.FullName;
    }

    private static long LogFileLength()
    {
        var file = CurrentLogFile();
        if (file is null || !File.Exists(file))
            return 0;

        // Share ReadWrite so we coexist with Serilog's open write handle.
        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return stream.Length;
    }

    /// <summary>Reads log content appended after <paramref name="fromOffset"/> (handles a midnight roll-over defensively).</summary>
    private static string ReadLogTail(long fromOffset)
    {
        var file = CurrentLogFile();
        if (file is null || !File.Exists(file))
            return string.Empty;

        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (fromOffset > stream.Length)
            fromOffset = 0;

        stream.Seek(fromOffset, SeekOrigin.Begin);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static bool WaitUntil(Func<bool> condition, TimeSpan timeout, int pollMs = 150)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            try
            {
                if (condition())
                    return true;
            }
            catch
            {
                // Element may be momentarily unavailable during UI updates; keep polling.
            }

            Thread.Sleep(pollMs);
        }

        return false;
    }

    public void Dispose()
    {
        try
        {
            _app?.Close();
            if (_app is not null && !_app.HasExited)
                _app.Kill();
        }
        catch
        {
            // Best-effort teardown.
        }

        _automation?.Dispose();
        _app?.Dispose();
        _simulator?.Dispose();
    }
}
