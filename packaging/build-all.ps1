<#
  build-all.ps1 — Build all distribution packages (MSIX, MSI, ZIP)
  Usage: .\packaging\build-all.ps1
#>
param(
    [string]$Configuration = "Release",
    [string]$Runtime       = "win-x64"
)

$ErrorActionPreference = "Stop"
$pkgDir = $PSScriptRoot

function Invoke-PackageBuild {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ScriptPath,
        [Parameter(Mandatory = $true)]
        [string]$PackageName
    )

    # Reset the native status before each child so an earlier command cannot
    # make a successful child appear to have failed.
    $global:LASTEXITCODE = 0
    & $ScriptPath -Configuration $Configuration -Runtime $Runtime
    $childSucceeded = $?
    $childExitCode = $LASTEXITCODE

    if ($childExitCode -ne 0) {
        throw "$PackageName build script failed with exit code $childExitCode."
    }
    if (-not $childSucceeded) {
        throw "$PackageName build script failed."
    }
}

Write-Host "========================================" -ForegroundColor White
Write-Host "  WindowThumbWall — Build All Packages"   -ForegroundColor White
Write-Host "========================================" -ForegroundColor White

# ── ZIP ──────────────────────────────────────────────────────
Write-Host "`n>>> [1/3] ZIP <<<" -ForegroundColor Magenta
Invoke-PackageBuild -ScriptPath "$pkgDir\build-zip.ps1" -PackageName "ZIP"

# ── MSI ──────────────────────────────────────────────────────
Write-Host "`n>>> [2/3] MSI <<<" -ForegroundColor Magenta
Invoke-PackageBuild -ScriptPath "$pkgDir\build-msi.ps1" -PackageName "MSI"

# ── MSIX ─────────────────────────────────────────────────────
Write-Host "`n>>> [3/3] MSIX <<<" -ForegroundColor Magenta
Invoke-PackageBuild -ScriptPath "$pkgDir\build-msix.ps1" -PackageName "MSIX"

Write-Host "`n========================================" -ForegroundColor Green
Write-Host "  All packages built successfully!"        -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
