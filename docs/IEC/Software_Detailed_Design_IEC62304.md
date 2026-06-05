# Software Detailed Design

Project: WaterFilterCBZ
Standard context: IEC 62304:2006+AMD1:2015, Clause 5.4 (Software detailed design)
Software safety class: C (see [Software Safety Classification](Software_Safety_Classification_IEC62304.md))
Document status: Draft
Last updated: 2026-06-05

## 1. Purpose and Scope

This document provides the detailed design of every software unit of WaterFilterCBZ, satisfying IEC 62304 Clause 5.4:
- **5.4.1** Subdivide the software into software units (the architecture, [§1.4](Software_Architecture_Design_IEC62304.md), defines the units; this document details them).
- **5.4.2** Develop a detailed design for each software unit (required for Class C).
- **5.4.3** Develop a detailed design for the interfaces between units and with external components (Class C).
- **5.4.4** Verify the detailed design (consistency with the architecture and requirements, no internal contradiction).

Each unit below records: responsibility, interface (inputs/outputs), key data/constants, algorithm/behavior, error handling, concurrency, and traceability to requirements ([SRS](Software_Requirements_Specification_IEC62304.md)), risk controls ([Risk file](Software_Risk_Management_IEC62304.md)), and architectural elements ([Architecture §4](Software_Architecture_Design_IEC62304.md)).

Conventions: types and members are named exactly as in source. "UI thread" means the WPF Dispatcher thread; "processing thread" means the background serial task.

## 2. Unit Index

| Unit | File | Architectural element |
|---|---|---|
| `App` | `App.xaml.cs` | AE-APP-001 |
| `LoggingService` | `Services/LoggingService.cs` | AE-LOG-001 |
| `MainWindow` | `MainWindow.xaml.cs` | AE-UI-001 |
| `SensorViewModel` | `ViewModels/SensorViewModel.cs` | AE-VM-001 |
| `SensorDisplayInfo` | `ViewModels/SensorViewModel.cs` | AE-VM-001, AE-MODEL-001 |
| `SensorParameter`, `SensorParameterRegistry`, `SensorValidationState` | `Models/SensorParameter.cs` | AE-MODEL-001 |
| `SensorSample` | `Models/SensorSample.cs` | AE-MODEL-001 |
| `SerialPortService` / `ISerialPortService` | `Services/SerialPortService.cs` | AE-ACQ-001, AE-PROTO-001 |
| `SerialPortHelper` | `Utils/SerialPortHelper.cs` | AE-UTIL-001 |
| `RelayCommand`, `RelayCommand<T>` | `ViewModels/RelayCommand.cs` | AE-VM-001 |
| `ViewModelBase` | `ViewModels/ViewModelBase.cs` | AE-VM-001 |
| `InvertBoolConverter` | `Converters/InvertBoolConverter.cs` | AE-UI-001 |

## 3. Shared Constants

| Constant | Value | Location | Rationale |
|---|---|---|---|
| `BaudRate` (default) | 115200 | `SerialPortService` | Default link speed (SRS-003). |
| Serial framing | 8 data bits, no parity, 1 stop bit | `SerialPortService.Connect` | Matches firmware (SRS-003). |
| Read/Write timeout | 500 ms | `SerialPortService.Connect` | Bounds blocking I/O. |
| `START_BYTE` | 0xAA | `SerialPortService` | Frame start (SRS-004). |
| `END_BYTE` | 0x55 | `SerialPortService` | Frame end (SRS-004). |
| `SENSOR_ENTRY_SIZE_BYTES` | 10 | `SerialPortService` | id(1)+ts(4)+unit(1)+float(4). |
| `MAX_SENSORS` | 4 | `SerialPortService` | Bounds count (RC, SRS-004/008). |
| `FrameAssemblyTimeout` | 350 ms | `SerialPortService` | Partial-frame reset (SRS-016 / RC-005). |
| `PLOT_UPDATE_THRESHOLD_MS` | 50 | `SensorViewModel` | Per-sensor chart throttle (RC-004). |
| Chart point cap | 300 / series | `SensorViewModel.UpdateChartForSensor` | Bounds chart memory (SRS-008). |
| `StaleThreshold` | 5 s | `SensorViewModel` / `SensorDisplayInfo` | Comms-loss timeout (SRS-C-001 / RC-002, OAI-004). |
| Stale timer interval | 1 s | `SensorViewModel` | Freshness re-evaluation cadence. |

## 4. Unit Detailed Designs

### 4.1 `App` (AE-APP-001)

- **Responsibility:** Application startup/shutdown; initialize and flush logging.
- **Interface:** `OnStartup` calls `LoggingService.ConfigureLogging()`; `OnExit` flushes via `Log.CloseAndFlush()`.
- **Error handling:** Startup logging failure must not prevent the UI from opening; logging configuration is defensive.
- **Traceability:** SRS-010.

### 4.2 `LoggingService` (AE-LOG-001)

- **Responsibility:** Configure Serilog sinks (debug + daily rolling file) and expose `LogDirectory` and `OpenLogDirectory()`.
- **Interface:** `ConfigureLogging()` (idempotent setup); `LogDirectory` → `%AppData%\WaterFilterCBZ\logs`; `OpenLogDirectory()` opens that folder.
- **Data:** Minimum level Information for the file sink; daily rolling.
- **Error handling:** Directory creation/opening failures are caught and logged; never throw to the caller.
- **Traceability:** SRS-010, SRS-014 / RC-007.

### 4.3 `MainWindow` (AE-UI-001)

- **Responsibility:** Compose the view; own the `SensorViewModel`; create/dispose the `SerialPortService`; bridge VM connect/disconnect events to the service; ensure cleanup on close.
- **Interface (events in):** `SensorViewModel.ConnectionStatusChanged`, `DisconnectionStatusChanged`. **(out):** constructs `SerialPortService(port, sample => _viewModel.AddSample(sample))`.
- **Key methods:** `InitializeSerialService(comPort)` disposes any prior service and wires the sample callback + status event; `OnConnectRequested` / `OnDisconnectRequested`; `OnClosed` disconnects and disposes.
- **Error handling:** Connect/disconnect wrapped in try/catch; failures set `StatusMessage` and are logged.
- **Concurrency:** Sample callback runs on the processing thread → `SensorViewModel.AddSample` marshals to the UI thread.
- **Traceability:** SRS-002, SRS-011, SRS-012 / RC-006.

### 4.4 `SensorViewModel` (AE-VM-001)

- **Responsibility:** Dashboard state: connection status/message, sample count, available ports, selected port, four `PlotModel`s, the `Sensors` collection, commands, staleness supervision, and validation logging.
- **Interface (bindable):** `ConnectionStatus`, `StatusMessage`, `SampleCount`, `AvailablePorts`, `SelectedPort`, `IsConnected`, `PlotModel1..4`, `Sensors`; commands `ClearDataCommand`, `OpenLogsCommand`, `ConnectCommand`, `DisconnectCommand`; events `ConnectionStatusChanged`, `DisconnectionStatusChanged`.
- **Key data:** `_sensorMap: Dictionary<string,SensorDisplayInfo>`, `_sensorPlotIndex`, `_plots[4]`, `_lastPlotUpdateBySensor`, `_staleTimer: DispatcherTimer`.

**`AddSample(SensorSample)` — algorithm:**
1. Null-guard.
2. If sensor id unknown in `_sensorMap`: create `SensorDisplayInfo(id, StaleThreshold, SensorParameterRegistry.ForSensorId(id))`, add to map, add to `Sensors` on the UI thread, log registration.
3. Capture `previousState = displayInfo.ValidationState`; call `AddValue(value)` (validation inside); set `LastUpdate`; increment `SampleCount`.
4. If `ValidationState` changed → `LogValidationTransition`.
5. If `ValidationState == Invalid` → **return** (do not chart a rejected value).
6. Else update the sensor's chart, throttled per sensor by `PLOT_UPDATE_THRESHOLD_MS` on the UI thread.
- Whole body wrapped in try/catch; exceptions logged, never propagated to the processing thread.

**`EvaluateSensorStaleness()`** (timer tick, UI thread): for each sensor call `EvaluateStaleness(DateTime.UtcNow)`; on a transition, log Warning (stale) or Information (fresh again).

**`UpdateChartForSensor`**: assign a plot index on first appearance (ignore sensors beyond 4 plots); append a `DataPoint(time, value)`; cap series at 300 points; `InvalidatePlot(true)`.

**`ClearAllData`**: clear maps, `Sensors`, sample count, and all series.

- **Error handling:** Defensive try/catch in `AddSample` and `OpenLogs`; `_staleTimer` created only when a Dispatcher exists (so headless tests are unaffected).
- **Concurrency:** All UI-collection and plot mutations occur via `App.Current?.Dispatcher`. Stale timer ticks on the UI thread.
- **Traceability:** SRS-001, SRS-007–SRS-012, SRS-014, SRS-C-001, SRS-C-003 / RC-002, RC-004, RC-006, RC-008.

### 4.5 `SensorDisplayInfo` (AE-VM-001 / AE-MODEL-001)

- **Responsibility:** Per-sensor display state and statistics; freshness; two-tier value validation.
- **Interface (bindable):** `SensorId`, `Parameter`, `DisplayName`, `Unit`, `CurrentValue`, `CurrentValueText`, `MinValue`, `MaxValue`, `AvgValue`, `ReadingCount`, `RejectedCount`, `LastUpdate`, `IsStale`, `StaleThreshold`, `LastSampleAtUtc`, `ValidationState`.
- **Construction:** `(string sensorId, TimeSpan? staleThreshold = null, SensorParameter? parameter = null)`.

**`AddValue(double value)` — algorithm (RC-002, RC-008):**
1. Record receipt: `_lastSampleAtUtc = DateTime.UtcNow`; `IsStale = false` (a sample arrived → link alive).
2. **Tier 1:** if `Parameter != null && !Parameter.IsPhysicallyPlausible(value)` → increment `RejectedCount` (cumulative audit count), set `ValidationState = Invalid` and **return** (do not update value/stats; last good value retained). `MinValue`/`MaxValue` report `0` while `ReadingCount == 0` so the init sentinels never reach the UI.
3. Update `_currentValue`, min, max, `ReadingCount`, running average.
4. **Tier 2:** `ValidationState = (Parameter != null && !IsWithinOperatingSpec(value)) ? OutOfSpec : Normal`.
5. Raise `PropertyChanged` for the derived values + `CurrentValueText`.

**`EvaluateStaleness(DateTime utcNow)`:** `shouldBeStale = ReadingCount > 0 && (utcNow - _lastSampleAtUtc) > StaleThreshold`; if it differs from `IsStale`, set and return `true` (changed) else `false`. A sensor with no readings is never stale.

- **Design notes:** Freshness uses **wall-clock receipt time** (`_lastSampleAtUtc`), independent of the MCU-derived `LastUpdate`, so clock skew on the device cannot mask a communication loss.
- **Traceability:** SRS-007, SRS-C-001, SRS-C-003.

### 4.6 `SensorParameter` / `SensorParameterRegistry` / `SensorValidationState` (AE-MODEL-001)

- **`SensorValidationState`** enum: `Normal`, `OutOfSpec`, `Invalid`.
- **`SensorParameter`** (immutable): `Name`, `Unit`, `OperatingMin/Max`, `PhysicalMin/Max`.
  - `IsPhysicallyPlausible(v)` = `!NaN && !Infinity && PhysicalMin ≤ v ≤ PhysicalMax`.
  - `IsWithinOperatingSpec(v)` = `OperatingMin ≤ v ≤ OperatingMax` (inclusive bounds).
  - `Classify(v)` → `Invalid` if not plausible; else `Normal`/`OutOfSpec`.
- **`SensorParameterRegistry`** (static): fixed `SENSOR_ID`→parameter map (case-insensitive on the formatted id string):

  | Id | Name | Unit | Operating | Physical |
  |---|---|---|---|---|
  | 0x01 | Conductivity | µS/cm | 0.0–1.3 | 0–200 |
  | 0x02 | Temperature | °C | 15–30 | -10–130 |
  | 0x03 | pH | pH | 5.0–7.0 | 0–14 |
  | 0x04 | Pressure | bar | 1–6 | 0–16 |

  `ForSensorId(id)` returns the definition or `null` (unknown id → displayed without range validation). Ranges are **defaults pending confirmation** against the device spec (OAI-003).
- **Traceability:** SRS-C-003 / RC-008.

### 4.7 `SensorSample` (AE-MODEL-001)

- **Responsibility:** Immutable-ish DTO for one decoded reading: `SensorId` (string, e.g. "0x01"), `Timestamp` (DateTime), `Value` (double). `ToString()` for logs.
- **Traceability:** SRS-006.

### 4.8 `SerialPortService` / `ISerialPortService` (AE-ACQ-001, AE-PROTO-001)

- **Responsibility:** Own the COM port; ingest bytes off the serial event; assemble/validate frames on a background task; emit `SensorSample`s via the callback.
- **Interface:** `ISerialPortService`: `IsConnected`, `ConnectionStatusChanged`, `Connect()`, `Disconnect()`, `SetPort(name)`, `Dispose()`. Constructor `(string portName, Action<SensorSample> onSampleReceived)` (callback non-null or throws).
- **Key data:** `_port: SerialPort?`, `_byteQueue: ConcurrentQueue<byte[]>`, `_receiveBuffer: List<byte>`, `_readTask: Task?`, `_cancellationTokenSource`, `_sawStartByteAtUtc`, `_mcuStartTime`, `_firstMcuTimestamp`.

**`Connect()`:** create `SerialPort` with the framing constants + 500 ms timeouts; subscribe `DataReceived`/`ErrorReceived`; open; start `ProcessIncomingDataAsync` on a `Task`; raise status. All wrapped in try/catch — failure logs and raises status (stays disconnected). Idempotent if already connected. (SRS-002, SRS-003 / RC-006.)

**`Disconnect()`:** cancel token; wait up to 2 s for the task; unsubscribe; close+dispose the port; raise status. Idempotent if not connected. Exceptions logged. (SRS-011.)

**`OnDataReceived` (serial thread):** read `BytesToRead` into a chunk; enqueue to `_byteQueue`. Kept minimal so the event handler does not block. Exceptions logged as warnings. (RC-004.)

**`ProcessIncomingDataAsync` (processing thread):** loop until cancellation; dequeue a chunk → append to `_receiveBuffer` → `ParseReceiveBuffer()`; if queue empty, `await Task.Delay(10, ct)`. `OperationCanceledException` is the expected exit; other exceptions logged.

**`ParseReceiveBuffer()` — algorithm (SRS-004/005/016 / RC-001, RC-005):** loop:
1. Find `START_BYTE`; if none → clear buffer, reset start-time, return; drop any leading noise.
2. If `< 2` bytes → set start-time if unset, return (await more).
3. If elapsed since first start byte > `FrameAssemblyTimeout` → log, clear buffer, reset, return.
4. Read `count = buffer[1]`; if `0 || > MAX_SENSORS` → log, `ResyncToNextStartByte(dropCurrentStartByte:true)`, continue.
5. `frameLength = 4 + count*10`; if buffer shorter → return (await more).
6. If `buffer[frameLength-1] != END_BYTE` → log, resync, continue.
7. Copy frame; if `!ValidateChecksum(frame)` → log, resync, continue.
8. `ParseFrame(frame)`; remove the frame bytes; reset start-time.

**`ResyncToNextStartByte(dropCurrentStartByte)`:** search for the next `START_BYTE` (from index 1 if dropping the current); if none → clear; else drop preceding bytes and set start-time. Enables recovery from malformed/noisy input.

**`ValidateChecksum(frame)`:** structural guard (length ≥ 4, start/end bytes), then compare `frame[^2]` to the 8-bit additive sum of bytes `0..len-3`. *Design note:* additive checksum is the current integrity mechanism; RC-001b (CRC) is a pending decision (SRS-C-008).

**`ParseFrame(frame)`:** re-validate structure/length; for each of `count` entries decode `sensorId(1)`, `timestampMs(uint32 LE)`, `unitId(1)`, `value(float32 LE)`; build `SensorSample{ SensorId="0x{id:X2}", Timestamp=DecodeTimestamp(ts), Value=value }`; invoke the callback; debug-log. *Note:* `unitId` is decoded and logged but not yet used for unit cross-checking (future work alongside SRS-C-002).

**`DecodeTimestamp(uint ms)`:** on the first sample, anchor `_mcuStartTime = DateTime.Now`, store `_firstMcuTimestamp`, return the anchor; thereafter return `anchor + (ms - first)` ms. (SRS-015.)

- **Concurrency:** Three threads cooperate — serial event (enqueue only), processing task (parse), UI (consume via callback→VM marshaling). The `ConcurrentQueue` is the hand-off; `_receiveBuffer` is touched only by the processing task.
- **Error handling:** Every external boundary (open, read, parse, task body) is guarded and logged; malformed frames are contained (no throw, resync).
- **Open item:** No explicit maximum length on `_receiveBuffer` yet (RC-005 / SRS-C-004 buffer cap pending).
- **Traceability:** SRS-002–006, SRS-015, SRS-016, SRS-017 / RC-001, RC-004, RC-005.

### 4.9 `SerialPortHelper` (AE-UTIL-001)

- **Responsibility:** Enumerate COM ports (`GetAvailablePorts()` via `SerialPort.GetPortNames()`) and check availability.
- **Error handling:** Returns an empty/safe result on enumeration failure.
- **Traceability:** SRS-001 / RC-006.

### 4.10 `RelayCommand` / `RelayCommand<T>` (AE-VM-001)

- **Responsibility:** `ICommand` adapter: `Execute` delegates to an `Action`; `CanExecute` to an optional predicate; raises `CanExecuteChanged` via `CommandManager.RequerySuggested`.
- **Traceability:** SRS-012.

### 4.11 `ViewModelBase` (AE-VM-001)

- **Responsibility:** `INotifyPropertyChanged` base; `OnPropertyChanged([CallerMemberName])` and `SetProperty(ref field, value)` (returns `true` when changed).
- **Traceability:** Supports all bindable VM state.

### 4.12 `InvertBoolConverter` (AE-UI-001)

- **Responsibility:** `IValueConverter` returning the boolean inverse; used to disable the port selector while connected.
- **Traceability:** SRS-012.

## 5. Interface Detailed Design (Clause 5.4.3)

| Interface | Producer → Consumer | Contract |
|---|---|---|
| Sample callback | `SerialPortService` → `SensorViewModel.AddSample` | `Action<SensorSample>`; invoked on the processing thread; consumer must marshal to UI. |
| Connection status | `SerialPortService.ConnectionStatusChanged` → `MainWindow` | .NET event; raised on connect/disconnect/failure. |
| VM connect/disconnect | `SensorViewModel` events → `MainWindow` | `Action` events; window owns the service lifecycle. |
| Data binding / commands | `SensorViewModel` → `MainWindow.xaml` | `INotifyPropertyChanged` + `ICommand`; automation ids on key controls (SRS-013). |
| Validation/stale state | `SensorDisplayInfo` → XAML | `ValidationState` enum + `IsStale` drive value colour and the OUT OF SPEC / INVALID / STALE indicators. |
| Serial framing | firmware ↔ `SerialPortService` | Binary frame per [Architecture §2.4](Software_Architecture_Design_IEC62304.md). |

## 6. Detailed Design Verification (Clause 5.4.4)

The detailed design is verified by review against these criteria:

| Criterion | Result |
|---|---|
| Implements the architecture ([§1](Software_Architecture_Design_IEC62304.md)) | Yes — every architectural element maps to one or more detailed units (§2). |
| Implements the requirements | Yes — each unit traces to SRS / SRS-C items (§4). |
| No internal contradictions | Verified: validation tiers, freshness, and threading responsibilities are mutually consistent. |
| Free of unintended functionality | The decoded `UNIT_ID` is currently unused (logged only); recorded as an open item, not hidden behaviour. |
| Risk controls realizable | RC-001/002/004/005/006/008 map to concrete units; pending RCs (003/009/010/011, 005 cap, 001b) are flagged. |

Open detailed-design items: explicit receive-buffer cap (SRS-C-004), processing-task failure surfacing (SRS-C-005), device/protocol-version check and `UNIT_ID` use (SRS-C-002), configuration protection/audit (SRS-C-007).

## 7. Revision History

| Revision | Date | Author | Description |
|---|---|---|---|
| 0.1 | 2026-06-05 | Claude | Initial detailed design covering all software units and interfaces, including the new stale-data supervision and two-tier value-validation units; detailed-design verification (5.4.4) and open items recorded. |
