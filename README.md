# WaterFilterCBZ - Real-Time Sensor Monitor

A WPF-based real-time sensor monitoring dashboard connected to a microcontroller via USB serial communication.

## Architecture Overview

### Project Structure
```
WaterFilterCBZ/
├── Models/                    # Data models
│   └── SensorSample.cs       # Sensor reading model with CSV parsing
├── Services/
│   ├── SerialPortService.cs  # USB/Serial communication handler
│   ├── LoggingService.cs     # Serilog configuration
│   └── LoggingService.cs     # Centralized Serilog setup
├── ViewModels/               # MVVM ViewModels
│   ├── ViewModelBase.cs      # INotifyPropertyChanged base
│   ├── RelayCommand.cs       # Command binding support
│   └── SensorViewModel.cs    # Main dashboard ViewModel
├── Utils/
│   └── SerialPortHelper.cs   # COM port discovery utilities
├── MainWindow.xaml           # Main UI (WPF)
├── MainWindow.xaml.cs        # Code-behind with OxyPlot binding
└── App.xaml.cs               # Application bootstrap
```

## Technology Stack

| Component | Technology | Version | Purpose |
|-----------|-----------|---------|---------|
| UI Framework | WPF | .NET 10.0-windows | Modern Windows UI |
| Charts | OxyPlot.Wpf | 2.1.2 | Real-time plotting |
| Logging | Serilog | 4.0.0 | Structured logging |
| Serial Comm | System.IO.Ports | 9.0.0 | USB CDC communication |
| Pattern | MVVM | - | Testable, maintainable architecture |

## Configuration

### Serial Port Settings
- **Port:** COM4 (configured in MainWindow.xaml.cs)
- **Baud Rate:** 115,200 bps
- **Data Bits:** 8
- **Parity:** None
- **Stop Bits:** 1
- **Line Terminator:** `\n` (newline)

### Message Format
Binary packet frame:
```
SOF(0xAA) | LEN | SENDER | RECEIVER | PAYLOAD | CRC(2 bytes) | EOF(0x55)
```

Payload types supported by the app:
- `0x01`: sensor data batch
- `0x02`: command
- `0x03`: response

Sensor data payload structure:
```
MSG_TYPE(0x01) | COUNT | SENSOR_BLOCK_1 | SENSOR_BLOCK_2 | ...
```

Each sensor block is 10 bytes:
- `SENSOR_ID` (1 byte)
- `TIMESTAMP` (4 bytes, uint32 ms)
- `UNIT_ID` (1 byte)
- `VALUE` (4 bytes float)

Parsing handles:
- Frame boundary detection with SOF/EOF
- CRC-16-CCITT validation
- Batch sensor entry decoding
- Thread-safe UI dispatching

## Key Components

### SensorViewModel
- Manages real-time sensor data collection
- Throttles UI updates to ~10 FPS to prevent overload
- Maintains per-sensor statistics: min, max, avg
- Updates OxyPlot charts dynamically
- Keeps 300 points per chart series (memory bounded)

### SerialPortService
- Background thread serial reading
- Thread-safe line buffering (ConcurrentQueue)
- Automatic CSV line parsing via SensorSample.TryParseCsv()
- UI thread marshaling with Dispatcher.BeginInvoke()
- Graceful connect/disconnect lifecycle
- Comprehensive error logging

### Logging
- **Debug**: Real-time output to VS debug window
- **File**: Daily rolling logs in `%AppData%\WaterFilterCBZ\logs\`
- **Levels**: Debug in console, Info+ in files
- Serilog structured logging for queryable event history

## UI Layout

### Status Bar (Top)
- Connection status (Connected/Disconnected)
- Current sample count
- Real-time status messages

### Main Chart Area (Left)
- OxyPlot real-time line chart
- Multi-sensor overlay (different colors)
- Pan/zoom support (right-click interactions)
- Timestamped X-axis, value-based Y-axis

### Sensor Panel (Right)
- Scrollable list of active sensors
- Per-sensor metrics:
  - Current value (live update)
  - Min/Max range
  - Running average
  - Reading count
  - Last update timestamp

### Control Bar (Bottom)
- "Clear Data" button: Reset all charts and statistics
- Usage hint for chart interactions

## Usage

### Running the Application
```bash
cd WaterFilterCBZ
dotnet run
```

The application will:
1. Initialize Serilog logging
2. Create a new MainWindow with OxyPlot chart
3. Connect to COM4 on window load
4. Begin listening for sensor data
5. Parse CSV lines and update charts in real time

### Adjusting COM Port
Edit `MainWindow.xaml.cs`, line ~24:
```csharp
_serialService = new SerialPortService("COM4", sample => ...);
```

### Changing Update Throttle
Edit `SensorViewModel.cs`, line ~11:
```csharp
private const int UI_UPDATE_THRESHOLD_MS = 100; // Adjust to 50-200 ms
```

Lower = more responsive, higher = less CPU. Default 100 ms ≈ 10 FPS.

## Monitoring Connection Issues

Check log files for detailed diagnostics:
```
%AppData%\WaterFilterCBZ\logs\
```

**Key Log Events:**
- `Serial port {PortName} opened at {BaudRate} bps` → Connection success
- `Failed to open serial port {PortName}` → COM port unavailable
- `Received sensor sample: {Sample}` → Data parsed successfully
- `Failed to parse sensor line: {Line}` → Legacy CSV or protocol mismatch / frame corruption


## File Locations

| File | Path |
|------|------|
| Application Logs | `%AppData%\WaterFilterCBZ\logs\` |
| Project File | `WaterFilterCBZ.csproj` |
| Log Configuration | `Services\LoggingService.cs` |

## Troubleshooting

### "COM4 not found"
- Check Device Manager for actual port name
- Update `MainWindow.xaml.cs` with correct port

### Charts not updating
- Check logs for parse errors
- Verify CSV format matches specification
- Confirm serial port is actually sending data (use Termite/PuTTY to test)

### High CPU usage
- Increase `UI_UPDATE_THRESHOLD_MS` to 200+ ms
- Reduce sensor frequency on microcontroller side
- Check if log file writes are blocking

## Architecture Decisions

✅ **Background thread reading** → No UI blocking on serial I/O
✅ **ConcurrentQueue buffering** → Thread-safe line collection
✅ **Throttled UI updates** → Prevents overwhelming data rendering
✅ **Per-sensor chart series** → Automatic multi-sensor support
✅ **Serilog structured logs** → Queryable event history
✅ **MVVM pattern** → Testable, decoupled logic

---

**Build Date:** April 26, 2026
**Framework:** .NET 10.0-windows
**Status**: Ready for integration with microcontroller firmware
