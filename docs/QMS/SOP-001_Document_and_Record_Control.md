# SOP-001 — Document and Record Control

| Field | Value |
|---|---|
| Document ID | SOP-001 |
| Title | Document and Record Control |
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

Define how QMS documents and quality records are created, reviewed, approved, identified,
distributed, changed, and retained, satisfying ISO 13485:2016 §4.2.4 (control of documents) and
§4.2.5 (control of records).

## 2. Scope

All controlled documents in the QMS (this folder) and the design/technical documents listed in the
[Controlled Document Register](Controlled_Document_Register.md), and all quality records produced
by the QMS procedures.

## 3. Responsibilities

| Role | Responsibility |
|---|---|
| Document owner | Maintains content; initiates reviews and changes. |
| Reviewer | Independently checks correctness/adequacy. Shall not be the sole author for safety-relevant documents. |
| Approver | Authorizes the document for use; for the Quality Manual this is Top Management. |
| Quality Manager | Maintains the Controlled Document Register; ensures only current versions are in use. |

## 4. Controlled-document header (mandatory)

Every controlled document shall carry a header block with: **Document ID, Title, Revision, Status,
Effective date, Document owner, Classification, Next review due**, an **Approval** block
(author / reviewer / approver with signature + date), and a **Revision history** table. A document
is **effective only when the approver signature and effective date are completed**.

## 5. Procedure

### 5.1 Identification and numbering
- Quality Manual: `QM-001`. Procedures: `SOP-nnn`. Registers: `QMS-REG-nnn`. Records/templates:
  `QMS-REC-nnn`. Design documents: `DOC-IEC-nnn` (see register).
- Each document has exactly one ID for its lifetime; superseded documents keep their ID with an
  incremented revision.

### 5.2 Creation, review, approval
1. Owner drafts at revision `0.x` with status **Draft**.
2. Independent reviewer checks; comments are resolved and recorded.
3. Approver authorizes; revision is set to the next integer (e.g. `1.0`), status **Approved**,
   effective date completed, and the Register updated.

### 5.3 Changes
- Any change requires the same review/approval path. Revision increments (minor: `x.y`; major:
  integer). The Revision history records what changed, by whom, when, and why.
- Changes affecting safety-relevant design documents shall be assessed under
  [SOP-007 Risk Management](SOP-007_Risk_Management.md) and IEC 62304 §7.4.

### 5.4 Distribution and obsolete documents
- The Register is the single source of current revision/status. Obsolete documents are marked
  **Superseded** and retained for history; they shall not be used for current work.

### 5.5 Records
- Records are legible, identifiable, and retrievable. Retention period: _defined by the
  manufacturer; for medical devices typically the device lifetime plus the regulatory minimum._
- Electronic records (e.g. CI logs, git history, application logs) are acceptable where their
  integrity and retention can be demonstrated.

## 6. Records produced by this SOP

- Controlled Document Register (QMS-REG-001).
- Review/approval evidence (signatures in each document's approval block; review comment records).

## 7. References

- ISO 13485:2016 §4.2.4, §4.2.5.
- [Controlled Document Register](Controlled_Document_Register.md);
  [Software Configuration Management Plan](../IEC/Software_Configuration_Management_Plan_IEC62304.md).

## Revision history

| Revision | Date | Author | Description |
|---|---|---|---|
| 0.1 | 2026-06-30 | Claude (AI draft) | Initial scaffold. |
