[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$MsiPath,
    [long]$MinimumSizeBytes = 10MB
)

$ErrorActionPreference = 'Stop'

$resolvedPath = (Resolve-Path -LiteralPath $MsiPath -ErrorAction Stop).Path
$item = Get-Item -LiteralPath $resolvedPath
if ($item.Length -lt $MinimumSizeBytes) {
    throw "MSI payload is implausibly small: $resolvedPath ($($item.Length) bytes; minimum $MinimumSizeBytes bytes)."
}

$installer = New-Object -ComObject WindowsInstaller.Installer
$database = $installer.OpenDatabase($resolvedPath, 0)
$view = $database.OpenView('SELECT `File`, `FileName` FROM `File`')
$view.Execute()

$rowCount = 0
$foundExecutable = $false
while ($record = $view.Fetch()) {
    $rowCount++
    $fileName = [string]$record.StringData(2)
    $longName = ($fileName -split '\|')[-1]
    if ($longName -eq 'WindowThumbWall.exe') {
        $foundExecutable = $true
    }
}

Write-Host "MSI payload verification: size=$($item.Length) bytes; fileRows=$rowCount; executable=$foundExecutable"
if ($rowCount -eq 0 -or -not $foundExecutable) {
    throw "MSI payload verification failed: WindowThumbWall.exe was not found in the File table."
}
