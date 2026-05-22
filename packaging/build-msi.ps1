<#
  build-msi.ps1 — Publish + build MSI for WindowThumbWall using WiX v5
  Usage: .\packaging\build-msi.ps1
  Prerequisites: WiX v5 dotnet tool (installed automatically if missing)
#>
param(
    [string]$Configuration = "Release",
    [string]$Runtime       = "win-x64"
)

$ErrorActionPreference = "Stop"
$root    = Split-Path $PSScriptRoot -Parent
$pubDir  = Join-Path $root "publish-msi-$Runtime"
$pkgDir  = Join-Path $root "packaging"

[xml]$projectXml = Get-Content (Join-Path $root "WindowThumbWall.csproj")
$appVersion = [string]$projectXml.Project.PropertyGroup.Version
$msiProductVersion = [string]$projectXml.Project.PropertyGroup.AssemblyVersion
if ([string]::IsNullOrWhiteSpace($appVersion) -or [string]::IsNullOrWhiteSpace($msiProductVersion)) {
    throw "Could not read version properties from WindowThumbWall.csproj."
}

$outMsi  = Join-Path $root "WindowThumbWall-v$appVersion-$Runtime.msi"

# ── 1. Ensure WiX v5 CLI is available ────────────────────────
Write-Host ">> Checking WiX toolset..." -ForegroundColor Cyan
$wixCmd = Get-Command wix -ErrorAction SilentlyContinue
if (-not $wixCmd) {
    Write-Host ">> Installing WiX v5 dotnet tool..." -ForegroundColor Yellow
    dotnet tool install --global wix
}
wix --version

# Ensure WixToolset.UI.wixext is available
Write-Host ">> Ensuring WiX UI extension..." -ForegroundColor Cyan
$wixVer = (wix --version) -replace '\+.*',''
wix extension add -g "WixToolset.UI.wixext/$wixVer" 2>$null
Write-Host "   WiX UI extension ready."

# ── 2. Publish self-contained ────────────────────────────────
Write-Host ">> Publishing..." -ForegroundColor Cyan
dotnet publish "$root\WindowThumbWall.csproj" `
    -c $Configuration -r $Runtime --self-contained `
    -p:PublishSingleFile=false `
    -o $pubDir

# ── 3. Build MSI ─────────────────────────────────────────────
Write-Host ">> Building MSI for $Runtime..." -ForegroundColor Cyan
if (Test-Path $outMsi) { Remove-Item $outMsi -Force }
$wixArch = if ($Runtime -like "*arm64*") { "arm64" } else { "x64" }
wix build "$pkgDir\WindowThumbWall.wxs" `
    -arch $wixArch `
    -ext WixToolset.UI.wixext `
    -d PublishDir="$pubDir" `
    -d ProductVersion="$msiProductVersion" `
    -o $outMsi

# ── Done ──────────────────────────────────────────────────────
$size = [math]::Round((Get-Item $outMsi).Length / 1MB, 1)
Write-Host ">> Done: $outMsi ($size MB)" -ForegroundColor Green
