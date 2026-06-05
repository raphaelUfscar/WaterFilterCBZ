# Software Problem Resolution Process

Project: WaterFilterCBZ
Standard context: IEC 62304:2006+AMD1:2015, Clause 9 (Software problem resolution process)
Software safety class: C (see [Software Safety Classification](Software_Safety_Classification_IEC62304.md))
Document status: Draft
Last updated: 2026-06-05

## 1. Purpose

This process defines how problems found in WaterFilterCBZ (during development, verification, or in the field) are recorded, investigated, resolved, risk-assessed, and verified. It satisfies IEC 62304 Clause 9:
- **9.1** Prepare problem reports.
- **9.2** Investigate the problem.
- **9.3** Advise relevant parties.
- **9.4** Use the change control process.
- **9.5** Maintain records.
- **9.6** Analyze problems for trends.
- **9.7** Verify software problem resolution.
- **9.8** Test documentation contents.

## 2. Problem Report (Clause 9.1)

Problems are tracked as issues in the project tracker (GitHub Issues for this repository). Each problem report contains:

| Field | Description |
|---|---|
| ID | Tracker issue number (`PR-<n>` referenced in commits/PRs) |
| Title / summary | Short description |
| Type | Defect / anomaly / enhancement / SOUP anomaly |
| Severity | Safety-relevant? (yes/no) + severity per Risk file scale |
| Affected version / commit | Where observed |
| Steps to reproduce | Inputs, environment, expected vs actual |
| Detected during | Dev / unit / integration / system test / field |
| Status | Open / investigating / fix-in-progress / verifying / closed / deferred |

## 3. Investigation (Clause 9.2)

For each problem:
1. Reproduce and characterize.
2. **Assess safety relevance**: does the problem relate to a hazard (HAZ-xxx) or a risk control (RC-xxx)? Record the determination.
3. Identify the affected configuration items, requirements (SRS-xxx / SRS-Cxx), architectural elements (AE-xxx), and units.
4. Determine root cause.
5. Document the investigation in the problem report.

If the problem is safety-relevant, the [Risk Management File](Software_Risk_Management_IEC62304.md) is updated and the change is treated as safety-relevant in change control (Clause 7.4).

## 4. Advisory Notification (Clause 9.3)

When investigation shows a problem could affect safety in released software, relevant parties (software lead, risk manager, and — per the device-level procedure — regulatory/quality and affected users) shall be advised. The advisory record (who, what, when, residual risk) is stored with the problem report. Field-safety actions are escalated to the device-level ISO 14971 / vigilance process.

## 5. Change Control Linkage (Clause 9.4)

Approved fixes are implemented through the change-control flow in the [Configuration Management Plan](Software_Configuration_Management_Plan_IEC62304.md): branch → PR → review + CI → risk re-assessment (if safety-relevant) → merge → tag. Each fix commit/PR references the problem report ID.

## 6. Records (Clause 9.5)

The following records are retained for each problem: report, investigation/root cause, safety determination, change(s) made (commits/PRs), verification evidence, and closure. Records live in the tracker, Git history, and CI artifacts (see Configuration Status Accounting, SCM §5).

## 7. Trend Analysis (Clause 9.6)

Problem reports are reviewed periodically (at minimum at each release, and at the maintenance review cadence in the [Maintenance Plan](Software_Maintenance_Plan_IEC62304.md)) to detect trends — recurring failure areas (e.g. parser, serial I/O), clusters around a SOUP component, or repeated safety-relevant findings. Trends that indicate a systemic issue trigger a corrective action and possible risk re-assessment.

## 8. Verification of Resolution (Clause 9.7)

Before a problem is closed:
- The fix is verified by a test that reproduces the original failure and confirms it is resolved (regression test added where feasible).
- For safety-relevant problems, the affected risk control(s) are re-verified and the residual risk re-evaluated.
- Regression suite (unit + relevant integration/system tests) passes in CI.
- The problem report records the verification evidence and links to it.

## 9. Test Documentation Contents (Clause 9.8)

Verification records for problem resolution include: test inputs, expected results, actual results, pass/fail, tester/automation identity, software version/commit, and tools/environment. For automated tests this is captured by the CI run (xUnit results, coverage) referenced from the problem report.

## 10. Workflow Summary

```text
Problem report (9.1)
  -> Investigate + safety assessment (9.2)
    -> Advise relevant parties if safety-relevant (9.3)
      -> Change control (9.4) -> implement fix
        -> Verify resolution + regression (9.7), record contents (9.8)
          -> Update Risk file if needed (7.4)
            -> Close + retain records (9.5)
   ... periodic trend analysis across reports (9.6)
```

## 11. Revision History

| Revision | Date | Author | Description |
|---|---|---|---|
| 0.1 | 2026-06-05 | Claude | Initial problem resolution process mapped to Clause 9.1–9.8, using GitHub Issues + Git/CI as the record system, with explicit safety-relevance assessment and risk re-evaluation linkage. |
