namespace WindowThumbWall;

public static class LocalizedText
{
    private static readonly IReadOnlyDictionary<string, string> En = new Dictionary<string, string>
    {
        ["app.title"] = "WindowThumbWall",
        ["label.filter"] = "Double-click to add:",
        ["label.autoApps"] = "Auto-add apps",
        ["label.settings"] = "Settings",
        ["button.fullscreen"] = "Fullscreen (Enter)",
        ["button.shortcuts"] = "Shortcut Keys: Enter / Esc",
        ["setting.osNotifications"] = "Reflect OS notifications",
        ["setting.osNotifications.supportedHint"] = "Requires the installed MSIX build with notification access enabled.",
        ["setting.osNotifications.requiresPackage.title"] = "OS notifications unavailable",
        ["setting.osNotifications.requiresPackage.message"] = "Reflect OS notifications only works in the installed MSIX build. The unpackaged dotnet/Debug run cannot subscribe to Windows notifications.",
        ["setting.osNotifications.requiresPackage.inline"] = "This run is unpackaged. Use the installed MSIX build to enable OS notification attention.",
        ["hint.summary"] = "Double-click a window -> add to monitor.\nRight-click a window -> Add to monitor / Add app.\nAuto-add app list: right-click or drag to reorder.\nClick cell -> activate source window.\nRight-click cell -> Unassign / Exit fullscreen.\nDrag cells -> reorder monitor layout.\nEnter / Esc -> toggle fullscreen.\nClick here for full controls.",
        ["hint.more"] = "→ Click for details",
        ["slot.empty"] = "(empty)",
        ["menu.unassign"] = "Unassign",
        ["menu.exitFullscreen"] = "Exit fullscreen",
        ["menu.addToMonitor"] = "Add to monitor",
        ["menu.addApp"] = "Add app",
        ["menu.moveUp"] = "Move up",
        ["menu.moveDown"] = "Move down",
        ["menu.removeAutoAdd"] = "Disable auto-add",
        ["guide.title"] = "WindowThumbWall - Controls",
        ["guide.header"] = "Controls",
        ["guide.version"] = "Version: {0}",
        ["guide.close"] = "Close",
        ["guide.column.input"] = "Input",
        ["guide.column.action"] = "Action",
        ["guide.input.assign"] = "Double-click (window list)",
        ["guide.input.windowMenu"] = "Right-click (window list)",
        ["guide.input.appMenu"] = "Right-click (auto-add apps)",
        ["guide.input.appDragReorder"] = "Drag and drop (auto-add apps)",
        ["guide.input.appResize"] = "Drag divider (lists)",
        ["guide.input.activate"] = "Left-click (cell)",
        ["guide.input.menu"] = "Right-click (cell)",
        ["guide.input.reorder"] = "Drag and drop (cell)",
        ["guide.input.fullscreen"] = "Enter",
        ["guide.input.exitFullscreen"] = "Esc",
        ["guide.desc"] = "Available controls in the app:",
        ["guide.action.assign"] = "Add window to grid",
        ["guide.action.windowMenu"] = "Open menu to add to monitor or add app",
        ["guide.action.appMenu"] = "Open menu to move up/down or disable auto-add",
        ["guide.action.appDragReorder"] = "Reorder auto-add apps by drag and drop",
        ["guide.action.appResize"] = "Resize window list and auto-add app list",
        ["guide.action.activate"] = "Activate assigned window",
        ["guide.action.menu"] = "Open cell menu (unassign / exit fullscreen)",
        ["guide.action.reorder"] = "Reorder monitored cells",
        ["guide.action.fullscreen"] = "Toggle fullscreen",
        ["guide.action.exitFullscreen"] = "Exit fullscreen"
    };

    private static readonly IReadOnlyDictionary<string, string> Ja = new Dictionary<string, string>
    {
        ["app.title"] = "WindowThumbWall",
        ["label.filter"] = "ダブルクリックで追加",
        ["label.autoApps"] = "自動追加アプリ",
        ["label.settings"] = "設定",
        ["button.fullscreen"] = "全画面表示 (Enter)",
        ["button.shortcuts"] = "ショートカットキー: Enter / Esc",
        ["setting.osNotifications"] = "OSの通知を反映させる",
        ["setting.osNotifications.supportedHint"] = "通知アクセスを許可したインストール版 MSIX で動作します。",
        ["setting.osNotifications.requiresPackage.title"] = "OS 通知は利用できません",
        ["setting.osNotifications.requiresPackage.message"] = "OS の通知反映は、インストール済みの MSIX 版でのみ利用できます。未パッケージの dotnet/Debug 実行では Windows 通知を購読できません。",
        ["setting.osNotifications.requiresPackage.inline"] = "この実行は未パッケージです。OS 通知の注意喚起を使うには、インストール版 MSIX で起動してください。",
        ["hint.summary"] = "ウィンドウをダブルクリック -> モニターに追加。\nウィンドウを右クリック -> モニターに追加 / アプリを追加。\n自動追加アプリ一覧は右クリックまたはドラッグで並び替え。\nセルを左クリック -> 元ウィンドウをアクティブ化。\nセルを右クリック -> 選択解除 / 全画面解除。\nセルをドラッグ -> モニター配置を並び替え。\nEnter / Esc -> 全画面切替。\nここをクリックで詳細表示。",
        ["hint.more"] = "→ クリックで詳細",
        ["slot.empty"] = "(空)",
        ["menu.unassign"] = "選択解除",
        ["menu.exitFullscreen"] = "全画面解除",
        ["menu.addToMonitor"] = "モニターに追加",
        ["menu.addApp"] = "アプリを追加",
        ["menu.moveUp"] = "上へ",
        ["menu.moveDown"] = "下へ",
        ["menu.removeAutoAdd"] = "自動追加を解除",
        ["guide.title"] = "WindowThumbWall - 操作一覧",
        ["guide.header"] = "操作一覧",
        ["guide.version"] = "バージョン: {0}",
        ["guide.close"] = "閉じる",
        ["guide.column.input"] = "入力",
        ["guide.column.action"] = "操作",
        ["guide.input.assign"] = "ダブルクリック (ウィンドウ一覧)",
        ["guide.input.windowMenu"] = "右クリック (ウィンドウ一覧)",
        ["guide.input.appMenu"] = "右クリック (自動追加アプリ)",
        ["guide.input.appDragReorder"] = "ドラッグ&ドロップ (自動追加アプリ)",
        ["guide.input.appResize"] = "境界をドラッグ (一覧の間)",
        ["guide.input.activate"] = "左クリック (セル)",
        ["guide.input.menu"] = "右クリック (セル)",
        ["guide.input.reorder"] = "ドラッグ&ドロップ (セル)",
        ["guide.input.fullscreen"] = "Enter",
        ["guide.input.exitFullscreen"] = "Esc",
        ["guide.desc"] = "アプリで利用できる操作:",
        ["guide.action.assign"] = "ウィンドウをグリッドへ追加",
        ["guide.action.windowMenu"] = "モニターに追加 / アプリを追加のメニューを開く",
        ["guide.action.appMenu"] = "上へ / 下へ / 自動追加解除のメニューを開く",
        ["guide.action.appDragReorder"] = "自動追加アプリをドラッグで並び替え",
        ["guide.action.appResize"] = "ウィンドウ一覧と自動追加アプリ一覧の高さを変更",
        ["guide.action.activate"] = "割り当て済みウィンドウをアクティブ化",
        ["guide.action.menu"] = "セルのメニューを開く (選択解除 / 全画面解除)",
        ["guide.action.reorder"] = "監視セルを並び替え",
        ["guide.action.fullscreen"] = "全画面表示の切り替え",
        ["guide.action.exitFullscreen"] = "全画面表示を終了"
    };

    public static bool IsJapanese =>
        System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ja", StringComparison.OrdinalIgnoreCase);

    public static string Get(string key)
    {
        var table = IsJapanese ? Ja : En;
        if (table.TryGetValue(key, out string? value))
            return value;

        if (En.TryGetValue(key, out value))
            return value;

        return key;
    }
}
