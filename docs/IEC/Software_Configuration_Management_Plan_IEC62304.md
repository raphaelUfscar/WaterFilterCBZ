# Software Configuration Management Plan

Project: WaterFilterCBZ
Standard context: IEC 62304:2006+AMD1:2015, Clause 8 (Software configuration management process)
Software safety class: C (see [Software Safety Classification](Software_Safety_Classification_IEC62304.md))
Document status: Draft
Last updated: 2026-06-05

## 1. Purpose

This plan defines how configuration items of WaterFilterCBZ are identified, controlled, and tracked, satisfying IEC 62304 Clause 8:
- **8.1** Configuration identification.
- **8.2** Change control.
- **8.3** Configuration status accounting.

Class C requires that SOUP configuration items also be uniquely identified (8.1.2).

## 2. Configuration Items (Clause 8.1)

| CI category | Items | Identification |
|---|---|---|
| Source code | All `.cs`, `.xaml`, `.slnx`, `.csproj` | Git commit SHA + release tag |
| Documentation | `docs/IEC/*.md`, `README.md`, `docs/*.md` | Git history + per-doc revision table |
| Build/CI config | `.github/workflows/*.yml`, `Jenkinsfile`, `Jenkinsfile.uitests`, `coverlet.runsettings` | Git |
| Test assets | `WaterFilterCBZ.Tests/*`, `WaterFilterCBZ.UITests/*`, `tools/*.py` | Git |
| SOUP / dependencies | NuGet packages (see §3), .NET SDK, Python simulator deps | Pinned versions in `.csproj` / `requirements-simulator.txt` / CI `dotnet-version` |
| Release artifacts | Published build output | GitHub Actions artifact `WaterFilterCBZ-publish-<run>` |

The repository (Git, branch `main`) is the configuration control system. A release is identified by an annotated Git tag.

## 3. SOUP Configuration Identification (Clause 8.1.2)

| SOUP | Version | Source of truth |
|---|---|---|
| .NET target | net10.0-windows | `WaterFilterCBZ.csproj`, CI `dotnet-version: 10.0.x` |
| System.IO.Ports | 9.0.0 | `WaterFilterCBZ.csproj` |
| OxyPlot.Wpf | 2.1.2 | `WaterFilterCBZ.csproj` |
| Serilog | 4.0.0 | `WaterFilterCBZ.csproj` |
| Serilog.Sinks.File | 6.0.0 | `WaterFilterCBZ.csproj` |
| Serilog.Sinks.Debug | 2.0.0 | `WaterFilterCBZ.csproj` |
| Microsoft.NET.Test.Sdk | 17.14.1 | `WaterFilterCBZ.Tests.csproj` |
| xunit | 2.9.3 | `WaterFilterCBZ.Tests.csproj` |
| xunit.runner.visualstudio | 3.1.4 | `WaterFilterCBZ.Tests.csproj` |
| coverlet.collector | 6.0.4 | `WaterFilterCBZ.Tests.csproj` |

SOUP versions are reviewed at each release and recorded in the release record (SDP §8). Anomaly lists for SOUP are reviewed per the [Maintenance Plan](Software_Maintenance_Plan_IEC62304.md) and [Risk file §5](Software_Risk_Management_IEC62304.md).

## 4. Change Control (Clause 8.2)

```text
Change request / Problem report
  -> Evaluate (impact on requirements, risk, architecture)   [links to Problem Resolution]
    -> Approve / branch from main
      -> Implement on feature branch
        -> Pull request to main
          -> Review + CI (build, unit test, SonarQube gate)
            -> Risk re-assessment (Clause 7.4) if safety-relevant
              -> Merge + (for release) tag
```

Rules:
- All changes are made on a branch and merged to `main` via pull request; direct pushes to `main` for substantive change are not permitted.
- Every change references the originating problem report or change request (see [Problem Resolution Process](Software_Problem_Resolution_Process_IEC62304.md)).
- CI (`.github/workflows/dotnet-desktop.yml`) must pass (restore, build, test, publish) before merge.
- Safety-relevant changes (touching acquisition, parsing, validation, risk-control units, or any `SRS-Cxx`) require explicit risk re-assessment per Clause 7.4 and regression verification.
- The SonarQube quality gate (`sonarqube.yml`) must pass.

## 5. Configuration Status Accounting (Clause 8.3)

Status of configuration items and changes is recoverable at any time via:
- Git commit history and tags (what changed, when, by whom).
- Pull request records (review, discussion, approval).
- CI run history and uploaded artifacts (build/test status, published output).
- Per-document revision tables in `docs/IEC/`.
- The traceability tables in [Architecture §4](Software_Architecture_Design_IEC62304.md).

For each controlled release, the release record (SDP §8) captures the tag, commit, SOUP versions, verification status, and known anomalies, providing the configuration baseline.

## 6. Branch and Tag Conventions

| Item | Convention |
|---|---|
| Mainline | `main` (protected) |
| Feature/fix branch | short descriptive name; one logical change |
| Release tag | annotated tag, e.g. `vMAJOR.MINOR.PATCH` |
| Commit message | imperative summary; reference problem report ID where applicable |

## 7. Backup and Retention

The authoritative repository is the remote Git host. Release artifacts are retained as CI artifacts. Records (PRs, CI runs, tags) are retained for the lifetime required by the device's regulatory/retention policy.

## 8. Revision History

| Revision | Date | Author | Description |
|---|---|---|---|
| 0.1 | 2026-06-05 | Claude | Initial SCM plan: CI identification, SOUP version inventory, branch/PR change-control flow with risk re-assessment, status accounting via Git/CI/PRs, branch & tag conventions. |
