# SOP-009 — Purchasing and Supplier / SOUP Control

| Field | Value |
|---|---|
| Document ID | SOP-009 |
| Title | Purchasing and Supplier / SOUP Control |
| Revision | 0.1 (Draft) |
| Status | **Draft — not approved for use** |
| Effective date | _pending approval_ |
| Document owner | Engineering Lead (role to be assigned) |
| Classification | Controlled |
| Next review due | _within 12 months of effective date_ |

> ⚠️ **AI-drafted template (QMS-009).** Not an approved QMS record. See [docs/QMS/README.md](README.md).

## Approval

| Role | Name | Signature | Date |
|---|---|---|---|
| Author (prepared by) | | | |
| Reviewer (checked by) | | | |
| Approver (authorized by) | | | |

## 1. Purpose

Ensure purchased product and third-party software components (SOUP — Software Of Unknown
Provenance) conform to requirements and are controlled, per ISO 13485:2016 §7.4 and IEC 62304
§5.3.3–§5.3.4, §7, §8.1.2.

## 2. Scope

All third-party / off-the-shelf software components used in WaterFilterCBZ, including runtime,
libraries, and tools that affect the product.

## 3. SOUP inventory

The SOUP components and versions are inventoried in the
[Software Configuration Management Plan](../IEC/Software_Configuration_Management_Plan_IEC62304.md)
and risk-evaluated in the
[Software Risk Management File §5](../IEC/Software_Risk_Management_IEC62304.md) (current items:
System.IO.Ports, .NET runtime/WPF, OxyPlot.Wpf, Serilog).

## 4. Procedure

1. **Selection:** specify functional and performance requirements for each SOUP item; record the
   version and source.
2. **Evaluation:** assess the SOUP's relevant failure modes and their contribution to hazards
   ([SOP-007](SOP-007_Risk_Management.md)); justify acceptability.
3. **Verification:** confirm the SOUP meets its requirements as integrated (via the
   [Verification and Test Plan](../IEC/Software_Verification_and_Test_Plan_IEC62304.md)).
4. **Anomaly monitoring (ongoing):** periodically review published anomaly/security lists for each
   SOUP version and evaluate the impact; raise [CAPA](SOP-003_Corrective_and_Preventive_Action.md)
   where needed. **This ongoing process is currently not operating (gap QMS-008): the inventory
   exists but the periodic review is not yet running.**
5. **Change control:** SOUP version changes are assessed under IEC 62304 §7.4.

## 5. Records produced

- SOUP inventory with versions/sources; evaluation/justification records; anomaly-review records;
  change assessments — retained per [SOP-001](SOP-001_Document_and_Record_Control.md).

## 6. References

- ISO 13485:2016 §7.4; IEC 62304 §5.3.3–§5.3.4, §7, §8.1.2;
  [Configuration Management Plan](../IEC/Software_Configuration_Management_Plan_IEC62304.md);
  [Software Maintenance Plan](../IEC/Software_Maintenance_Plan_IEC62304.md).

## Revision history

| Revision | Date | Author | Description |
|---|---|---|---|
| 0.1 | 2026-06-30 | Claude (AI draft) | Initial scaffold. |
