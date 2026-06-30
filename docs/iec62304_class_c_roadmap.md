# WaterFilterCBZ — IEC 62304 Class C Compliance Roadmap

Tracks the work remaining to reach a defensible **IEC 62304 software safety Class C** baseline. It complements the lifecycle documents under [docs/IEC/](IEC/README.md): this file is the *action/gating* view, while the risk file, SRS, and architecture doc are the authoritative records.

**Status legend:** ✅ Done · 🔶 Drafted/partial · ⚠️ Partial · ❌ Not started · 🔵 Decision pending _(last verified against code: 2026-06-30)_

Identifier scheme (shared with [docs/IEC](IEC/README.md)): `HAZ-*` hazard · `RC-*` risk control · `SRS-*` requirement · `OAI-*` open input.

## 1. Done (on `main`)

- ✅ **Safety class assigned: C** — [Safety Classification](IEC/Software_Safety_Classification_IEC62304.md) (OAI-001).
- ✅ **Lifecycle documentation set** authored (clauses 5.1–5.4, 5.5–5.7, 6, 7, 8, 9) — see [docs/IEC/README.md](IEC/README.md).
- ✅ **RC-002 / SRS-C-001 — stale-data supervision** (HAZ-002): 5 s per-sensor freshness, UI flag, transition logging. Verified.
- ✅ **RC-008 / SRS-C-003 — two-tier value validation** (HAZ-001): reject implausible / flag out-of-spec, per-sensor rejected counter, transition logging. Verified. Ranges now **user-configurable** (`sensor-ranges.json`, per-field fallback to defaults); _numeric defaults still to be confirmed — see §3._
- ✅ **RC-005 / SRS-C-004 — receive-buffer cap** (HAZ-005): 4096-byte cap, drop+reset+log on overflow. Verified (PR #22, 2026-06-30).
- ✅ **RC-009 / SRS-C-005 — processing-task fault surfacing** (HAZ-004): `ProcessingFaulted` → degraded state + explicit reconnect. Verified (PR #23, 2026-06-30).
- ✅ **Detailed Design (Clause 5.4)** authored for every unit + interface.
- ✅ Min/Max sentinel defect fixed (found against real hardware) — Clause 9 in action.
- 🔶 **Draft ISO 13485 QMS scaffold** — [docs/QMS/](QMS/) (Quality Manual + 9 SOPs + register); unapproved drafts, see §7.

## 2. Remaining risk controls (release-gating)

Until these are implemented **and verified**, residual risk for HAZ-001/003/004/005 is not acceptable for a Class C release (per the [Risk Management File](IEC/Software_Risk_Management_IEC62304.md)).

| Priority | Item | Control / Req | Hazard | Status | Notes |
|---|---|---|---|---|---|
| P1 | Receive-buffer maximum length (drop + log on overflow) | RC-005 / SRS-C-004 | HAZ-005 | ✅ | `MAX_RECEIVE_BUFFER_BYTES` (4096) cap in `SerialPortService.ParseReceiveBuffer`; verified by `SerialPortServiceFramingTests` (2026-06-30). |
| P1 | Device identity + serial protocol/schema version check | RC-003 / SRS-C-002 | HAZ-003 | ❌ | Needs firmware-side scheme (OAI-006). Also enables using the decoded `UNIT_ID`. |
| P2 | Processing-task failure supervision (surface degraded/error state) | RC-009 / SRS-C-005 | HAZ-004 | ✅ | `SerialPortService.ProcessingFaulted` → `SensorViewModel` degraded state + explicit reconnect; verified by `SerialPortServiceFramingTests` + `SensorViewModelProcessingFaultTests` (2026-06-30). |
| P2 | Verified failure-state taxonomy in UI | RC-010 / SRS-C-006 | HAZ-001..005 | ✅ | `MonitoringState` enum + pure resolver + colour-coded status chip; all 8 states defined, derived, unit-verified (2026-06-30). Parser-error/device-mismatch detectors land with RC-003 + parser supervisor (hooks present). |
| P3 | Configuration protection + audit (port, baud, ranges, timeout) | RC-011 / SRS-C-007 | — | ❌ | Validated load + change audit trail. Pairs with persistent config (see features roadmap §3). |
| P3 | Stronger integrity code (CRC vs 8-bit additive checksum) | RC-001b / SRS-C-008 | HAZ-001 | 🔵 | Decide per risk analysis; current additive checksum may be sufficient. |

## 3. Open inputs to confirm (block acceptance values, not mechanisms)

| Item | Needed for | Status |
|---|---|---|
| Per-sensor numeric ranges + `SENSOR_ID`→parameter mapping confirmation | RC-008 acceptance criteria | ⚠️ Defaults implemented in `SensorParameterRegistry` and now **user-configurable** via `sensor-ranges.json`; the *mechanism* is done, but the **numeric values must still be confirmed against the device specification** (current defaults are pharma purified-water placeholders). |
| Device-level harm severities (ISO 14971) | Risk evaluation in [Risk file](IEC/Software_Risk_Management_IEC62304.md) | ⚠️ Severities are provisional (OAI-002 intended use confirmed; severities still to fix). |
| Maximum sustained sample rate + comms-loss timeout | SRS-NF-001, RC-004 acceptance | ⚠️ Timeout fixed at 5 s; max rate (OAI-004) still open. |
| Firmware protocol versioning / device identity scheme | RC-003 / SRS-C-002 design | ❌ (OAI-006) |

## 4. Documentation completeness

- ✅ Clauses 4.3, 5.1–5.4, 5.5–5.7, 6, 7, 8, 9 have draft deliverables.
- ⚠️ **Release record (Clause 5.8)** — template exists in the [Development Plan §8](IEC/Software_Development_Plan_IEC62304.md); to be filled at the first controlled release (known-anomalies list + SOUP versions + sign-off).
- ⚠️ All IEC docs are **Draft**; promote to Reviewed/Approved once the device-level inputs (§3) are fixed.
- ⚠️ **Segregation rationale** — no validated segregation between safety and non-safety items yet, so the whole system is Class C; revisit if down-classification of charting/cosmetic items is desired ([Architecture §3](IEC/Software_Architecture_Design_IEC62304.md)).

## 5. Verification gaps

The test backlog and status (parser rejection/resync unit tests, headless `SerialPortService` ↔ `SensorViewModel` integration test, sustained-rate/stress and malformed-stream system tests) is owned by the [Project Roadmap — Testing](project_roadmap.md#testing). It is not restated here.

Class C gating note: each remaining risk control in §2 must have positive **and** negative tests before its RC can be marked **Verified** in the [Risk Management File](IEC/Software_Risk_Management_IEC62304.md).

## 6. Code quality / tooling notes

- 🔵 **SonarCloud smell** on `SensorViewModel._staleTimer` ("declare as local variable") is a **false positive** — the `DispatcherTimer` must be held in a field or it is garbage-collected and stops ticking. Action: leave as-is, or add a justified suppression with a comment if a clean Sonar report is required.
- CI runner notice: `windows-latest` redirects to `windows-2025-vs2026` by 2026-06-15; no action needed unless pinning is desired.

## 7. QMS / process gaps (audit-readiness, beyond the software clauses)

IEC 62304 documents are necessary but **not sufficient** for an official audit (notified body / FDA): they live inside a quality system and a design history file with controlled, reviewed, approved, evidence-backed records. The current set is a strong *scaffold* but the surrounding compliance system is largely absent. These gaps are the dominant blockers to audit-readiness and are tracked here so they are not mistaken for "done". See the [effort report §4](IEC62304_effort_report.md) for the full assessment.

> A **draft ISO 13485 QMS document scaffold** now exists at [docs/QMS/](QMS/) (Quality Manual + 9 SOPs + controlled-document register). It gives the system a structure but, by itself, **does not close any gap below**: the documents are unapproved AI drafts, no roles are assigned, and no records exist. It moves QMS-001/QMS-003 from "absent" to "drafted, pending adoption and qualified sign-off".

| ID | Gap | Status | Note |
|---|---|---|---|
| QMS-001 | No **ISO 13485 quality management system** wrapping these documents | 🔶 | **Draft scaffold added (2026-06-30):** [docs/QMS/](QMS/) — Quality Manual + 9 SOPs + controlled-document register, cross-linked to the IEC set. **Not closed:** documents are unapproved AI drafts (QMS-009); the system is not operating (no assigned roles, no records). |
| QMS-002 | No device-level **ISO 14971 risk management file**; software severities are provisional and intended use is assumed | ❌ | Confirm intended use → real hazard analysis → fix severities (links OAI-002, OAI-003). [SOP-007](QMS/SOP-007_Risk_Management.md) defines the process; the device-level file is still missing. |
| QMS-003 | **Document control** is informal (markdown revision tables) — no controlled document IDs, approval signatures, or review records | 🔶 | **Procedure + register drafted:** [SOP-001](QMS/SOP-001_Document_and_Record_Control.md) defines a controlled-document header (IDs, approval/signature blocks, effective dates) and the [register](QMS/Controlled_Document_Register.md) lists all docs. **Not closed:** signatures blank; IEC docs not yet migrated to the controlled header; no recorded reviews/approvals. |
| QMS-004 | **Verification records** are automated test runs only — no witnessed/approved protocols & results, no independence evidence | ⚠️ | Tie CI evidence to approved protocols; record reviewer ≠ author for safety-relevant changes. |
| QMS-005 | **Usability engineering file (IEC 62366-1)** not addressed | ❌ | Required for a real submission. |
| QMS-006 | **Cybersecurity (IEC 81001-5-1 / premarket guidance)** and a formal **SBOM** not addressed | ❌ | Threat model, secure-by-design evidence, SBOM generation. |
| QMS-007 | **Clinical evaluation / real intended-use statement** by a manufacturer absent | ❌ | Drives classification and severity; currently assumed. |
| QMS-008 | **SOUP anomaly-monitoring process** not operating (inventory exists, process does not) | ⚠️ | Stand up periodic SOUP anomaly/security review per the maintenance plan. |
| QMS-009 | Documents are **AI-drafted and unreviewed** by qualified humans | ❌ | Mandatory qualified author/reviewer ownership and sign-off. |
| QMS-010 | **Controlled traceability matrix** (Hazard → RC → Requirement → Architecture → Unit → Verification) maintained as scattered tables, not one tool-backed, version-controlled matrix | 🔶 | **Consolidated matrix drafted (2026-06-30):** [DOC-IEC-011 Traceability Matrix](IEC/Traceability_Matrix.md). **Not closed:** maintained by hand; the tool-backed CI generation/consistency check is still pending (see its §4). |
| QMS-011 | **Software validation against intended use** (distinct from verification) not performed | ❌ | IEC 62304 5.x / ISO 13485 7.3.7: demonstrate the software meets *user needs / intended use*, not just specified requirements. Blocked on a real intended-use statement (QMS-007). |
| QMS-012 | **Validation of software tools** used in development/test/QMS (compiler/test framework/CI) not addressed | ❌ | ISO 13485 §4.1.6: tools whose output is relied on must be validated for intended use, with records. Define which tools need validation and to what depth. |
| QMS-013 | **Regulatory strategy & market classification** undefined (MDR vs FDA, SaMD determination, applicable rule/class) | ❌ | Determines which standards/submission apply; currently only an internal IEC 62304 safety class exists. Drives QMS-005/006/007 scope. |
| QMS-014 | **Post-market surveillance / vigilance & feedback** process not established | ❌ | ISO 13485 §8.2.1 + regulatory PMS/vigilance: collect field feedback, complaints, and incidents and feed risk management/CAPA. Maintenance plan covers SOUP only. |
| QMS-015 | **Design History File (DHF) / design records consolidation** not assembled | ⚠️ | The IEC + QMS docs are the raw material; assemble and index them as a controlled DHF (links to [Controlled Document Register](QMS/Controlled_Document_Register.md)) with review/approval and release records. |

> Rough audit-readiness today ≈ 15–25% even though software-clause *document coverage* ≈ 60–70%. Closing §2 (risk controls) does **not** close these; **QMS-001/002/003/007/009 are the critical path** — without a confirmed intended use (QMS-007) and qualified, recorded human ownership (QMS-009), most other gaps cannot be genuinely closed. The QMS scaffold (§7 intro) provides structure but no evidence.

## 8. Suggested next steps (order)

Software-engineering track (this repo):

1. ~~**RC-005 buffer cap**~~ — done (2026-06-30).
2. ~~**RC-008 ranges user-configurable**~~ — mechanism done (2026-06-30); still **confirm the numeric values** against the real device (§3).
3. ~~**RC-009 task-failure supervision**~~ — done (2026-06-30).
4. ~~**RC-010 failure-state taxonomy**~~ — done (2026-06-30); `MonitoringState` + colour-coded chip. (Parser-error/device-mismatch detectors arrive with RC-003 + parser supervisor.)
5. **RC-011 config protection** + persistent configuration — now also covers integrity of `sensor-ranges.json`.
6. **RC-003 device/protocol identity** (needs OAI-006) — also unlocks `UNIT_ID` use.
7. **RC-004 sustained-rate stress test** — closes the HAZ-004 verification gap.
8. Fill the **release record** (Clause 5.8) and promote docs from Draft.

Compliance track (organizational, prerequisite for any audit): address §7. QMS-001/003 now have a **draft scaffold** at [docs/QMS/](QMS/); the next moves are organizational, not authoring — assign qualified roles, adopt the SOPs, perform recorded reviews/approvals (QMS-009), migrate the IEC docs onto the controlled-document header, then stand up QMS-002 (device ISO 14971) and the operating records.

## 8a. Phased path to a "decent audit" project

A pragmatic ordering from "good scaffold" to "credible audit candidate". Each phase is gated by the previous one; software work (§2) proceeds in parallel but cannot substitute for the organizational phases.

| Phase | Goal | Key items | Exit criteria |
|---|---|---|---|
| **0 — Foundation (now)** | Honest, code-backed scaffold | §1 done items; QMS scaffold; this roadmap | Gaps known and tracked (this document) |
| **1 — Define the product** | Stop assuming | QMS-007 intended use; QMS-013 regulatory strategy/classification | A manufacturer-owned intended-use statement and target-market pathway exist |
| **2 — Stand up the QMS** | Make the system real, not paper | QMS-001/003/009 (assign roles, adopt SOPs, recorded reviews/approvals, controlled-doc header on IEC docs), QMS-012 tool validation | QMS operating with records; documents Approved, not Draft |
| **3 — Real risk basis** | Replace provisional severities | QMS-002 device ISO 14971 file; finalize RC-008 numeric ranges (§3); QMS-011 validation vs intended use | Residual-risk acceptability and benefit–risk conclusion can be stated |
| **4 — Complete the controls & evidence** | Close the technical gaps | Remaining §2 RCs (RC-003/010/011, RC-001b decision), RC-004 stress test, QMS-004 verification records, QMS-010 traceability matrix, release record (5.8) | Every RC Verified with positive+negative tests; one controlled release |
| **5 — Companion standards & lifecycle** | Submission-grade | QMS-005 usability (IEC 62366-1), QMS-006 cybersecurity + SBOM, QMS-008 SOUP anomaly monitoring, QMS-014 post-market surveillance, QMS-015 DHF assembly | Companion files exist and PMS/vigilance operating |

> Phases 1–2 are the true unlock: they are **organizational, not coding**, and most remaining gaps depend on them. AI/code work can keep advancing §2 and drafting templates, but a "decent audit project" begins when a qualified manufacturer owns the intended use, the QMS operates with records, and the device-level risk file is real.

## 9. Revision history

| Revision | Date | Author | Description |
|---|---|---|---|
| 0.1 | 2026-06-05 | Claude | Initial Class C compliance roadmap: done items, remaining release-gating risk controls, open inputs, documentation/verification gaps, code-quality notes, and ordered next steps. |
| 0.2 | 2026-06-05 | Claude | De-duplicated: §5 verification gaps now point to the merged [Project Roadmap — Testing](project_roadmap.md#testing) instead of restating the test backlog (single source of truth). |
| 0.3 | 2026-06-05 | Claude | Added §7 QMS / process gaps (QMS-001..009) capturing the audit-readiness blockers from the [effort report](IEC62304_effort_report.md); split next steps into software vs compliance tracks. |
| 0.4 | 2026-06-30 | Claude | Refreshed status to current code (RC-005, RC-009 done; RC-008 ranges configurable; QMS draft scaffold). Added missing audit-readiness gaps QMS-010..015 (traceability matrix, validation vs intended use, tool validation, regulatory strategy, post-market surveillance, DHF). Added §8a phased path to a decent-audit project. |
