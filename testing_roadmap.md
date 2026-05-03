# WaterFilterCBZ - Testing Roadmap

This document outlines the planned testing strategy for the WaterFilterCBZ application, focusing on unit, integration, and system-level tests, as identified in the main `README.md`.

## 1. Unit Testing

Focus on testing individual components in isolation.

-   **`SerialPortService` mock tests:** Develop unit tests for the `SerialPortService` by mocking the `SerialPort` class or its dependencies to ensure correct handling of serial data, framing, CRC validation, and connection lifecycle.
-   **`SensorSample` CSV parser unit tests:** Create tests to verify the `SensorSample.TryParseCsv()` method (or equivalent binary parsing logic) correctly parses various valid and invalid sensor data formats.
-   **ViewModel MVVM binding tests:** Implement unit tests for `SensorViewModel` to ensure data binding, command execution, property changes, and data processing logic function as expected, independent of the UI.

## 2. Integration Testing

Verify the interaction between different components of the application. This would involve testing the `SerialPortService` interacting with the `SensorViewModel`, or the `SensorViewModel` updating `OxyPlot` models.

## 3. System-Level Testing

Test the complete application as a whole, often involving actual hardware.

-   **End-to-end communication with a microcontroller:** Conduct tests with a physical microcontroller sending data to the application to validate the entire data pipeline from serial reception to UI display.
-   **UI responsiveness and data accuracy:** Verify that the UI updates correctly and responsively with real-time data, and that displayed values and charts accurately reflect the incoming sensor data.
-   **Error handling and logging:** Test various error conditions (e.g., port disconnection, malformed data) to ensure the application handles them gracefully and logs appropriate messages.