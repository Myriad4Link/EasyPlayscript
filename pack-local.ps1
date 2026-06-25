#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Rebuilds and repacks all EasyPlayscript NuGet packages into the local feed.
.DESCRIPTION
    Cleans old .nupkg files, builds all packable projects in dependency order,
    and clears the NuGet global cache for EasyPlayscript packages so consumers
    pick up the new versions on next restore.
.PARAMETER Configuration
    Build configuration. Defaults to Release.
.PARAMETER SkipCacheClear
    If set, does not clear the NuGet global cache entries.
#>
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$SkipCacheClear
)

$ErrorActionPreference = "Stop"
$repoRoot = $PSScriptRoot
$outputDir = Join-Path $repoRoot "nuget-local"

Write-Host "==> Cleaning old packages" -ForegroundColor Cyan
Remove-Item -Path "$outputDir\*.nupkg" -Force -ErrorAction SilentlyContinue

Write-Host "==> Building and packing EasyPlayscript.Core" -ForegroundColor Cyan
dotnet pack (Join-Path $repoRoot "EasyPlayscript.Core") -c $Configuration -o $outputDir
if ($LASTEXITCODE -ne 0) { throw "Failed to pack EasyPlayscript.Core" }

Write-Host "==> Building and packing EasyPlayscript.Generator" -ForegroundColor Cyan
dotnet pack (Join-Path $repoRoot "EasyPlayscript.Generator") -c $Configuration -o $outputDir
if ($LASTEXITCODE -ne 0) { throw "Failed to pack EasyPlayscript.Generator" }

Write-Host "==> Building and packing EasyPlayscript.BuildTask" -ForegroundColor Cyan
dotnet pack (Join-Path $repoRoot "EasyPlayscript.BuildTask") -c $Configuration -o $outputDir
if ($LASTEXITCODE -ne 0) { throw "Failed to pack EasyPlayscript.BuildTask" }

if (-not $SkipCacheClear) {
    Write-Host "==> Clearing NuGet global cache for EasyPlayscript packages" -ForegroundColor Cyan
    $packagesDir = Join-Path $env:USERPROFILE ".nuget\packages"
    foreach ($pkg in @("easyplayscript.core", "easyplayscript.generator", "easyplayscript.buildtask")) {
        $path = Join-Path $packagesDir $pkg
        if (Test-Path $path) {
            Remove-Item -Recurse -Force $path -ErrorAction SilentlyContinue
        }
    }
}

Write-Host ""
Write-Host "Done. Packages in ${outputDir}:" -ForegroundColor Green
Get-ChildItem "$outputDir\*.nupkg" | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
