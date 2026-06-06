# WaterFilterCBZ.UITests

End-to-end UI Automation tests that drive the real WPF window using
[FlaUI](https://github.com/FlaUI/FlaUI) (UIA3) and the bundled Python serial simulator
feeding frames over a virtual COM pair. Two test classes:

- **`ConnectionWorkflowTests`** — the full operator workflow:
  **select port → connect → observe live data → open logs → clear → disconnect**, also
  verifying each command is recorded in the rolling log file.
- **`AlarmWorkflowTests`** — the safety alarms, each driven by a simulator `--scenario` and
  asserting both the on-screen indicator and the matching audit-log entry:
  **out-of-spec** (RC-008 tier 2), **invalid/rejected** (RC-008 tier 1),
  **stale data** (RC-002), and **frame-corruption resilience** (malformed frames are dropped,
  not surfaced as a false alarm).

These tests are **local / dedicated-agent only**. They are deliberately excluded from the
fast CI run (the GitHub workflow and `Jenkinsfile` test `WaterFilterCBZ.Tests.csproj`
explicitly) and instead have their own [`Jenkinsfile.uitests`](../Jenkinsfile.uitests)
pipeline, because they need an interactive desktop session, a virtual COM pair, and Python.
When that environment is missing the tests **self-skip** with a precise reason rather than
failing.

## One-time machine setup

1. **Create a virtual COM pair.** Install [com0com](https://com0com.sourceforge.net/) (or any
   null-modem emulator) and create a pair, e.g. `COM3 <-> COM11`. The two names are the two
   ends of one cable: the simulator writes to one end, the app reads from the other.
2. **Install Python + the simulator dependency:**

   ```powershell
   python -m pip install -r tools/requirements-simulator.txt
   ```

3. **Build the app** so the test can find `WaterFilterCBZ.exe`:

   ```powershell
   dotnet build WaterFilterCBZ.csproj -c Debug
   ```

## Running

```powershell
dotnet test WaterFilterCBZ.UITests/WaterFilterCBZ.UITests.csproj
```

Do not touch the mouse/keyboard while it runs — UI Automation drives the real cursor.

## Configuration (environment variables)

| Variable | Default | Purpose |
| --- | --- | --- |
| `WFCBZ_APP_PORT` | `COM3` | Port the **app** connects to (reader end of the pair) |
| `WFCBZ_SIM_PORT` | `COM11` | Port the **simulator** writes to (writer end of the pair) |
| `WFCBZ_PYTHON` | `python` | Python launcher (e.g. an absolute path, or `py`) |
| `WFCBZ_APP_EXE` | _auto_ | Explicit path to `WaterFilterCBZ.exe` (otherwise the newest build under `bin/` is used) |

`WFCBZ_APP_PORT` and `WFCBZ_SIM_PORT` must be the two ends of the **same** virtual pair, or
the app will connect but receive no data and the test will fail at the "samples received" step.

## What the tests assert

**`ConnectionWorkflowTests`** (simulator `--scenario normal`):

- After **Connect**, `ConnectionStatus` shows `Connected` and `SampleCount` climbs above zero.
- At least one sensor registers in the Active Sensors panel.
- **Open Logs** produces an `Opened logs` status.
- **Clear Data** produces a `Data cleared` status.
- **Disconnect** returns `ConnectionStatus` to `Disconnected`.
- Each command (connect, open logs, clear, disconnect) is written to the rolling log file
  under `%AppData%\WaterFilterCBZ\logs` — verified by snapshotting the file length before the
  action and asserting the new tail contains the expected entry.

**`AlarmWorkflowTests`** — connect, then assert the alarm indicator **and** its audit-log entry:

| Test | Simulator | Indicator (AutomationId) | Log substring |
| --- | --- | --- | --- |
| Out-of-spec | `--scenario out-of-spec` | `SensorOutOfSpecIndicator` | `is out of operating spec` |
| Invalid/rejected | `--scenario invalid` | `SensorInvalidIndicator` + `SensorRejectedCount` > 0 | `implausible` |
| Stale | `--scenario stale` | `SensorStaleIndicator` (after the feed goes silent; stays `Connected`) | `data is stale` |
| Corruption resilience | `--scenario normal --inject-errors checksum` | _none_ — `SampleCount` keeps climbing, no false `SensorInvalidIndicator` | — |

> Note: exercising **Open Logs** launches the OS file browser at the logs folder (that is what
> the feature does); the test reads the log file directly rather than driving that window.

Elements are located by `AutomationProperties.AutomationId` (`ConnectButton`,
`DisconnectButton`, `ClearDataButton`, `PortSelector`, `ConnectionStatusText`,
`StatusMessageText`, `SampleCountText`, `SensorList`, and the alarm indicators
`SensorOutOfSpecIndicator` / `SensorInvalidIndicator` / `SensorRejectedCount` /
`SensorStaleIndicator`) so assertions are immune to UI text and locale (decimal separators,
localized error strings, etc.).

## Simulator scenarios

The bundled simulator (`tools/sensor_simulator.py`) generates per-sensor values aligned to the
app's `SensorParameterRegistry`, so a scenario deterministically drives a given alarm state:

| Flag | Effect |
| --- | --- |
| `--scenario normal` | All sensors within their operating band (no alarm). |
| `--scenario out-of-spec` | Plausible value above the operating band (OutOfSpec / tier 2). |
| `--scenario invalid` | Value beyond the physical range (rejected / tier 1). |
| `--scenario stale` | A short burst of normal frames, then silence so the 5 s stale timer fires. |
| `--target-sensor 1-4` | Restrict the anomaly to a single sensor (default: all). |
| `--inject-errors {checksum,end-byte,count,partial,noise}` | Periodically emit malformed frames (parser resilience). |

## Note on button timing

The app's `RelayCommand` re-evaluates `CanExecute` only on `CommandManager.RequerySuggested`
(i.e. on UI input). The test's `ClickWhenEnabled` helper nudges the mouse over a button to
trigger that requery before invoking it, so a freshly-enabled Connect/Disconnect button is
clicked reliably.
