# WaterFilterCBZ - Real-Time Sensor Monitor

WaterFilterCBZ is a WPF desktop dashboard for monitoring sensor readings from a microcontroller over USB serial communication. It discovers available COM ports, connects to the selected port, parses framed binary sensor packets, and displays live values and charts for up to four sensors.

## Project Structure

```text
WaterFilterCBZ/
|-- Converters/
|   `-- InvertBoolConverter.cs       # WPF boolean inversion converter
|-- Models/
|   `-- SensorSample.cs              # Sensor reading model and legacy CSV parser helper
|-- Services/
|   |-- LoggingService.cs            # Serilog configuration
|   `-- SerialPortService.cs         # Serial connection, frame parsing, and lifecycle handling
|-- Utils/
|   `-- SerialPortHelper.cs          # COM port discovery helpers
|-- tools/
|   |-- sensor_simulator.py          # Python serial frame simulator
|   |-- requirements-simulator.txt   # Python simulator dependency
|   `-- test_sensor_simulator.py     # Python frame encoder tests
|-- ViewModels/
|   |-- RelayCommand.cs              # ICommand implementation
|   |-- SensorViewModel.cs           # Dashboard state, charts, commands, and sensor statistics
|   `-- ViewModelBase.cs             # INotifyPropertyChanged base
|-- WaterFilterCBZ.Tests/
|   |-- RelayCommandTests.cs
|   |-- SensorDisplayInfoTests.cs
|   |-- SensorSampleTests.cs
|   `-- SerialPortServiceTests.cs
|-- App.xaml / App.xaml.cs           # Application startup and shutdown
|-- MainWindow.xaml / MainWindow.xaml.cs
|-- WaterFilterCBZ.csproj
`-- WaterFilterCBZ.slnx
```

## Technology Stack

| Component | Technology | Purpose |
|-----------|------------|---------|
| UI framework | WPF on .NET 10.0 Windows | Desktop dashboard |
| Charts | OxyPlot.Wpf 2.1.2 | Live sensor plots |
| Logging | Serilog | Debug and rolling file logs |
| Serial communication | System.IO.Ports | USB CDC / COM port communication |
| Tests | xUnit, Microsoft.NET.Test.Sdk | Unit testing |
| Coverage | coverlet.collector | OpenCover reports for SonarQube |
| Pattern | MVVM | Testable UI state and commands |

## Serial Communication

The app reads binary frames from the selected COM port at `115200` baud by default. `SerialPortService.BaudRate` can be configured before connecting.

Serial settings:

| Setting | Value |
|---------|-------|
| Baud rate | 115200 |
| Data bits | 8 |
| Parity | None |
| Stop bits | 1 |

### Frame Format

Current serial parsing expects this frame shape:

```text
START(0xAA) | COUNT | SENSOR_ENTRY_1 ... SENSOR_ENTRY_N | CHECKSUM | END(0x55)
```

Rules:

- `COUNT` must be from `1` to `4`.
- Each sensor entry is 10 bytes.
- `CHECKSUM` is the 8-bit sum of all bytes from `START` through the last sensor-entry byte.
- `END` must be `0x55`.
- Invalid frames trigger parser resynchronization to the next start byte.
- Partial frames are discarded after the frame assembly timeout.

Sensor entry layout:

```text
SENSOR_ID(1 byte) | TIMESTAMP_MS(uint32) | UNIT_ID(1 byte) | VALUE(float32)
```

`TIMESTAMP_MS` is treated as a microcontroller millisecond counter. The first received timestamp is anchored to the PC wall clock; later samples are offset from that first sample.

### Python Serial Simulator

The repository includes a Python simulator for validating the full serial integration path without a physical sensor board. It writes the same binary frames described above to one side of a serial connection, so WaterFilterCBZ can connect to the other side exactly as it would connect to firmware.

Install the simulator dependency:

```powershell
python -m pip install -r tools/requirements-simulator.txt
```

Confirm which serial ports Python can see:

```powershell
python tools/sensor_simulator.py --list-ports
```

For PC-only testing on Windows, create a virtual null-modem pair such as:

```text
COM10 <-> COM11
```

Run the simulator on one side of the pair:

```powershell
python tools/sensor_simulator.py --port COM10
```

Then start WaterFilterCBZ, select `COM11`, and connect. The default simulator run emits four sensors at `10 Hz` using mixed sine, ramp, step, and noise signals.

If the simulator prints `could not open`, confirm that the requested port appears in `--list-ports`. If it does not appear, the virtual COM pair has not been created with that name. If it does appear, close any other program using that side of the pair and run the simulator on one port while WaterFilterCBZ uses the paired port.

Useful simulator options:

```powershell
python tools/sensor_simulator.py --port COM10 --sensors 2 --rate-hz 20 --profile sine
python tools/sensor_simulator.py --port COM10 --duration-seconds 30
python tools/sensor_simulator.py --port COM10 --inject-errors checksum
python tools/sensor_simulator.py --port COM10 --inject-errors end-byte
python tools/sensor_simulator.py --port COM10 --inject-errors noise
```

Supported profiles are `sine`, `ramp`, `step`, `noise`, and `mixed`. Supported error injection modes are `none`, `checksum`, `end-byte`, `count`, `partial`, and `noise`. Error injection periodically sends malformed traffic and then resumes valid frames, which is useful for confirming parser resynchronization and warning logs.

## Application Behavior

### Connection Flow

- The app refreshes available COM ports on startup.
- A COM port can be selected from the bottom control bar.
- The selected port is disabled while connected.
- Connect and disconnect are routed through the `SensorViewModel` commands and handled by `MainWindow`.

The default selected port is `COM4`, but the UI will select an available port if `COM4` is not present.

### Dashboard

- Top status bar shows connection state, status message, and sample count.
- Main area contains four OxyPlot charts in a 2x2 grid.
- Each sensor is assigned to one chart, up to four active sensors.
- Each chart keeps the latest 300 points.
- Chart updates are throttled per sensor to reduce UI load.
- The right panel shows active sensors with current value, min, max, average, reading count, and last update time.
- The bottom bar provides Clear Data, COM port selection, Connect, and Disconnect controls.

## Logging

Logs are written to:

```text
%AppData%\WaterFilterCBZ\logs\
```

Logging outputs:

- Debug output for local development.
- Daily rolling log files for Info-level and above.

Useful events include:

- `Serial port {PortName} opened at {BaudRate} bps`
- `Failed to open serial port {PortName}`
- `Received sensor sample: {Sample} unit={UnitId}`
- Frame validation and resynchronization warnings
- Connection status changes

## Build and Test

Restore and build:

```powershell
dotnet restore
dotnet build WaterFilterCBZ.csproj -c Release
```

Run the app:

```powershell
dotnet run --project WaterFilterCBZ.csproj
```

Run tests:

```powershell
dotnet test WaterFilterCBZ.Tests/WaterFilterCBZ.Tests.csproj --no-restore
```

Run simulator frame encoding tests:

```powershell
python -m unittest discover -s tools -p "test_*.py"
```

Generate OpenCover coverage locally:

```powershell
dotnet test WaterFilterCBZ.Tests/WaterFilterCBZ.Tests.csproj --configuration Release --collect:"XPlat Code Coverage" -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover
```

Coverage reports are generated under:

```text
WaterFilterCBZ.Tests/TestResults/<run-id>/coverage.opencover.xml
```

## Continuous Integration

The repository includes:

- `.github/workflows/dotnet-desktop.yml` for restore, build, test, publish, and artifact upload.
- `.github/workflows/sonarqube.yml` for SonarQube analysis with OpenCover coverage import.

The SonarQube workflow passes `SONAR_TOKEN` through environment variables and verifies that a `coverage.opencover.xml` file was generated before ending analysis.

## Troubleshooting

### No COM ports are listed

- Confirm the microcontroller is connected.
- Check Windows Device Manager for the assigned COM port.
- Reconnect the device and restart the app if Windows has not exposed the port yet.

### Connection fails

- Make sure no other tool is using the same COM port.
- Confirm the selected port matches the microcontroller.
- Check the logs for `Failed to open serial port`.

### Charts are not updating

- Confirm the device is sending the binary frame format described above.
- Check logs for invalid count, checksum, or end-byte warnings.
- Verify that no more than four distinct sensors are expected in the dashboard.

### SonarQube shows 0.0% coverage

- Confirm the workflow generated `coverage.opencover.xml`.
- Confirm `sonar.cs.opencover.reportsPaths` points to `**/TestResults/**/coverage.opencover.xml`.
- Check the `Verify coverage report` workflow step output.

## Current Status

The app is ready for integration with firmware that emits the framed binary protocol described above. Unit tests cover the core model, command, sensor display, and serial service behavior.
