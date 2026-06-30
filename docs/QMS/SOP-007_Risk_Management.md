# SOP-007 — Risk Management

| Field | Value |
|---|---|
| Document ID | SOP-007 |
| Title | Risk Management |
| Revision | 0.1 (Draft) |
| Status | **Draft — not approved for use** |
| Effective date | _pending approval_ |
| Document owner | Risk Manager (role to be assigned) |
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

Define the risk management process applied throughout the product life-cycle, per ISO 14971:2019
and ISO 13485:2016 §7.1, including the software-specific risk activities of IEC 62304 §7.

## 2. Scope

Risks arising from the WaterFilterCBZ product and its software, across design, release, and
maintenance.

## 3. Process (ISO 14971)

1. **Risk management planning** and a risk management file.
2. **Risk analysis:** identify intended use and characteristics; identify hazards and hazardous
   situations; estimate risk.
3. **Risk evaluation** against acceptability criteria.
4. **Risk control:** select and implement measures; verify implementation and effectiveness;
   evaluate residual risk and new risks introduced.
5. **Overall residual risk evaluation** and benefit–risk determination.
6. **Risk management review** and report.
7. **Production and post-production** information feeding back into risk management.

## 4. Software-specific risk activities (IEC 62304 §7)

Software contributions to hazardous situations, software risk controls, their verification, and the
risk management of software changes are documented in the
[Software Risk Management File](../IEC/Software_Risk_Management_IEC62304.md).

## 5. Critical dependency — device-level ISO 14971 file (QMS-002)

> The software risk file currently uses **provisional** severities and an **assumed** intended use.
> A device-level ISO 14971 risk management file, based on a **confirmed intended use** (OAI-002),
> is required and does not yet exist (**QMS-002**). Until it does, residual-risk acceptability and
> benefit–risk conclusions **cannot** be finalized. This SOP defines the process; the device-level
> file is the missing organizational input.

## 6. Acceptability criteria

Provisional criteria are stated in the
[Software Risk Management File §2.1](../IEC/Software_Risk_Management_IEC62304.md); they must be
ratified at device level. Per IEC 62304/ISO 14971, the probability of a software fault is assumed
to be 1.

## 7. Records produced

- Risk management plan; risk management file; risk control verification evidence; risk management
  report; review records — retained per [SOP-001](SOP-001_Document_and_Record_Control.md).

## 8. References

- ISO 14971:2019; ISO 13485:2016 §7.1; IEC 62304 §7;
  [Software Risk Management File](../IEC/Software_Risk_Management_IEC62304.md).

## Revision history

| Revision | Date | Author | Description |
|---|---|---|---|
| 0.1 | 2026-06-30 | Claude (AI draft) | Initial scaffold. |
