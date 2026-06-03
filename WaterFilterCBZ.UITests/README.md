# WaterFilterCBZ.UITests

End-to-end UI Automation tests that drive the real WPF window through the full
connection workflow — **select port → connect → observe live data → open logs → clear → disconnect** —
using [FlaUI](https://github.com/FlaUI/FlaUI) (UIA3) and the bundled Python serial
simulator feeding frames over a virtual COM pair. It also verifies each command is
recorded in the rolling log file.

These tests are **local / dedicated-agent only**. They are deliberately excluded from the
fast CI run (the GitHub workflow tests `WaterFilterCBZ.Tests.csproj` explicitly), because
they need an interactive desktop session, a virtual COM pair, and Python. When that
environment is missing the test **self-skips** with a precise reason rather than failing.

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

## What the test asserts

- After **Connect**, `ConnectionStatus` shows `Connected` and `SampleCount` climbs above zero.
- At least one sensor registers in the Active Sensors panel.
- **Open Logs** produces an `Opened logs` status.
- **Clear Data** produces a `Data cleared` status.
- **Disconnect** returns `ConnectionStatus` to `Disconnected`.
- Each command (connect, open logs, clear, disconnect) is written to the rolling log file
  under `%AppData%\WaterFilterCBZ\logs` — verified by snapshotting the file length before the
  action and asserting the new tail contains the expected entry.

> Note: exercising **Open Logs** launches the OS file browser at the logs folder (that is what
> the feature does); the test reads the log file directly rather than driving that window.

Elements are located by `AutomationProperties.AutomationId` (`ConnectButton`,
`DisconnectButton`, `ClearDataButton`, `PortSelector`, `ConnectionStatusText`,
`StatusMessageText`, `SampleCountText`, `SensorList`) so assertions are immune to UI text and
locale (decimal separators, localized error strings, etc.).

## Note on button timing

The app's `RelayCommand` re-evaluates `CanExecute` only on `CommandManager.RequerySuggested`
(i.e. on UI input). The test's `ClickWhenEnabled` helper nudges the mouse over a button to
trigger that requery before invoking it, so a freshly-enabled Connect/Disconnect button is
clicked reliably.
