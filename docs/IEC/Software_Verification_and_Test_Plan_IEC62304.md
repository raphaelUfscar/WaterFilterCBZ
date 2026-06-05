# Software Verification and Test Plan

Project: WaterFilterCBZ
Standard context: IEC 62304:2006+AMD1:2015, Clauses 5.5 (unit implementation & verification), 5.6 (integration & integration testing), 5.7 (system testing)
Software safety class: C (see [Software Safety Classification](Software_Safety_Classification_IEC62304.md))
Document status: Draft
Last updated: 2026-06-05

## 1. Purpose and Scope

This plan defines how WaterFilterCBZ is verified at the unit, integration, and system levels, and how each requirement (SRS-xxx / SRS-Cxx) and risk control (RC-xxx) is verified. For Class C, IEC 62304 requires documented unit verification acceptance criteria (5.5.2/5.5.3), integration testing with regression (5.6), and system testing with anomaly evaluation against risk (5.7).

## 2. Verification Strategy and Levels

| Level | Clause | Method | Implementation |
|---|---|---|---|
| Unit | 5.5 | xUnit unit tests with acceptance criteria | `WaterFilterCBZ.Tests` (CI) |
| Integration | 5.6 | Component-wiring tests + simulator-driven parser/serial tests | Proposed CI-friendly integration tests + Python simulator |
| System / E2E | 5.7 | Full UI workflow over a virtual COM pair | `WaterFilterCBZ.UITests` (`ConnectionWorkflowTests`, local/dedicated agent) |
| Static analysis | — | SonarQube quality gate + coverage | `.github/workflows/sonarqube.yml` |

Verification methods used in requirement tables: `T` test, `A` analysis, `I` inspection/review, `D` demonstration.

### 2.1 Independence (Class C)

For Class C, verification of safety-relevant units and risk controls should not be performed solely by the author of the code where practical. For a small team this is satisfied by independent **review** of test design and results (PR review) plus automated, repeatable CI execution. The reviewer of a safety-relevant change shall not be its sole author.

## 3. Unit Verification (Clause 5.5)

### 3.1 Existing unit coverage

| Test class | Scope | Requirements covered |
|---|---|---|
| `SerialPortServiceTests` | Construction, connection-failure behavior, baud-rate config, port setting, disconnect/dispose, timestamp decode from valid frame | SRS-002, SRS-003, SRS-006, SRS-011, SRS-015 |
| `SensorDisplayInfoTests` | Current/min/max/avg/count + property-change notification | SRS-007 |
| `RelayCommandTests` | Command execution + enablement | SRS-012 |
| `LoggingServiceTests` | Log configuration / directory | SRS-010 |
| `SensorViewModelOpenLogsTests` | Open Logs command | SRS-014 |
| `InvertBoolConverterTests` | UI converter | (support) |

### 3.2 Unit verification gaps to close for Class C

| Unit / behavior | Requirement / RC | Planned test |
|---|---|---|
| `ParseReceiveBuffer` rejection paths: invalid count, invalid end byte, invalid checksum, noise-before-start, resync | SRS-004, SRS-005 / RC-001 | Parser rejection + resync unit tests |
| Frame-assembly timeout reset | SRS-016 / RC-005 | Timeout reset unit test |
| `ParseFrame` edge cases: invalid length, unit handling, multi-sensor | SRS-006 | Frame parsing unit tests |
| `SensorViewModel.AddSample` new-sensor registration, sample count, dispatcher path, chart assignment beyond 4 | SRS-007, SRS-008 | View-model unit tests |
| Buffer cap drop/reset | SRS-C-004 / RC-005 | Test after feature implemented |
| Stale-data state transition | SRS-C-001 / RC-002 | **Done** — `SensorDisplayInfoTests` (becomes stale after 5 s, fresh within threshold, recovers on new sample, no-op when unchanged, `IsStale` PropertyChanged) |
| Range/unit plausibility rejection | SRS-C-003 / RC-008 | **Done** — `SensorParameterTests` (classify normal/out-of-spec/invalid, NaN/Inf, inclusive bounds, registry map) + `SensorDisplayInfoTests` (reject keeps last good, out-of-spec displayed, recovery) |
| Device/protocol-version rejection | SRS-C-002 / RC-003 | Test after feature implemented |
| Processing-task failure surfaced | SRS-C-005 / RC-009 | Test after feature implemented |

### 3.3 Unit acceptance criteria (5.5.3)

A unit passes when: all its tests pass in CI; the behavior matches the detailed design; and for safety-relevant units, every associated risk-control behavior has at least one positive and one negative (rejection/fault) test. Coverage is monitored via SonarQube; safety-relevant units target high branch coverage with rationale for any uncovered branches.

## 4. Integration Testing (Clause 5.6)

| Integration concern | Test | Status |
|---|---|---|
| `SerialPortService` → `SensorViewModel` sample wiring (headless, CI-friendly) | Proposed integration test feeding frames to the sample callback and asserting view-model state | Proposed |
| Parser behavior under malformed stream | Python simulator `--inject-errors {checksum,end-byte,count,partial,noise}` driving the parser; assert resync + warning logs | Partially available via simulator |
| Logging integration | Assert safety-relevant events reach the rolling log file | Partial (E2E asserts command logging) |
| Chart/COM lifecycle | Covered partially by `ConnectionWorkflowTests` | Partial |

Integration test runs include a **regression** of previously passing integration tests (Clause 5.6.7). New integration tests are added with each integrated risk-control feature.

## 5. System Testing (Clause 5.7)

| System scenario | Test | Status |
|---|---|---|
| Full operator workflow: enumerate → select → connect → live data → open logs → clear → disconnect, with command logging | `ConnectionWorkflowTests` (FlaUI/UIA3 + Python simulator over virtual COM pair) | Implemented (local/dedicated agent; self-skips without prerequisites; excluded from CI) |
| Sustained high-rate input (responsiveness) | Proposed stress system test | Proposed (needs OAI-004 rate) |
| Disconnect/reconnect cycling | Proposed | Proposed |
| Malformed-stream injection during live monitoring | Proposed (simulator error modes) | Proposed |
| Stale-data presentation | Proposed (after SRS-C-001) | Blocked on feature |
| Failure-state taxonomy presentation | Proposed (after SRS-C-006) | Blocked on feature |
| Hardware-in-the-loop with representative firmware | Proposed | Proposed |

Per Clause 5.7.3, anomalies found in system testing are entered into the [Problem Resolution Process](Software_Problem_Resolution_Process_IEC62304.md) and evaluated against the [Risk Management File](Software_Risk_Management_IEC62304.md).

## 6. Requirement → Verification Traceability

| Requirement | Method | Verification artifact | Status |
|---|:--:|---|---|
| SRS-001 | T,D | `ConnectionWorkflowTests`; unit test recommended | Partial |
| SRS-002 | T,D | `SerialPortServiceTests`, `ConnectionWorkflowTests` | Verified |
| SRS-003 | T | `SerialPortServiceTests` (baud/config) | Verified |
| SRS-004 | T | Parser tests (acceptance) + rejection tests pending | Partial |
| SRS-005 | T | Parser rejection tests pending | Planned |
| SRS-006 | T | `SerialPortServiceTests` (decode) | Verified |
| SRS-007 | T,D | `SensorDisplayInfoTests`, `ConnectionWorkflowTests` | Verified |
| SRS-008 | D,A | Manual + analysis; unit test pending | Partial |
| SRS-009 | T,D | `ConnectionWorkflowTests` | Verified |
| SRS-010 | T,I | `LoggingServiceTests`, `ConnectionWorkflowTests` | Partial |
| SRS-011 | T,D | `SerialPortServiceTests`, `ConnectionWorkflowTests` | Verified |
| SRS-012 | T,D | `RelayCommandTests`, `ConnectionWorkflowTests` | Verified |
| SRS-013 | I,T | Automation IDs used by `ConnectionWorkflowTests` | Verified |
| SRS-014 | T,D | `SensorViewModelOpenLogsTests` | Verified |
| SRS-015 | T | `SerialPortServiceTests` (timestamp) | Verified |
| SRS-016 | T | Timeout unit test pending | Planned |
| SRS-017 | A,T | Design analysis; stress test pending | Partial |
| SRS-C-001 | T,D | `SensorDisplayInfoTests` stale-data cases | Verified (5 s, RC-002) |
| SRS-C-003 | T | `SensorParameterTests`, `SensorDisplayInfoTests` validation cases | Verified (RC-008) |
| SRS-C-002, C-004..C-008 | T/A | After implementation (see §3.2/§5) | Planned |
| SRS-C-009 | A,I | Architecture review | Held (constraint) |

## 7. Verification Environment

| Item | Value |
|---|---|
| Build/test runner | Windows runner, .NET 10 SDK (`10.0.x`) |
| Unit/CI | GitHub Actions `dotnet-desktop.yml`; SonarQube `sonarqube.yml`; Jenkins `Jenkinsfile` (coverage) |
| E2E | Local/dedicated agent with virtual COM pair + Python simulator (`tools/sensor_simulator.py`) |
| Coverage | coverlet → OpenCover (C#), Coverage.py (Python) → SonarQube |

## 8. Entry / Exit Criteria for Release Verification

- Entry: all planned tests defined and traced to requirements/RCs; build green.
- Exit: every requirement and risk control has verification evidence or a documented, risk-assessed deferral; all safety-relevant tests pass; anomalies evaluated against risk; results archived (CI artifacts + release record, SDP §8).

## 9. Revision History

| Revision | Date | Author | Description |
|---|---|---|---|
| 0.1 | 2026-06-05 | Claude | Initial verification & test plan covering unit/integration/system levels, existing vs planned coverage, Class C independence, requirement→verification traceability, environment, and release entry/exit criteria. |
