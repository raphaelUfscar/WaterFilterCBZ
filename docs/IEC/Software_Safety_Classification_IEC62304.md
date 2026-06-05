# Software Safety Classification

Project: WaterFilterCBZ
Standard context: IEC 62304:2006+AMD1:2015, Clause 4.3 (Software safety classification)
Document status: Draft
Last updated: 2026-06-05

## 1. Purpose

This document records the IEC 62304 software safety classification for WaterFilterCBZ, the rationale for that classification, and the resulting process obligations. It resolves open architectural item **OAI-001** in the [Software Architecture Design](Software_Architecture_Design_IEC62304.md).

Per IEC 62304 Clause 4.3, the manufacturer assigns to each software system a safety class (A, B, or C) based on the possible effects on the patient, operator, or other people resulting from a hazardous situation to which the software system can contribute. The classification is derived from the risk analysis maintained in the [Software Risk Management File](Software_Risk_Management_IEC62304.md).

## 2. Classification Scheme (IEC 62304 Clause 4.3)

| Class | Definition |
|---|---|
| A | No injury or damage to health is possible. |
| B | Non-serious injury is possible. |
| C | Death or serious injury is possible. |

Classification is performed **before** considering external risk control measures (hardware, independent alarms, clinical procedures). A software system is assigned the class corresponding to the most severe hazardous situation it can contribute to, assuming any software failure leading to that situation occurs.

## 3. Intended Use Basis

> Intended use was confirmed on 2026-06-05 (OAI-002): **pharmaceutical / medical purified-water quality monitoring**. Device-level harm severities remain to be finalized in the ISO 14971 file. If the intended use changes, this classification must be re-evaluated.

| Attribute | Value for classification |
|---|---|
| Intended use | Real-time monitoring and visualization of purified-water quality in a pharmaceutical / medical context: up to four channels (conductivity, temperature, pH, pressure/flow) acquired from a microcontroller and presented to an operator who may act on the displayed values. |
| Operator | Trained operator / technician. |
| Use environment | Supervised operational setting; not a fully autonomous control loop. |
| Decision dependence | The displayed values, statistics, and trends may inform operator decisions where incorrect, stale, missing, or implausible values could contribute to a hazardous situation. |
| Control authority | The software performs **no** automatic therapy, dosing, actuation, or shutoff. It is display/monitoring only. |

## 4. Assigned Classification

**WaterFilterCBZ software system safety class: C**

### 4.1 Rationale

Although the application performs no automatic control action, it is a **monitoring and decision-support** system. Under the assumed intended use, the hazardous situations identified in the [Risk Management File](Software_Risk_Management_IEC62304.md) include an operator relying on a displayed value that is **incorrect (corrupted data), stale (communication silently lost), implausible (out of physical range), or sourced from an incompatible device**. If such a value contributes to a wrong operator decision in a safety-relevant fluid/water-treatment context, the resulting hazardous situation could plausibly contribute to serious injury before considering external risk controls.

In accordance with IEC 62304 Clause 4.3, where the most severe possible harm is death or serious injury and external risk control measures are not credited at the classification stage, the software system is conservatively assigned **Class C**.

### 4.2 Segmentation note

IEC 62304 permits decomposing a software system into software items of differing classes when adequate **segregation** is demonstrated (Clause 4.3 c, 5.3.5). The current architecture does **not** yet provide validated segregation between safety-relevant items (acquisition, frame validation, freshness/validity logic) and non-safety items (charting, cosmetic UI). Therefore the entire software system inherits Class C until segregation is designed, justified, and verified. Candidate segregation boundaries are listed in [Software Architecture Design §3](Software_Architecture_Design_IEC62304.md). Any future down-classification of an item must be recorded in this document with its segregation evidence.

## 5. Process Obligations Resulting From Class C

IEC 62304 makes most activities mandatory for Class C. The following table summarizes the obligations and the deliverable that addresses each.

| Clause | Activity | Required for C | Deliverable |
|---|---|:---:|---|
| 5.1 | Software development planning | Yes | [Software Development Plan](Software_Development_Plan_IEC62304.md) |
| 5.2 | Software requirements analysis | Yes | [Software Requirements Specification](Software_Requirements_Specification_IEC62304.md) |
| 5.3 | Software architectural design | Yes | [Software Architecture Design](Software_Architecture_Design_IEC62304.md) |
| 5.4 | Software detailed design | Yes | To be authored (detailed design / unit specs) |
| 5.5 | Software unit implementation **and verification** | Yes | [Verification & Test Plan](Software_Verification_and_Test_Plan_IEC62304.md) |
| 5.6 | Software integration and integration testing | Yes | [Verification & Test Plan](Software_Verification_and_Test_Plan_IEC62304.md) |
| 5.7 | Software system testing | Yes | [Verification & Test Plan](Software_Verification_and_Test_Plan_IEC62304.md) |
| 5.8 | Software release | Yes | SDP §Release; documented known anomalies required |
| 6 | Software maintenance | Yes | [Software Maintenance Plan](Software_Maintenance_Plan_IEC62304.md) |
| 7 | Software risk management | Yes | [Software Risk Management File](Software_Risk_Management_IEC62304.md) |
| 8 | Software configuration management | Yes | [Software Configuration Management Plan](Software_Configuration_Management_Plan_IEC62304.md) |
| 9 | Software problem resolution | Yes | [Software Problem Resolution Process](Software_Problem_Resolution_Process_IEC62304.md) |

Class C additionally requires (versus Class A/B):

- Detailed design of **every** software unit (5.4.2, 5.4.3) and documented unit verification acceptance criteria (5.5.2, 5.5.3).
- Documentation of the architecture down to the software-item interface level (5.3.2–5.3.6) including segregation rationale.
- Integration and integration-test plans with regression strategy (5.6).
- Documented system test, with verification that anomalies are evaluated against risk (5.7).
- A release record listing all known residual anomalies, each assessed for safety impact (5.8.2, 5.8.3).

## 6. SOUP (Software of Unknown Provenance)

Class C requires hazard evaluation of SOUP and documentation of required SOUP functional/performance and hardware/software requirements (Clauses 5.3.3–5.3.4, 7.1.2, 8.1.2). The current SOUP inventory:

| SOUP item | Version | Function used | Notes for risk evaluation |
|---|---|---|---|
| .NET runtime (WPF) | net10.0-windows | Application platform, UI, threading | Platform trust; anomaly monitoring required. |
| System.IO.Ports | 9.0.0 | Serial I/O and COM enumeration | Directly in the safety-relevant acquisition path. |
| OxyPlot.Wpf | 2.1.2 | Chart rendering | Presentation only; lower safety relevance. |
| Serilog (+ File, Debug sinks) | 4.0.0 / 6.0.0 / 2.0.0 | Diagnostic and audit logging | Supports traceability of failures. |
| xUnit, Microsoft.NET.Test.Sdk, coverlet | 2.9.3 / 17.14.1 / 6.0.4 | Verification tooling (not shipped) | Tool qualification consideration only. |

The full SOUP anomaly-list review and version-tracking obligation is administered through the [Configuration Management Plan](Software_Configuration_Management_Plan_IEC62304.md) and evaluated in the [Risk Management File](Software_Risk_Management_IEC62304.md).

## 7. Re-classification Triggers

This classification shall be reviewed when any of the following occur:

- The intended use or claims change (e.g. the software gains any control/actuation authority).
- A new hazard or a change in harm severity is identified in the Risk Management File.
- Architectural segregation between safety and non-safety items is introduced (may allow item-level down-classification).
- A SOUP component with a higher safety relevance is added or materially changed.

## 8. Revision History

| Revision | Date | Author | Description |
|---|---|---|---|
| 0.1 | 2026-06-05 | Claude | Initial classification: software system assigned Class C; rationale, process obligations, SOUP inventory, and re-classification triggers recorded. Resolves OAI-001. |
| 0.2 | 2026-06-05 | Claude | Intended use confirmed as pharmaceutical/medical purified-water quality monitoring (OAI-002); intended-use table updated from assumed to confirmed. |
