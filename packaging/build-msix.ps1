<#
  build-msix.ps1 — Publish + package + sign MSIX for WindowThumbWall
  Usage: .\packaging\build-msix.ps1
#>
param(
    [string]$Configuration = "Release",
    [string]$Runtime       = "win-x64",
    [string]$CertSubject   = "CN=tsuchim"
)

$ErrorActionPreference = "Stop"
$root     = Split-Path $PSScriptRoot -Parent
$pubDir   = Join-Path $root "publish-msix"
$pkgDir   = Join-Path $root "packaging"
$outMsix  = Join-Path $root "WindowThumbWall-v0.2-win-x64.msix"

# ── 1. Publish self-contained ────────────────────────────────
Write-Host ">> Publishing..." -ForegroundColor Cyan
dotnet publish "$root\WindowThumbWall.csproj" `
    -c $Configuration -r $Runtime --self-contained `
    -p:PublishSingleFile=false `
    -o $pubDir

# ── 2. Copy manifest + assets into publish folder ────────────
Write-Host ">> Preparing package layout..." -ForegroundColor Cyan
Copy-Item "$pkgDir\AppxManifest.xml" $pubDir -Force
Copy-Item "$pkgDir\Assets" "$pubDir\Assets" -Recurse -Force

# ── 3. Find Windows SDK tools ────────────────────────────────
$makeappx = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin\*\x64\makeappx.exe" -ErrorAction SilentlyContinue |
            Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
$signtool = Join-Path (Split-Path $makeappx) "signtool.exe"

# ── 4. Create MSIX ───────────────────────────────────────────
Write-Host ">> Creating MSIX..." -ForegroundColor Cyan
if (Test-Path $outMsix) { Remove-Item $outMsix -Force }
& $makeappx pack /d $pubDir /p $outMsix /o

# ── 5. Sign with certificate from CurrentUser\My (optional) ──
Write-Host ">> Attempting to sign MSIX..." -ForegroundColor Cyan
$cert = Get-ChildItem Cert:\CurrentUser\My -ErrorAction SilentlyContinue |
        Where-Object { $_.Subject -eq $CertSubject -and $_.NotAfter -gt (Get-Date) } |
        Select-Object -First 1

if (-not $cert) {
    Write-Host ">> WARNING: Certificate '$CertSubject' not found — MSIX is UNSIGNED." -ForegroundColor Yellow
} else {
    & $signtool sign /fd SHA256 /sha1 $cert.Thumbprint /td SHA256 $outMsix
    if ($LASTEXITCODE -ne 0) {
        Write-Host ">> WARNING: Signing failed — MSIX is UNSIGNED." -ForegroundColor Yellow
    } else {
        Write-Host ">> Signed successfully." -ForegroundColor Green
    }
}

# ── Done ──────────────────────────────────────────────────────
$size = [math]::Round((Get-Item $outMsix).Length / 1MB, 1)
Write-Host ">> Done: $outMsix ($size MB)" -ForegroundColor Green
