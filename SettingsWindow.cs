using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WindowThumbWall;

public sealed class SettingsWindow : Window
{
    public SettingsWindow(bool osNotificationAttentionEnabled, Action<bool> onNotificationAttentionChanged)
    {
        ArgumentNullException.ThrowIfNull(onNotificationAttentionChanged);

        Title = LocalizedText.Get("label.settings");
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
        ResizeMode = ResizeMode.NoResize;

        var root = new Grid { Margin = new Thickness(14) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new TextBlock
        {
            Text = LocalizedText.Get("label.settings"),
            Foreground = Brushes.White,
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 12)
        };
        Grid.SetRow(header, 0);

        var notificationCheckBox = new CheckBox
        {
            Content = LocalizedText.Get("setting.osNotifications"),
            IsChecked = osNotificationAttentionEnabled,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 14)
        };
        notificationCheckBox.Checked += (_, _) => onNotificationAttentionChanged(true);
        notificationCheckBox.Unchecked += (_, _) => onNotificationAttentionChanged(false);
        Grid.SetRow(notificationCheckBox, 1);

        var footer = new Grid();
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetRow(footer, 2);

        var closeButton = new Button
        {
            Content = LocalizedText.Get("guide.close"),
            HorizontalAlignment = HorizontalAlignment.Right,
            Padding = new Thickness(12, 6, 12, 6),
            MinWidth = 90
        };
        closeButton.Click += (_, _) => Close();
        Grid.SetColumn(closeButton, 1);

        root.Children.Add(header);
        root.Children.Add(notificationCheckBox);
        footer.Children.Add(closeButton);
        root.Children.Add(footer);

        Content = root;
        Loaded += (_, _) =>
        {
            MinWidth = ActualWidth;
            MinHeight = ActualHeight;
            SizeToContent = SizeToContent.Manual;
        };
    }
}
