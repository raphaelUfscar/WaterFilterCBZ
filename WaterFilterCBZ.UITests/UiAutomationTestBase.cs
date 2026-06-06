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
/// Shared scaffolding for the end-to-end UI Automation tests: owns the simulator + app +
/// automation lifecycle, exposes the window's elements by AutomationId (locale-independent),
/// and provides connect/log/alarm helpers. Concrete fixtures drive a specific scenario.
///
/// All UI tests share the <c>[Collection("UI")]</c> collection so only one app instance owns
/// the COM port at a time. Requires the interactive environment in
/// WaterFilterCBZ.UITests/README.md; tests self-skip when it is absent.
/// </summary>
public abstract class UiAutomationTestBase : IDisposable
{
    protected static readonly TimeSpan ShortWait = TimeSpan.FromSeconds(5);
    protected static readonly TimeSpan DataWait = TimeSpan.FromSeconds(10);
    // The app's stale threshold is 5 s; allow headroom for the 1 s supervision tick and UI refresh.
    protected static readonly TimeSpan StaleWait = TimeSpan.FromSeconds(12);

    protected SimulatorProcess? _simulator;
    protected Application? _app;
    protected UIA3Automation? _automation;
    protected Window? _window;

    /// <summary>Skips the calling test (with a precise reason) when the UI environment is incomplete.</summary>
    protected static void SkipIfEnvironmentMissing()
    {
        var skip = TestEnvironment.GetSkipReason();
        Skip.If(skip is not null, skip ?? string.Empty);
    }

    /// <summary>Starts the bundled simulator on the writer side of the pair with the given options.</summary>
    protected void StartSimulator(
        string? scenario = null,
        string? profile = null,
        int? targetSensor = null,
        string? injectErrors = null)
    {
        _simulator = SimulatorProcess.Start(
            TestEnvironment.Python,
            TestEnvironment.ResolveSimulatorScript()!,
            TestEnvironment.SimPort,
            scenario: scenario,
            profile: profile,
            targetSensor: targetSensor,
            injectErrors: injectErrors);
    }

    /// <summary>Launches the WPF app and resolves its main window.</summary>
    protected void LaunchApp()
    {
        _app = Application.Launch(TestEnvironment.ResolveAppExe()!);
        _automation = new UIA3Automation();
        _window = _app.GetMainWindow(_automation, ShortWait)
                  ?? throw new InvalidOperationException("App main window did not appear.");
    }

    /// <summary>
    /// Drives the real UI from the app's auto-connect state to a clean connection on the app-side
    /// port: disconnect, select the port, connect, and confirm the status flips and frames arrive.
    /// </summary>
    protected void ConnectThroughUi()
    {
        if (DisconnectButton.IsEnabled)
            ClickWhenEnabled(DisconnectButton, "Disconnect");
        Assert.True(
            WaitUntil(() => ConnectionStatus.Contains("Disconnected", StringComparison.OrdinalIgnoreCase), ShortWait),
            $"Expected 'Disconnected' but status was '{ConnectionStatus}'.");

        Assert.True(WaitUntil(() => PortSelector.IsEnabled, ShortWait), "Port selector never became enabled.");
        PortSelector.Select(TestEnvironment.AppPort);

        ClickWhenEnabled(ConnectButton, "Connect");
        Assert.True(
            WaitUntil(() => ConnectionStatus.Contains("Connected", StringComparison.OrdinalIgnoreCase), ShortWait),
            $"Expected 'Connected' but status was '{ConnectionStatus}'. Simulator output:{Environment.NewLine}{SimulatorOutput}");

        Assert.True(
            WaitUntil(() => SampleCount > 0, DataWait),
            $"No samples were received within {DataWait.TotalSeconds:N0}s. Simulator output:{Environment.NewLine}{SimulatorOutput}");
    }

    protected string SimulatorOutput => _simulator?.OutputSoFar ?? string.Empty;

    // --- Element accessors (located by AutomationId, so they are locale-independent) ---

    protected Button ConnectButton => FindButton("ConnectButton");
    protected Button DisconnectButton => FindButton("DisconnectButton");
    protected Button ClearDataButton => FindButton("ClearDataButton");
    protected Button OpenLogsButton => FindButton("OpenLogsButton");
    protected ComboBox PortSelector => Find("PortSelector").AsComboBox();

    protected string ConnectionStatus => Find("ConnectionStatusText").Name ?? string.Empty;
    protected string StatusMessage => Find("StatusMessageText").Name ?? string.Empty;

    protected int SampleCount
    {
        get
        {
            var raw = Find("SampleCountText").Name ?? "0";
            var digits = new string(raw.Where(char.IsDigit).ToArray());
            return int.TryParse(digits, out var n) ? n : 0;
        }
    }

    protected int SensorCount =>
        _window!.FindAllDescendants(cf => cf.ByControlType(ControlType.Text))
            .Count(e => (e.Name ?? string.Empty).StartsWith("Sensor: 0x", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// True when at least one element with <paramref name="automationId"/> is present in the
    /// automation tree. WPF collapses hidden alarm indicators out of the tree, so presence is
    /// equivalent to visibility for the SensorOutOfSpec/Invalid/Stale indicators.
    /// </summary>
    protected bool AnyVisible(string automationId) =>
        _window!.FindAllDescendants(cf => cf.ByAutomationId(automationId)).Length > 0;

    /// <summary>Highest "Rejected: N" count currently shown across the sensor panel (0 when none).</summary>
    protected int MaxRejectedCount()
    {
        var max = 0;
        foreach (var el in _window!.FindAllDescendants(cf => cf.ByAutomationId("SensorRejectedCount")))
        {
            var digits = new string((el.Name ?? string.Empty).Where(char.IsDigit).ToArray());
            if (int.TryParse(digits, out var n))
                max = Math.Max(max, n);
        }
        return max;
    }

    protected AutomationElement Find(string automationId) =>
        _window!.FindFirstDescendant(cf => cf.ByAutomationId(automationId))
        ?? throw new InvalidOperationException($"Element '{automationId}' not found in the window.");

    protected Button FindButton(string automationId) => Find(automationId).AsButton();

    // --- Helpers ---

    /// <summary>
    /// Clicks a button once it is enabled. The app's RelayCommand relies on
    /// CommandManager.RequerySuggested, so CanExecute is only re-evaluated on UI input —
    /// moving the mouse over the button nudges that requery before we invoke it.
    /// </summary>
    protected static void ClickWhenEnabled(Button button, string name)
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
    protected void AssertLoggedAfter(string actionName, long beforeOffset, string expectedSubstring)
    {
        Assert.True(
            WaitUntil(() => ReadLogTail(beforeOffset).Contains(expectedSubstring, StringComparison.OrdinalIgnoreCase), ShortWait),
            $"Expected a log entry containing '{expectedSubstring}' after {actionName}, but it was not found.{Environment.NewLine}" +
            $"New log content since the action:{Environment.NewLine}{ReadLogTail(beforeOffset)}");
    }

    /// <summary>Newest rolling log file (app-*.txt) in the app's log directory, or null if none yet.</summary>
    protected static string? CurrentLogFile()
    {
        if (!Directory.Exists(TestEnvironment.LogDirectory))
            return null;

        return new DirectoryInfo(TestEnvironment.LogDirectory)
            .GetFiles("app-*.txt")
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .FirstOrDefault()?.FullName;
    }

    protected static long LogFileLength()
    {
        var file = CurrentLogFile();
        if (file is null || !File.Exists(file))
            return 0;

        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return stream.Length;
    }

    /// <summary>Reads log content appended after <paramref name="fromOffset"/> (handles a midnight roll-over defensively).</summary>
    protected static string ReadLogTail(long fromOffset)
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

    protected static bool WaitUntil(Func<bool> condition, TimeSpan timeout, int pollMs = 150)
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

    public virtual void Dispose()
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
        GC.SuppressFinalize(this);
    }
}
