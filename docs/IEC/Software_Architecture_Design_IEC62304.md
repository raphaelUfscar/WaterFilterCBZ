# Software Architectural Design Document

Project: WaterFilterCBZ  
Standard context: IEC 62304 software architectural design  
Document status: Draft  
Last updated: 2026-05-12

## Document Purpose

This document describes the software architecture of WaterFilterCBZ for IEC 62304 design control purposes. It identifies the high-level software decomposition, interfaces between software items, architectural safety segregation, and traceability links from hazards and risk controls to requirements, architecture, units, and verification.

WaterFilterCBZ is a Windows WPF desktop application that monitors up to four sensor channels received from a microcontroller through USB serial communication. The current application is a monitoring and visualization system. If the software is used to make or support medical-device safety decisions, the safety classification, risk controls, verification depth, and segregation evidence must be completed according to the final intended use.

## 1. Software Architecture

### 1.1 Architectural Overview

WaterFilterCBZ uses a layered desktop architecture based on WPF and MVVM. The application separates presentation, dashboard state, serial communication, domain data, infrastructure utilities, and logging.

```text
WaterFilterCBZ Application
|-- Presentation Layer
|   |-- App lifecycle
|   |-- MainWindow view
|   `-- XAML bindings and chart host controls
|-- ViewModel Layer
|   |-- SensorViewModel
|   |-- SensorDisplayInfo
|   |-- RelayCommand
|   `-- ViewModelBase
|-- Service Layer
|   |-- SerialPortService
|   |-- ISerialPortService
|   `-- LoggingService
|-- Domain Model Layer
|   `-- SensorSample
|-- Utility Layer
|   `-- SerialPortHelper
|-- External Interfaces
|   |-- USB serial / COM port
|   |-- System.IO.Ports
|   |-- WPF Dispatcher
|   |-- OxyPlot.Wpf
|   `-- Serilog file/debug sinks
`-- Verification
    `-- WaterFilterCBZ.Tests
```

### 1.2 Software Systems

| Software system | Responsibility | Primary implementation |
|---|---|---|
| WaterFilterCBZ desktop application | Provides real-time sensor monitoring, connection control, chart display, sensor statistics, and operational logging. | `App.xaml`, `App.xaml.cs`, `MainWindow.xaml`, `MainWindow.xaml.cs` |
| Sensor data acquisition system | Opens the selected COM port, receives byte chunks, assembles framed packets, validates frame structure and checksum, and emits sensor samples. | `Services/SerialPortService.cs` |
| Dashboard state and presentation model | Maintains connection state, selected port, available ports, sensor statistics, charts, and commands. | `ViewModels/SensorViewModel.cs` |
| Verification system | Provides automated unit tests for parser-relevant behavior, commands, data models, and display calculations. | `WaterFilterCBZ.Tests/*` |

### 1.3 Subsystems and Software Items

| Subsystem | Software item | Description | Safety relevance |
|---|---|---|---|
| Application lifecycle | `App` | Configures logging on startup and flushes logging on exit. | Supports auditability and post-event diagnosis. |
| Main window composition | `MainWindow` | Owns the view model, creates and disposes the serial service, and bridges UI connection commands to service calls. | Controls connection lifecycle and resource cleanup. |
| User interface | `MainWindow.xaml` | Displays connection state, sample count, charts, active sensors, and connection controls. | Human-facing presentation of monitored values. |
| View model | `SensorViewModel` | Maintains dashboard state, command enablement, chart models, sample count, and sensor statistics. | Prevents unsafe UI thread access through dispatcher use; limits chart history. |
| Sensor display model | `SensorDisplayInfo` | Tracks current, minimum, maximum, average, reading count, and last update for each active sensor. | Supports operator interpretation of sensor values. |
| Command abstraction | `RelayCommand`, `RelayCommand<T>` | Implements WPF `ICommand` for UI actions. | Prevents invalid connect/disconnect actions through command predicates. |
| Serial service interface | `ISerialPortService` | Abstracts serial-service lifecycle for testability and substitution. | Enables verification and future isolation. |
| Serial service | `SerialPortService` | Handles COM configuration, asynchronous byte ingestion, parser state, frame validation, timestamp decoding, and sample dispatch. | Primary input validation and malformed-frame containment. |
| Sensor sample model | `SensorSample` | Represents a decoded sensor reading. Includes legacy CSV parser helper. | Carries measurement identity, timestamp, and value. |
| Serial port discovery | `SerialPortHelper` | Lists available COM ports and checks port availability. | Reduces incorrect connection selection risk. |
| Logging | `LoggingService` | Configures debug and rolling file logs using Serilog. | Provides diagnostic evidence for failures and unexpected input. |

### 1.4 Units, Modules, and Components

```text
Application startup and shutdown
|-- App.OnStartup()
|-- App.OnExit()
`-- LoggingService.ConfigureLogging()

Window and lifecycle orchestration
|-- MainWindow.InitializeSerialService()
|-- MainWindow.OnConnectRequested()
|-- MainWindow.OnDisconnectRequested()
`-- MainWindow.OnClosed()

Serial acquisition and parsing
|-- SerialPortService.Connect()
|-- SerialPortService.Disconnect()
|-- SerialPortService.SetPort()
|-- SerialPortService.OnDataReceived()
|-- SerialPortService.ProcessIncomingDataAsync()
|-- SerialPortService.ParseReceiveBuffer()
|-- SerialPortService.ResyncToNextStartByte()
|-- SerialPortService.ValidateChecksum()
|-- SerialPortService.ParseFrame()
`-- SerialPortService.DecodeTimestamp()

Dashboard model
|-- SensorViewModel.RefreshAvailablePorts()
|-- SensorViewModel.AddSample()
|-- SensorViewModel.UpdateChartForSensor()
|-- SensorViewModel.ClearAllData()
`-- SensorViewModel.UpdateConnectionStatus()

Domain and display state
|-- SensorSample.TryParseCsv()
|-- SensorDisplayInfo.AddValue()
|-- ViewModelBase.SetProperty()
`-- ViewModelBase.OnPropertyChanged()
```

### 1.5 Runtime Data Flow

```text
Microcontroller
  -> USB serial / COM port
    -> System.IO.Ports.SerialPort
      -> SerialPortService.OnDataReceived()
        -> ConcurrentQueue<byte[]>
          -> SerialPortService.ProcessIncomingDataAsync()
            -> SerialPortService.ParseReceiveBuffer()
              -> frame validation and checksum validation
                -> SerialPortService.ParseFrame()
                  -> SensorSample
                    -> SensorViewModel.AddSample()
                      -> SensorDisplayInfo statistics
                      -> OxyPlot PlotModel update via WPF Dispatcher
                        -> MainWindow display
```

### 1.6 Static Dependencies

| Component | Depends on | Notes |
|---|---|---|
| `App` | `LoggingService`, Serilog | Initializes infrastructure. |
| `MainWindow` | `SensorViewModel`, `SerialPortService`, Serilog | Orchestrates runtime object creation. |
| `SensorViewModel` | `SensorSample`, `SerialPortHelper`, OxyPlot, WPF Dispatcher, Serilog | Presentation logic and dashboard state. |
| `SerialPortService` | `System.IO.Ports`, `SensorSample`, Serilog, `ConcurrentQueue` | Hardware interface and parser. |
| `SerialPortHelper` | `System.IO.Ports` | Port discovery. |
| `LoggingService` | Serilog | Logging setup. |
| Tests | xUnit, production components | Unit-level verification. |

## 2. Interfaces

### 2.1 User Interface Interfaces

| Interface | Type | Producer | Consumer | Data or command |
|---|---|---|---|---|
| WPF data binding | Property binding | `SensorViewModel` | `MainWindow.xaml` | Connection status, status message, sample count, sensor list, available ports, selected port, chart models. |
| WPF commands | `ICommand` | `SensorViewModel` | Buttons in `MainWindow.xaml` | Clear data, connect, disconnect. |
| View model events | .NET `Action` events | `SensorViewModel` | `MainWindow` | Connection request and disconnection request. |
| Property change notification | `INotifyPropertyChanged` | `ViewModelBase` derivatives | WPF binding engine | Property updates for UI refresh. |

### 2.2 Service Interfaces

| Interface | Type | Operations | Current implementation |
|---|---|---|---|
| `ISerialPortService` | C# interface | `Connect()`, `Disconnect()`, `SetPort()`, `Dispose()`, `IsConnected`, `ConnectionStatusChanged` | Implemented by `SerialPortService`. |
| Sample callback | `Action<SensorSample>` | Emits validated decoded samples to the application state layer. | Passed from `MainWindow.InitializeSerialService()` to `SerialPortService`. |
| Connection status event | .NET event | Notifies when serial connection state changes. | Raised by `SerialPortService.OnConnectionStatusChanged()`. |

### 2.3 Hardware and Operating System Interfaces

| Interface | Type | Configuration or behavior | Architectural controls |
|---|---|---|---|
| USB serial / COM port | Hardware abstraction through `System.IO.Ports.SerialPort` | 115200 baud default, 8 data bits, no parity, 1 stop bit, 500 ms read/write timeout. | Explicit port selection, connection status handling, exception logging, disconnect disposal. |
| COM port discovery | Windows serial-port enumeration | `SerialPort.GetPortNames()` | UI selection list is refreshed from available ports. |
| File system logging | `%AppData%/WaterFilterCBZ/logs` | Daily rolling logs at information level and above. | Centralized logging initialization and shutdown flush. |
| WPF Dispatcher | UI-thread marshaling | `Dispatcher.Invoke()` and `Dispatcher.BeginInvoke()` | Prevents direct background-thread mutation of UI-bound collections and plot updates. |

### 2.4 Serial Protocol Interface

The current binary serial protocol uses framed packets.

```text
START(0xAA) | COUNT | SENSOR_ENTRY_1 ... SENSOR_ENTRY_N | CHECKSUM | END(0x55)
```

| Field | Size | Rule |
|---|---:|---|
| `START` | 1 byte | Must be `0xAA`. Parser resynchronizes to this byte. |
| `COUNT` | 1 byte | Must be 1 through 4. |
| `SENSOR_ENTRY` | 10 bytes each | `SENSOR_ID(1)`, `TIMESTAMP_MS(uint32)`, `UNIT_ID(1)`, `VALUE(float32)`. |
| `CHECKSUM` | 1 byte | 8-bit sum from `START` through the final payload byte. |
| `END` | 1 byte | Must be `0x55`. |

Parser behavior:

- Invalid sensor counts are rejected.
- Invalid end bytes are rejected.
- Invalid checksums are rejected.
- Noise before a start byte is discarded.
- Parser resynchronizes to the next start byte after malformed frames.
- Partial frame assembly is reset after the configured timeout.
- Valid frames emit one `SensorSample` per sensor entry.

### 2.5 Internal Queues, Events, and Asynchronous Execution

| Mechanism | Location | Purpose | Safety consideration |
|---|---|---|---|
| `ConcurrentQueue<byte[]>` | `SerialPortService` | Transfers byte chunks from the serial event handler to the processing task. | Keeps serial event handling short and separates acquisition from parsing. |
| Background task | `SerialPortService.ProcessIncomingDataAsync()` | Processes queued byte chunks until cancellation. | Cancellation token supports controlled disconnect. |
| Parser receive buffer | `SerialPortService._receiveBuffer` | Maintains frame assembly state across chunks. | Bounded by parser resynchronization and timeout behavior, but no explicit maximum buffer length is currently enforced beyond malformed-frame handling. |
| WPF dispatcher | `SensorViewModel` | Moves UI-bound collection and chart updates onto the UI thread. | Prevents cross-thread UI access faults. |
| OxyPlot model invalidation | `SensorViewModel.UpdateChartForSensor()` | Refreshes chart visualization after point insertion. | Per-sensor throttling reduces UI overload. |

### 2.6 External Library Interfaces

| Library | Use | Architectural dependency |
|---|---|---|
| `System.IO.Ports` | Serial communication and COM port enumeration. | Hardware access abstraction. |
| `OxyPlot.Wpf` | Real-time line charts. | Visualization layer. |
| `Serilog` | Structured debug and file logging. | Audit and diagnostics layer. |
| `xUnit` | Unit testing. | Verification layer. |

### 2.7 Embedded-System Interface Considerations

WaterFilterCBZ runs on Windows and does not contain RTOS tasks, ISRs, shared memory, or embedded driver code. The embedded microcontroller is outside this repository but forms part of the complete system architecture through the serial protocol.

For the complete medical-device system architecture, the following firmware-side interfaces should be documented in the corresponding embedded design file:

- Sensor sampling tasks and scheduling period.
- Any ISR-to-task queues used to acquire raw sensor measurements.
- Firmware serial transmit task.
- Sensor driver interfaces.
- Communication bus interfaces between sensors and the microcontroller.
- Watchdog, brownout, and communication-fault handling in firmware.
- Firmware/software version compatibility for the serial frame protocol.

## 3. Segregation for Safety

### 3.1 Safety Context

The current software provides monitoring, visualization, and logging of sensor values. The final IEC 62304 software safety class must be assigned from the device risk management file based on the intended medical use and whether incorrect, delayed, or missing displayed values can contribute to hazardous situations.

If classified as Class C, the architecture must provide strong evidence that safety-critical behavior is isolated, validated, supervised, and verified. The table below separates implemented architectural controls from controls that should be completed or formally justified for a Class C submission.

### 3.2 Implemented Segregation and Risk-Reduction Controls

| Control | Architectural location | Hazard contribution reduced |
|---|---|---|
| UI, state, service, model, and utility separation | MVVM layering across `MainWindow`, `SensorViewModel`, `SerialPortService`, models, and utilities. | Reduces coupling between display logic and serial parsing. |
| Serial-service interface | `ISerialPortService` | Supports testability and future substitution of a validated acquisition component. |
| Dedicated serial acquisition service | `SerialPortService` | Contains hardware access, parser state, checksum validation, and resynchronization in one module. |
| Asynchronous byte queue | `ConcurrentQueue<byte[]>` in `SerialPortService` | Reduces blocking inside the serial data-received event handler. |
| Frame validation before sample dispatch | `ParseReceiveBuffer()`, `ValidateChecksum()`, `ParseFrame()` | Reduces risk of presenting corrupt serial data as valid samples. |
| Sensor count limit | `MAX_SENSORS = 4` | Prevents unexpected packet counts from expanding processing or display beyond the designed scope. |
| Parser resynchronization | `ResyncToNextStartByte()` | Allows recovery from malformed or noisy input. |
| Frame assembly timeout | `FrameAssemblyTimeout` | Prevents indefinite partial-frame accumulation. |
| UI thread marshaling | `Dispatcher.Invoke()` and `Dispatcher.BeginInvoke()` | Reduces concurrency faults in UI-bound data structures. |
| Chart point retention limit | Maximum 300 points per sensor series. | Reduces unbounded chart memory growth during long monitoring sessions. |
| Connection lifecycle cleanup | `Disconnect()`, `Dispose()`, `MainWindow.OnClosed()` | Reduces stale serial connection and resource leak risks. |
| Operational logging | `LoggingService` and Serilog calls throughout services and view model. | Supports diagnosis of connection, parsing, and processing failures. |

### 3.3 Required or Recommended Class C Hardening Items

The following items are not fully implemented in the current repository and should be handled as requirements, design changes, verification activities, or documented risk acceptances if Class C applies.

| Safety need | Recommended architectural control | Current status |
|---|---|---|
| Prevent undetected communication loss | Add heartbeat or maximum sample age supervision per sensor, with visible stale-data state and alarm/event logging. | Not currently implemented. |
| Avoid accepting physically implausible values | Add sensor-specific range validation, unit validation, and invalid-value rejection or quarantine. | Not currently implemented. |
| Prevent display of data from incompatible firmware | Add protocol version, device identity, and frame schema compatibility checks. | Not currently implemented. |
| Strengthen corruption detection | Replace 8-bit additive checksum with CRC if required by risk analysis. | Current checksum is simple additive validation. |
| Bound parser memory under sustained malicious or noisy input | Add explicit maximum receive-buffer length and log/drop behavior. | Partial mitigation exists through resynchronization and timeout. |
| Detect processing-task failure | Add task fault reporting, reconnect strategy, or supervisory watchdog at the application layer. | Exceptions are logged, but no supervisor restarts the task. |
| Isolate safety-critical decisions | Keep any therapy, dosing, shutoff, or alarm decision logic outside the display layer and in a separately verified component. | No therapy-control logic exists in this application. |
| Protect configuration | Add controlled configuration storage, validation, and audit trail for port, baud rate, thresholds, and sensor definitions. | Port selection and baud rate exist, but no controlled persistent configuration. |
| Support cybersecurity and misuse controls | Add device authentication or trusted-port/device identity checks if required by the threat model. | Not currently implemented. |
| Ensure verified failure presentation | Define and test UI states for disconnected, stale, invalid, out-of-range, parser error, and device mismatch conditions. | Some connection states exist; detailed failure-state taxonomy is incomplete. |

### 3.4 Architectural Safety Principles

The architecture should observe these principles as the software evolves:

- Safety-relevant data must be validated before entering dashboard state.
- UI rendering must not perform safety decisions.
- Hardware communication faults must be contained in the service layer and surfaced as explicit application states.
- Background processing failures must be logged and made visible to the operator when they affect monitoring.
- Sensor identity, value, unit, timestamp, and freshness must be treated as separate validity concerns.
- Any future risk-control algorithms must be implemented in dedicated, testable components with clear interfaces and independent verification.

### 3.5 Failure Handling

| Failure condition | Current behavior | IEC 62304 concern |
|---|---|---|
| COM port cannot be opened | Error is logged; connection status event is raised; application remains disconnected. | Verify UI message and user recovery path. |
| Serial read error | Warning is logged; processing continues. | Define acceptable recovery behavior and alarm visibility. |
| Serial port error event | Error is logged. | Determine whether operator notification is required. |
| Invalid sensor count | Frame is rejected and parser resynchronizes. | Covered by architectural input validation; add direct parser tests. |
| Invalid end byte | Frame is rejected and parser resynchronizes. | Covered by architectural input validation; add direct parser tests. |
| Invalid checksum | Frame is rejected and parser resynchronizes. | Covered by architectural input validation; add direct parser tests. |
| Partial frame timeout | Receive buffer is cleared and warning is logged. | Add test coverage and define operator visibility if frequent. |
| More than four sensor identities displayed | Additional chart assignment is ignored after four plots. | Define intended behavior for extra sensors and operator notification. |
| UI chart update overload | Plot updates are throttled per sensor. | Verify performance at maximum supported sample rate. |

## 4. Traceability

### 4.1 Traceability Model

The recommended traceability chain is:

```text
Hazard
  -> Risk Control
    -> Software Requirement
      -> Architectural Element
        -> Unit
          -> Verification
```

Traceability identifiers in this draft are proposed identifiers. They should be synchronized with the project requirements specification, risk management file, detailed design, and verification protocol.

### 4.2 Architectural Element Identifiers

| ID | Architectural element | Implementation |
|---|---|---|
| AE-APP-001 | Application lifecycle and logging startup/shutdown | `App`, `LoggingService` |
| AE-UI-001 | Main WPF dashboard view | `MainWindow.xaml`, `MainWindow.xaml.cs` |
| AE-VM-001 | Dashboard view model and command layer | `SensorViewModel`, `RelayCommand`, `ViewModelBase` |
| AE-ACQ-001 | Serial acquisition service | `SerialPortService`, `ISerialPortService` |
| AE-PROTO-001 | Serial frame parser and validator | `ParseReceiveBuffer()`, `ValidateChecksum()`, `ParseFrame()` |
| AE-MODEL-001 | Sensor sample and display data models | `SensorSample`, `SensorDisplayInfo` |
| AE-UTIL-001 | Serial port discovery | `SerialPortHelper` |
| AE-LOG-001 | Diagnostic logging | `LoggingService`, Serilog usage |
| AE-TEST-001 | Automated unit tests | `WaterFilterCBZ.Tests` |

### 4.3 Requirement-to-Architecture Traceability

| Software requirement ID | Requirement summary | Architectural element | Unit or component | Verification |
|---|---|---|---|---|
| SRS-001 | The software shall enumerate available Windows COM ports. | AE-UTIL-001, AE-VM-001 | `SerialPortHelper.GetAvailablePorts()`, `SensorViewModel.RefreshAvailablePorts()` | Unit/integration test to be added; manual UI verification. |
| SRS-002 | The software shall allow the user to select and connect to a COM port. | AE-UI-001, AE-VM-001, AE-ACQ-001 | `SelectedPort`, `ConnectCommand`, `MainWindow.InitializeSerialService()`, `SerialPortService.Connect()` | Existing serial failure test; UI integration test to be added. |
| SRS-003 | The software shall receive sensor data using configured serial settings. | AE-ACQ-001 | `SerialPortService.Connect()`, `OnDataReceived()` | Existing baud-rate tests; hardware integration test to be added. |
| SRS-004 | The software shall accept only well-formed binary frames. | AE-PROTO-001 | `ParseReceiveBuffer()`, `ValidateChecksum()`, `ParseFrame()` | Parser validation tests to be expanded. |
| SRS-005 | The software shall reject frames with invalid sensor count, checksum, or end byte. | AE-PROTO-001 | `ParseReceiveBuffer()`, `ValidateChecksum()` | Parser rejection tests to be added. |
| SRS-006 | The software shall decode each valid sensor entry into a sensor sample. | AE-PROTO-001, AE-MODEL-001 | `ParseFrame()`, `DecodeTimestamp()`, `SensorSample` | Existing timestamp decode test. |
| SRS-007 | The software shall display current sensor value and summary statistics. | AE-VM-001, AE-MODEL-001, AE-UI-001 | `SensorViewModel.AddSample()`, `SensorDisplayInfo.AddValue()` | Existing `SensorDisplayInfo` tests; UI test to be added. |
| SRS-008 | The software shall plot sensor values over time for up to four sensors. | AE-VM-001, AE-UI-001 | `UpdateChartForSensor()`, OxyPlot `PlotModel` bindings | Unit/integration test to be added; manual chart verification. |
| SRS-009 | The software shall allow the user to clear displayed sensor data. | AE-VM-001, AE-UI-001 | `ClearDataCommand`, `ClearAllData()` | Unit test to be added. |
| SRS-010 | The software shall log startup, shutdown, connection, parsing, and processing events. | AE-APP-001, AE-LOG-001, AE-ACQ-001, AE-VM-001 | `LoggingService`, Serilog calls | Log file verification test/procedure to be added. |
| SRS-011 | The software shall disconnect and release serial resources on user request or application close. | AE-UI-001, AE-ACQ-001 | `OnDisconnectRequested()`, `OnClosed()`, `Disconnect()`, `Dispose()` | Existing dispose/disconnect tests; UI lifecycle test to be added. |

### 4.4 Risk-Control Traceability

| Hazard ID | Hazardous situation | Risk control ID | Risk control summary | Requirement | Architectural element | Verification |
|---|---|---|---|---|---|---|
| HAZ-001 | Incorrect sensor value is displayed due to corrupt serial data. | RC-001 | Validate frame structure and checksum before accepting data. | SRS-004, SRS-005 | AE-PROTO-001 | Parser validation/rejection tests. |
| HAZ-002 | Operator relies on stale data after communication stops. | RC-002 | Detect missing samples or stale sensor timestamps and display stale state. | SRS-C-001 proposed | AE-VM-001, AE-ACQ-001 | Not implemented; stale-data tests required. |
| HAZ-003 | Data from wrong or incompatible device is displayed. | RC-003 | Verify device identity and protocol version before accepting samples. | SRS-C-002 proposed | AE-PROTO-001, AE-ACQ-001 | Not implemented; compatibility tests required. |
| HAZ-004 | Application hangs or becomes unresponsive under high-rate input. | RC-004 | Use asynchronous acquisition and throttle chart updates. | SRS-003, SRS-008 | AE-ACQ-001, AE-VM-001 | Performance/stress test required. |
| HAZ-005 | Malformed serial stream causes memory growth or parser lockup. | RC-005 | Resynchronize parser and time out partial frames; add explicit buffer cap. | SRS-004, SRS-C-003 proposed | AE-PROTO-001 | Partial coverage; malformed-stream stress tests required. |
| HAZ-006 | User connects to unavailable or incorrect COM port. | RC-006 | Enumerate available ports and show connection status. | SRS-001, SRS-002 | AE-UTIL-001, AE-VM-001, AE-UI-001 | Manual and automated UI tests required. |
| HAZ-007 | Failure cannot be reconstructed after incident. | RC-007 | Record relevant operational events in rolling logs. | SRS-010 | AE-LOG-001 | Log verification procedure required. |

### 4.5 Detailed Design Traceability

| Architectural element | Detailed design unit | Existing test coverage | Additional verification recommended |
|---|---|---|---|
| AE-ACQ-001 | `SerialPortService.Connect()` | Port-open failure behavior, default/configurable baud rate. | Successful hardware connection test; serial error event handling. |
| AE-ACQ-001 | `SerialPortService.Disconnect()` and `Dispose()` | Disconnected no-op and repeated dispose behavior. | Disconnect while processing queued data. |
| AE-PROTO-001 | `SerialPortService.ParseFrame()` | Valid multi-sensor frame timestamp decoding. | Invalid length, invalid count, invalid value encoding, unit handling. |
| AE-PROTO-001 | `SerialPortService.ParseReceiveBuffer()` | Not directly covered. | Noise before start byte, invalid checksum, invalid end byte, timeout, resync. |
| AE-VM-001 | `SensorViewModel.AddSample()` | Indirectly through display info tests only. | New sensor registration, sample count, dispatcher behavior, chart assignment. |
| AE-MODEL-001 | `SensorDisplayInfo.AddValue()` | Current, min, max, average, count, property change. | Numeric edge cases if required by risk analysis. |
| AE-MODEL-001 | `SensorSample.TryParseCsv()` | Valid/invalid CSV and invariant decimal parsing. | Confirm whether legacy CSV parser remains in intended design. |
| AE-VM-001 | `RelayCommand` | Command behavior tests exist. | Command enablement under connection state transitions. |
| AE-LOG-001 | `LoggingService.ConfigureLogging()` | Not directly covered. | Verify log path creation and minimum event content. |

### 4.6 Verification Traceability Summary

| Verification artifact | Scope |
|---|---|
| `SerialPortServiceTests` | Serial service construction, connection failure, baud rate configuration, port setting, disconnect/dispose behavior, timestamp decoding from valid frames. |
| `SensorDisplayInfoTests` | Sensor statistics and property-change notification. |
| `SensorSampleTests` | Legacy CSV parser behavior. |
| `RelayCommandTests` | Command execution and command enablement behavior. |
| Proposed integration tests | COM-port lifecycle using test double or loopback, parser malformed-frame behavior, stale-data state, chart update behavior, logging behavior. |
| Proposed system tests | Hardware-in-the-loop verification with representative firmware, sustained serial input, disconnect/reconnect, malformed-stream injection, operator-visible failure states. |

## 5. Open Architectural Items

| ID | Item | Rationale |
|---|---|---|
| OAI-001 | Confirm IEC 62304 software safety class. | Determines required rigor, independence, and architectural risk controls. |
| OAI-002 | Define intended clinical or operational use of displayed values. | Determines whether stale or incorrect display can contribute to harm. |
| OAI-003 | Define sensor ranges, units, and plausibility rules. | Needed for safety validation beyond frame integrity. |
| OAI-004 | Define maximum expected sample rate and communication-loss timeout. | Needed for performance and stale-data requirements. |
| OAI-005 | Decide whether the legacy CSV parser remains in the product baseline. | Unused code should be justified, removed, or verified. |
| OAI-006 | Add formal protocol versioning and device identity if required by risk management. | Prevents incompatible firmware/software combinations. |
| OAI-007 | Expand parser tests to cover rejection and resynchronization paths. | Current tests cover selected valid-frame behavior but not all risk controls. |

## 6. Revision History

| Revision | Date | Author | Description |
|---|---|---|---|
| 0.1 | 2026-05-12 | Codex | Initial draft based on current WaterFilterCBZ repository architecture. |
