# WaterFilterCBZ - Testing Roadmap

This document outlines the planned testing strategy for the WaterFilterCBZ application, focusing on unit, integration, and system-level tests, as identified in the main `README.md`.

**Status legend:** ✅ Done · ⚠️ Partial · ❌ Not started _(last verified against code: 2026-06-03)_

## 1. Unit Testing

Focus on testing individual components in isolation.

- **`SerialPortService` mock tests:** Develop unit tests for the `SerialPortService` by mocking the `SerialPort` class or its dependencies to ensure correct handling of serial data, framing, CRC validation, and connection lifecycle.
  - ⚠️ **Partial.** `SerialPortServiceTests` covers connection lifecycle (connect failure, disconnect, dispose, baud rate, `SetPort`) and frame parsing with MCU-timestamp anchoring. `SerialPort` is not mocked; **checksum validation** and **parser resynchronization** have no direct tests yet.
- **`SensorSample` CSV parser unit tests:** Create tests to verify the `SensorSample.TryParseCsv()` method (or equivalent binary parsing logic) correctly parses various valid and invalid sensor data formats.
  - ❌ **Obsolete / removed (2026-06-03).** The CSV parser was legacy dead code (the live pipeline uses binary frames) and has been removed along with its tests. Binary frame parsing is covered under the `SerialPortService` item above.
- **ViewModel MVVM binding tests:** Implement unit tests for `SensorViewModel` to ensure data binding, command execution, property changes, and data processing logic function as expected, independent of the UI.
  - ⚠️ **Partial.** `SensorDisplayInfoTests` (statistics) and `SensorViewModelOpenLogsTests` (OpenLogs command) exist. Still missing: `AddSample`, `ClearAllData`, `RefreshAvailablePorts`, and `ConnectCommand`/`DisconnectCommand` `CanExecute` logic.

## 2. Integration Testing

Verify the interaction between different components of the application. This would involve testing the `SerialPortService` interacting with the `SensorViewModel`, or the `SensorViewModel` updating `OxyPlot` models.

- ❌ **Not started (automated).** No automated integration tests exist. The Python serial simulator (`tools/sensor_simulator.py`) enables _manual_ end-to-end exercising of this path.

## 3. System-Level Testing

Test the complete application as a whole, often involving actual hardware.

- **End-to-end communication with a microcontroller:** Conduct tests with a physical microcontroller sending data to the application to validate the entire data pipeline from serial reception to UI display.
  - ⚠️ **Partially enabled.** The Python simulator emits the real binary frame format over a virtual COM pair, so the full pipeline can be exercised without hardware — but this is manual, not automated.
- **UI responsiveness and data accuracy:** Verify that the UI updates correctly and responsively with real-time data, and that displayed values and charts accurately reflect the incoming sensor data.
  - ❌ **Not started.** Verified manually only.
- **Error handling and logging:** Test various error conditions (e.g., port disconnection, malformed data) to ensure the application handles them gracefully and logs appropriate messages.
  - ⚠️ **Partially enabled.** The simulator's `--inject-errors` modes (checksum, end-byte, count, partial, noise) can trigger parser resync and warning logs on demand, but there is no automated assertion on the outcomes.
