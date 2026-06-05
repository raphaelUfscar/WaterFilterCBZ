# WaterFilterCBZ - Features and Improvements Roadmap

This document outlines potential future features and code-level improvements for the WaterFilterCBZ application, extracted from the main `README.md`.

**Status legend:** ✅ Done · ⚠️ Partial · ❌ Not started _(last verified against code: 2026-06-05)_

> Safety-related items below (alarms/thresholds, persistent configuration) intersect the IEC 62304 risk controls tracked in the [Class C compliance roadmap](iec62304_class_c_roadmap.md).

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
