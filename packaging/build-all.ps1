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

Write-Host "========================================" -ForegroundColor White
Write-Host "  WindowThumbWall — Build All Packages"   -ForegroundColor White
Write-Host "========================================" -ForegroundColor White

# ── ZIP ──────────────────────────────────────────────────────
Write-Host "`n>>> [1/3] ZIP <<<" -ForegroundColor Magenta
& "$pkgDir\build-zip.ps1" -Configuration $Configuration -Runtime $Runtime

# ── MSI ──────────────────────────────────────────────────────
Write-Host "`n>>> [2/3] MSI <<<" -ForegroundColor Magenta
& "$pkgDir\build-msi.ps1" -Configuration $Configuration -Runtime $Runtime

# ── MSIX ─────────────────────────────────────────────────────
Write-Host "`n>>> [3/3] MSIX <<<" -ForegroundColor Magenta
& "$pkgDir\build-msix.ps1" -Configuration $Configuration -Runtime $Runtime

Write-Host "`n========================================" -ForegroundColor Green
Write-Host "  All packages built successfully!"        -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
