[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('Write', 'Verify')]
    [string]$Mode,
    [Parameter(Mandatory)]
    [string]$Directory,
    [Parameter(Mandatory)]
    [ValidatePattern('^v[0-9]+\.[0-9]+\.[0-9]+$')]
    [string]$Tag,
    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9a-f]{40}$')]
    [string]$TagObject,
    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9a-f]{40}$')]
    [string]$Commit,
    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9a-f]{40}$')]
    [string]$Tree,
    [string]$Toolchain
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$manifestPath = Join-Path $Directory 'release-provenance.json'
$names = foreach ($architecture in 'x64', 'arm64') {
    foreach ($extension in 'zip', 'msi', 'msix') {
        "WindowThumbWall-$Tag-win-$architecture.$extension"
    }
}
$names += 'release-notes.md', 'store-listing-ja.md', 'store-listing-en.md',
    'store-whats-new-ja.md', 'store-whats-new-en.md'
$unexpectedPackage = Get-ChildItem -LiteralPath $Directory -File |
    Where-Object { $_.Extension -in '.zip', '.msi', '.msix' -and $_.Name -cnotin $names } |
    Select-Object -First 1
if ($unexpectedPackage) {
    throw "Unexpected distribution package ($($unexpectedPackage.Name))."
}

$files = foreach ($name in $names) {
    $file = Get-Item -LiteralPath (Join-Path $Directory $name)
    if ($file.PSIsContainer -or $file.Length -eq 0) {
        throw "The required release build output is empty or invalid ($name)."
    }

    [pscustomobject]@{
        name = $name
        bytes = $file.Length
        sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

if ($Mode -eq 'Write') {
    if ([string]::IsNullOrWhiteSpace($Toolchain)) {
        throw 'A toolchain record is required to write release provenance.'
    }

    [ordered]@{
        schemaVersion = 1
        tag = $Tag
        tagObject = $TagObject
        commit = $Commit
        tree = $Tree
        toolchain = $Toolchain
        files = @($files)
    } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $manifestPath -Encoding utf8
    return
}

$record = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($record.schemaVersion -ne 1 -or $record.tag -cne $Tag -or
    $record.tagObject -cne $TagObject -or $record.commit -cne $Commit -or $record.tree -cne $Tree -or
    [string]::IsNullOrWhiteSpace($record.toolchain) -or @($record.files).Count -ne $files.Count) {
    throw 'The release provenance does not match the expected tag, source or package set.'
}

foreach ($file in $files) {
    $entry = @($record.files | Where-Object { $_.name -ceq $file.name })
    if ($entry.Count -ne 1 -or $entry[0].bytes -ne $file.bytes -or $entry[0].sha256 -cne $file.sha256) {
        throw "Release provenance failed for $($file.name)."
    }
}

Write-Host "Verified release provenance and $($files.Count) file hashes for $Tag."
