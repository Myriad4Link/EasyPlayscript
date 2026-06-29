#!/usr/bin/env pwsh
# Runs code coverage for both test projects and opens the HTML reports.

$ErrorActionPreference = "Stop"

$testProjects = @(
    "EasyPlayscript.Tests",
    "EasyPlayscript.LSP.Tests"
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

foreach ($project in $testProjects) {
    Write-Host "`n=== Running coverage for $project ===" -ForegroundColor Cyan
    dotnet test $project --collect:"XPlat Code Coverage" --results-directory "TestResults/$project"
}

$reportDir = Join-Path $scriptDir "coveragereport"
$xmlPaths = @()

foreach ($project in $testProjects) {
    $resultsDir = Join-Path $scriptDir "TestResults" $project
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

$htmlPath = Join-Path $reportDir "index.html"
if (Test-Path $htmlPath) {
    Write-Host "Opening $htmlPath" -ForegroundColor Green
    Start-Process $htmlPath
}
