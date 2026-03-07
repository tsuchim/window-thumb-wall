# WindowThumbWall

[English README (README.md)](README.md)

WPF デスクトップアプリ (.NET 10) で、外部ウィンドウの **DWM ライブサムネイル** を
柔軟なグリッドに並べて表示します。

## 特徴

- **ライブサムネイル**: DWM Thumbnail API による低負荷表示
- **可変グリッド**: 1x1 から NxN まで自動拡張/縮小
- **フルスクリーン**: `Enter` で切り替え、`Esc` で解除
- **ショートカット操作一覧ウィンドウ**: 左メニュー下部のショートカットヒント表示をクリックで表示
- **国際化対応**: OS の UI 言語に応じて UI と操作一覧を日本語/英語で自動切替
- **ウィンドウピッカー**: 左ペインで一覧＋フィルタ
- **自動クリーンアップ**: 対象ウィンドウが閉じたら自動で除去

## クイックスタート

1. `WindowThumbWall.slnx` を Visual Studio 2026 以降で開く
2. `F5` でビルドして起動
3. 左ペインのウィンドウをダブルクリックでグリッドに追加
4. 左メニュー下部のショートカットヒント表示をクリックして操作一覧を表示
5. `Enter` でフルスクリーンを切り替え

## キーボードショートカット

| キー | 操作 |
|------|------|
| `Enter` | フルスクリーン切り替え |
| `Esc` | フルスクリーン解除 |

## アーキテクチャ

| ファイル | 役割 |
|---------|------|
| `NativeMethods.cs` | P/Invoke とネイティブ補助処理 |
| `ThumbHost.cs` | セルごとの子 HWND を作る `HwndHost` |
| `ThumbnailSlot.cs` | 1 セル分の DWM サムネイル管理 |
| `WindowInfo.cs` | ウィンドウ一覧用データモデル |
| `MainWindow.xaml/.cs` | メイン UI と操作ロジック |
| `LocalizedText.cs` | 日本語/英語の文言テーブル |
| `ShortcutGuideWindow.cs` | 操作一覧を表示するポップアップ |

## ダウンロード

| 形式 | ファイル | 備考 |
|------|----------|------|
| **ZIP** | `WindowThumbWall-v0.2-win-x64.zip` | ポータブル |
| **MSI** | `WindowThumbWall-v0.2-win-x64.msi` | インストーラ（アンインストール実行ユーザーのローカル状態を削除） |
| **MSIX** | `WindowThumbWall-v0.2-win-x64.msix` | パッケージ（アンインストール時にパッケージデータを削除） |

## プライバシー概要

- WindowThumbWall は個人情報を収集・送信しません。
- 表示と復元のために、ユーザーが明示的に指定したウィンドウ情報とアプリ名のみをローカルデータ領域に保存します。
- サムネイル/スクリーンショット相当の画像取得は、その場の画面表示のためだけに行い、他用途には使用しません。
- アンインストール時は、MSIX で保存データが削除され、MSI ではアンインストール実行ユーザーのローカル状態が削除されます（ZIP はポータブル形式のため手動削除）。

## セキュリティ / 署名

詳細は [docs/code-signing-policy.md](docs/code-signing-policy.md)。
プライバシーとローカル保存データは [PRIVACY.md](PRIVACY.md) を参照してください。

## パッケージビルド

```powershell
.\packaging\build-all.ps1
```

## 動作要件

- Windows 10 以降
- .NET 10 SDK
- Visual Studio 2026（推奨）

## ライセンス

GNU General Public License v3.0（[LICENSE](LICENSE)）。
