<#
  build-zip.ps1 — Publish + create portable ZIP for WindowThumbWall
  Usage: .\packaging\build-zip.ps1
#>
param(
    [string]$Configuration = "Release",
    [string]$Runtime       = "win-x64"
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent

[xml]$projectXml = Get-Content (Join-Path $root "WindowThumbWall.csproj")
$appVersion = [string]$projectXml.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($appVersion)) {
    throw "Could not read <Version> from WindowThumbWall.csproj."
}

$outZip = Join-Path $root "WindowThumbWall-v$appVersion-$Runtime.zip"
$stageRoot = Join-Path $root "artifacts"
$stageName = ".windowthumbwall-zip-stage-$([guid]::NewGuid().ToString('N'))"
$stageDir = Join-Path $stageRoot $stageName
$publishDir = Join-Path $stageDir "publish"
$stageZip = Join-Path $stageDir "package.zip"

try {
    # A new, isolated publish directory prevents stale files from prior builds
    # from entering the archive.
    New-Item -ItemType Directory -Path $publishDir -ErrorAction Stop | Out-Null

    Write-Host ">> Publishing for $Runtime..." -ForegroundColor Cyan
    dotnet publish (Join-Path $root "WindowThumbWall.csproj") `
        -c $Configuration -r $Runtime --self-contained `
        -p:PublishSingleFile=false `
        -o $publishDir
    $publishExitCode = $LASTEXITCODE
    if ($publishExitCode -ne 0) {
        throw "dotnet publish failed with exit code $publishExitCode; ZIP was not updated."
    }

    Get-ChildItem -LiteralPath $publishDir -Filter *.pdb -Recurse -File | Remove-Item -Force

    Write-Host ">> Creating ZIP..." -ForegroundColor Cyan
    Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $stageZip -CompressionLevel Optimal -ErrorAction Stop

    # Publish only the completed archive. Keep a previously valid ZIP intact if
    # publishing the replacement fails.
    if (Test-Path -LiteralPath $outZip -PathType Leaf) {
        [System.IO.File]::Replace($stageZip, $outZip, [NullString]::Value)
    } else {
        [System.IO.File]::Move($stageZip, $outZip)
    }

    $size = [math]::Round((Get-Item -LiteralPath $outZip).Length / 1MB, 1)
    Write-Host ">> Done: $outZip ($size MB)" -ForegroundColor Green
}
finally {
    # Remove only this invocation's exact scratch directory, after validating
    # its location and generated name.
    $resolvedRoot = [System.IO.Path]::GetFullPath($stageRoot)
    $resolvedStage = [System.IO.Path]::GetFullPath($stageDir)
    if ([string]::Equals([System.IO.Path]::GetDirectoryName($resolvedStage), $resolvedRoot, [System.StringComparison]::OrdinalIgnoreCase) -and
        [System.IO.Path]::GetFileName($resolvedStage) -match '^\.windowthumbwall-zip-stage-[0-9a-f]{32}$' -and
        (Test-Path -LiteralPath $resolvedStage -PathType Container)) {
        Remove-Item -LiteralPath $resolvedStage -Recurse -Force -ErrorAction Stop
    }
}
