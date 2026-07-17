<#
  build-msi.ps1 — Publish + build MSI for WindowThumbWall using WiX v7
  Prerequisite: WinGet WiXToolset.WiXCLI installation with OSMF EULA accepted
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

# ── 1. Require WiX v7 CLI ────────────────────────────────────
Write-Host ">> Checking WiX toolset..." -ForegroundColor Cyan
$wixCmd = Get-Command wix -ErrorAction SilentlyContinue
if (-not $wixCmd) {
    throw "WiX 7 is required. Install it with: winget install --id WiXToolset.WiXCLI --exact"
}
$wixVersion = wix --version
if ($wixVersion -notmatch '^7\.') {
    throw "WiX 7 is required; found $wixVersion. Install WiXToolset.WiXCLI through WinGet."
}
Write-Host $wixVersion

# Ensure WixToolset.UI.wixext is available
Write-Host ">> Ensuring WiX UI extension..." -ForegroundColor Cyan
$wixVer = '7.0.0'
wix extension add -acceptEula wix7 -g "WixToolset.UI.wixext/$wixVer"
if ($LASTEXITCODE -ne 0) {
    throw "Failed to add the WiX UI extension. Confirm that WiX was installed with its license accepted."
}
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
    -acceptEula wix7 `
    -arch $wixArch `
    -ext WixToolset.UI.wixext `
    -d PublishDir="$pubDir" `
    -d ProductVersion="$msiProductVersion" `
    -o $outMsi
if ($LASTEXITCODE -ne 0) {
    throw "WiX MSI build failed."
}

& "$PSScriptRoot\verify-msi-payload.ps1" -MsiPath $outMsi

# ── Done ──────────────────────────────────────────────────────
$size = [math]::Round((Get-Item $outMsi).Length / 1MB, 1)
Write-Host ">> Done: $outMsi ($size MB)" -ForegroundColor Green
