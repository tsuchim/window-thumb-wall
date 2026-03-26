<#
  build-msix.ps1 - Build WindowThumbWall.Package.wapproj with Visual Studio MSBuild
  Usage: .\packaging\build-msix.ps1

  This script intentionally does not use dotnet build for the .wapproj.
  Desktop Bridge imports for the packaging project come from Visual Studio MSBuild tooling.
#>
param(
    [string]$Configuration = "Release",
    [string]$Platform = "",
    [string]$Runtime = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Resolve-PackagePlatform {
    param(
        [string]$RequestedPlatform,
        [string]$RequestedRuntime
    )

    if ($RequestedPlatform) {
        if ($RequestedPlatform -notin @("x64", "ARM64")) {
            throw "Unsupported -Platform '$RequestedPlatform'. Use x64 or ARM64."
        }

        return $RequestedPlatform
    }

    if ($RequestedRuntime -match "arm64") {
        return "ARM64"
    }

    return "x64"
}

function Get-VisualStudioPackagingToolchain {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (-not (Test-Path $vswhere)) {
        throw "vswhere.exe was not found at '$vswhere'. Install Visual Studio 2022 or later with MSIX/Desktop Bridge packaging tools."
    }

    $rawInstances = & $vswhere -all -prerelease -products * -format json
    if ($LASTEXITCODE -ne 0) {
        throw "vswhere.exe failed while locating Visual Studio installations."
    }

    $instances = $rawInstances | ConvertFrom-Json
    if ($null -eq $instances) {
        $instances = @()
    } elseif ($instances -isnot [System.Array]) {
        $instances = @($instances)
    }

    $candidates = foreach ($instance in $instances) {
        $installationPath = $instance.installationPath
        if (-not $installationPath) {
            continue
        }

        $msbuildPath = Join-Path $installationPath "MSBuild\Current\Bin\MSBuild.exe"
        if (-not (Test-Path $msbuildPath)) {
            continue
        }

        $desktopBridgeRoot = Join-Path $installationPath "MSBuild\Microsoft\DesktopBridge"
        $desktopBridgePropsPath = Join-Path $desktopBridgeRoot "Microsoft.DesktopBridge.props"
        $desktopBridgeTargetsPath = Join-Path $desktopBridgeRoot "Microsoft.DesktopBridge.targets"

        [pscustomobject]@{
            DisplayName = $instance.displayName
            InstallationPath = $installationPath
            InstallationVersion = $instance.installationVersion
            MSBuildPath = $msbuildPath
            DesktopBridgePropsPath = $desktopBridgePropsPath
            DesktopBridgeTargetsPath = $desktopBridgeTargetsPath
            HasDesktopBridge = (Test-Path $desktopBridgePropsPath) -and (Test-Path $desktopBridgeTargetsPath)
        }
    }

    $usableCandidates = $candidates |
        Where-Object { $_.HasDesktopBridge } |
        Sort-Object { [version]$_.InstallationVersion } -Descending

    if ($usableCandidates) {
        return $usableCandidates | Select-Object -First 1
    }

    $candidateSummary = if ($candidates) {
        ($candidates | ForEach-Object {
            @(
                "- $($_.DisplayName) [$($_.InstallationVersion)]"
                "  MSBuild: $($_.MSBuildPath)"
                "  DesktopBridge props: $($_.DesktopBridgePropsPath)"
                "  DesktopBridge targets: $($_.DesktopBridgeTargetsPath)"
            ) -join "`n"
        }) -join "`n"
    } else {
        "- No Visual Studio installations with MSBuild were found by vswhere."
    }

    throw @"
Visual Studio MSBuild/Desktop Bridge tooling is required to build packaging/WindowThumbWall.Package.wapproj.
Install Visual Studio 2022 or later with MSIX/Desktop Bridge packaging tools, then rerun this script.

Detected installations:
$candidateSummary
"@
}

$projectPath = Join-Path $PSScriptRoot "WindowThumbWall.Package.wapproj"
if (-not (Test-Path $projectPath)) {
    throw "Packaging project not found: $projectPath"
}

$resolvedPlatform = Resolve-PackagePlatform -RequestedPlatform $Platform -RequestedRuntime $Runtime
$toolchain = Get-VisualStudioPackagingToolchain

Write-Output "Using Visual Studio packaging toolchain:"
Write-Output "  DisplayName=$($toolchain.DisplayName)"
Write-Output "  InstallationPath=$($toolchain.InstallationPath)"
Write-Output "MSBuildPath=$($toolchain.MSBuildPath)"
Write-Output "DesktopBridgeProps=$($toolchain.DesktopBridgePropsPath)"
Write-Output "DesktopBridgeTargets=$($toolchain.DesktopBridgeTargetsPath)"

$msbuildArgs = @(
    $projectPath
    "/restore"
    "/p:Configuration=$Configuration"
    "/p:Platform=$resolvedPlatform"
    "/m"
)

Write-Output "Building WindowThumbWall.Package.wapproj with Visual Studio MSBuild..."
& $toolchain.MSBuildPath @msbuildArgs
exit $LASTEXITCODE
