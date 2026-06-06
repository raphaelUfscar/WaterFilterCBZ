# WaterFilterCBZ — Project Roadmap

This document tracks planned **features / code improvements** and the **testing strategy** for WaterFilterCBZ. It is the single home for general (non-regulatory) backlog and test status.

IEC 62304 Class C **compliance** work (safety class, risk controls, regulatory gating) is tracked separately in the [Class C compliance roadmap](iec62304_class_c_roadmap.md); safety-related items below cross-link to it.

**Status legend:** ✅ Done · ⚠️ Partial · ❌ Not started _(last verified against code: 2026-06-05)_

---

# Features and improvements

## 1. COM Port Selection UI

Enhance the user interface to provide more flexible serial port management.

- **Auto-discover available ports at startup:** Implement logic to automatically detect and list all available COM ports when the application launches.
  - ✅ **Done.** `SensorViewModel` calls `RefreshAvailablePorts()` in its constructor.
- **Dropdown selector in status bar:** Integrate a dropdown menu, possibly in the status bar or a dedicated settings area, allowing users to select the desired COM port.
  - ✅ **Done.** A port `ComboBox` lives in the bottom control bar (`MainWindow.xaml`); it is disabled while connected via `InvertBoolConverter`.
- **Reconnect button:** Add a button to manually initiate a connection attempt to the currently selected COM port.
  - ⚠️ **Partial.** Separate Connect and Disconnect buttons exist, but there is no single "Reconnect" action, and no manual "refresh ports" button.

## 2. Data Export

Provide functionality for users to export collected sensor data.

- **CSV export of current view:** Allow exporting the currently displayed sensor data (e.g., the data points in the charts) to a CSV file.
  - ❌ **Not started.**
- **JSON-L streaming export:** Implement a feature for streaming sensor data in JSON-L (JSON Lines) format, potentially for integration with other data analysis tools.
  - ❌ **Not started.**

## 3. Sensor Calibration

Introduce calibration capabilities for individual sensors.

- **Per-sensor offset/scale factors:** Develop a mechanism to apply offset and scaling factors to sensor readings, allowing for calibration.
  - ❌ **Not started.**
- **Persistent configuration files:** Store calibration settings persistently, perhaps in a configuration file, so they are retained across application sessions.
  - ❌ **Not started.** Overlaps with RC-011 / SRS-C-007 (controlled, validated, audited configuration of port, baud, per-sensor ranges, and timeout) in the [Class C compliance roadmap](iec62304_class_c_roadmap.md).

## 4. Advanced Charting

Expand the charting capabilities of the dashboard.

- **Multiple Y-axes for sensors with different ranges:** Support displaying sensors with vastly different value ranges on the same chart using multiple Y-axes.
  - ❌ **Not started.** Each chart currently uses a single `LinearAxis` and one `LineSeries`.
- **Annotation markers (thresholds, alarms):** Add the ability to place visual markers on charts to indicate thresholds, alarms, or specific events.
  - ❌ **Not started.**
- **Moving average overlay:** Implement an option to overlay a moving average line on sensor data series for trend analysis.
  - ❌ **Not started.**

## 5. Alarms & Thresholds

Integrate an alerting system based on sensor readings.

- **Per-sensor min/max alerts:** Allow users to define minimum and maximum thresholds for each sensor, triggering alerts when values fall outside these ranges.
  - ⚠️ **Partial (2026-06-05).** Per-parameter operating/physical ranges exist (`SensorParameter`/`SensorParameterRegistry`, RC-008 / SRS-C-003): values outside the operating spec are flagged, and implausible values are rejected. These ranges are currently **fixed defaults in code**, not user-defined; making them configurable is tracked as RC-011 / SRS-C-007 (see Class C roadmap).
- **Visual/audio notifications:** Provide visual cues (e.g., changing UI element colors) and/or audio notifications when an alert is triggered.
  - ⚠️ **Partial (2026-06-05).** Visual cues implemented: value colour + `OUT OF SPEC` / `INVALID` / `STALE` badges in the Active Sensors panel. **Audio** notifications are not implemented (the chosen alarm policy is visual + log only).
- **Alert log history:** Maintain a log of all triggered alerts with timestamps and relevant sensor data.
  - ⚠️ **Partial (2026-06-05).** Validation/staleness **state transitions** are logged to the rolling file (out-of-spec, invalid + cumulative rejected count, stale/recovery). A dedicated, queryable alert history view is not implemented.

---

# Testing

This section owns the test strategy and status. For the Class C verification *gating* view (which risk controls still need tests before they can be marked Verified) see the [Class C compliance roadmap](iec62304_class_c_roadmap.md).

## Unit testing

Focus on testing individual components in isolation.

- **`SerialPortService` mock tests:** Develop unit tests for the `SerialPortService` by mocking the `SerialPort` class or its dependencies to ensure correct handling of serial data, framing, CRC validation, and connection lifecycle.
  - ⚠️ **Partial.** `SerialPortServiceTests` covers connection lifecycle (connect failure, disconnect, dispose, baud rate, `SetPort`) and frame parsing with MCU-timestamp anchoring. `SerialPort` is not mocked; **checksum validation** and **parser resynchronization** have no direct tests yet.
- **`SensorSample` CSV parser unit tests:** Verify parsing logic correctly handles valid and invalid sensor data formats.
  - ❌ **Obsolete / removed (2026-06-03).** The CSV parser was legacy dead code (the live pipeline uses binary frames) and has been removed along with its tests. Binary frame parsing is covered under the `SerialPortService` item above.
- **ViewModel MVVM binding tests:** Implement unit tests for `SensorViewModel` to ensure data binding, command execution, property changes, and data processing logic function as expected, independent of the UI.
  - ⚠️ **Partial (expanded 2026-06-05).** Now covered: `SensorDisplayInfoTests` (statistics, staleness, two-tier validation, rejected counter, Min/Max-with-no-readings), `SensorViewModelValidationTests` (`AddSample` null/counting/transitions/unknown sensor, `EvaluateSensorStaleness`, `ClearAllData`), and `SensorViewModelOpenLogsTests`. Still missing: `RefreshAvailablePorts`, and `ConnectCommand`/`DisconnectCommand` `CanExecute` logic. (UI-collection updates remain Dispatcher-gated, so `Sensors`-population assertions need the UI tests.)

## Integration testing

Verify the interaction between components — `SerialPortService` ↔ `SensorViewModel`, or `SensorViewModel` updating `OxyPlot` models.

- ⚠️ **Partial (local/dedicated agent).** `WaterFilterCBZ.UITests` drives the full connection workflow end-to-end through the real WPF UI (FlaUI + UIA3): select port → connect → live data from the Python simulator over a virtual COM pair → clear → disconnect. It is excluded from the fast CI run and self-skips when the COM pair / Python are absent (see `WaterFilterCBZ.UITests/README.md`). A pure headless `SerialPortService` ↔ `SensorViewModel` integration test (no UI, CI-friendly) is still not present.

## System-level testing

Test the complete application as a whole, often involving actual hardware.

- **End-to-end communication with a microcontroller:** Conduct tests with a physical microcontroller sending data to the application to validate the entire data pipeline from serial reception to UI display.
  - ⚠️ **Partially automated.** `WaterFilterCBZ.UITests` exercises the full pipeline (simulator → serial → parse → ViewModel → UI) end-to-end over a virtual COM pair without hardware — both the operator workflow (`ConnectionWorkflowTests`) and the safety alarms (`AlarmWorkflowTests`: out-of-spec, invalid/rejected, stale, frame-corruption resilience). Validation against a _physical_ microcontroller is still manual.
- **UI responsiveness and data accuracy:** Verify that the UI updates correctly and responsively with real-time data, and that displayed values and charts accurately reflect the incoming sensor data.
  - ❌ **Not started.** Verified manually only.
- **Error handling and logging:** Test various error conditions (e.g., port disconnection, malformed data) to ensure the application handles them gracefully and logs appropriate messages.
  - ✅ **Automated.** The simulator's `--inject-errors` modes (checksum, end-byte, count, partial, noise) drive malformed frames, and the simulator's `--scenario` modes drive value-level alarms (out-of-spec, invalid, stale). Outcomes are asserted both at unit level (`SerialPortServiceFramingTests`: resync + rejection paths) and end-to-end (`AlarmWorkflowTests`: alarm indicators + audit-log entries; corruption resilience).
