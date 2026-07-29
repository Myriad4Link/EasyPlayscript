param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SkipServerPublish,
    [switch]$SkipPackage
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

Write-Host "=== EasyPlayscript VS Code Extension Packager ==="
Write-Host ""

# Step 1: Publish the LSP server
if (-not $SkipServerPublish) {
    Write-Host "[1/4] Publishing LSP server ($Configuration, $Runtime)..."

    $publishDir = Join-Path $root "EasyPlayscript.LSP" "bin" $Configuration "net10.0" $Runtime "publish"
    dotnet publish (Join-Path $root "EasyPlayscript.LSP") `
        -c $Configuration `
        -r $Runtime `
        --self-contained false `
        /p:PublishSingleFile=false `
        -o $publishDir

    if ($LASTEXITCODE -ne 0) {
        Write-Error "dotnet publish failed"
        exit 1
    }

    # Step 2: Copy server output to vscode/server/
    Write-Host "[2/4] Copying server to vscode/server/..."

    $serverDest = Join-Path $root "vscode" "server"
    if (Test-Path $serverDest) {
        Remove-Item -Recurse -Force $serverDest
    }

    # Copy all files EXCEPT .pdb files
    New-Item -ItemType Directory -Path $serverDest -Force | Out-Null
    Get-ChildItem $publishDir -File | ForEach-Object {
        Copy-Item $_.FullName -Destination (Join-Path $serverDest $_.Name)
    }

    Write-Host "  Copied $((Get-ChildItem $serverDest).Count) files"
} else {
    Write-Host "[1/4] Skipping server publish (--SkipServerPublish)"
    Write-Host "[2/4] Skipping server copy (--SkipServerPublish)"
}

# Step 3: npm install + compile
Write-Host "[3/4] npm install + compile..."
Push-Location (Join-Path $root "vscode")
try {
    npm install
    if ($LASTEXITCODE -ne 0) {
        Write-Error "npm install failed"
        exit 1
    }
    npm run compile
    if ($LASTEXITCODE -ne 0) {
        Write-Error "npm run compile failed"
        exit 1
    }
} finally {
    Pop-Location
}

# Step 4: Package .vsix
if (-not $SkipPackage) {
    Write-Host "[4/4] Packaging .vsix..."
    Push-Location (Join-Path $root "vscode")
    try {
        npx vsce package
        if ($LASTEXITCODE -ne 0) {
            Write-Error "vsce package failed"
            exit 1
        }
        $vsix = Get-ChildItem "*.vsix" | Select-Object -First 1
        Write-Host "  Created: $($vsix.Name)"
    } finally {
        Pop-Location
    }
} else {
    Write-Host "[4/4] Skipping .vsix package (--SkipPackage)"
}

Write-Host ""
Write-Host "=== Done ==="
