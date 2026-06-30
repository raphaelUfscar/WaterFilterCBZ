# Traceability Matrix

| Field | Value |
|---|---|
| Document ID | DOC-IEC-011 |
| Title | Traceability Matrix |
| Revision | 0.1 (Draft) |
| Status | **Draft — not approved for use** |
| Effective date | _pending approval_ |
| Document owner | Engineering Lead (role to be assigned) |
| Classification | Controlled |
| Next review due | _within 12 months of effective date_ |

> ⚠️ **AI-drafted (QMS-009).** Closes the *content* side of gap **QMS-010** (a single consolidated
> matrix); the *tooling* side (auto-generation/CI consistency check) is still pending — see §4.
> This matrix is maintained **by hand** and must be updated with every change that adds or alters a
> hazard, risk control, requirement, unit, or test (see [SOP-001 §5.3](../QMS/SOP-001_Document_and_Record_Control.md)).

## 1. Purpose

Provide the single, end-to-end traceability view required for IEC 62304 / ISO 13485 design control:
**Hazard → Risk Control → Requirement → Architectural Element → Unit → Verification**. It
consolidates links that are otherwise spread across the [Risk Management File](Software_Risk_Management_IEC62304.md),
[SRS](Software_Requirements_Specification_IEC62304.md), [Architecture](Software_Architecture_Design_IEC62304.md §4),
[Detailed Design](Software_Detailed_Design_IEC62304.md), and the
[Verification & Test Plan](Software_Verification_and_Test_Plan_IEC62304.md). Those documents remain
the authoritative records; this matrix is the cross-cutting index over them.

## 2. Risk-control traceability (Hazard → RC → Requirement → Architecture → Unit → Verification)

| Hazard | Risk control | Requirement(s) | Architectural element | Unit(s) | Verification artifact | Status |
|---|---|---|---|---|---|---|
| HAZ-001 (incorrect value) | RC-001 frame + checksum validation | SRS-004, SRS-005 | AE-PROTO-001 | `SerialPortService.ValidateChecksum`/`ParseReceiveBuffer`/`ParseFrame` | `SerialPortServiceTests`, `SerialPortServiceFramingTests` | Partial (rejection-path coverage growing) |
| HAZ-001 (incorrect value) | RC-008 two-tier range/unit validation | SRS-C-003 | AE-MODEL-001, AE-VM-001 | `SensorParameter`, `SensorParameterRegistry`, `SensorDisplayInfo.AddValue` | `SensorParameterTests`, `SensorDisplayInfoTests`, `SensorParameterRegistryConfigTests`, `SensorRangeConfigLoaderTests` | ✅ Verified |
| HAZ-001 (incorrect value) | RC-001b CRC (vs additive checksum) | SRS-C-008 | AE-PROTO-001 | — | — | 🔵 Decision pending |
| HAZ-002 (stale value) | RC-002 stale-data supervision (5 s) | SRS-C-001 | AE-VM-001, AE-ACQ-001 | `SensorDisplayInfo.EvaluateStaleness`, `SensorViewModel.EvaluateSensorStaleness` | `SensorDisplayInfoTests` | ✅ Verified |
| HAZ-003 (wrong/incompatible device) | RC-003 device identity + protocol version | SRS-C-002 | AE-PROTO-001, AE-ACQ-001 | — (detector pending) | — | ❌ Not implemented |
| HAZ-004 (unavailable/unresponsive) | RC-004 async acquisition + chart throttle | SRS-003, SRS-008, SRS-017 | AE-ACQ-001, AE-VM-001 | `SerialPortService.ProcessIncomingDataAsync`, `SensorViewModel.UpdateChartForSensor` | Design analysis; **sustained-rate stress test pending** | ⚠️ Implemented, stress test pending |
| HAZ-004 (unavailable/unresponsive) | RC-009 processing-task fault surfacing | SRS-C-005 | AE-ACQ-001, AE-VM-001 | `SerialPortService.ProcessingFaulted`, `SensorViewModel.NotifyProcessingFault` | `SerialPortServiceFramingTests`, `SensorViewModelProcessingFaultTests` | ✅ Verified |
| HAZ-005 (resource exhaustion / parser lockup) | RC-005 buffer cap + resync + timeout | SRS-005, SRS-016, SRS-C-004 | AE-PROTO-001 | `SerialPortService.ParseReceiveBuffer` (`MAX_RECEIVE_BUFFER_BYTES`) | `SerialPortServiceFramingTests` | ✅ Verified |
| HAZ-006 (wrong/unavailable port) | RC-006 port enumeration + status + command enablement | SRS-001, SRS-002, SRS-012 | AE-UTIL-001, AE-VM-001, AE-UI-001 | `SerialPortHelper`, `SensorViewModel` commands | `ConnectionWorkflowTests` (E2E) | ✅ Verified |
| HAZ-001..005 (failure presentation) | RC-010 UI failure-state taxonomy | SRS-C-006 | AE-VM-001, AE-UI-001 | `MonitoringState`, `MonitoringStateResolver`, `SensorViewModel.MonitoringState`, `MonitoringStateToBrushConverter` | `MonitoringStateResolverTests`, `SensorViewModelMonitoringStateTests`, `MonitoringStateToBrushConverterTests` | ✅ Verified (parser-error/device-mismatch detectors pending RC-003 + parser supervisor) |
| (configuration) | RC-011 config protection + audit | SRS-C-007 | AE-VM-001 | — | — | ❌ Not implemented |
| HAZ-007 (no diagnostic evidence) | RC-007 safety-relevant event logging | SRS-010 | AE-LOG-001 | `LoggingService`, log calls across units | `ConnectionWorkflowTests` (command logging); **full event-coverage procedure pending** | ⚠️ Implemented, coverage procedure pending |
| (architecture constraint) | RC-007c no safety logic in presentation layer | SRS-C-009 | architecture-wide | — | Design review | Held (constraint) |

## 3. Requirement → verification quick index

| Requirement | Verification artifact(s) | Status |
|---|---|---|
| SRS-001..017 (baseline functional) | `SerialPortServiceTests`, `SerialPortServiceFramingTests`, `SensorDisplayInfoTests`, `SensorViewModel*Tests`, `RelayCommandTests`, `LoggingServiceTests`, `SerialPortHelperTests`, `ConnectionWorkflowTests` | See [SRS §6 traceability](Software_Requirements_Specification_IEC62304.md) |
| SRS-C-001 / RC-002 | `SensorDisplayInfoTests` | ✅ |
| SRS-C-002 / RC-003 | — | ❌ |
| SRS-C-003 / RC-008 | `SensorParameterTests`, `SensorDisplayInfoTests`, `SensorParameterRegistryConfigTests`, `SensorRangeConfigLoaderTests` | ✅ |
| SRS-C-004 / RC-005 | `SerialPortServiceFramingTests` | ✅ |
| SRS-C-005 / RC-009 | `SerialPortServiceFramingTests`, `SensorViewModelProcessingFaultTests` | ✅ |
| SRS-C-006 / RC-010 | `MonitoringStateResolverTests`, `SensorViewModelMonitoringStateTests`, `MonitoringStateToBrushConverterTests` | ✅ |
| SRS-C-007 / RC-011 | — | ❌ |
| SRS-C-008 / RC-001b | — | 🔵 |
| SRS-C-009 / RC-007c | Design review | Held |

## 4. Gap to fully close QMS-010

This document closes the **content** half of QMS-010 (a single, controlled, end-to-end matrix).
To fully close the gap, the matrix should become **tool-backed**:

- Generate or cross-check the rows against the source documents and the test assembly in CI, so a
  renamed/removed test, requirement, or RC that breaks a trace fails the build rather than silently
  rotting this table.
- Until then, this matrix is maintained manually and is only as current as its last edit
  (Revision/▾ history below). Treat it as an index, not an independent source of truth.

## Revision history

| Revision | Date | Author | Description |
|---|---|---|---|
| 0.1 | 2026-06-30 | Claude (AI draft) | Initial consolidated traceability matrix (Hazard → RC → Requirement → Architecture → Unit → Verification); content side of QMS-010. |
