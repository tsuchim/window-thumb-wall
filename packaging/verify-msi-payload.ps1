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

$rowCount = 0
$foundExecutable = $false
$installer = $null
$database = $null
$view = $null

try {
    $installer = New-Object -ComObject WindowsInstaller.Installer
    $database = $installer.OpenDatabase($resolvedPath, 0)
    $view = $database.OpenView('SELECT `File`, `FileName` FROM `File`')
    $view.Execute()

    while ($record = $view.Fetch()) {
        try {
            $rowCount++
            $fileName = [string]$record.StringData(2)
            $longName = ($fileName -split '\|')[-1]
            if ($longName -eq 'WindowThumbWall.exe') {
                $foundExecutable = $true
            }
        }
        finally {
            [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($record)
        }
    }
}
finally {
    if ($view) {
        $view.Close()
        [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($view)
    }
    if ($database) {
        [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($database)
    }
    if ($installer) {
        [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($installer)
    }
}

Write-Host "MSI payload verification: size=$($item.Length) bytes; fileRows=$rowCount; executable=$foundExecutable"
if ($rowCount -eq 0) {
    throw 'MSI payload verification failed: the File table contains no payload rows.'
}
if (-not $foundExecutable) {
    throw 'MSI payload verification failed: WindowThumbWall.exe was not found in the File table.'
}
