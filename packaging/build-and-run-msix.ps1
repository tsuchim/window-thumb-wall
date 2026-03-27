<#
  build-and-run-msix.ps1 - Build, install, and launch the local test MSIX package
  Usage: .\packaging\build-and-run-msix.ps1
#>
param(
    [string]$Configuration = "Release",
    [string]$Platform = "",
    [string]$Runtime = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$buildScript = Join-Path $PSScriptRoot "build-msix.ps1"
$installScript = Join-Path $PSScriptRoot "install-msix.ps1"

if (-not (Test-Path $buildScript)) {
    throw "Build script not found: $buildScript"
}

if (-not (Test-Path $installScript)) {
    throw "Install script not found: $installScript"
}

& $buildScript -Configuration $Configuration -Platform $Platform -Runtime $Runtime
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

& $installScript
exit $LASTEXITCODE
