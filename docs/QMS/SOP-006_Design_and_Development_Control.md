# SOP-006 — Design and Development Control

| Field | Value |
|---|---|
| Document ID | SOP-006 |
| Title | Design and Development Control |
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

Define how software design and development is planned, controlled, reviewed, verified, validated,
and released, per ISO 13485:2016 §7.3, implemented through the IEC 62304 software life-cycle.

## 2. Scope

The design and development of WaterFilterCBZ software.

## 3. Method: IEC 62304 life-cycle

This SOP adopts the IEC 62304 process documented in [docs/IEC/](../IEC/). The mapping of ISO 13485
§7.3 stages to the IEC 62304 documents is the design-control backbone:

| ISO 13485 §7.3 stage | IEC 62304 artifact |
|---|---|
| 7.3.2 Planning | [Software Development Plan](../IEC/Software_Development_Plan_IEC62304.md) |
| 7.3.3 Inputs | [Software Requirements Specification](../IEC/Software_Requirements_Specification_IEC62304.md); safety class ([classification](../IEC/Software_Safety_Classification_IEC62304.md)) |
| 7.3.4 Outputs | [Architecture](../IEC/Software_Architecture_Design_IEC62304.md); [Detailed Design](../IEC/Software_Detailed_Design_IEC62304.md); source code |
| 7.3.5 Review | Design reviews; PR review (independent reviewer ≠ sole author for safety-relevant changes) |
| 7.3.6 Verification | [Verification and Test Plan](../IEC/Software_Verification_and_Test_Plan_IEC62304.md); unit/integration/system tests in CI |
| 7.3.7 Validation | Validation against intended use _(pending real intended-use statement, OAI-002 / QMS-002)_ |
| 7.3.8 Transfer | Release/build process (SCM) |
| 7.3.9 Change control | IEC 62304 §7.4; [SOP-001 §5.3](SOP-001_Document_and_Record_Control.md); [CAPA](SOP-003_Corrective_and_Preventive_Action.md) |
| 7.3.10 Design file | The [docs/IEC/](../IEC/) set + this QMS = the design history (see [Register](Controlled_Document_Register.md)) |

## 4. Risk management integration

Risk management ([SOP-007](SOP-007_Risk_Management.md) / ISO 14971) runs throughout design and
development; risk controls are traced in the
[Software Risk Management File](../IEC/Software_Risk_Management_IEC62304.md).

## 5. Reviews and independence

Design reviews and code reviews are recorded. For safety-relevant changes (Class C), the reviewer
shall not be the sole author. Verification independence is addressed in the
[Verification and Test Plan §2.1](../IEC/Software_Verification_and_Test_Plan_IEC62304.md).

## 6. Records produced

- Design plan, inputs, outputs, review records, verification/validation results, change records —
  retained per [SOP-001](SOP-001_Document_and_Record_Control.md).

## 7. Honest status

The IEC 62304 *documents* exist as drafts and substantial software-clause coverage is in place, but
design **validation** against a confirmed intended use is pending (QMS-002), and review/approval
records are not yet formalized (QMS-003/QMS-004).

## 8. References

- ISO 13485:2016 §7.3; IEC 62304:2006+AMD1:2015; the [docs/IEC/](../IEC/) set.

## Revision history

| Revision | Date | Author | Description |
|---|---|---|---|
| 0.1 | 2026-06-30 | Claude (AI draft) | Initial scaffold. |
