<#
  install-msix.ps1 - Install the latest local test MSIX package in the current terminal
  Usage: .\packaging\install-msix.ps1
#>
param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Get-LatestPackageDirectory {
    $appPackagesRoot = Join-Path $PSScriptRoot "AppPackages"
    if (-not (Test-Path $appPackagesRoot)) {
        throw "AppPackages directory not found. Run .\packaging\build-msix.ps1 first."
    }

    $packageDir = Get-ChildItem -Path $appPackagesRoot -Directory -Filter "*_Test" |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if ($null -eq $packageDir) {
        throw "No local test package directory was found under '$appPackagesRoot'. Run .\packaging\build-msix.ps1 first."
    }

    return $packageDir
}

function Get-TargetArchitecture {
    if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq [System.Runtime.InteropServices.Architecture]::Arm64) {
        return "ARM64"
    }

    return "x64"
}

function Get-DependencyPackages {
    param(
        [string]$PackageDirectory,
        [string]$TargetArchitecture
    )

    $packages = New-Object System.Collections.Generic.List[string]

    $platformDir = Join-Path (Join-Path $PackageDirectory "Dependencies") $TargetArchitecture
    if (Test-Path $platformDir) {
        Get-ChildItem -Path $platformDir -File |
            Where-Object { $_.Extension -in @(".appx", ".msix") } |
            Sort-Object Name |
            ForEach-Object { $packages.Add($_.FullName) }
    }

    # Windows App Runtime dependencies are packaged under win32.
    $win32Dir = Join-Path (Join-Path $PackageDirectory "Dependencies") "win32"
    if (Test-Path $win32Dir) {
        Get-ChildItem -Path $win32Dir -File |
            Where-Object { $_.Extension -in @(".appx", ".msix") } |
            Sort-Object Name |
            ForEach-Object {
                $candidate = $_
                if (-not ($packages | Where-Object { [System.IO.Path]::GetFileName($_) -eq $candidate.Name })) {
                    $packages.Add($candidate.FullName)
                }
            }
    }

    return $packages
}

function Test-DeveloperModeEnabled {
    $appModelUnlock = Get-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock" -ErrorAction SilentlyContinue
    if ($null -eq $appModelUnlock) {
        return $false
    }

    $allowDevelopmentWithoutDevLicense =
        if ($appModelUnlock.PSObject.Properties.Name -contains "AllowDevelopmentWithoutDevLicense") {
            $appModelUnlock.AllowDevelopmentWithoutDevLicense
        }
        else {
            0
        }

    $allowAllTrustedApps =
        if ($appModelUnlock.PSObject.Properties.Name -contains "AllowAllTrustedApps") {
            $appModelUnlock.AllowAllTrustedApps
        }
        else {
            0
        }

    return ($allowDevelopmentWithoutDevLicense -eq 1) -or ($allowAllTrustedApps -eq 1)
}

function Import-CertificateForCurrentUser {
    param(
        [string]$CertificatePath
    )

    Write-Output "Importing certificate into CurrentUser\\TrustedPeople..."
    certutil.exe -user -addstore TrustedPeople $CertificatePath | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to import the certificate into CurrentUser\\TrustedPeople. certutil exit code: $LASTEXITCODE"
    }

    Write-Output ""
    Write-Output "Importing certificate into CurrentUser\\Root..."
    certutil.exe -user -addstore Root $CertificatePath | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to import the certificate into CurrentUser\\Root. certutil exit code: $LASTEXITCODE"
    }
}

function Install-Dependencies {
    param(
        [System.Collections.Generic.List[string]]$DependencyPackages
    )

    foreach ($dependency in $DependencyPackages) {
        Write-Output "Installing dependency: $dependency"
        Add-AppxPackage -Path $dependency -ForceApplicationShutdown
    }
}

function Get-LocalRegisterStagePath {
    param(
        [string]$TargetArchitecture,
        [version]$PackageVersion
    )

    $stagePath = Join-Path $PSScriptRoot ("local-register\" + $TargetArchitecture)
    if (Test-Path $stagePath) {
        Remove-Item -Path $stagePath -Recurse -Force
    }

    New-Item -ItemType Directory -Path $stagePath -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $stagePath "Assets") -Force | Out-Null

    $rid = if ($TargetArchitecture -eq "ARM64") { "win-arm64" } else { "win-x64" }
    $repoRoot = Split-Path -Parent $PSScriptRoot
    $publishPath = Join-Path $repoRoot ("bin\" + $TargetArchitecture + "\Release\net10.0-windows10.0.17763.0\" + $rid + "\msixpublish")

    if (-not (Test-Path $publishPath)) {
        throw "Publish layout not found: $publishPath. Run .\packaging\build-msix.ps1 first."
    }

    Copy-Item (Join-Path $PSScriptRoot "Assets\*") (Join-Path $stagePath "Assets") -Recurse -Force
    Copy-Item (Join-Path $publishPath "*") $stagePath -Recurse -Force
    Copy-Item (Join-Path $PSScriptRoot "AppxManifest.xml") (Join-Path $stagePath "AppxManifest.xml") -Force

    [xml]$stageManifest = Get-Content (Join-Path $stagePath "AppxManifest.xml")
    $stageManifest.Package.Identity.Version = $PackageVersion.ToString()
    $settings = New-Object System.Xml.XmlWriterSettings
    $settings.Indent = $true
    $settings.OmitXmlDeclaration = $false
    $settings.Encoding = [System.Text.UTF8Encoding]::new($false)
    $writer = [System.Xml.XmlWriter]::Create((Join-Path $stagePath "AppxManifest.xml"), $settings)
    try {
        $stageManifest.Save($writer)
    }
    finally {
        $writer.Dispose()
    }

    return $stagePath
}

function Install-BundlePackage {
    param(
        [string]$BundlePath,
        [System.Collections.Generic.List[string]]$DependencyPackages
    )

    Write-Output ""
    Write-Output "Installing MSIX package..."
    $DependencyPackages | ForEach-Object { Write-Output "  Dependency: $_" }
    Add-AppxPackage -Path $BundlePath -DependencyPath $DependencyPackages -ForceApplicationShutdown
}

function Install-RegisteredPackageFallback {
    param(
        [string]$TargetArchitecture,
        [System.Collections.Generic.List[string]]$DependencyPackages,
        [string]$CertificatePath,
        [version]$PackageVersion
    )

    if (-not (Test-DeveloperModeEnabled)) {
        throw @"
Windows is blocking both local install paths in the current terminal.

Reason 1:
- The signed .msixbundle is not trusted for machine-wide sideloading from the current certificate state.

Reason 2:
- Developer Mode or sideloading policy is disabled, so unpackaged registration fallback is also blocked.

Choose one of these one-time fixes, then rerun:
1. Recommended: enable Windows Developer Mode.
2. Or import the test certificate as administrator:
   Import-Certificate -FilePath "$CertificatePath" -CertStoreLocation Cert:\LocalMachine\TrustedPeople

After either fix, run:
.\packaging\build-and-run-msix.ps1 -Configuration Release
"@
    }

    Write-Output ""
    Write-Output "Bundle installation failed because the local machine does not trust the test certificate."
    Write-Output "Trying unpackaged registration fallback using Developer Mode..."

    Install-Dependencies -DependencyPackages $DependencyPackages
    $stagePath = Get-LocalRegisterStagePath -TargetArchitecture $TargetArchitecture -PackageVersion $PackageVersion
    Add-AppxPackage -Register (Join-Path $stagePath "AppxManifest.xml") -ForceApplicationShutdown
}

function Launch-InstalledApp {
    $installedPackage = Get-AppxPackage tsuchim.WindowThumbWall |
        Sort-Object Version -Descending |
        Select-Object -First 1

    if ($null -eq $installedPackage) {
        throw "The package installation completed, but the installed package identity could not be resolved."
    }

    Write-Output ""
    Write-Output "Installed package: $($installedPackage.PackageFullName)"
    Write-Output "Launching WindowThumbWall..."
    Start-Process explorer.exe "shell:AppsFolder\$($installedPackage.PackageFamilyName)!App" | Out-Null
}

$packageDir = Get-LatestPackageDirectory
$targetArchitecture = Get-TargetArchitecture
$bundlePath = Get-ChildItem -Path $packageDir.FullName -File -Filter *.msixbundle |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1 -ExpandProperty FullName
$certificatePath = Get-ChildItem -Path $packageDir.FullName -File -Filter *.cer |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1 -ExpandProperty FullName
$dependencyPackages = Get-DependencyPackages -PackageDirectory $packageDir.FullName -TargetArchitecture $targetArchitecture
$packageVersionMatch = [regex]::Match([System.IO.Path]::GetFileNameWithoutExtension($bundlePath), '_(\d+\.\d+\.\d+\.\d+)_')
$packageVersion =
    if ($packageVersionMatch.Success) {
        [version]$packageVersionMatch.Groups[1].Value
    }
    else {
        throw "Package version could not be parsed from bundle name: $bundlePath"
    }

if (-not $bundlePath) {
    throw "No .msixbundle file was found in '$($packageDir.FullName)'."
}

if (-not $certificatePath) {
    throw "No certificate file was found in '$($packageDir.FullName)'."
}

Write-Output "Installing local test package from: $($packageDir.FullName)"
Write-Output "Bundle: $bundlePath"
Write-Output "Certificate: $certificatePath"
Write-Output "Target architecture: $targetArchitecture"
Write-Output ""

Import-CertificateForCurrentUser -CertificatePath $certificatePath

try {
    Install-BundlePackage -BundlePath $bundlePath -DependencyPackages $dependencyPackages
}
catch {
    $message = $_.Exception.Message
    $hresult = $_.Exception.HResult

    if ($message -like "*0x800B0109*" -or $hresult -eq -2146762487) {
        Install-RegisteredPackageFallback -TargetArchitecture $targetArchitecture -DependencyPackages $dependencyPackages -CertificatePath $certificatePath -PackageVersion $packageVersion
    }
    else {
        throw
    }
}

Launch-InstalledApp
