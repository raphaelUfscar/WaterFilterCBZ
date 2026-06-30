# Quality Management System (ISO 13485) — WaterFilterCBZ

> ⚠️ **DRAFT SCAFFOLD — NOT AN OPERATING QMS.** Every document in this folder is an
> **AI-drafted template** (gap **QMS-009**). None is approved, effective, or authoritative.
> A QMS is an *organizational* system: it only exists once qualified, named personnel adopt
> these procedures, perform the activities they describe, **and produce signed, dated records**.
> These files give that system a starting structure — they do not, by themselves, make the
> project ISO 13485 compliant or audit-ready. See [QMS-001/009 in the roadmap](../iec62304_class_c_roadmap.md).

## 1. What this is

This folder scaffolds an ISO 13485:2016 quality management system that **wraps** the existing
IEC 62304 software documentation (in [docs/IEC/](../IEC/)). IEC 62304 describes the software
life-cycle *process*; ISO 13485 is the quality system the manufacturer operates that process
within. Without the QMS wrapper, the IEC 62304 documents have no controlling framework — this is
why QMS-001 is flagged as the highest-priority structural gap.

## 2. How these documents are controlled

All QMS documents use the **controlled-document header** defined in
[SOP-001 Document and Record Control](SOP-001_Document_and_Record_Control.md): a unique document
ID, revision, status, effective date, owner, and an **approval block** with author / reviewer /
approver signature lines. Signatures are intentionally blank — they must be completed by qualified
personnel before a document becomes effective. The master list of all controlled documents and
their current revision/status is the
[Controlled Document Register](Controlled_Document_Register.md).

## 3. Document set

| ID | Document | ISO 13485 clause(s) |
|---|---|---|
| QM-001 | [Quality Manual](QM-001_Quality_Manual.md) | 4.2.2 |
| SOP-001 | [Document and Record Control](SOP-001_Document_and_Record_Control.md) | 4.2.4, 4.2.5 |
| SOP-002 | [Management Review](SOP-002_Management_Review.md) | 5.6 |
| SOP-003 | [Corrective and Preventive Action (CAPA)](SOP-003_Corrective_and_Preventive_Action.md) | 8.5.2, 8.5.3 |
| SOP-004 | [Control of Nonconforming Product](SOP-004_Control_of_Nonconforming_Product.md) | 8.3 |
| SOP-005 | [Internal Audit](SOP-005_Internal_Audit.md) | 8.2.4 |
| SOP-006 | [Design and Development Control](SOP-006_Design_and_Development_Control.md) | 7.3 |
| SOP-007 | [Risk Management](SOP-007_Risk_Management.md) | 7.1, ISO 14971 |
| SOP-008 | [Competence, Training and Awareness](SOP-008_Competence_Training_and_Awareness.md) | 6.2 |
| SOP-009 | [Purchasing and Supplier / SOUP Control](SOP-009_Purchasing_and_Supplier_SOUP_Control.md) | 7.4 |

## 4. Relationship to the IEC 62304 set

| QMS document | Governs / connects to |
|---|---|
| SOP-006 Design and Development Control | The whole [docs/IEC/](../IEC/) life-cycle set; IEC 62304 is the software-specific design-control method. |
| SOP-007 Risk Management | [Software Risk Management File](../IEC/Software_Risk_Management_IEC62304.md) and the (pending) device-level ISO 14971 file (QMS-002). |
| SOP-003 CAPA + SOP-004 Nonconforming Product | [Software Problem Resolution Process](../IEC/Software_Problem_Resolution_Process_IEC62304.md). |
| SOP-001 Document and Record Control | All controlled documents, including the IEC 62304 set (currently controlled only by informal revision tables — gap QMS-003). |
| SOP-009 Purchasing / SOUP | The SOUP inventory in the [Configuration Management Plan](../IEC/Software_Configuration_Management_Plan_IEC62304.md) (anomaly-monitoring process gap QMS-008). |

## 5. Known gaps this scaffold does NOT close

- **QMS-002** — device-level ISO 14971 risk management file and a real intended-use/clinical basis.
- **QMS-005** — usability engineering file (IEC 62366-1).
- **QMS-006** — cybersecurity (IEC 81001-5-1) and formal SBOM.
- **QMS-009** — qualified human authorship, review, and approval of every document here.

These remain open in the [Class C compliance roadmap](../iec62304_class_c_roadmap.md) §7.
