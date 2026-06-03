using System.Diagnostics;
using System.IO;

namespace WaterFilterCBZ.UITests;

/// <summary>
/// Launches the bundled Python serial simulator (tools/sensor_simulator.py) on the
/// writer side of the virtual COM pair and shuts it down on dispose, so a test owns
/// its data source for its lifetime.
/// </summary>
internal sealed class SimulatorProcess : IDisposable
{
    private readonly Process _process;
    private readonly List<string> _output = new();

    private SimulatorProcess(Process process) => _process = process;

    public string OutputSoFar
    {
        get { lock (_output) { return string.Join(Environment.NewLine, _output); } }
    }

    public bool HasExited => _process.HasExited;

    /// <summary>
    /// Start the simulator emitting <paramref name="sensors"/> sensors at <paramref name="rateHz"/> Hz
    /// on <paramref name="port"/>. Throws if the process exits immediately (e.g. the port is busy).
    /// </summary>
    public static SimulatorProcess Start(string python, string scriptPath, string port, int sensors = 4, int rateHz = 10)
    {
        var psi = new ProcessStartInfo
        {
            FileName = python,
            WorkingDirectory = Path.GetDirectoryName(scriptPath)!,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add(scriptPath);
        psi.ArgumentList.Add("--port");
        psi.ArgumentList.Add(port);
        psi.ArgumentList.Add("--sensors");
        psi.ArgumentList.Add(sensors.ToString());
        psi.ArgumentList.Add("--rate-hz");
        psi.ArgumentList.Add(rateHz.ToString());

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var sim = new SimulatorProcess(process);

        process.OutputDataReceived += (_, e) => sim.Capture(e.Data);
        process.ErrorDataReceived += (_, e) => sim.Capture(e.Data);

        if (!process.Start())
            throw new InvalidOperationException($"Failed to start Python simulator: {python} {scriptPath}");

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // The simulator reports "could not open" and exits quickly when the port is busy
        // or missing; give it a moment and surface that as a clear failure.
        if (process.WaitForExit(1500))
        {
            throw new InvalidOperationException(
                $"Simulator exited immediately (port '{port}' busy or unavailable?). Output:{Environment.NewLine}{sim.OutputSoFar}");
        }

        return sim;
    }

    private void Capture(string? line)
    {
        if (line is null) return;
        lock (_output) { _output.Add(line); }
    }

    public void Dispose()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(3000);
            }
        }
        catch
        {
            // Best-effort teardown; nothing useful to do if the process is already gone.
        }
        finally
        {
            _process.Dispose();
        }
    }
}
