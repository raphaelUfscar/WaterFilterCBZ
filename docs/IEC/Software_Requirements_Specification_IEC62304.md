# Software Requirements Specification

Project: WaterFilterCBZ
Standard context: IEC 62304:2006+AMD1:2015, Clause 5.2 (Software requirements analysis)
Software safety class: C (see [Software Safety Classification](Software_Safety_Classification_IEC62304.md))
Document status: Draft
Last updated: 2026-06-05

## 1. Purpose and Scope

This document specifies the software requirements for WaterFilterCBZ, a Windows WPF desktop application that monitors up to four sensor channels received from a microcontroller over a USB serial (COM) interface. Requirements are derived from the system intended use, the architectural design, and the risk controls in the [Software Risk Management File](Software_Risk_Management_IEC62304.md).

IEC 62304 Clause 5.2.2 requires that requirements include, as appropriate: functional and capability requirements; software system inputs/outputs; interfaces; alarms/warnings; security; usability; data definition; operation/maintenance; regulatory; and risk control requirements implemented in software. Clause 5.2.6 requires that risk control measures implemented in software be included as requirements.

### 1.1 Requirement identification

- `SRS-0xx` — baseline functional/interface requirements implemented in the current code.
- `SRS-Cxx` — Class C safety/risk-control requirements derived from the Risk Management File. Some are **not yet implemented** and are marked accordingly; they trace to risk controls RC-xxx.
- Verification method: `T` (test), `A` (analysis), `I` (inspection/review), `D` (demonstration).

### 1.2 Definitions

| Term | Definition |
|---|---|
| Frame | A binary serial packet, fields in order: `START(0xAA)`, `COUNT`, `ENTRY[1..N]`, `CHECKSUM`, `END(0x55)`. |
| Sensor entry | 10 bytes: `SENSOR_ID(1)`, `TIMESTAMP_MS(uint32 LE)`, `UNIT_ID(1)`, `VALUE(float32 LE)`. |
| Sample | One decoded sensor reading (`SensorSample`: id, timestamp, value). |
| Stale data | A displayed value whose most recent update exceeds an allowed maximum age. |
| SOUP | Software of Unknown Provenance (third-party components). |

## 2. Baseline Functional Requirements (implemented)

| ID | Requirement | Verification | Architectural element | Status |
|---|---|:--:|---|---|
| SRS-001 | The software shall enumerate available Windows COM ports and present them for selection. | T, D | AE-UTIL-001, AE-VM-001 | Implemented |
| SRS-002 | The software shall allow the operator to select a COM port and initiate a connection to it. | T, D | AE-UI-001, AE-VM-001, AE-ACQ-001 | Implemented |
| SRS-003 | The software shall configure the serial port to 115200 baud, 8 data bits, no parity, 1 stop bit, with 500 ms read/write timeouts, and receive data using those settings. | T | AE-ACQ-001 | Implemented |
| SRS-004 | The software shall accept only well-formed binary frames as defined in §1.2 (valid start byte, sensor count 1–4, correct length, valid end byte, valid checksum). | T | AE-PROTO-001 | Implemented |
| SRS-005 | The software shall reject any frame with an invalid sensor count, checksum, or end byte, and shall resynchronize to the next start byte without crashing. | T | AE-PROTO-001 | Implemented |
| SRS-006 | The software shall decode each sensor entry of a valid frame into a sample containing sensor identity, timestamp, and value. | T | AE-PROTO-001, AE-MODEL-001 | Implemented |
| SRS-007 | The software shall display, per active sensor, the current value and summary statistics (minimum, maximum, average, reading count, last update time). | T, D | AE-VM-001, AE-MODEL-001, AE-UI-001 | Implemented |
| SRS-008 | The software shall plot sensor values over time for up to four sensors, retaining at most 300 points per series, with per-sensor update throttling. | D, A | AE-VM-001, AE-UI-001 | Implemented |
| SRS-009 | The software shall allow the operator to clear all displayed sensor data and statistics. | T, D | AE-VM-001, AE-UI-001 | Implemented |
| SRS-010 | The software shall log startup, shutdown, connection state changes, parsing/validation warnings, and processing errors to a daily rolling file at Information level and above. | T, I | AE-APP-001, AE-LOG-001 | Implemented |
| SRS-011 | The software shall disconnect and release serial resources on operator request and on application close. | T, D | AE-UI-001, AE-ACQ-001 | Implemented |
| SRS-012 | The software shall reflect connection state in the UI and prevent invalid connect/disconnect actions through command enablement. | T, D | AE-VM-001, AE-UI-001 | Implemented |
| SRS-013 | The software shall provide stable, locale-independent automation identifiers on status texts, the port selector, and command buttons. | I, T | AE-UI-001 | Implemented |
| SRS-014 | The software shall provide an operator action to open the log folder location. | T, D | AE-VM-001 | Implemented |
| SRS-015 | The software shall anchor the first received microcontroller timestamp to the PC wall clock and offset subsequent sample timestamps from that anchor. | T | AE-PROTO-001 | Implemented |
| SRS-016 | The software shall discard partial/incomplete frames after a frame-assembly timeout (350 ms) and reset parser state, logging the event. | T | AE-PROTO-001 | Implemented |
| SRS-017 | The software shall process incoming serial bytes asynchronously so that the serial receive event handler does not block on parsing or UI work. | A, T | AE-ACQ-001 | Implemented |

## 3. Class C Safety / Risk-Control Requirements

These requirements derive from risk controls in the [Risk Management File](Software_Risk_Management_IEC62304.md). Per Clause 5.2.6 they are part of the software requirements. Items marked **Not implemented** are open development work needed for a Class C release.

| ID | Requirement | Traces to | Verification | Status |
|---|---|---|:--:|---|
| SRS-C-001 | The software shall detect loss of communication or absence of new samples and, when a sensor's most recent update exceeds the configured maximum age (**5 s**, OAI-004), present an explicit **stale** state for that sensor and log the transition. | RC-002 / HAZ-002 | T, D | **Implemented (2026-06-05)** — `SensorDisplayInfo.IsStale`/`EvaluateStaleness`, 1 s `DispatcherTimer`, UI flag |
| SRS-C-002 | The software shall verify device identity and serial protocol version/schema compatibility before accepting samples, and shall reject and report data from an incompatible source. | RC-003 / HAZ-003 | T | **Not implemented** |
| SRS-C-003 | The software shall validate each decoded value against the per-parameter physical and operating ranges (fixed SENSOR_ID mapping), rejecting physically-implausible values (keeping the last good value, not charting them) and visibly flagging out-of-spec-but-plausible values, logging each transition. A cumulative per-sensor rejected-sample count shall be maintained, displayed, and included in the transition log. | RC-008 / HAZ-001 | T | **Implemented (2026-06-05)** — `SensorParameter`/`SensorParameterRegistry`, `SensorDisplayInfo.AddValue` two-tier validation + `RejectedCount`. Ranges are **user-configurable (2026-06-30)** via `sensor-ranges.json` (`SensorRangeConfigLoader` → `SensorParameterRegistry.Configure`): per-field fallback to built-in defaults, inconsistent/non-finite overrides rejected and logged. Config-file integrity is RC-011 (pending); numeric defaults still to be confirmed against the device spec. |
| SRS-C-004 | The software shall enforce an explicit maximum receive-buffer length; on exceeding it the software shall drop/reset the buffer and log the event, bounding memory under sustained noisy or malicious input. | RC-005 / HAZ-005 | T | **Implemented (2026-06-30)** — `MAX_RECEIVE_BUFFER_BYTES` (4096) cap in `SerialPortService.ParseReceiveBuffer`: drop + reset + log on overflow |
| SRS-C-005 | The software shall detect failure or termination of the background acquisition/processing task and surface a visible degraded/error state to the operator (and attempt a defined recovery or require explicit reconnect). | RC-009 / HAZ-004 | T | **Implemented (2026-06-30)** — `SerialPortService.ProcessingFaulted` event on abnormal task exit → `SensorViewModel.NotifyProcessingFault` sets `HasProcessingFault`, forces disconnected/"Processing fault" state; recovery requires explicit reconnect (clears fault) |
| SRS-C-006 | The software shall present a defined, verified UI state for each of the following conditions: disconnected, connecting, connected-healthy, stale, invalid-value, parser-error, device-mismatch, and processing-fault. | RC-010 / HAZ-001..005 | T, D | **Implemented (2026-06-30)** — `MonitoringState` enum + pure `MonitoringStateResolver` (deterministic precedence), surfaced as a colour-coded status chip (`MonitoringStateToBrushConverter`); all 8 states defined, derived, and unit-verified. _Detectors for `parser-error` and `device-mismatch` land with the parser-error supervisor and RC-003 respectively; the states/hooks (`NotifyParserError`/`NotifyDeviceMismatch`) exist now._ |
| SRS-C-007 | The software shall protect operational configuration (port, baud rate, per-sensor ranges/units, freshness timeout) with validation on load and an audit record of changes. | RC-011 | T, I | **Not implemented** |
| SRS-C-008 | Where required by risk analysis, the software shall use an error-detecting code stronger than an 8-bit additive checksum (e.g. CRC) for frame integrity. | RC-001 / HAZ-001 | A, T | **Decision pending** (current: 8-bit additive checksum) |
| SRS-C-009 | The software shall not perform any therapy, dosing, actuation, or safety-shutoff decision in the presentation layer; any such decision logic, if introduced, shall reside in a separately specified and independently verified software item. | RC-007 | A, I | Held as constraint (no such logic exists) |

### 3.1 Parameter definitions and ranges (SRS-C-003 / OAI-003)

Fixed SENSOR_ID convention and default ranges (pharmaceutical / medical purified water). Values are **documented defaults to be confirmed against the device specification**; they are implemented in `SensorParameterRegistry`.

| SENSOR_ID | Parameter | Unit | Operating spec (flag if outside) | Physical range (reject if outside) |
|---|---|---|---|---|
| 0x01 | Conductivity | µS/cm | 0.0 – 1.3 | 0 – 200 |
| 0x02 | Temperature | °C | 15 – 30 | -10 – 130 |
| 0x03 | pH | pH | 5.0 – 7.0 | 0 – 14 |
| 0x04 | Pressure | bar | 1 – 6 | 0 – 16 |

A value that is NaN/infinite or outside the physical range is rejected (last good value retained); a value within the physical range but outside the operating spec is displayed and flagged. An unknown SENSOR_ID is displayed without range validation.

## 4. Interface Requirements

### 4.1 Hardware / OS

| ID | Requirement |
|---|---|
| SRS-IF-001 | The software shall interface with serial hardware through `System.IO.Ports.SerialPort` and shall not assume a specific COM port number. |
| SRS-IF-002 | The software shall marshal all UI-bound state and chart updates onto the WPF UI thread. |
| SRS-IF-003 | The software shall write logs under `%AppData%\WaterFilterCBZ\logs\`. |

### 4.2 Serial protocol (input data definition)

The frame and sensor-entry layout in §1.2 is the normative input data definition. The parser shall treat sensor identity, value, unit, timestamp, and freshness as **separate validity concerns** (architectural principle; see SRS-C-001..C-003).

## 5. Non-Functional Requirements

| ID | Requirement | Verification |
|---|---|:--:|
| SRS-NF-001 | The software shall remain responsive and shall not lose connection-state integrity at the maximum supported sustained sample rate (to be quantified in OAI-004). | T |
| SRS-NF-002 | The software shall recover parser synchronization within one valid frame after a burst of malformed/noise input. | T |
| SRS-NF-003 | The software shall run on Windows 10/11 x64 with the .NET 10 desktop runtime. | I |
| SRS-NF-004 | Chart memory shall be bounded during long monitoring sessions (≤300 points/series). | A |
| SRS-NF-005 | Log files shall roll daily to bound disk usage. | I |

## 6. Constraints and Assumptions

- Intended use is **pharmaceutical / medical purified-water quality monitoring**; the four channels are conductivity, temperature, pH, and pressure/flow (OAI-002 resolved 2026-06-05).
- The microcontroller firmware is outside this repository; the serial frame protocol is the system boundary.
- The communication-loss timeout is fixed at **5 s** (OAI-004 resolved). Per-parameter ranges/units are implemented as defaults (§3.1, OAI-003 resolved) **pending confirmation against the device specification**. Maximum sample rate remains an open input (OAI-004 rate) for SRS-NF-001.
- Classification is Class C (§ Software Safety Classification); all requirements are subject to Class C verification rigor.

## 7. Requirements Traceability

Forward and backward traceability (Hazard → Risk Control → Requirement → Architecture → Unit → Verification) is maintained in [Software Architecture Design §4](Software_Architecture_Design_IEC62304.md) and the [Risk Management File](Software_Risk_Management_IEC62304.md). Each `SRS-Cxx` requirement traces to a risk control; each requirement is verified per the [Verification & Test Plan](Software_Verification_and_Test_Plan_IEC62304.md).

## 8. Open Requirement Inputs

| ID | Needed to finalize |
|---|---|
| OAI-002 | Resolved: pharma/medical purified-water monitoring. Device-level severities still to confirm. |
| OAI-003 | Resolved: mapping + default ranges implemented (§3.1). Numeric defaults to be confirmed against the device specification before release. |
| OAI-004 | Resolved: communication-loss timeout = 5 s (SRS-C-001). Max sample rate still open → SRS-NF-001. |
| OAI-006 | Protocol versioning / device identity scheme → SRS-C-002. |

## 9. Revision History

| Revision | Date | Author | Description |
|---|---|---|---|
| 0.1 | 2026-06-05 | Claude | Initial SRS: baseline implemented requirements (SRS-001..017) extracted from current code; Class C risk-control requirements (SRS-C-001..009) derived from the Risk Management File; interface, non-functional, and open-input requirements recorded. |
| 0.2 | 2026-06-05 | Claude | Recorded intended use (pharma/medical purified water) and fixed 5 s comms-loss timeout (OAI-002/004); SRS-C-001 marked implemented and verified; constraints and open-input tables updated; fixed frame-definition table cell that contained literal pipe characters. |
| 0.3 | 2026-06-05 | Claude | SRS-C-003 marked implemented (two-tier validation); added §3.1 parameter definitions/ranges table and SENSOR_ID mapping (OAI-003 resolved, defaults pending confirmation); constraints and open-input tables updated. |
