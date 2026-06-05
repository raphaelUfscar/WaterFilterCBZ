# Continuous Integration & Coverage

Contributor/CI reference for WaterFilterCBZ: the pipelines, how to generate coverage locally, and how to fix common SonarQube coverage problems. (User-facing build/run/test commands live in the root [README](../README.md#build-and-test).)

## Pipelines

| File | Purpose |
|---|---|
| `.github/workflows/dotnet-desktop.yml` | Restore, build, test, publish, and upload a runnable build artifact (Windows runner). |
| `.github/workflows/sonarqube.yml` | SonarQube/SonarCloud analysis importing C# (OpenCover) and Python (Coverage.py) coverage. |
| `Jenkinsfile` | Alternative pipeline (restore/build/test + Cobertura coverage) on a `dotnet10` agent. |

The SonarQube workflow passes `SONAR_TOKEN` through environment variables and verifies that both `coverage.opencover.xml` and `tools/coverage.xml` were generated before ending analysis.

The unit test project (`WaterFilterCBZ.Tests`) runs in CI. The end-to-end UI tests (`WaterFilterCBZ.UITests`) are **excluded** from the fast CI run and self-skip when their prerequisites (a virtual COM pair + Python) are absent — see [WaterFilterCBZ.UITests/README.md](../WaterFilterCBZ.UITests/README.md).

## Generate coverage locally

C# coverage (OpenCover format, consumed by SonarQube):

```powershell
dotnet test WaterFilterCBZ.Tests/WaterFilterCBZ.Tests.csproj --configuration Release --collect:"XPlat Code Coverage" -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover
```

Reports are written under:

```text
WaterFilterCBZ.Tests/TestResults/<run-id>/coverage.opencover.xml
```

Python simulator coverage:

```powershell
python -m pip install -r tools/requirements-simulator.txt
python -m coverage run --source=tools --omit="tools/test_*.py" -m unittest discover -s tools -p "test_*.py"
python -m coverage xml -o tools/coverage.xml
```

## Troubleshooting: SonarQube shows 0.0% coverage

- Confirm the workflow generated `coverage.opencover.xml`.
- Confirm `sonar.cs.opencover.reportsPaths` points to `**/TestResults/**/coverage.opencover.xml`.
- Confirm the workflow generated `tools/coverage.xml` for the Python simulator.
- Confirm `sonar.python.coverage.reportPaths` points to `tools/coverage.xml`.
- Check the `Verify coverage reports` workflow step output.

## Quality gate: new-code coverage

SonarCloud enforces a **≥ 80% coverage on new code** gate. When adding code, add unit tests in the same change. For view-model logic whose UI updates are `Dispatcher`-gated (and therefore skipped headless), drive the testable branches directly — see `SensorViewModelValidationTests` for the pattern, and `InternalsVisibleTo` in `WaterFilterCBZ.csproj` for exercising internal members.
