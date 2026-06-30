# SOP-004 — Control of Nonconforming Product

| Field | Value |
|---|---|
| Document ID | SOP-004 |
| Title | Control of Nonconforming Product |
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

Ensure product that does not conform to requirements is identified and controlled to prevent
unintended use or release, per ISO 13485:2016 §8.3.

## 2. Scope

Applies to nonconforming WaterFilterCBZ software at any stage: a build/release failing
verification, a released version found defective, or a configuration item that does not meet its
requirement.

## 3. What "nonconforming product" means for software

A software release (or candidate) is nonconforming when it fails to meet a specified requirement or
a safety-relevant acceptance criterion (e.g. a failed verification in the
[Verification and Test Plan](../IEC/Software_Verification_and_Test_Plan_IEC62304.md), an
unresolved Class C anomaly, or a defective released build).

## 4. Procedure

1. **Identify and segregate** the nonconforming item (e.g. mark the build/release as not for use;
   prevent distribution).
2. **Document** the nonconformity (NCR record) and notify relevant roles.
3. **Evaluate** disposition options:
   - **Correct/rework** then re-verify before release;
   - **Withhold/quarantine** (do not release);
   - **Concession** (use as-is) — only with documented risk-based justification and authorization;
     not permitted for unresolved Class C safety issues.
4. **Re-verify** corrected product against the affected requirements/risk controls.
5. If released product is affected, evaluate the need for **field action/advisory notice** per the
   manufacturer's regulatory obligations, and raise a [CAPA](SOP-003_Corrective_and_Preventive_Action.md).

## 5. NCR record (minimum fields)

NCR ID/date; description; affected version/configuration item; risk evaluation; disposition and
justification; re-verification evidence; authorization; closure.

## 6. Records produced

- Nonconformity records (NCRs); disposition authorizations — retained per
  [SOP-001](SOP-001_Document_and_Record_Control.md).

## 7. References

- ISO 13485:2016 §8.3; IEC 62304 §5.8 (release), §9 (problem resolution);
  [SOP-003 CAPA](SOP-003_Corrective_and_Preventive_Action.md).

## Revision history

| Revision | Date | Author | Description |
|---|---|---|---|
| 0.1 | 2026-06-30 | Claude (AI draft) | Initial scaffold. |
