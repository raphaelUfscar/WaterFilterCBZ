# Software Maintenance Plan

Project: WaterFilterCBZ
Standard context: IEC 62304:2006+AMD1:2015, Clause 6 (Software maintenance process)
Software safety class: C (see [Software Safety Classification](Software_Safety_Classification_IEC62304.md))
Document status: Draft
Last updated: 2026-06-05

## 1. Purpose

This plan establishes how WaterFilterCBZ is maintained after release, satisfying IEC 62304 Clause 6:
- **6.1** Establish a software maintenance plan.
- **6.2** Problem and modification analysis.
- **6.3** Modification implementation.

For Class C, maintenance reuses the same rigor as development: every modification is risk-assessed, verified, and configuration-controlled.

## 2. Maintenance Plan (Clause 6.1)

| Aspect | Approach |
|---|---|
| Scope | Corrective (defect fixes), adaptive (SOUP/OS/.NET updates), and perfective (enhancements such as the pending Class C risk controls). |
| Process reuse | Maintenance uses the same lifecycle, [Configuration Management](Software_Configuration_Management_Plan_IEC62304.md), [Problem Resolution](Software_Problem_Resolution_Process_IEC62304.md), and [Risk Management](Software_Risk_Management_IEC62304.md) processes as initial development. |
| Inputs | Field problem reports, internal anomalies, SOUP anomaly lists, OS/.NET advisories, change requests. |
| Trigger sources | GitHub Issues, CI failures, SOUP security/anomaly notices, user feedback. |
| Review cadence | At minimum per release, plus a periodic (e.g. quarterly) maintenance review of open problems, SOUP versions, and trend analysis. |

## 3. Problem and Modification Analysis (Clause 6.2)

For each maintenance request:
1. Record it as a problem report / change request (Problem Resolution Process §2).
2. Evaluate impact on **existing requirements, architecture, risk controls, and SOUP** — including whether the change introduces new hazardous situations or affects existing risk controls (Clause 7.4).
3. Classify the change as safety-relevant or not, and record the rationale.
4. Decide approve / defer / reject; for safety-relevant items, the risk manager participates.

### 3.1 SOUP maintenance

SOUP versions (see [SCM §3](Software_Configuration_Management_Plan_IEC62304.md)) are monitored for published anomalies and security advisories. When a SOUP anomaly could affect WaterFilterCBZ safety (per [Risk file §5](Software_Risk_Management_IEC62304.md)), a problem report is opened and the upgrade is treated as a safety-relevant modification.

## 4. Modification Implementation (Clause 6.3)

Approved modifications follow the development process:
- Implement on a branch; update affected requirements, architecture, detailed design, and risk file.
- Re-verify affected units, integration, and system behavior; **run regression** so previously verified behavior (especially risk controls) is not broken.
- Re-evaluate residual risk for safety-relevant changes (Clause 7.4).
- Merge via PR with passing CI and review; tag if released.
- Update the release record and known-anomaly list (SDP §8).

## 5. Regression Strategy

| Layer | Regression scope on change |
|---|---|
| Unit | Full `WaterFilterCBZ.Tests` suite in CI on every PR. |
| Integration | Re-run integration tests touching the changed area (and parser/serial if affected). |
| System / E2E | Re-run `ConnectionWorkflowTests` (local/dedicated agent) for changes affecting connection, parsing, display, or logging. |
| Risk controls | Re-verify every RC whose requirement (SRS-xxx / SRS-Cxx) or unit is touched. |

## 6. Records

Maintenance activity is recorded through the same mechanisms as development: tracker issues, Git history/tags, PRs, CI artifacts, and updates to the IEC document revision histories. Retention follows the device-level retention policy.

## 7. Revision History

| Revision | Date | Author | Description |
|---|---|---|---|
| 0.1 | 2026-06-05 | Claude | Initial maintenance plan: corrective/adaptive/perfective scope, modification analysis with safety-relevance + Clause 7.4 linkage, SOUP monitoring, modification implementation with regression strategy. |
