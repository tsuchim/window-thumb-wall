<#
  build-zip.ps1 — Publish + create portable ZIP for WindowThumbWall
  Usage: .\packaging\build-zip.ps1
#>
param(
    [string]$Configuration = "Release",
    [string]$Runtime       = "win-x64"
)

$ErrorActionPreference = "Stop"
$root    = Split-Path $PSScriptRoot -Parent
$pubDir  = Join-Path $root "publish-zip-$Runtime"

[xml]$projectXml = Get-Content (Join-Path $root "WindowThumbWall.csproj")
$appVersion = [string]$projectXml.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($appVersion)) {
    throw "Could not read <Version> from WindowThumbWall.csproj."
}

$outZip  = Join-Path $root "WindowThumbWall-v$appVersion-$Runtime.zip"

# ── 1. Publish self-contained ────────────────────────────────
Write-Host ">> Publishing for $Runtime..." -ForegroundColor Cyan
dotnet publish "$root\WindowThumbWall.csproj" `
    -c $Configuration -r $Runtime --self-contained `
    -p:PublishSingleFile=false `
    -o $pubDir

# ── 2. Remove PDB files (optional, keeps ZIP small) ──────────
Get-ChildItem $pubDir -Filter *.pdb -Recurse | Remove-Item -Force

# ── 3. Create ZIP ────────────────────────────────────────────
Write-Host ">> Creating ZIP..." -ForegroundColor Cyan
if (Test-Path $outZip) { Remove-Item $outZip -Force }
Compress-Archive -Path "$pubDir\*" -DestinationPath $outZip -CompressionLevel Optimal

# ── Done ──────────────────────────────────────────────────────
$size = [math]::Round((Get-Item $outZip).Length / 1MB, 1)
Write-Host ">> Done: $outZip ($size MB)" -ForegroundColor Green
