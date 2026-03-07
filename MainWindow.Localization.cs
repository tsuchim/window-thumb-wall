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
        ShortcutGuideButton.Content = LocalizedText.Get("hint.summary");
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

