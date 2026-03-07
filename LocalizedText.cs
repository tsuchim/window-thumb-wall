namespace WindowThumbWall;

public static class LocalizedText
{
    private static readonly IReadOnlyDictionary<string, string> En = new Dictionary<string, string>
    {
        ["app.title"] = "WindowThumbWall",
        ["label.filter"] = "Filter windows:",
        ["label.autoApps"] = "Auto-add apps",
        ["button.fullscreen"] = "Fullscreen (Enter)",
        ["button.shortcuts"] = "Shortcut Keys: Enter / Esc",
        ["hint.summary"] = "Double-click -> assign to grid.\nRight-click window list item -> add to monitor / add app.\nRight-click app list item -> disable auto-add.\nLeft-click cell -> activate window.\nRight-click cell -> menu (unassign / exit fullscreen).\nDrag monitor area -> reorder (with preview).",
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
        ["guide.close"] = "Close",
        ["guide.column.input"] = "Input",
        ["guide.column.action"] = "Action",
        ["guide.input.assign"] = "Double-click (window list)",
        ["guide.input.windowMenu"] = "Right-click (window list)",
        ["guide.input.appMenu"] = "Right-click (auto-add apps)",
        ["guide.input.appResize"] = "Drag divider (lists)",
        ["guide.input.activate"] = "Left-click (cell)",
        ["guide.input.menu"] = "Right-click (cell)",
        ["guide.input.reorder"] = "Drag and drop (cell)",
        ["guide.input.fullscreen"] = "Enter",
        ["guide.input.exitFullscreen"] = "Esc",
        ["guide.desc"] = "Click any item area in the app to perform these actions:",
        ["guide.action.assign"] = "Add window to grid",
        ["guide.action.windowMenu"] = "Open menu to add to monitor or add app",
        ["guide.action.appMenu"] = "Reorder auto-add apps or disable auto-add",
        ["guide.action.appResize"] = "Resize window list and auto-add app list",
        ["guide.action.activate"] = "Activate assigned window",
        ["guide.action.menu"] = "Open cell context menu",
        ["guide.action.reorder"] = "Reorder cells",
        ["guide.action.fullscreen"] = "Toggle fullscreen",
        ["guide.action.exitFullscreen"] = "Exit fullscreen"
    };

    private static readonly IReadOnlyDictionary<string, string> Ja = new Dictionary<string, string>
    {
        ["app.title"] = "WindowThumbWall",
        ["label.filter"] = "ウィンドウをフィルタ",
        ["label.autoApps"] = "自動追加アプリ",
        ["button.fullscreen"] = "全画面表示 (Enter)",
        ["button.shortcuts"] = "ショートカットキー: Enter / Esc",
        ["hint.summary"] = "ダブルクリック -> グリッドに割り当て。\nウィンドウ一覧項目を右クリック -> モニターに追加 / アプリを追加。\nアプリ一覧項目を右クリック -> 自動追加を解除。\nセルを左クリック -> ウィンドウをアクティブ化。\nセルを右クリック -> メニュー (選択解除 / 全画面解除)。\nモニター領域をドラッグ -> 並び替え (プレビュー付き)。",
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
        ["guide.close"] = "閉じる",
        ["guide.column.input"] = "入力",
        ["guide.column.action"] = "操作",
        ["guide.input.assign"] = "ダブルクリック (ウィンドウ一覧)",
        ["guide.input.windowMenu"] = "右クリック (ウィンドウ一覧)",
        ["guide.input.appMenu"] = "右クリック (自動追加アプリ)",
        ["guide.input.appResize"] = "境界をドラッグ (一覧の間)",
        ["guide.input.activate"] = "左クリック (セル)",
        ["guide.input.menu"] = "右クリック (セル)",
        ["guide.input.reorder"] = "ドラッグ&ドロップ (セル)",
        ["guide.input.fullscreen"] = "Enter",
        ["guide.input.exitFullscreen"] = "Esc",
        ["guide.desc"] = "アプリ内で以下の操作が使えます:",
        ["guide.action.assign"] = "ウィンドウをグリッドへ追加",
        ["guide.action.windowMenu"] = "モニターに追加 / アプリを追加のメニューを開く",
        ["guide.action.appMenu"] = "自動追加アプリの並び替え / 自動追加解除",
        ["guide.action.appResize"] = "ウィンドウ一覧と自動追加アプリ一覧の高さを変更",
        ["guide.action.activate"] = "割り当て済みウィンドウをアクティブ化",
        ["guide.action.menu"] = "セルのコンテキストメニューを開く",
        ["guide.action.reorder"] = "セルの並び替え",
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
