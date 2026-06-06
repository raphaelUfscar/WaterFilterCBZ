pipeline {
    agent { label 'dotnet10' }

    options {
        buildDiscarder(logRotator(numToKeepStr: '10'))
        timeout(time: 1, unit: 'HOURS')
    }

    environment {
        CONFIGURATION = 'Release'
        // Fast CI runs only the unit test project. WaterFilterCBZ.UITests is
        // end-to-end (interactive desktop + COM pair) and is intentionally
        // excluded here; run it on a dedicated interactive agent.
        TEST_PROJECT = 'WaterFilterCBZ.Tests\\WaterFilterCBZ.Tests.csproj'
        RESULTS_DIR  = 'TestResults'
        // Overall line-coverage target. Below this marks the build UNSTABLE
        // (not failed). Note this is overall coverage, which differs from the
        // SonarCloud gate's "80% on new code".
        MIN_COVERAGE = '80'
    }

    stages {
        stage('Checkout') {
            steps {
                checkout scm
            }
        }

        stage('Restore') {
            steps {
                bat 'dotnet restore'
            }
        }

        stage('Build') {
            steps {
                bat 'dotnet build -c %CONFIGURATION% --no-restore'
            }
        }

        stage('Test') {
            steps {
                // The agent workspace is reused between runs, so remove stale
                // results first; otherwise old TestResults\<guid> coverage and
                // junit files would be picked up by the steps below.
                bat 'if exist %RESULTS_DIR% rmdir /s /q %RESULTS_DIR%'

                // JUnit XML is emitted for the Jenkins junit step (Jenkins
                // cannot parse VSTest .trx natively); .trx is kept for archiving
                // and traceability. Coverage is collected only from the unit
                // test project, which carries coverlet.collector.
                bat 'dotnet test "%TEST_PROJECT%" -c %CONFIGURATION% --no-build --no-restore --results-directory %RESULTS_DIR% --collect:"XPlat Code Coverage" --logger "junit;LogFilePath=test-results.junit.xml" --logger "trx;LogFileName=test-results.trx"'
            }
        }

        stage('Coverage') {
            steps {
                // Parse the Cobertura line-rate and compare against MIN_COVERAGE.
                // The script never fails the build; falling short drops a flag
                // file that the script block below turns into an UNSTABLE result.
                powershell '''
                    $ErrorActionPreference = 'Stop'
                    if (Test-Path coverage-unstable.flag) { Remove-Item coverage-unstable.flag }

                    $report = Get-ChildItem -Path $env:RESULTS_DIR -Recurse -Filter coverage.cobertura.xml |
                              Where-Object { $_.FullName -notmatch '\\\\In\\\\' } |
                              Select-Object -First 1
                    if (-not $report) { throw "No coverage.cobertura.xml found under $env:RESULTS_DIR" }

                    [xml]$xml = Get-Content $report.FullName
                    $pct = [math]::Round([double]$xml.coverage.'line-rate' * 100, 2)
                    Write-Host "Line coverage: $pct% (target $env:MIN_COVERAGE%)"

                    if ($pct -lt [double]$env:MIN_COVERAGE) {
                        "Line coverage $pct% is below target $env:MIN_COVERAGE%" | Out-File coverage-unstable.flag
                    }
                '''

                // Best-effort HTML coverage report for download from the build
                // page. Wrapped so a missing tool / offline agent never fails
                // the build.
                catchError(buildResult: 'SUCCESS', stageResult: 'SUCCESS') {
                    bat 'dotnet tool update --global dotnet-reportgenerator-globaltool'
                    bat 'reportgenerator -reports:%RESULTS_DIR%\\**\\coverage.cobertura.xml -targetdir:%RESULTS_DIR%\\coverage-report -reporttypes:Html'
                }

                script {
                    if (fileExists('coverage-unstable.flag')) {
                        unstable(readFile('coverage-unstable.flag').trim())
                    }
                }
            }
        }
    }

    post {
        always {
            // Record unit test results (JUnit XML; the test project writes it
            // next to its .csproj, hence the recursive glob).
            junit testResults: '**/test-results.junit.xml', allowEmptyResults: false

            // Archive coverage (raw + HTML report) and the runnable Release app.
            archiveArtifacts artifacts: "${RESULTS_DIR}/**/coverage.cobertura.xml", fingerprint: true, allowEmptyArchive: true
            archiveArtifacts artifacts: "${RESULTS_DIR}/coverage-report/**", fingerprint: true, allowEmptyArchive: true
            archiveArtifacts artifacts: 'bin/Release/**/*', fingerprint: true, allowEmptyArchive: true
        }

        failure {
            echo 'Build FAILED!'
        }

        unstable {
            echo 'Build UNSTABLE (coverage below target).'
        }

        success {
            echo 'Build SUCCEEDED!'
        }
    }
}
