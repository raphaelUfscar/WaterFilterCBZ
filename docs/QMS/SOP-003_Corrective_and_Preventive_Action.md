# SOP-003 — Corrective and Preventive Action (CAPA)

| Field | Value |
|---|---|
| Document ID | SOP-003 |
| Title | Corrective and Preventive Action (CAPA) |
| Revision | 0.1 (Draft) |
| Status | **Draft — not approved for use** |
| Effective date | _pending approval_ |
| Document owner | Quality Manager (role to be assigned) |
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

Define how nonconformities and potential nonconformities are addressed by corrective action
(to eliminate causes of detected problems) and preventive action (to eliminate causes of potential
problems), per ISO 13485:2016 §8.5.2 and §8.5.3.

## 2. Scope

Applies to nonconformities from any source: internal audits, software anomalies, nonconforming
product, complaints, management review, and SOUP anomalies.

## 3. Sources and relationship to software problem resolution

Software defects are recorded and resolved through the
[Software Problem Resolution Process](../IEC/Software_Problem_Resolution_Process_IEC62304.md)
(IEC 62304 §9). A CAPA is raised when a problem (or trend of problems) indicates a systemic cause
requiring action beyond the individual fix.

## 4. Procedure

1. **Identify and record** the (potential) nonconformity with a unique CAPA ID.
2. **Evaluate** the need for action and its significance, including risk impact
   ([SOP-007](SOP-007_Risk_Management.md)).
3. **Investigate root cause.**
4. **Plan and implement** corrective/preventive action proportionate to the risk.
5. **Verify** that the action is effective and introduces no new hazard.
6. **Record** results; **close** the CAPA; feed status to
   [management review](SOP-002_Management_Review.md).

Actions affecting design/safety shall be assessed under IEC 62304 §7.4 (risk management of
software changes) before implementation.

## 5. CAPA record (minimum fields)

| Field | Notes |
|---|---|
| CAPA ID / date / owner | Unique identifier. |
| Source & description | Where it came from; what the problem is. |
| Risk evaluation | Severity/impact; link to hazards/risk controls. |
| Root cause | Investigation outcome. |
| Action plan | Corrective and/or preventive, with due dates. |
| Effectiveness verification | Evidence the action worked and added no new risk. |
| Closure | Approver, date. |

## 6. Records produced

- CAPA records (retained per [SOP-001](SOP-001_Document_and_Record_Control.md)); CAPA log/index.

## 7. References

- ISO 13485:2016 §8.5.2, §8.5.3; IEC 62304 §9, §7.4;
  [Software Problem Resolution Process](../IEC/Software_Problem_Resolution_Process_IEC62304.md).

## Revision history

| Revision | Date | Author | Description |
|---|---|---|---|
| 0.1 | 2026-06-30 | Claude (AI draft) | Initial scaffold. |
