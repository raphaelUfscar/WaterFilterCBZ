# Quality Manual

| Field | Value |
|---|---|
| Document ID | QM-001 |
| Title | Quality Manual |
| Revision | 0.1 (Draft) |
| Status | **Draft — not approved for use** |
| Effective date | _pending approval_ |
| Document owner | Quality Manager (role to be assigned) |
| Classification | Controlled |
| Next review due | _within 12 months of effective date_ |

> ⚠️ **AI-drafted template (QMS-009).** Not an approved QMS record. Scope, intended use, and
> organizational details below are **placeholders** that must be confirmed by the manufacturer.
> See [docs/QMS/README.md](README.md).

## Approval

| Role | Name | Signature | Date |
|---|---|---|---|
| Author (prepared by) | | | |
| Reviewer (checked by) | | | |
| Approver (authorized by, e.g. Top Management) | | | |

## 1. Purpose

This Quality Manual describes the quality management system (QMS) established for the design and
development of **WaterFilterCBZ**, a software product for monitoring purified-water quality
parameters. It is the top-level QMS document required by ISO 13485:2016 §4.2.2.

## 2. Scope and intended use _(placeholder — confirm)_

- **Product:** WaterFilterCBZ — Windows desktop software that monitors up to four sensor channels
  (conductivity, temperature, pH, pressure) received from a microcontroller over serial.
- **Assumed intended use:** monitoring/visualization of pharmaceutical / medical purified-water
  quality parameters. **This intended-use statement is assumed (OAI-002) and must be confirmed by
  the manufacturer**, as it drives classification, severity, and applicable regulation.
- **Software safety class:** C (see [Software Safety Classification](../IEC/Software_Safety_Classification_IEC62304.md)).
- **QMS scope:** software design, development, verification, release, and maintenance activities
  for this product. Manufacturing/sterilization/installation/servicing clauses of ISO 13485 are
  **not applicable** to a software-only product except as noted; non-applicability must be
  justified here by the manufacturer.

### 2.1 Exclusions and non-applicable clauses _(to be justified)_

| ISO 13485 clause | Applicable? | Justification |
|---|---|---|
| 7.5.2 Cleanliness of product | No | Software product; no physical product. |
| 7.5.5 Sterilization | No | Not a sterile device. |
| 7.5.9 Servicing | _TBD_ | Depends on whether servicing is offered. |
| 7.5.11 Preservation of product | Partial | Applies as software archival/integrity (SCM). |

## 3. Normative references

- ISO 13485:2016 — Medical devices — Quality management systems.
- IEC 62304:2006+AMD1:2015 — Medical device software life-cycle processes.
- ISO 14971:2019 — Application of risk management to medical devices.
- _(As applicable)_ IEC 62366-1 (usability), IEC 81001-5-1 (cybersecurity).

## 4. Quality policy and objectives _(placeholder)_

Top management shall define and approve a quality policy and measurable quality objectives.
Draft placeholders pending management approval:

- **Policy:** deliver monitoring software that is safe, correct, and traceable, maintained under a
  controlled life-cycle, with continual improvement driven by objective evidence.
- **Objectives (examples, to be set by management):** all safety-relevant requirements verified
  before release; defined risk controls implemented and verified; no unresolved Class C anomalies
  at release; documented review/approval for every controlled document.

## 5. QMS structure and processes

The QMS is documented hierarchically: this Quality Manual → procedures (SOPs) → records/templates.
The controlling procedures are listed in [docs/QMS/README.md](README.md) §3 and indexed in the
[Controlled Document Register](Controlled_Document_Register.md).

### 5.1 Process interactions (overview)

```text
Management Review (SOP-002)
   ^             |
   | objectives  | inputs (audits, CAPA, NC, risk)
   |             v
Design & Development Control (SOP-006) --uses--> IEC 62304 life-cycle (docs/IEC/)
   |                                                  |
   |--> Risk Management (SOP-007 / ISO 14971) --------|
   |--> Purchasing & SOUP Control (SOP-009)
   v
Verification & Release --> Nonconforming Product (SOP-004) --> CAPA (SOP-003)
   ^                                                              |
   |---------------- Internal Audit (SOP-005) --------------------|
Document & Record Control (SOP-001) and Competence/Training (SOP-008) support all processes.
```

## 6. Management responsibility (§5)

Top management is responsible for establishing the quality policy and objectives, ensuring
resources, assigning roles and responsibilities, appointing a management representative, and
conducting [management reviews](SOP-002_Management_Review.md). Roles referenced across the QMS
(Quality Manager, Engineering Lead, Risk Manager, Top Management) are **roles, not named persons**;
the manufacturer shall assign named, qualified personnel and record this assignment.

## 7. Resource management (§6)

The manufacturer shall provide competent personnel and adequate infrastructure. Competence and
training requirements are defined in
[SOP-008 Competence, Training and Awareness](SOP-008_Competence_Training_and_Awareness.md).

## 8. Product realization (§7)

Software design and development is governed by
[SOP-006 Design and Development Control](SOP-006_Design_and_Development_Control.md), which adopts
the IEC 62304 life-cycle documented in [docs/IEC/](../IEC/). Risk management
([SOP-007](SOP-007_Risk_Management.md)) and purchasing/SOUP control
([SOP-009](SOP-009_Purchasing_and_Supplier_SOUP_Control.md)) feed product realization.

## 9. Measurement, analysis and improvement (§8)

Conformity and effectiveness are maintained through
[internal audit](SOP-005_Internal_Audit.md), [control of nonconforming product](SOP-004_Control_of_Nonconforming_Product.md),
and [CAPA](SOP-003_Corrective_and_Preventive_Action.md), with results reviewed in
[management review](SOP-002_Management_Review.md).

## 10. Honest status

This manual is a **draft scaffold**. The QMS it describes is not yet operating: roles are
unassigned, the quality policy/objectives are unapproved placeholders, the intended use is
assumed, and no records exist yet. It is a starting framework, not evidence of compliance.

## Revision history

| Revision | Date | Author | Description |
|---|---|---|---|
| 0.1 | 2026-06-30 | Claude (AI draft) | Initial Quality Manual scaffold. |
