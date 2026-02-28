# WindowThumbWall

WPF デスクトップアプリ (.NET 10) で、外部ウィンドウの **DWM ライブサムネイル** を柔軟なグリッドに並べて表示します（画面キャプチャではありません）。

[English README (README.md)](README.md)

## 特徴

- **ライブサムネイル**: DWM Thumbnail API による低負荷表示
- **可変グリッド**: 1×1 から N×N まで自動拡張/縮小
- **フルスクリーン**: `Enter` でトグル、`Esc` で解除
- **ウィンドウピッカー**: 左ペインで一覧＋フィルタ
- **自動クリーンアップ**: 対象ウィンドウが閉じたら自動で除去

## ダウンロード

| 形式 | ファイル | 備考 |
|------|----------|------|
| **ZIP** | `WindowThumbWall-v0.2-win-x64.zip` | 展開して実行（ポータブル） |
| **MSI** | `WindowThumbWall-v0.2-win-x64.msi` | 従来型インストーラ（Program Files + スタートメニュー） |
| **MSIX** | `WindowThumbWall-v0.2-win-x64.msix` | モダンパッケージ（サイドロード/証明書の信頼が必要な場合あり） |

いずれも **self-contained (x64)** で、.NET ランタイムの別途インストールは不要です。

## 署名（セキュリティ）に関する注意

配布しているバイナリには、**公的な（商用の）コード署名**は付与されていません。

- **ZIP / MSI**: 通常 **未署名** です。
- **MSIX**: **未署名**、または **自己署名/テスト証明書**の可能性があります。

環境によっては Windows SmartScreen / Defender で警告が表示される場合があります。

## パッケージのビルド

```powershell
# まとめて作成
.\packaging\build-all.ps1

# 個別に作成
.\packaging\build-zip.ps1
.\packaging\build-msi.ps1    # WiX が必要（自動インストール）
.\packaging\build-msix.ps1   # Windows SDK が必要
```

## 動作要件

- Windows 10 以降
- .NET 10 SDK（開発時）
- Visual Studio 2026（推奨）

## ライセンス

GNU General Public License v3.0（[LICENSE](LICENSE) を参照）。
