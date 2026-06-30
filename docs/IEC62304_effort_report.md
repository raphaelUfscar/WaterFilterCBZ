# IEC 62304 Documentation Effort — Final Report

_WaterFilterCBZ · prepared 2026-06-05_

---

## 1. What was created

### 1.1 IEC 62304 lifecycle document set (`docs/IEC/`)

| Clause | File | Status |
|---|---|---|
| 4.3 | `Software_Safety_Classification_IEC62304.md` | New |
| 5.1 | `Software_Development_Plan_IEC62304.md` | New |
| 5.2 | `Software_Requirements_Specification_IEC62304.md` | New |
| 5.3 | `Software_Architecture_Design_IEC62304.md` | Pre-existing, updated to Class C |
| 5.4 | `Software_Detailed_Design_IEC62304.md` | New |
| 5.5–5.7 | `Software_Verification_and_Test_Plan_IEC62304.md` | New |
| 6 | `Software_Maintenance_Plan_IEC62304.md` | New |
| 7 | `Software_Risk_Management_IEC62304.md` | New |
| 8 | `Software_Configuration_Management_Plan_IEC62304.md` | New |
| 9 | `Software_Problem_Resolution_Process_IEC62304.md` | New |
| — | `README.md` (index + identifier scheme) | New |

### 1.2 Supporting docs
- `docs/iec62304_class_c_roadmap.md` — compliance/gating view (remaining risk controls, open inputs).
- `docs/project_roadmap.md` — merged features + testing roadmap.
- `docs/ci_and_coverage.md` — CI/coverage/Sonar internals (moved out of README).

### 1.3 Code that backs the docs (not just paper)
- **RC-002 / SRS-C-001** stale-data supervision (5 s freshness, visual flag, logged transition).
- **RC-008 / SRS-C-003** two-tier value validation (reject implausible / flag out-of-spec) + per-sensor rejected counter.
- A hardware-found defect fix (Min/Max sentinel leak) with a regression test.
- Test count 42 → 76; new-code coverage gate satisfied.

---

## 2. The rules behind it

IEC 62304 is a **process** standard, not a product checklist. The work followed its core logic:

1. **Classify first (Clause 4.3).** Safety class (A/B/C) is assigned from the worst credible harm *before* crediting external controls. Class drives how much rigor every later clause demands. We assigned **C** conservatively from an assumed intended use.
2. **Clause → deliverable mapping.** Each clause (5.1–5.8, 6, 7, 8, 9) produces a specific artifact; Class C makes nearly all of them mandatory and requires detailed design of *every* unit.
3. **One traceability chain.** Everything threads through **Hazard → Risk Control → Requirement → Architecture → Unit → Verification**, enforced by a shared ID scheme (`HAZ-/RC-/SRS-/AE-/OAI-`).
4. **Risk management is the spine (Clause 7 ↔ ISO 14971).** Requirements that exist only to reduce risk (the `SRS-C-*` items) are first-class and must trace to a risk control and a verification.
5. **SOUP is explicit (Clause 5.3 / 7.1.2).** Third-party components are inventoried with versions and evaluated for failure contribution.
6. **Ground docs in code, not aspiration.** Every claim was checked against the actual source (constants, parser, CI), and unimplemented controls are labelled *Not implemented*, not glossed.
7. **Single source of truth.** Each fact lives in one document; others link rather than restate (applied to the roadmaps and README too).

---

## 3. How to reuse this in similar projects

This set is a strong **template kit** for any small embedded/desktop device-software repo. To replicate:

**Copy and re-parameterize**
1. Keep the file set and clause mapping in `docs/IEC/README.md` as the skeleton.
2. Re-run the **classification decision** for the new intended use — this is the only decision that changes everything downstream. Don't inherit "Class C" blindly.
3. Keep the **identifier scheme** and the **traceability chain**; they're project-agnostic.
4. Replace the **domain specifics**: hazards, sensor/parameter ranges, SOUP inventory, protocol, file/line references.

**Carry over the working method**
- Decide domain questions explicitly up front (intended use, ranges, timeouts, alarm policy) — these unblock the risk file and SRS.
- Mark status honestly (`✅/⚠️/❌/🔵`) and date it; treat unimplemented risk controls as a roadmap, not a claim.
- Implement the 1–2 highest-risk controls *with tests* so the docs aren't pure paper.
- Keep a compliance roadmap separate from the feature/test roadmap.

**Do not reuse blindly**
- Range defaults, severities, and the safety class are project-specific and were placeholders here.
- The revision tables are informal — a real project needs controlled document IDs/approvals (see §4).

A reasonable "starter kit" = the 11 `docs/IEC/` files with the project-specific tables emptied to templates + the ID scheme + the clause/deliverable table.

---

## 4. Honest assessment: how close is this to audit-passing professional software?

**Short answer: the *scaffolding* is genuinely good; the *compliance system* is largely absent. This would help a real certification effort start fast, but as-is it would not pass a notified-body / FDA audit — and isn't close.**

A real audit (e.g., MDR notified body, FDA) examines a **quality management system (ISO 13485)** and a **design history file** with controlled, reviewed, approved, signed, evidence-backed records — IEC 62304 documents are only *part* of that. The candid scorecard:

| Area | State | Gap to audit-ready |
|---|---|---|
| Document **structure & coverage** | Strong — all software clauses have a draft | Mostly cosmetic |
| **Traceability** model | Strong concept, present in tables | Needs a controlled, tool-backed matrix |
| Code-backed risk controls | 2 of ~11 implemented + tested | Most controls unimplemented |
| **Risk management** (ISO 14971) | Software-side draft, **provisional severities** | No device-level RM file; no real hazard analysis |
| **Intended use / clinical** | **Assumed**, not defined by a manufacturer | No clinical evaluation, no real intended-use statement |
| **QMS context (ISO 13485)** | **Draft scaffold** at [docs/QMS/](QMS/) (manual + 9 SOPs + register) | Unapproved AI drafts; no assigned roles, no operating records — structure only, not a working QMS |
| **Document control** | Procedure + controlled-document header + register **drafted** ([SOP-001](QMS/SOP-001_Document_and_Record_Control.md)) | Signatures blank; IEC docs not yet on the controlled header; no recorded reviews/approvals |
| **Verification records** | Automated tests pass | No witnessed/approved protocols & results, no independence evidence |
| Usability (IEC 62366), Cybersecurity (IEC 81001-5-1), SBOM | **Not addressed** | Required for modern submissions |
| SOUP | Inventoried | No anomaly-monitoring process actually operating |
| Authorship | AI-drafted in hours, **unreviewed** | Must be authored/reviewed by qualified personnel |

**Rough maturity:** document *type* coverage of the software portion ≈ 60–70%; genuine *audit-readiness* ≈ 15–25%. The honest framing: **we built a high-quality skeleton and proved two risk controls in code — we did not build a compliant medical-device software project.**

### The biggest gaps (in priority order)
1. **No operating QMS / ISO 13485 wrapper** — a draft document scaffold now exists at [docs/QMS/](QMS/), but it is unapproved and not yet operating; IEC 62304 is meaningless to an auditor outside a working one.
2. **No real, device-level ISO 14971 risk management** — severities are placeholders; intended use is assumed.
3. **No document control with reviews/approvals/signatures** — markdown revision tables aren't records.
4. **Most risk controls unimplemented**; range defaults unconfirmed against the device.
5. **Missing companion standards** — usability engineering, cybersecurity, clinical evaluation, formal SBOM.
6. **No human qualified-author/reviewer sign-off** — everything is AI-drafted and explicitly "Draft."

> These gaps are tracked as explicit open items in the [Class C compliance roadmap](iec62304_class_c_roadmap.md) (§"QMS / process gaps").

### What it's genuinely good for
A real team could adopt this as the **starting structure** and save substantial early effort: the clause mapping, traceability discipline, SOUP inventory, the candid open-item/roadmap tracking, and two reference implementations of risk controls with tests. That is real value — it just isn't certification.

---

## 5. Bottom line

> This project now has a **well-organized, code-grounded IEC 62304 *documentation scaffold* with two verified safety risk controls** — an excellent learning artifact and a strong head start for a real effort. It is **not** an audit-ready medical-device software package: it lacks the surrounding QMS, real risk management, controlled records with approvals, the companion standards, and qualified human review that an official audit requires. Treat the green checkmarks as "structure in place," not "compliance achieved."
