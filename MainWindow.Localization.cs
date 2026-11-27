using System.Windows;
using System.Windows.Automation;

namespace WindowThumbWall;

public partial class MainWindow
{
    private SettingsWindow? _notificationSettingsWindow;

    private void ApplyLocalization()
    {
        Title = LocalizedText.Get("app.title");
        FilterLabel.Text = LocalizedText.Get("label.filter");
        AppListLabel.Text = LocalizedText.Get("label.autoApps");
        SpecificWindowsLabel.Text = LocalizedText.Get("label.specificWindows");
        UpdateStateNotice();
        CollapseLeftPanelButton.Content = LocalizedText.Get("button.collapseSidebar");
        string expandSidebar = LocalizedText.Get("button.expandSidebar");
        ExpandLeftPanelButton.ToolTip = expandSidebar;
        AutomationProperties.SetName(ExpandLeftPanelButton, expandSidebar);
        FullScreenButton.Content = LocalizedText.Get("button.fullscreen");
        SettingsButton.Content = LocalizedText.Get("label.settings");
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

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        NotificationAvailability availability = _notificationAttentionRequested && !SupportsNotificationAttentionRuntime()
            ? NotificationAvailability.RequiresPackage
            : _notificationAvailability;
        var settingsWindow = new SettingsWindow(
            _notificationAttentionRequested,
            LocalizedText.Get($"setting.osNotifications.status.{availability}"),
            enabled => SetNotificationAttentionEnabled(enabled))
        {
            Owner = this
        };

        _notificationSettingsWindow = settingsWindow;
        settingsWindow.Closed += (_, _) =>
        {
            if (ReferenceEquals(_notificationSettingsWindow, settingsWindow))
                _notificationSettingsWindow = null;
        };
        settingsWindow.ShowDialog();
    }
}
