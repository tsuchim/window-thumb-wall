param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [Parameter(Mandatory = $false)]
    [string]$OutputDir = 'dist'
)

$ErrorActionPreference = 'Stop'

# Normalize version
$plainVersion = $Version -replace '^v', ''
$isDev = ($Version -eq 'dev')

# Paths
$releaseNotesInput = "packaging/release-notes-$plainVersion.md"
$releaseNotesJaInput = "packaging/release-notes-ja-$plainVersion.md"
$releaseNotesEnInput = "packaging/release-notes-en-$plainVersion.md"
$releaseHighlightsInput = "packaging/release-highlights-$plainVersion.md"
$releaseHighlightsJaInput = "packaging/release-highlights-ja-$plainVersion.md"
$releaseHighlightsEnInput = "packaging/release-highlights-en-$plainVersion.md"

# Ensure output directory
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

function Write-Utf8NoBomFile {
    param(
        [string]$Path,
        [string]$Content
    )

    [System.IO.File]::WriteAllText(
        $Path,
        $Content,
        [System.Text.UTF8Encoding]::new($false)
    )
}

function Get-HighlightsContent {
    param(
        [string]$PreferredPath,
        [string]$FallbackPath,
        [string]$DefaultContent
    )

    if ($PreferredPath -and (Test-Path $PreferredPath)) {
        return (Get-Content $PreferredPath -Raw).Trim()
    }

    if ($FallbackPath -and (Test-Path $FallbackPath)) {
        $rawContent = (Get-Content $FallbackPath -Raw).Trim()
        $changesSection = [regex]::Match(
            $rawContent,
            '(?ms)^#{1,6}\s*Changes\s*$\s*(.*?)(?=^#{1,6}\s+\S|\z)'
        )

        if ($changesSection.Success -and -not [string]::IsNullOrWhiteSpace($changesSection.Groups[1].Value)) {
            return $changesSection.Groups[1].Value.Trim()
        }

        if (-not [string]::IsNullOrWhiteSpace($rawContent)) {
            return $rawContent
        }
    }

    return $DefaultContent.Trim()
}

function Get-PreferredOrNull {
    param(
        [string]$PreferredPath,
        [string]$FallbackPath
    )

    if ($PreferredPath -and (Test-Path $PreferredPath)) {
        return (Get-Content $PreferredPath -Raw).Trim()
    }

    if ($FallbackPath -and (Test-Path $FallbackPath)) {
        $rawContent = (Get-Content $FallbackPath -Raw).Trim()
        $changesSection = [regex]::Match(
            $rawContent,
            '(?ms)^#{1,6}\s*Changes\s*$\s*(.*?)(?=^#{1,6}\s+\S|\z)'
        )

        if ($changesSection.Success -and -not [string]::IsNullOrWhiteSpace($changesSection.Groups[1].Value)) {
            return $changesSection.Groups[1].Value.Trim()
        }

        if (-not [string]::IsNullOrWhiteSpace($rawContent)) {
            return $rawContent
        }
    }

    return $null
}

# Load highlights if available
$defaultHighlightsEn = @"
- Maintenance and stability improvements.
- Small usability polish.
- Packaging and release metadata refresh.
"@

$defaultHighlightsJa = @"
- 安定性の改善。
- 使い勝手の細かな改善。
- パッケージングとリリース文面の整備。
"@

$highlightsCommon = Get-PreferredOrNull `
    -PreferredPath $releaseHighlightsInput `
    -FallbackPath $releaseNotesInput

$defaultJaContent = $defaultHighlightsJa
if ($highlightsCommon) {
    $defaultJaContent = $highlightsCommon
}

$defaultEnContent = $defaultHighlightsEn
if ($highlightsCommon) {
    $defaultEnContent = $highlightsCommon
}

$highlightsJa = Get-HighlightsContent `
    -PreferredPath $releaseHighlightsJaInput `
    -FallbackPath $releaseNotesJaInput `
    -DefaultContent $defaultJaContent

$highlightsEn = Get-HighlightsContent `
    -PreferredPath $releaseHighlightsEnInput `
    -FallbackPath $releaseNotesEnInput `
    -DefaultContent $defaultEnContent

$downloadVersion = $Version
if ($isDev) {
    $downloadVersion = 'dev'
}

$repository = 'tsuchim/window-thumb-wall'
if ($env:GITHUB_REPOSITORY) {
    $repository = $env:GITHUB_REPOSITORY
}

$serverUrl = 'https://github.com'
if ($env:GITHUB_SERVER_URL) {
    $serverUrl = $env:GITHUB_SERVER_URL.TrimEnd('/')
}
$repositoryUrl = "$serverUrl/$repository"
if ($isDev) {
    $downloadsSection = [string]::Join("`n", @(
        '## Downloads'
        'Download links are omitted for dev builds. Use the workflow artifacts from the current run instead.'
    ))
} else {
    $downloadsSection = [string]::Join("`n", @(
        '## Downloads'
        'Official builds are available in the following formats:'
        ''
        '### Windows x64 (Standard)'
        "- **ZIP**: [WindowThumbWall-$downloadVersion-win-x64.zip]($repositoryUrl/releases/download/$downloadVersion/WindowThumbWall-$downloadVersion-win-x64.zip)"
        "- **MSI**: [WindowThumbWall-$downloadVersion-win-x64.msi]($repositoryUrl/releases/download/$downloadVersion/WindowThumbWall-$downloadVersion-win-x64.msi)"
        "- **MSIX**: [WindowThumbWall-$downloadVersion-win-x64.msix]($repositoryUrl/releases/download/$downloadVersion/WindowThumbWall-$downloadVersion-win-x64.msix)"
        ''
        '### Windows ARM64 (Surface Pro, etc.)'
        "- **ZIP**: [WindowThumbWall-$downloadVersion-win-arm64.zip]($repositoryUrl/releases/download/$downloadVersion/WindowThumbWall-$downloadVersion-win-arm64.zip)"
        "- **MSI**: [WindowThumbWall-$downloadVersion-win-arm64.msi]($repositoryUrl/releases/download/$downloadVersion/WindowThumbWall-$downloadVersion-win-arm64.msi)"
        "- **MSIX**: [WindowThumbWall-$downloadVersion-win-arm64.msix]($repositoryUrl/releases/download/$downloadVersion/WindowThumbWall-$downloadVersion-win-arm64.msix)"
    ))
}

# --- 1. GitHub Release Notes (dist/release-notes.md) ---
$ghNotes = @"
# Window Thumb Wall $Version

Keep multiple windows visible at once with a fast, low-overhead live thumbnail wall for Windows.

$downloadsSection

## Why Use It
- Watch multiple windows without constant app switching.
- Keep logs, dashboards, browsers, chats, and terminals visible together.
- Use a second display as a lightweight monitoring wall.
- Notice when monitored windows need attention through red or orange borders, and keep track of the active source window with a white border.
- Click a thumbnail to jump back to its source window.
- Stay local and private with no telemetry or cloud dependency.

## What's New
$highlightsEn

---
For privacy details, see [PRIVACY.md]($repositoryUrl/blob/main/PRIVACY.md).
"@.Trim()

Write-Utf8NoBomFile -Path (Join-Path $OutputDir 'release-notes.md') -Content $ghNotes

# --- 2. Store Listing JA (dist/store-listing-ja.md) ---
$storeJa = @"
# Window Thumb Wall - ストア掲載情報 (日本語)

## 製品名
Window Thumb Wall

## 概要 (Short Description)
複数のウィンドウを同時に見渡せる、低負荷なライブサムネイル壁面アプリです。

## 詳細説明 (Full Description)
Window Thumb Wall は、複数のウィンドウをライブサムネイルとして並べて表示できる Windows アプリです。

アプリを何度も切り替えなくても、必要なウィンドウを一度に見続けられます。ビルドログ、ダッシュボード、チャット、ブラウザ、端末、チャート、監視画面など、仕事や確認に必要な情報をひとつの壁面にまとめられます。

表示には Windows 標準の Desktop Window Manager サムネイル API を使っているため、画面キャプチャや配信ソフトのような重い構成なしで、軽快にライブ表示を維持できます。

主な価値:
- 複数ウィンドウを同時に確認できる。
- Alt+Tab を繰り返す回数を減らせる。
- 余っている画面領域を監視壁面として活用できる。
- 静止画ではなくライブの更新状態をそのまま見られる。
- 点滅中や通知を出した可能性が高いウィンドウを赤枠/オレンジ枠で見つけやすく、今アクティブな元ウィンドウも白枠で追いやすい。
- サムネイルから元ウィンドウへすぐ戻れる。

主な機能:
- 外部ウィンドウのライブサムネイル表示。
- 柔軟な壁面レイアウト。
- ウィンドウ選択リストからの追加。
- サムネイルクリックで元ウィンドウをアクティブ化。
- タスクバー点滅や通知候補解決に連動した赤枠/オレンジ枠通知。
- 現在アクティブな元ウィンドウを示す白枠表示。
- フルスクリーン表示。
- 最前面表示。
- ローカル完結、テレメトリなし。

## プロモーション テキスト
空いている画面を、そのままライブ監視壁面に変えられます。

## 検索キーワード
ウィンドウ監視, サムネイル, 壁面表示, ダッシュボード, マルチタスク, 生プレビュー, Windows ツール

## 更新内容 (What's New)
$highlightsJa
"@

Write-Utf8NoBomFile -Path "$OutputDir/store-listing-ja.md" -Content $storeJa

$storeWhatsNewJa = @"
# Window Thumb Wall - ストア更新内容 (日本語)

## 更新内容 (What's New)
$highlightsJa
"@

Write-Utf8NoBomFile -Path "$OutputDir/store-whats-new-ja.md" -Content $storeWhatsNewJa

# --- 3. Store Listing EN (dist/store-listing-en.md) ---
$storeEn = @"
# Window Thumb Wall - Store Listing (English)

## Product Name
Window Thumb Wall

## Short Description
Keep multiple windows visible at once with a fast, low-overhead live thumbnail wall for Windows.

## Full Description
Window Thumb Wall lets you monitor multiple windows at the same time by arranging live thumbnails in a clean, flexible wall layout.

Instead of constantly switching between apps, you can keep the windows that matter visible on one screen: build logs, dashboards, chats, browsers, terminals, charts, status pages, or any other desktop window supported by Windows.

Because the app uses the native Windows Desktop Window Manager thumbnail API, it shows live window content with very low overhead. You get a practical monitoring workspace without setting up screen capture tools, streaming software, or custom overlays.

Why people use Window Thumb Wall:
- Monitor several windows without alt-tabbing.
- Keep reference information visible while working in another app.
- Build a simple monitoring wall on a second display.
- Follow live content instead of static screenshots.
- Notice when a window needs attention through red or orange borders, and keep track of the active source window with a white border.
- Jump back to the source window with a single click on its thumbnail.

Key features:
- Live thumbnails for external windows.
- Flexible wall layout that adapts to your open slots.
- Window picker for quickly adding targets.
- Click a thumbnail to activate its source window.
- Red or orange borders based on taskbar flashes and notification candidate resolution.
- A white border for the currently active monitored source window.
- Fullscreen mode for dedicated monitoring.
- Always-on-top support.
- Local-only operation with no telemetry.

Window Thumb Wall is a practical desktop utility for people who want persistent visibility with minimal setup and minimal overhead.

## Promotional Text
Turn spare screen space into a live window wall.

## Search Keywords
window monitor, thumbnail wall, desktop dashboard, live preview, multitasking, productivity, windows utility

## What's New
$highlightsEn
"@

Write-Utf8NoBomFile -Path "$OutputDir/store-listing-en.md" -Content $storeEn

$storeWhatsNewEn = @"
# Window Thumb Wall - Store What's New (English)

## What's New
$highlightsEn
"@

Write-Utf8NoBomFile -Path "$OutputDir/store-whats-new-en.md" -Content $storeWhatsNewEn

Write-Host "Generated release metadata in $OutputDir"
