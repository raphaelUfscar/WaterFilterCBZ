# WaterFilterCBZ — IEC 62304 Class C Compliance Roadmap

Tracks the work remaining to reach a defensible **IEC 62304 software safety Class C** baseline. It complements the lifecycle documents under [docs/IEC/](IEC/README.md): this file is the *action/gating* view, while the risk file, SRS, and architecture doc are the authoritative records.

**Status legend:** ✅ Done · ⚠️ Partial · ❌ Not started · 🔵 Decision pending _(last verified against code: 2026-06-05)_

Identifier scheme (shared with [docs/IEC](IEC/README.md)): `HAZ-*` hazard · `RC-*` risk control · `SRS-*` requirement · `OAI-*` open input.

## 1. Done (this branch)

- ✅ **Safety class assigned: C** — [Safety Classification](IEC/Software_Safety_Classification_IEC62304.md) (OAI-001).
- ✅ **Lifecycle documentation set** authored (clauses 5.1–5.4, 5.5–5.7, 6, 7, 8, 9) — see [docs/IEC/README.md](IEC/README.md).
- ✅ **RC-002 / SRS-C-001 — stale-data supervision** (HAZ-002): 5 s per-sensor freshness, UI flag, transition logging. Verified.
- ✅ **RC-008 / SRS-C-003 — two-tier value validation** (HAZ-001): reject implausible / flag out-of-spec, per-sensor rejected counter, transition logging. Verified. _Range defaults still to be confirmed — see §3._
- ✅ **Detailed Design (Clause 5.4)** authored for every unit + interface.
- ✅ Min/Max sentinel defect fixed (found against real hardware) — Clause 9 in action.

## 2. Remaining risk controls (release-gating)

Until these are implemented **and verified**, residual risk for HAZ-001/003/004/005 is not acceptable for a Class C release (per the [Risk Management File](IEC/Software_Risk_Management_IEC62304.md)).

| Priority | Item | Control / Req | Hazard | Status | Notes |
|---|---|---|---|---|---|
| P1 | Receive-buffer maximum length (drop + log on overflow) | RC-005 / SRS-C-004 | HAZ-005 | ✅ | `MAX_RECEIVE_BUFFER_BYTES` (4096) cap in `SerialPortService.ParseReceiveBuffer`; verified by `SerialPortServiceFramingTests` (2026-06-30). |
| P1 | Device identity + serial protocol/schema version check | RC-003 / SRS-C-002 | HAZ-003 | ❌ | Needs firmware-side scheme (OAI-006). Also enables using the decoded `UNIT_ID`. |
| P2 | Processing-task failure supervision (surface degraded/error state) | RC-009 / SRS-C-005 | HAZ-004 | ✅ | `SerialPortService.ProcessingFaulted` → `SensorViewModel` degraded state + explicit reconnect; verified by `SerialPortServiceFramingTests` + `SensorViewModelProcessingFaultTests` (2026-06-30). |
| P2 | Verified failure-state taxonomy in UI | RC-010 / SRS-C-006 | HAZ-001..005 | ⚠️ | disconnected/connecting/connected/stale/out-of-spec/invalid exist; parser-error, device-mismatch, processing-fault states still to define + test. |
| P3 | Configuration protection + audit (port, baud, ranges, timeout) | RC-011 / SRS-C-007 | — | ❌ | Validated load + change audit trail. Pairs with persistent config (see features roadmap §3). |
| P3 | Stronger integrity code (CRC vs 8-bit additive checksum) | RC-001b / SRS-C-008 | HAZ-001 | 🔵 | Decide per risk analysis; current additive checksum may be sufficient. |

## 3. Open inputs to confirm (block acceptance values, not mechanisms)

| Item | Needed for | Status |
|---|---|---|
| Per-sensor numeric ranges + `SENSOR_ID`→parameter mapping confirmation | RC-008 acceptance criteria | ⚠️ Defaults implemented in `SensorParameterRegistry`; **confirm against the device specification** (current defaults are pharma purified-water placeholders). |
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

| ID | Gap | Status | Note |
|---|---|---|---|
| QMS-001 | No **ISO 13485 quality management system** wrapping these documents | ❌ | IEC 62304 is meaningless to an auditor outside a QMS. Highest-priority structural gap. |
| QMS-002 | No device-level **ISO 14971 risk management file**; software severities are provisional and intended use is assumed | ❌ | Confirm intended use → real hazard analysis → fix severities (links OAI-002, OAI-003). |
| QMS-003 | **Document control** is informal (markdown revision tables) — no controlled document IDs, approval signatures, or review records | ❌ | Promote docs from Draft via recorded reviews + approvals by qualified personnel. |
| QMS-004 | **Verification records** are automated test runs only — no witnessed/approved protocols & results, no independence evidence | ⚠️ | Tie CI evidence to approved protocols; record reviewer ≠ author for safety-relevant changes. |
| QMS-005 | **Usability engineering file (IEC 62366-1)** not addressed | ❌ | Required for a real submission. |
| QMS-006 | **Cybersecurity (IEC 81001-5-1 / premarket guidance)** and a formal **SBOM** not addressed | ❌ | Threat model, secure-by-design evidence, SBOM generation. |
| QMS-007 | **Clinical evaluation / real intended-use statement** by a manufacturer absent | ❌ | Drives classification and severity; currently assumed. |
| QMS-008 | **SOUP anomaly-monitoring process** not operating (inventory exists, process does not) | ⚠️ | Stand up periodic SOUP anomaly/security review per the maintenance plan. |
| QMS-009 | Documents are **AI-drafted and unreviewed** by qualified humans | ❌ | Mandatory qualified author/reviewer ownership and sign-off. |

> Rough audit-readiness today ≈ 15–25% even though software-clause *document coverage* ≈ 60–70%. Closing §2 (risk controls) does **not** close these; QMS-001/002/003/009 are the critical path.

## 8. Suggested next steps (order)

Software-engineering track (this repo):

1. ~~**RC-005 buffer cap**~~ — done (2026-06-30); removed the denial-of-service/HAZ-005 gap.
2. **Confirm RC-008 ranges + `SENSOR_ID` map** against the real device (§3) — turns the validation from placeholder to real.
3. **RC-003 device/protocol identity** (needs OAI-006) — also unlocks `UNIT_ID` use.
4. ~~**RC-009 task-failure supervision**~~ — done (2026-06-30); then **RC-010 failure-state taxonomy** completion.
5. **RC-011 config protection** + persistent configuration.
6. Fill the **release record** and promote docs from Draft.

Compliance track (organizational, prerequisite for any audit): address §7 — start with QMS-001 (ISO 13485), QMS-002 (ISO 14971), QMS-003/009 (document control + qualified review).

## 9. Revision history

| Revision | Date | Author | Description |
|---|---|---|---|
| 0.1 | 2026-06-05 | Claude | Initial Class C compliance roadmap: done items, remaining release-gating risk controls, open inputs, documentation/verification gaps, code-quality notes, and ordered next steps. |
| 0.2 | 2026-06-05 | Claude | De-duplicated: §5 verification gaps now point to the merged [Project Roadmap — Testing](project_roadmap.md#testing) instead of restating the test backlog (single source of truth). |
| 0.3 | 2026-06-05 | Claude | Added §7 QMS / process gaps (QMS-001..009) capturing the audit-readiness blockers from the [effort report](IEC62304_effort_report.md); split next steps into software vs compliance tracks. |
