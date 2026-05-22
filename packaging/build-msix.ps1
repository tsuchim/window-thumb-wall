<#
  build-msix.ps1 - Build WindowThumbWall.Package.wapproj with Visual Studio MSBuild
  Usage: .\packaging\build-msix.ps1

  This script intentionally does not use dotnet build for the .wapproj.
  Desktop Bridge imports for the packaging project come from Visual Studio MSBuild tooling.
#>
param(
    [string]$Configuration = "Release",
    [string]$Platform = "",
    [string]$Runtime = "",
    [switch]$Unsigned = $false
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

function Get-DevCertificatePaths {
    $root = Join-Path $env:LOCALAPPDATA "WindowThumbWall\devcert"
    return [pscustomobject]@{
        Root = $root
        PfxPath = Join-Path $root "WindowThumbWall.LocalTest.pfx"
        CerPath = Join-Path $root "WindowThumbWall.LocalTest.cer"
    }
}

function Ensure-LocalSigningCertificate {
    param(
        [string]$Publisher
    )

    $paths = Get-DevCertificatePaths
    if (-not (Test-Path $paths.Root)) {
        New-Item -ItemType Directory -Path $paths.Root -Force | Out-Null
    }

    if ((Test-Path $paths.PfxPath) -and (Test-Path $paths.CerPath)) {
        return $paths
    }

    $subject = $Publisher
    $certificate = New-SelfSignedCertificate `
        -Type Custom `
        -Subject $subject `
        -FriendlyName "WindowThumbWall Local Test Certificate" `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -KeyAlgorithm RSA `
        -KeyLength 2048 `
        -HashAlgorithm SHA256 `
        -KeyExportPolicy Exportable `
        -NotAfter (Get-Date).AddYears(5) `
        -TextExtension @(
            "2.5.29.37={text}1.3.6.1.5.5.7.3.3",
            "2.5.29.19={text}CA=false"
        ) `
        -KeyUsage DigitalSignature

    $password = ConvertTo-SecureString -String "WindowThumbWallLocalTest" -Force -AsPlainText
    Export-PfxCertificate -Cert $certificate -FilePath $paths.PfxPath -Password $password | Out-Null
    Export-Certificate -Cert $certificate -FilePath $paths.CerPath | Out-Null

    return $paths
}

function Remove-DuplicateCertificatesFromAppPackages {
    param(
        [string]$AppPackagesRoot
    )

    if (-not (Test-Path $AppPackagesRoot)) {
        return
    }

    Get-ChildItem -Path $AppPackagesRoot -Directory -Filter "*_Test" | ForEach-Object {
        Get-ChildItem -Path $_.FullName -Filter "*.cer" -File |
            Where-Object { $_.Name -eq "WindowThumbWall.LocalTest.cer" } |
            Remove-Item -Force
    }
}

function Get-ManifestIdentity {
    param(
        [string]$ManifestPath
    )

    [xml]$manifestXml = Get-Content $ManifestPath
    $identity = $manifestXml.Package.Identity
    if ($null -eq $identity) {
        throw "Identity could not be read from manifest: $ManifestPath"
    }

    return [pscustomobject]@{
        Name = [string]$identity.Name
        Publisher = [string]$identity.Publisher
        Version = [version][string]$identity.Version
    }
}

function Resolve-LocalPackageVersion {
    param(
        [string]$PackageName,
        [version]$BaseVersion
    )

    $installedPackage = Get-AppxPackage $PackageName -ErrorAction SilentlyContinue |
        Sort-Object Version -Descending |
        Select-Object -First 1

    if ($null -eq $installedPackage) {
        return $BaseVersion
    }

    $installedVersion = [version]$installedPackage.Version
    if ($installedVersion.Major -eq $BaseVersion.Major -and
        $installedVersion.Minor -eq $BaseVersion.Minor -and
        $installedVersion.Build -eq $BaseVersion.Build -and
        $installedVersion.Revision -ge $BaseVersion.Revision) {
        return [version]::new(
            $BaseVersion.Major,
            $BaseVersion.Minor,
            $BaseVersion.Build,
            $installedVersion.Revision + 1)
    }

    return $BaseVersion
}

function Set-ManifestVersion {
    param(
        [string]$ManifestPath,
        [version]$TargetVersion
    )

    [xml]$manifestXml = Get-Content $ManifestPath
    $manifestXml.Package.Identity.Version = $TargetVersion.ToString()
    $settings = New-Object System.Xml.XmlWriterSettings
    $settings.Indent = $true
    $settings.OmitXmlDeclaration = $false
    $settings.Encoding = [System.Text.UTF8Encoding]::new($false)
    $tempPath = "$ManifestPath.tmp"
    $writer = [System.Xml.XmlWriter]::Create($tempPath, $settings)
    try {
        $manifestXml.Save($writer)
    }
    finally {
        $writer.Dispose()
    }

    try {
        [System.IO.File]::Replace($tempPath, $ManifestPath, $null, $true)
    }
    catch {
        [System.IO.File]::Copy($tempPath, $ManifestPath, $true)
        Remove-Item $tempPath -ErrorAction SilentlyContinue
    }
}

$projectPath = Join-Path $PSScriptRoot "WindowThumbWall.Package.wapproj"
if (-not (Test-Path $projectPath)) {
    throw "Packaging project not found: $projectPath"
}

$packageManifestPath = Join-Path $PSScriptRoot "Package.appxmanifest"
$registerManifestPath = Join-Path $PSScriptRoot "AppxManifest.xml"
$packageIdentity = Get-ManifestIdentity -ManifestPath $packageManifestPath
$publisher = $packageIdentity.Publisher
$packageName = $packageIdentity.Name
$basePackageVersion = $packageIdentity.Version
$effectivePackageVersion = Resolve-LocalPackageVersion -PackageName $packageName -BaseVersion $basePackageVersion

$resolvedPlatform = Resolve-PackagePlatform -RequestedPlatform $Platform -RequestedRuntime $Runtime
$toolchain = Get-VisualStudioPackagingToolchain

Write-Output "Using Visual Studio packaging toolchain:"
Write-Output "  DisplayName=$($toolchain.DisplayName)"
Write-Output "  InstallationPath=$($toolchain.InstallationPath)"
Write-Output "MSBuildPath=$($toolchain.MSBuildPath)"
Write-Output "DesktopBridgeProps=$($toolchain.DesktopBridgePropsPath)"
Write-Output "DesktopBridgeTargets=$($toolchain.DesktopBridgeTargetsPath)"
Write-Output "BasePackageVersion=$basePackageVersion"
Write-Output "EffectivePackageVersion=$effectivePackageVersion"

$msbuildArgs = @(
    $projectPath
    "/restore"
    "/p:Configuration=$Configuration"
    "/p:Platform=$resolvedPlatform"
    "/m"
)

if ($Unsigned) {
    Write-Output "Building unsigned package artifacts."
}
else {
    $certificatePaths = Ensure-LocalSigningCertificate -Publisher $publisher
    Write-Output "Using local test signing certificate:"
    Write-Output "  PfxPath=$($certificatePaths.PfxPath)"
    Write-Output "  CerPath=$($certificatePaths.CerPath)"

    $msbuildArgs += @(
        "/p:AppxPackageSigningEnabled=true"
        "/p:PackageCertificateKeyFile=$($certificatePaths.PfxPath)"
        "/p:PackageCertificatePassword=WindowThumbWallLocalTest"
    )
}

Write-Output "Building WindowThumbWall.Package.wapproj with Visual Studio MSBuild..."
if ($effectivePackageVersion -ne $basePackageVersion) {
    $originalPackageManifest = Get-Content $packageManifestPath -Raw
    $originalRegisterManifest = Get-Content $registerManifestPath -Raw
    Set-ManifestVersion -ManifestPath $packageManifestPath -TargetVersion $effectivePackageVersion
    Set-ManifestVersion -ManifestPath $registerManifestPath -TargetVersion $effectivePackageVersion
}

try {
    & $toolchain.MSBuildPath @msbuildArgs
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}
finally {
    if ($effectivePackageVersion -ne $basePackageVersion) {
        [System.IO.File]::WriteAllText($packageManifestPath, $originalPackageManifest, [System.Text.UTF8Encoding]::new($false))
        [System.IO.File]::WriteAllText($registerManifestPath, $originalRegisterManifest, [System.Text.UTF8Encoding]::new($false))
    }
}

Remove-DuplicateCertificatesFromAppPackages -AppPackagesRoot (Join-Path $PSScriptRoot "AppPackages")

exit 0
