using System.IO;
using System.IO.Ports;

namespace WaterFilterCBZ.UITests;

/// <summary>
/// Resolves the moving parts an end-to-end UI run depends on (the built app exe,
/// the Python simulator, and the virtual COM pair) and decides whether the
/// environment is complete enough to run. When something is missing the test
/// self-skips with a precise reason instead of failing.
/// </summary>
internal static class TestEnvironment
{
    /// <summary>App-side port the WPF app connects to and reads from (default COM3).</summary>
    public static string AppPort =>
        Environment.GetEnvironmentVariable("WFCBZ_APP_PORT") ?? "COM3";

    /// <summary>Writer-side port the simulator emits frames on; the pair partner of <see cref="AppPort"/> (default COM11).</summary>
    public static string SimPort =>
        Environment.GetEnvironmentVariable("WFCBZ_SIM_PORT") ?? "COM11";

    /// <summary>Python launcher; override with WFCBZ_PYTHON (e.g. an absolute path or "py").</summary>
    public static string Python =>
        Environment.GetEnvironmentVariable("WFCBZ_PYTHON") ?? "python";

    public static bool PortExists(string port) =>
        SerialPort.GetPortNames().Any(p => string.Equals(p, port, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Returns a human-readable reason the environment is not runnable, or null when everything is present.
    /// </summary>
    public static string? GetSkipReason()
    {
        if (!OperatingSystem.IsWindows())
            return "UI automation tests require Windows.";

        if (ResolveAppExe() is null)
            return "Could not locate WaterFilterCBZ.exe. Build the app first, or set WFCBZ_APP_EXE.";

        if (ResolveSimulatorScript() is null)
            return "Could not locate tools/sensor_simulator.py.";

        if (!PortExists(SimPort))
            return $"Simulator port {SimPort} not found. Create a virtual COM pair ({SimPort} <-> {AppPort}) or set WFCBZ_SIM_PORT.";

        if (!PortExists(AppPort))
            return $"App port {AppPort} not found. Create a virtual COM pair ({SimPort} <-> {AppPort}) or set WFCBZ_APP_PORT.";

        return null;
    }

    /// <summary>Walk up from the test output directory to the repository root (the folder holding the .slnx).</summary>
    public static string? ResolveRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (dir.GetFiles("WaterFilterCBZ.slnx").Length > 0)
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }

    /// <summary>Locate the built app exe, honoring WFCBZ_APP_EXE, then probing Debug/Release output folders.</summary>
    public static string? ResolveAppExe()
    {
        var overridePath = Environment.GetEnvironmentVariable("WFCBZ_APP_EXE");
        if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
            return overridePath;

        var root = ResolveRepoRoot();
        if (root is null)
            return null;

        // Prefer the most recently built exe across configurations/TFMs.
        var candidates = Directory.EnumerateFiles(
                Path.Combine(root, "bin"),
                "WaterFilterCBZ.exe",
                SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToList();

        return candidates.FirstOrDefault();
    }

    public static string? ResolveSimulatorScript()
    {
        var root = ResolveRepoRoot();
        if (root is null)
            return null;

        var script = Path.Combine(root, "tools", "sensor_simulator.py");
        return File.Exists(script) ? script : null;
    }
}
