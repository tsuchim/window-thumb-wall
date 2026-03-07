using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WindowThumbWall;

public sealed class ShortcutGuideWindow : Window
{
    public ShortcutGuideWindow()
    {
        Title = LocalizedText.Get("guide.title");
        Width = 520;
        Height = 390;
        MinWidth = 450;
        MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));

        var root = new Grid { Margin = new Thickness(14) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new TextBlock
        {
            Text = LocalizedText.Get("guide.header"),
            Foreground = Brushes.White,
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        };
        Grid.SetRow(header, 0);

        var desc = new TextBlock
        {
            Text = LocalizedText.Get("guide.desc"),
            Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
            Margin = new Thickness(0, 0, 0, 10)
        };
        Grid.SetRow(desc, 1);

        var rows = BuildRows();
        var list = new ListView
        {
            ItemsSource = rows,
            Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Margin = new Thickness(0, 0, 0, 12)
        };

        var view = new GridView();
        view.Columns.Add(new GridViewColumn { Header = LocalizedText.Get("guide.column.input"), Width = 180, DisplayMemberBinding = new System.Windows.Data.Binding(nameof(GuideItem.Input)) });
        view.Columns.Add(new GridViewColumn { Header = LocalizedText.Get("guide.column.action"), Width = 290, DisplayMemberBinding = new System.Windows.Data.Binding(nameof(GuideItem.Action)) });
        list.View = view;
        Grid.SetRow(list, 2);

        var closeButton = new Button
        {
            Content = LocalizedText.Get("guide.close"),
            HorizontalAlignment = HorizontalAlignment.Right,
            Padding = new Thickness(12, 6, 12, 6),
            MinWidth = 90
        };
        closeButton.Click += (_, _) => Close();
        Grid.SetRow(closeButton, 3);

        root.Children.Add(header);
        root.Children.Add(desc);
        root.Children.Add(list);
        root.Children.Add(closeButton);
        Content = root;
    }

    private static List<GuideItem> BuildRows() =>
    [
        new(LocalizedText.Get("guide.input.assign"), LocalizedText.Get("guide.action.assign")),
        new(LocalizedText.Get("guide.input.windowMenu"), LocalizedText.Get("guide.action.windowMenu")),
        new(LocalizedText.Get("guide.input.appMenu"), LocalizedText.Get("guide.action.appMenu")),
        new(LocalizedText.Get("guide.input.appResize"), LocalizedText.Get("guide.action.appResize")),
        new(LocalizedText.Get("guide.input.activate"), LocalizedText.Get("guide.action.activate")),
        new(LocalizedText.Get("guide.input.menu"), LocalizedText.Get("guide.action.menu")),
        new(LocalizedText.Get("guide.input.reorder"), LocalizedText.Get("guide.action.reorder")),
        new(LocalizedText.Get("guide.input.fullscreen"), LocalizedText.Get("guide.action.fullscreen")),
        new(LocalizedText.Get("guide.input.exitFullscreen"), LocalizedText.Get("guide.action.exitFullscreen"))
    ];

    private sealed record GuideItem(string Input, string Action);
}

