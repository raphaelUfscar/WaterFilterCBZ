# End-to-End UI Testing — Technologies, Fundamentals, and Practice

This report explains the end-to-end (E2E) UI test added to WaterFilterCBZ
(`WaterFilterCBZ.UITests`): the technologies it is built on, the principles behind
them, the trade-offs, how to reuse the approach in other projects, and an honest
assessment of how close it is to professional software UI testing.

For the practical "how to set up and run it" steps, see
[../WaterFilterCBZ.UITests/README.md](../WaterFilterCBZ.UITests/README.md). This
document is the conceptual companion to that runbook.

## 1. What the test actually does

It launches the _real_, shipped `WaterFilterCBZ.exe` and drives it the way a user
would — clicking buttons, selecting a COM port, reading the on-screen status — while
the Python simulator feeds real binary sensor frames over a virtual serial cable.
The single test walks the full workflow and asserts each transition:

`select port → Connect → live data arrives → Clear Data → Disconnect`

Nothing is mocked. The serial stack, frame parser, view-models, OxyPlot charts, and
WPF rendering are all the production code paths.

## 2. Technology stack

| Technology | Role in the test |
| --- | --- |
| **Microsoft UI Automation (UIA)** | The Windows accessibility API that exposes every control as a queryable element with properties and "patterns" (Invoke, Value, SelectionItem…). The foundation everything else sits on. |
| **FlaUI (`FlaUI.UIA3`)** | A friendly .NET wrapper over the raw UIA COM API. Launches the app, finds elements, clicks, reads text. |
| **xUnit** | Test runner and assertion framework, consistent with the unit-test project. |
| **`Xunit.SkippableFact`** | Adds runtime-conditional skipping (`[SkippableFact]` + `Skip.If`) — xUnit v2 cannot skip dynamically on its own. |
| **`System.IO.Ports`** | Enumerates COM ports so the test can decide whether the required virtual pair exists. |
| **Python simulator** (`tools/sensor_simulator.py`) | The deterministic data source; emits the exact binary frame format the firmware would. |
| **Virtual COM pair** (e.g. com0com) | A software null-modem cable: two port names that are the two ends of one link. The simulator writes one end, the app reads the other. |

### How the layers stack up

```text
  xUnit test  ──drives──▶  FlaUI (UIA3)  ──calls──▶  Windows UI Automation
                                                          │ inspects/acts on
                                                          ▼
                                                    WaterFilterCBZ.exe (real WPF UI)
                                                          ▲ reads serial frames
                                                          │
   Python simulator ──writes frames──▶ COM11 ⇄ COM3 (virtual null-modem pair)
```

## 3. Fundamentals

### 3.1 UI Automation: the accessibility tree

Windows builds an _automation tree_ of every visible control — the same tree screen
readers use. Each node carries properties (`Name`, `AutomationId`, `ControlType`,
`IsEnabled`) and supports interaction _patterns_:

- **InvokePattern** — "press" a button.
- **ValuePattern** — read/set a text value.
- **SelectionItemPattern** — choose an item in a combo box / list.

Because this is the accessibility layer, **a UI that is testable this way is also a
UI that is accessible** — the two goals reinforce each other.

### 3.2 Locate by identity, not by appearance

The test finds controls by **`AutomationProperties.AutomationId`** (e.g.
`ConnectButton`, `ConnectionStatusText`), which we added in
[../MainWindow.xaml](../MainWindow.xaml). This matters more than it looks:

- Display text changes with **locale** — on this machine decimals render as `0,4`
  and OS errors come back in Portuguese. Matching on visible text would be brittle.
- Text changes when **copy** is reworded; an `AutomationId` is a stable contract
  between the UI and its tests.

A stable ID is the single highest-leverage habit for durable UI tests.

### 3.3 Own your data source; make it deterministic

A UI test is only as reliable as the data behind it. Instead of depending on
hardware or whatever happened to be on the port, the test **starts and stops its own
simulator** ([../WaterFilterCBZ.UITests/SimulatorProcess.cs](../WaterFilterCBZ.UITests/SimulatorProcess.cs))
and points the app at the paired port. The test controls the full environment for
its lifetime, then tears it down.

### 3.4 Synchronize by polling, never by sleeping

GUIs are asynchronous: a click kicks off work that finishes "soon". The test never
`Sleep`s a fixed guess; it **polls a condition until it becomes true or a timeout
expires** (`WaitUntil(() => SampleCount > 0, …)`). This is the difference between a
fast, stable suite and a flaky one full of magic `Thread.Sleep(2000)` calls.

### 3.5 Fail clearly, skip honestly

E2E tests have real prerequisites. Rather than fail confusingly when they are
missing, the test computes a precise reason and **self-skips**
([../WaterFilterCBZ.UITests/TestEnvironment.cs](../WaterFilterCBZ.UITests/TestEnvironment.cs)):
_"Simulator port COM11 not found. Create a virtual COM pair…"_. A skip with a reason
is information; a red failure on a machine that was never set up is noise.

### 3.6 Where E2E sits in the test pyramid

```text
        ▲  fewer, slower, higher-fidelity
   E2E / UI         ← this project: 1 test, whole-app, real I/O
   Integration      ← service ↔ view-model wiring (still open here)
   Unit             ← 36 tests: parsers, commands, stats
        ▼  many, fast, isolated
```

E2E tests are the apex: highest confidence per test, but the slowest and most
expensive to keep green. You want a _few_ that cover critical user journeys — not
hundreds.

## 4. How this implementation is structured

| Piece | Responsibility |
| --- | --- |
| [`TestEnvironment`](../WaterFilterCBZ.UITests/TestEnvironment.cs) | Resolve the exe, simulator script, ports, and Python; decide skip-or-run. |
| [`SimulatorProcess`](../WaterFilterCBZ.UITests/SimulatorProcess.cs) | Start/stop the data source; fail fast with captured output if the port is busy. |
| [`ConnectionWorkflowTests`](../WaterFilterCBZ.UITests/ConnectionWorkflowTests.cs) | Drive the workflow, assert each transition, tear everything down. |
| `AutomationId`s in [`MainWindow.xaml`](../MainWindow.xaml) | The stable, locale-proof contract the test binds to. |

One project-specific subtlety worth calling out: the app's `RelayCommand` re-checks
`CanExecute` only on `CommandManager.RequerySuggested` (i.e. on UI input), so a
freshly-enabled button can read as stale for a moment. The `ClickWhenEnabled` helper
nudges the mouse over the button to trigger that requery before invoking — a small
example of how E2E tests sometimes have to respect framework timing the unit layer
never sees.

## 5. Pros and cons

### Pros

- **Highest confidence.** Exercises the real binary, real serial stack, real
  rendering — the things unit tests deliberately stub out.
- **Catches integration and wiring bugs** that only appear when components meet:
  data-binding mistakes, command enable/disable logic, threading/marshalling.
- **Tests the product as shipped**, including the build/packaging of the exe.
- **Reuses the accessibility layer**, so it nudges the app toward being accessible.
- **Living documentation** of the critical user journey.

### Cons

- **Slower** (seconds per test vs. milliseconds) and **heavier** to run.
- **Environment-dependent**: needs an interactive desktop session, here also a
  virtual COM pair and Python. Hard to run on a vanilla headless CI runner.
- **More prone to flakiness** (timing, focus, screen state) — disciplined polling
  and stable IDs are mandatory, not optional.
- **Coarse failure signal**: "the workflow broke" localizes a bug less precisely
  than a failing unit test.
- **Higher maintenance**: UI restructuring can ripple into the tests.

The takeaway: invest in a _small number_ of E2E tests over the most important
journeys, and push everything you can down to faster integration/unit tests.

## 6. Reusing this approach in another project

The _shape_ transfers even when the tools change. A checklist:

1. **Add stable identifiers to the UI.** WPF/WinUI: `AutomationProperties.AutomationId`.
   Web: `data-testid`. Mobile: accessibility identifiers. Do this first.
2. **Pick the driver for your platform** (see table below).
3. **Make the data deterministic.** Own the backend the UI talks to — a local fake
   server, an in-process stub, a seeded database, or (as here) a simulated device.
4. **Launch the real app and drive a user journey**, locating elements by ID.
5. **Synchronize by polling conditions**, never fixed sleeps.
6. **Self-skip when prerequisites are absent**, with a precise reason.
7. **Always tear down** processes/resources in a `finally`/`Dispose`, even on failure.
8. **Keep the suite small and journey-focused**; let unit/integration carry the bulk.

### Driver per platform

| App type | Common E2E drivers |
| --- | --- |
| Windows desktop (WPF/WinForms/WinUI/Win32) | **FlaUI**, WinAppDriver / Appium |
| Web | Playwright, Cypress, Selenium |
| Cross-platform desktop + mobile | Appium |
| macOS / iOS | XCUITest, Appium |
| Android | Espresso, UI Automator, Appium |

The concepts in section 3 — identity-based locators, owned/deterministic data,
polling synchronization, the pyramid — are identical across all of them.

## 7. How close is this to professional UI testing?

**The fundamentals here _are_ the professional fundamentals.** Driving the shipped
app through the real accessibility API, locating by stable IDs, owning a
deterministic data source, polling instead of sleeping, conditional skipping, and
guaranteed teardown are exactly what mature UI suites do. FlaUI, UIA, and the
page-driver style are used in production test suites at real companies.

What a fully **production-grade** suite typically adds on top — natural next steps
if this grows:

- **CI integration on a dedicated interactive agent** (a self-hosted runner with the
  COM pair pre-provisioned), so the journey is verified on every change rather than
  only locally. Today this is deliberately local-only.
- **Page Object Model**: factor element locators and actions into reusable
  "page"/"screen" classes so many tests share one maintained map of the UI. Our
  single test inlines its accessors; that is fine for one test, less so for twenty.
- **Diagnostics on failure**: auto-capture a screenshot (and ideally a video) and
  attach it to the test result. Invaluable for debugging a CI failure you can't see.
- **Broader journey coverage**: error paths (port busy, malformed frames, mid-stream
  disconnect), multiple sensors, long-run stability.
- **Quarantine + retry policy**: a controlled, _measured_ retry for genuinely
  non-deterministic UI steps, with flaky tests tracked rather than silently re-run.
- **Reporting/trends**: surface E2E results and flake rates alongside unit coverage.

In short: this is a real, correctly-built E2E test that mirrors professional
technique on a small scale. The gap to a "professional suite" is not the technique —
it is breadth, CI automation, and the supporting tooling (page objects, screenshots,
reporting) that you layer on as the number of journeys grows.

## 8. Further reading

- FlaUI — <https://github.com/FlaUI/FlaUI>
- Microsoft UI Automation overview — <https://learn.microsoft.com/dotnet/framework/ui-automation/ui-automation-overview>
- `AutomationProperties.AutomationId` — <https://learn.microsoft.com/dotnet/api/system.windows.automation.automationproperties.automationid>
- The Practical Test Pyramid (Martin Fowler) — <https://martinfowler.com/articles/practical-test-pyramid.html>
- xUnit — <https://xunit.net/>
