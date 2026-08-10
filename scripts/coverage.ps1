#!/usr/bin/env pwsh
# Runs code coverage for both test projects and opens the HTML reports.

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$reportDir = Join-Path $repoRoot "coveragereport"
$resultsRoot = Join-Path $repoRoot "TestResults"
$testProjects = @(
    "EasyPlayscript.Tests",
    "EasyPlayscript.LSP.Tests"
)

Push-Location $repoRoot
try {
    # Clean stale coverage data so old XMLs and HTMLs don't pollute the report.
    if (Test-Path $resultsRoot) { Remove-Item -Recurse -Force $resultsRoot }
    if (Test-Path $reportDir) { Remove-Item -Recurse -Force $reportDir }

    foreach ($project in $testProjects) {
        Write-Host "`n=== Running coverage for $project ===" -ForegroundColor Cyan
        dotnet test $project --collect:"XPlat Code Coverage" --results-directory "TestResults/$project"
        if ($LASTEXITCODE -ne 0) { throw "Tests failed for $project" }
    }

    $xmlPaths = @()

    foreach ($project in $testProjects) {
        $resultsDir = Join-Path $repoRoot "TestResults" $project
        $xmlFile = Get-ChildItem -Path $resultsDir -Filter "coverage.cobertura.xml" -Recurse | Select-Object -First 1
        if ($xmlFile) {
            $xmlPaths += $xmlFile.FullName
        }
        else {
            Write-Warning "No coverage XML found for $project"
        }
    }

    if ($xmlPaths.Count -eq 0) {
        Write-Error "No coverage data found. Ensure tests ran successfully."
        exit 1
    }

    $reportsArg = "-reports:" + ($xmlPaths -join ";")

    Write-Host "`n=== Generating HTML report ===" -ForegroundColor Cyan
    & reportgenerator $reportsArg "-targetdir:$reportDir" "-reporttypes:Html"
    if ($LASTEXITCODE -ne 0) { throw "reportgenerator failed" }

    $htmlPath = Join-Path $reportDir "index.html"
    if (Test-Path $htmlPath) {
        Write-Host "Opening $htmlPath" -ForegroundColor Green
        Start-Process $htmlPath
    }
}
finally {
    Pop-Location
}
