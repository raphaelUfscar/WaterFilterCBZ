# Software Risk Management File

Project: WaterFilterCBZ
Standard context: IEC 62304:2006+AMD1:2015 Clause 7 (Software risk management process), interfacing to ISO 14971 (Application of risk management to medical devices)
Software safety class: C (see [Software Safety Classification](Software_Safety_Classification_IEC62304.md))
Document status: Draft
Last updated: 2026-06-05

## 1. Purpose and Scope

This file documents the software-related risk management activities for WaterFilterCBZ as required by IEC 62304 Clause 7, which operates within the overall device risk management process of ISO 14971. It identifies hazardous situations that software can contribute to, the contributing software causes, the risk control measures (RCs) implemented in software, and the verification and traceability of those controls.

IEC 62304 Clause 7 requires the manufacturer to:
- **7.1** Analyze software contributing to hazardous situations (including SOUP failures).
- **7.2** Define risk control measures and trace them.
- **7.3** Verify risk control measures.
- **7.4** Manage risks arising from software changes.

> Scope note: This file covers the **software** contribution only. The complete device hazard analysis, harm severity assignments, and acceptability criteria belong to the device-level ISO 14971 risk management file, which is the source of truth for severity. Severity values below are **provisional** pending OAI-002.

## 2. Risk Analysis Method

### 2.1 Rating scales (provisional — align with device-level ISO 14971 file)

Severity (S):

| Level | Label | Description |
|--:|---|---|
| 1 | Negligible | No injury; inconvenience only. |
| 2 | Minor | Temporary non-serious injury; no intervention. |
| 3 | Serious | Reversible serious injury or intervention required. |
| 4 | Critical | Irreversible serious injury. |
| 5 | Catastrophic | Death. |

Probability of occurrence of harm (P):

| Level | Label |
|--:|---|
| 1 | Improbable |
| 2 | Remote |
| 3 | Occasional |
| 4 | Probable |
| 5 | Frequent |

Risk index = S × P. Provisional acceptability: ≤4 acceptable; 5–9 acceptable with risk control + justification; ≥10 not acceptable without effective risk control.

> Per IEC 62304/ISO 14971, for software the **probability of a software fault is assumed to be 1** (software does not fail randomly). The P column therefore reflects the probability that the software contribution leads to harm given the contextual factors (operator detection, exposure), not the probability of the software defect itself.

## 3. Software-Contributable Hazardous Situations

| Hazard ID | Hazardous situation | Software contributing cause | SOUP involved |
|---|---|---|---|
| HAZ-001 | Operator acts on an **incorrect** displayed value. | Corrupted serial data passes validation (weak checksum), or an implausible value is displayed as valid. | System.IO.Ports |
| HAZ-002 | Operator relies on a **stale** value after communication silently stops. | No freshness/maximum-age supervision; last value remains on screen. | System.IO.Ports |
| HAZ-003 | Operator acts on data from a **wrong/incompatible device or firmware**. | No device-identity or protocol-version verification. | System.IO.Ports |
| HAZ-004 | Monitoring becomes **unavailable/unresponsive** under high input rate or task failure. | UI overload, or silent termination of the background processing task. | .NET runtime, OxyPlot |
| HAZ-005 | **Resource exhaustion / parser lockup** under sustained malformed or malicious input. | Receive buffer has no explicit maximum length. | System.IO.Ports, .NET runtime |
| HAZ-006 | Operator connects to an **unavailable/incorrect COM port** and assumes monitoring is active. | Port selection without connection confirmation feedback. | System.IO.Ports |
| HAZ-007 | A field failure **cannot be reconstructed** for investigation. | Insufficient diagnostic logging of safety-relevant events. | Serilog |

## 4. Risk Evaluation and Risk Controls

Initial risk is evaluated **before** the software risk control; residual risk **after**. (S, P, index values are provisional.)

| Hazard | Initial S×P=R | Risk control ID | Risk control (software measure) | Requirement | Architectural element | Residual S×P=R | Status |
|---|:--:|---|---|---|---|:--:|---|
| HAZ-001 | 4×3=12 | RC-001 | Validate frame structure + checksum before accepting data. | SRS-004, SRS-005 | AE-PROTO-001 | 4×2=8 | Implemented (8-bit additive) |
| HAZ-001 | 4×3=12 | RC-008 | Per-sensor range/unit plausibility validation; reject/quarantine implausible values. | SRS-C-003 | AE-MODEL-001, AE-VM-001 | 4×1=4 | **Implemented (2026-06-05)**: two-tier validation, reject implausible / flag out-of-spec |
| HAZ-001 | 4×3=12 | RC-001b | Use CRC instead of additive checksum if analysis requires. | SRS-C-008 | AE-PROTO-001 | — | **Decision pending** |
| HAZ-002 | 4×3=12 | RC-002 | Detect missing samples / stale timestamps; show explicit stale state + log. | SRS-C-001 | AE-VM-001, AE-ACQ-001 | 4×1=4 | **Implemented (2026-06-05)**: 5 s per-sensor freshness supervision + UI flag + log |
| HAZ-003 | 4×2=8 | RC-003 | Verify device identity + protocol version before accepting samples. | SRS-C-002 | AE-PROTO-001, AE-ACQ-001 | 4×1=4 (target) | **Not implemented** |
| HAZ-004 | 3×3=9 | RC-004 | Asynchronous acquisition + per-sensor chart throttling. | SRS-003, SRS-008, SRS-017 | AE-ACQ-001, AE-VM-001 | 3×2=6 | Implemented |
| HAZ-004 | 3×3=9 | RC-009 | Detect processing-task failure; surface degraded/error state; require explicit reconnect. | SRS-C-005 | AE-ACQ-001, AE-VM-001 | 3×1=3 | **Implemented** |
| HAZ-005 | 3×3=9 | RC-005 | Parser resync + frame-assembly timeout + explicit 4096-byte buffer cap (drop/reset/log on overflow). | SRS-005, SRS-016, SRS-C-004 | AE-PROTO-001 | 3×1=3 | **Implemented** |
| HAZ-006 | 3×2=6 | RC-006 | Enumerate ports; show connection status; command enablement. | SRS-001, SRS-002, SRS-012 | AE-UTIL-001, AE-VM-001, AE-UI-001 | 3×1=3 | Implemented |
| HAZ-001..005 | — | RC-010 | Defined, verified UI state for each failure condition (`MonitoringState` taxonomy + colour-coded chip). | SRS-C-006 | AE-VM-001, AE-UI-001 | — | **Implemented** (parser-error/device-mismatch detectors pending RC-003 + parser supervisor) |
| — | — | RC-011 | Configuration protection + audit (port, baud, ranges, timeout). | SRS-C-007 | AE-VM-001 | — | **Not implemented** |
| HAZ-007 | 3×3=9 | RC-007 | Record safety-relevant operational events to rolling logs. | SRS-010 | AE-LOG-001 | 3×2=6 | Implemented |
| (constraint) | — | RC-007c | No safety decision logic in presentation layer. | SRS-C-009 | architecture-wide | — | Held |

## 5. SOUP Risk Evaluation (Clause 7.1.2 / 7.1.3)

| SOUP | Failure mode considered | Possible contribution | Mitigation in software |
|---|---|---|---|
| System.IO.Ports 9.0.0 | Dropped/duplicated bytes, port exception, silent stop | HAZ-001, HAZ-002, HAZ-004, HAZ-005 | Frame validation, resync, timeout, buffer cap, error logging, processing-task fault surfacing (RC-009). |
| .NET runtime (WPF) | Dispatcher/threading fault, unhandled exception | HAZ-004 | UI marshaling, try/catch with logging; processing-task fault surfaced via `ProcessingFaulted` → degraded state + explicit reconnect (RC-009). |
| OxyPlot.Wpf 2.1.2 | Render slowdown/leak | HAZ-004 | 300-point cap, throttling. |
| Serilog (+sinks) | Log write failure | HAZ-007 (loss of evidence) | Non-blocking sinks; failure does not stop monitoring. |

SOUP versions and anomaly-list monitoring are tracked by the [Configuration Management Plan](Software_Configuration_Management_Plan_IEC62304.md).

## 6. Verification of Risk Controls (Clause 7.3)

Each implemented RC must have verification evidence; each pending RC must have a defined verification before a Class C release. See the [Verification & Test Plan](Software_Verification_and_Test_Plan_IEC62304.md).

| Risk control | Verification status | Evidence / planned test |
|---|---|---|
| RC-001 | Partial | `SerialPortServiceTests` (valid frame decode); **rejection-path tests for invalid count/checksum/end-byte to be added**. |
| RC-004 | Partial | Async design analysis; **sustained-rate stress test to be added**. |
| RC-005 | Verified | Resync/timeout covered by design + simulator error injection; buffer-cap covered by `SerialPortServiceFramingTests` (overflow drops/resets; at-cap still parses a valid frame). |
| RC-006 | Verified | `ConnectionWorkflowTests` (E2E port select / connect / disconnect). |
| RC-007 | Partial | `ConnectionWorkflowTests` asserts commands reach the log file; **full event-coverage procedure pending**. |
| RC-002 | Verified | `SensorDisplayInfoTests` stale-data cases (becomes stale after 5 s, stays fresh within threshold, recovers on new sample, transition `PropertyChanged`). |
| RC-008 | Verified | `SensorParameterTests` (classification: normal/out-of-spec/invalid, NaN/Inf, inclusive bounds, registry mapping) + `SensorDisplayInfoTests` (reject keeps last good, out-of-spec displayed, recovery, stale cleared on rejected sample) + `SensorParameterRegistryConfigTests` / `SensorRangeConfigLoaderTests` (user-configurable ranges: per-field fallback, inconsistent/non-finite override rejection, JSON load incl. malformed→defaults). |
| RC-009 | Verified | `SerialPortServiceFramingTests` (processing-task fault raises `ProcessingFaulted`; normal cancellation does not) + `SensorViewModelProcessingFaultTests` (degraded state, reconnect clears fault). |
| RC-010 | Verified | `MonitoringStateResolverTests` (all 8 states + precedence ordering + labels) + `SensorViewModelMonitoringStateTests` (VM drives each reachable state: disconnected/connecting/healthy/invalid/processing-fault/device-mismatch/parser-error, reconnect & clear-data transitions). _Parser-error & device-mismatch detectors land with RC-003 + the parser-error supervisor._ |
| RC-003, RC-011, RC-001b | Not started | Features not yet implemented; tests to follow implementation. |

## 7. Risk Management of Software Changes (Clause 7.4)

For each change (defect fix or enhancement), the [Problem Resolution Process](Software_Problem_Resolution_Process_IEC62304.md) and [Maintenance Plan](Software_Maintenance_Plan_IEC62304.md) require:
1. Analyze whether the change introduces new hazardous situations or affects existing RCs.
2. Update affected hazards/RCs/requirements in this file.
3. Re-verify affected RCs and run regression tests (§ Verification & Test Plan).
4. Re-evaluate residual risk and overall acceptability.

## 8. Residual Risk Summary

RC-002 (stale-data supervision, HAZ-002) and RC-008 (two-tier value validation, HAZ-001) are **implemented and verified** as of 2026-06-05; RC-005 (receive-buffer cap, HAZ-005) and RC-009 (processing-task fault surfacing, HAZ-004) are **implemented and verified** as of 2026-06-30. Several Class C risk controls (RC-003, RC-010, RC-011) remain **not yet implemented**, and RC-001b (CRC) is a pending decision. Until they are implemented and verified, the residual risk for HAZ-003 is **not yet acceptable for a Class C release**; HAZ-004 acceptability additionally depends on completing the RC-004 sustained-rate stress test, and HAZ-001 residual risk depends on confirming the RC-008 range defaults against the device specification. The overall residual-risk acceptability statement and benefit-risk conclusion are made at the device level (ISO 14971) once severities (OAI-002) and acceptance values (OAI-003, OAI-004) are fixed and the controls are verified.

## 9. Open Risk Inputs

| ID | Item |
|---|---|
| OAI-002 | Resolved (2026-06-05): intended use = pharma/medical purified-water monitoring. Severities still to be confirmed at device level. |
| OAI-003 | Resolved (2026-06-05): SENSOR_ID→parameter mapping + default operating/physical ranges implemented (RC-008). Numeric defaults still to be confirmed against the device specification. |
| OAI-004 | Resolved (2026-06-05): communication-loss timeout = 5 s (RC-002). Max sample rate still open → RC-004 acceptance. |
| OAI-006 | Protocol versioning / device identity → RC-003 design. |

## 10. Revision History

| Revision | Date | Author | Description |
|---|---|---|---|
| 0.1 | 2026-06-05 | Claude | Initial software risk management file: 7 software-contributable hazards, risk controls RC-001..011 with implemented/pending status, SOUP risk evaluation, RC verification status, change-risk process, and residual-risk summary. Severities provisional pending OAI-002. |
| 0.2 | 2026-06-05 | Claude | Intended use confirmed as pharma/medical purified-water monitoring (OAI-002); RC-002 stale-data supervision implemented and verified (residual HAZ-002 = 4); communication-loss timeout fixed at 5 s (OAI-004); RC verification, residual-risk, and open-input sections updated. |
| 0.3 | 2026-06-05 | Claude | RC-008 two-tier value validation implemented and verified (residual HAZ-001 = 4, subject to confirming range defaults); SENSOR_ID→parameter mapping defined (OAI-003 resolved); RC verification, residual-risk, and open-input sections updated. |
