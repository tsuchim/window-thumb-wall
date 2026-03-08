using System.Windows;

namespace WindowThumbWall;

public partial class MainWindow
{
    private void ApplyLocalization()
    {
        Title = LocalizedText.Get("app.title");
        FilterLabel.Text = LocalizedText.Get("label.filter");
        AppListLabel.Text = LocalizedText.Get("label.autoApps");
        FullScreenButton.Content = LocalizedText.Get("button.fullscreen");
        ShortcutGuideButton.Content = BuildQuickHelpText();
    }

    private static string BuildQuickHelpText()
    {
        string summary = LocalizedText.Get("hint.summary");
        string[] lines = summary
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (lines.Length <= 4)
            return string.Join('\n', lines);

        var visible = new List<string>(4);
        visible.AddRange(lines.Take(3));
        visible.Add(LocalizedText.Get("hint.more"));
        return string.Join('\n', visible);
    }

    private void ShortcutGuideButton_Click(object sender, RoutedEventArgs e)
    {
        if (_shortcutGuideWindow is { IsLoaded: true })
        {
            _shortcutGuideWindow.Activate();
            return;
        }

        _shortcutGuideWindow = new ShortcutGuideWindow
        {
            Owner = this
        };
        _shortcutGuideWindow.Closed += (_, _) => _shortcutGuideWindow = null;
        _shortcutGuideWindow.Show();
    }
}
