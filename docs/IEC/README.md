# IEC 62304 Documentation Set — WaterFilterCBZ

This folder contains the IEC 62304:2006+AMD1:2015 software lifecycle documentation for WaterFilterCBZ.

**Software safety class: C** (death or serious injury possible) — see the Safety Classification below.

> Status: all documents are **drafts**. Several Class C risk controls are not yet implemented in code (see the Risk Management File and SRS). The classification rests on an assumed intended use; confirm the open items (OAI-002, OAI-003, OAI-004, OAI-006) before relying on these documents for a regulatory submission.

## Documents and clause mapping

| IEC 62304 clause | Document |
|---|---|
| 4.3 — Safety classification | [Software Safety Classification](Software_Safety_Classification_IEC62304.md) |
| 5.1 — Development planning | [Software Development Plan](Software_Development_Plan_IEC62304.md) |
| 5.2 — Requirements analysis | [Software Requirements Specification](Software_Requirements_Specification_IEC62304.md) |
| 5.3 — Architectural design | [Software Architecture Design](Software_Architecture_Design_IEC62304.md) |
| 5.4 — Detailed design | [Software Detailed Design](Software_Detailed_Design_IEC62304.md) |
| 5.5–5.7 — Unit/integration/system verification | [Software Verification and Test Plan](Software_Verification_and_Test_Plan_IEC62304.md) |
| 5.8 — Release | Template in [Development Plan §8](Software_Development_Plan_IEC62304.md) |
| 6 — Maintenance | [Software Maintenance Plan](Software_Maintenance_Plan_IEC62304.md) |
| 7 — Risk management | [Software Risk Management File](Software_Risk_Management_IEC62304.md) |
| 8 — Configuration management | [Software Configuration Management Plan](Software_Configuration_Management_Plan_IEC62304.md) |
| 9 — Problem resolution | [Software Problem Resolution Process](Software_Problem_Resolution_Process_IEC62304.md) |

## Identifier scheme (cross-document)

| Prefix | Meaning | Defined in |
|---|---|---|
| `SRS-0xx` | Implemented functional/interface requirement | SRS |
| `SRS-Cxx` | Class C safety / risk-control requirement | SRS |
| `HAZ-xxx` | Software-contributable hazardous situation | Risk Management File |
| `RC-xxx` | Risk control measure | Risk Management File |
| `AE-xxx` | Architectural element | Architecture Design §4 |
| `OAI-xxx` | Open architectural/risk input item | Architecture Design §5 |
| `PR-<n>` | Problem report | Problem Resolution Process |

## Traceability

The end-to-end chain is **Hazard → Risk Control → Requirement → Architectural Element → Unit → Verification**, maintained primarily in [Architecture Design §4](Software_Architecture_Design_IEC62304.md) and cross-referenced from the Risk Management File and Verification & Test Plan.

## Open items to close for the Class C baseline

- ✅ **OAI-001** — Software safety class assigned: **C**.
- ✅ **OAI-002** — Intended use confirmed: pharmaceutical/medical purified-water monitoring (device-level severities still to finalize).
- ✅ **OAI-003** — SENSOR_ID→parameter mapping + default operating/physical ranges implemented (SRS-C-003 / RC-008). **Numeric defaults still to be confirmed against the device specification.**
- 🔶 **OAI-004** — Communication-loss timeout fixed at **5 s** (✅ SRS-C-001 / RC-002 implemented); **max sample rate still open** (SRS-NF-001 / RC-004).
- **OAI-006** — Protocol versioning / device identity (SRS-C-002 / RC-003).
- Implement and verify remaining risk controls: RC-003, RC-009, RC-010, RC-011, buffer cap (RC-005), and decide RC-001b (CRC). **RC-002 and RC-008 done (2026-06-05).**
- ✅ Author the **Detailed Design** (Clause 5.4).
