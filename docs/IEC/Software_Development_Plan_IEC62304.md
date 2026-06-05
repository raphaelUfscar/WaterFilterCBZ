# Software Development Plan

Project: WaterFilterCBZ
Standard context: IEC 62304:2006+AMD1:2015, Clause 5.1 (Software development planning)
Software safety class: C (see [Software Safety Classification](Software_Safety_Classification_IEC62304.md))
Document status: Draft
Last updated: 2026-06-05

## 1. Purpose

This Software Development Plan (SDP) defines how WaterFilterCBZ is developed, verified, and released in conformance with IEC 62304. It satisfies Clause 5.1, which requires a plan covering: development activities and deliverables, traceability, configuration management, problem resolution, verification, and the standards/methods/tools used, scaled to the assigned **Class C**.

## 2. Lifecycle Model

WaterFilterCBZ uses an **iterative/incremental** lifecycle. Each increment passes through requirements → design → implementation → verification, with risk management and configuration management applied continuously. This is permitted by IEC 62304, which does not mandate a specific lifecycle but requires all Class C activities to be performed and their outputs maintained.

```text
Plan (5.1)
  -> Requirements (5.2) ----+
       -> Architecture (5.3) |   Risk Mgmt (7) — continuous
            -> Detailed design (5.4)   Config Mgmt (8) — continuous
                 -> Unit impl + verify (5.5)  Problem Resolution (9) — continuous
                      -> Integration + test (5.6)
                           -> System test (5.7)
                                -> Release (5.8)
                                     -> Maintenance (6)
```

## 3. Deliverables and Status

| Clause | Deliverable | Location | Status |
|---|---|---|---|
| 4.3 | Software Safety Classification | [link](Software_Safety_Classification_IEC62304.md) | Draft 0.1 |
| 5.1 | Software Development Plan (this doc) | this file | Draft 0.1 |
| 5.2 | Software Requirements Specification | [link](Software_Requirements_Specification_IEC62304.md) | Draft 0.1 |
| 5.3 | Software Architecture Design | [link](Software_Architecture_Design_IEC62304.md) | Draft 0.3 |
| 5.4 | Software Detailed Design | [link](Software_Detailed_Design_IEC62304.md) | Draft 0.1 |
| 5.5–5.7 | Verification & Test Plan + results | [link](Software_Verification_and_Test_Plan_IEC62304.md) | Draft 0.1 |
| 5.8 | Release record / known-anomalies list | §8 of this doc (template) | Template |
| 6 | Software Maintenance Plan | [link](Software_Maintenance_Plan_IEC62304.md) | Draft 0.1 |
| 7 | Software Risk Management File | [link](Software_Risk_Management_IEC62304.md) | Draft 0.1 |
| 8 | Software Configuration Management Plan | [link](Software_Configuration_Management_Plan_IEC62304.md) | Draft 0.1 |
| 9 | Software Problem Resolution Process | [link](Software_Problem_Resolution_Process_IEC62304.md) | Draft 0.1 |

## 4. Activities, Inputs, Outputs, and Acceptance (Class C)

| Activity | Inputs | Outputs | Entry → Exit criteria |
|---|---|---|---|
| Requirements analysis (5.2) | Intended use, risk file | SRS, risk-control requirements | Reviewed, traced to hazards, verifiable → SRS baselined |
| Architectural design (5.3) | SRS | Architecture doc, interfaces, SOUP reqs, segregation rationale | Each requirement allocated; interfaces defined → architecture reviewed |
| Detailed design (5.4) | Architecture | Unit-level design for each unit | Every Class C unit designed; interfaces specified → design reviewed |
| Implementation + unit verification (5.5) | Detailed design | Source + unit tests + results | Acceptance criteria met; unit verification passes |
| Integration + integration test (5.6) | Units | Integration test plan + results | Integration tests pass; regression run |
| System test (5.7) | Integrated software | System test results; anomaly evaluation | Requirements & RCs verified; anomalies risk-assessed |
| Release (5.8) | Verified software | Release record, residual anomaly list, archived config | All activities complete; residual anomalies accepted |

## 5. Standards, Methods, and Conventions

| Area | Standard / method |
|---|---|
| Language / platform | C# / .NET 10 (`net10.0-windows`), WPF, MVVM. |
| Coding | Nullable reference types enabled; `ImplicitUsings` enabled; service interfaces (`ISerialPortService`) for testability; UI-thread marshaling via WPF Dispatcher. |
| Reviews | All changes via pull request to `main`; review required (see Configuration Management Plan). |
| Static analysis | SonarQube (`.github/workflows/sonarqube.yml`) with C# OpenCover + Python Coverage imports. |
| Risk-control coding | Risk controls implemented as identified units with explicit verification (see SRS-Cxx / RC-xxx). |

## 6. Tools

| Tool | Use | Qualification consideration |
|---|---|---|
| .NET 10 SDK | Build/test/publish | Version pinned in CI (`10.0.x`). |
| GitHub Actions (`dotnet-desktop.yml`) | Restore, build, test, publish, artifact | Primary CI on Windows runners. |
| Jenkins (`Jenkinsfile`) | Build/test + coverage on `dotnet10` agent | Secondary pipeline. |
| xUnit + coverlet | Unit test + coverage | Verification tool. |
| FlaUI / UIA3 + Python simulator | End-to-end UI tests over virtual COM pair | Local/dedicated-agent only; excluded from CI. |
| SonarQube | Static analysis & coverage gate | Quality gate. |
| Serilog | Runtime diagnostic/audit logging | Shipped SOUP (see Risk file). |

## 7. Verification and Risk Management Integration

- **Verification planning** is defined in the [Verification & Test Plan](Software_Verification_and_Test_Plan_IEC62304.md); every requirement and risk control has an assigned verification method and evidence.
- **Risk management** ([Risk file](Software_Risk_Management_IEC62304.md)) runs continuously; risk-control requirements (SRS-Cxx) are tracked to closure.
- **Traceability** (Hazard → RC → Requirement → Architecture → Unit → Verification) is maintained in [Architecture §4](Software_Architecture_Design_IEC62304.md).

## 8. Release Process (Clause 5.8) — Record Template

A release of WaterFilterCBZ is permitted only when the following are recorded:

- [ ] All planned Class C activities (5.2–5.7) complete for the release scope.
- [ ] Verification complete; results stored under version control / CI artifacts.
- [ ] Residual anomalies listed and each evaluated for safety impact (5.8.3); none unacceptable.
- [ ] Risk Management File updated; residual risk acceptable at device level.
- [ ] Configuration items and tool versions archived; release tagged in Git (see Configuration Management Plan).
- [ ] Released version, build, and SOUP versions documented.

| Field | Value (per release) |
|---|---|
| Release version / tag | _to be filled_ |
| Build / commit | _to be filled_ |
| Date | _to be filled_ |
| Known residual anomalies | _list + risk assessment_ |
| SOUP versions | _from csproj at release_ |
| Approver | _name / role_ |

## 9. Roles and Responsibilities

| Role | Responsibility |
|---|---|
| Software lead | Maintains this plan, architecture, and release decisions. |
| Developer | Implements requirements/risk controls; writes unit tests; raises problem reports. |
| Verification | Executes and records verification; maintains traceability. |
| Risk manager | Maintains the Risk Management File; assesses change impact. |

(For a small team, one person may hold multiple roles; independence requirements for Class C verification activities are noted in the Verification & Test Plan.)

## 10. Revision History

| Revision | Date | Author | Description |
|---|---|---|---|
| 0.1 | 2026-06-05 | Claude | Initial SDP: lifecycle, deliverables and status, per-activity entry/exit criteria, standards/tools, verification & risk integration, release record template, roles. |
