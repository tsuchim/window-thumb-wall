using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace WindowThumbWall;

public sealed class ShortcutGuideWindow : Window
{
    public ShortcutGuideWindow()
    {
        Title = LocalizedText.Get("guide.title");
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));

        var root = new Grid { Margin = new Thickness(14) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
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

        var rows = BuildRows();
        var list = new ListView
        {
            ItemsSource = rows,
            Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Margin = new Thickness(0, 0, 0, 12),
            FontWeight = FontWeights.Normal,
            FontStyle = FontStyles.Normal
        };
        list.ItemContainerStyle = new Style(typeof(ListViewItem))
        {
            Setters =
            {
                new Setter(Control.FontWeightProperty, FontWeights.Normal),
                new Setter(Control.FontStyleProperty, FontStyles.Normal)
            }
        };

        var view = new GridView();
        view.ColumnHeaderContainerStyle = new Style(typeof(GridViewColumnHeader))
        {
            Setters =
            {
                new Setter(Control.FontWeightProperty, FontWeights.Normal),
                new Setter(Control.FontStyleProperty, FontStyles.Normal)
            }
        };
        view.Columns.Add(new GridViewColumn
        {
            Header = LocalizedText.Get("guide.column.input"),
            Width = 180,
            CellTemplate = BuildNormalTextCellTemplate(nameof(GuideItem.Input))
        });
        view.Columns.Add(new GridViewColumn
        {
            Header = LocalizedText.Get("guide.column.action"),
            Width = 290,
            CellTemplate = BuildNormalTextCellTemplate(nameof(GuideItem.Action))
        });
        list.View = view;
        Grid.SetRow(list, 1);

        var footer = new Grid();
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetRow(footer, 2);

        var version = new TextBlock
        {
            Text = BuildVersionText(),
            Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        };
        Grid.SetColumn(version, 0);

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
        root.Children.Add(list);
        footer.Children.Add(version);
        footer.Children.Add(closeButton);
        root.Children.Add(footer);
        Content = root;

        Loaded += (_, _) => FreezeMinimumSizeFromContent(list);
    }

    private static List<GuideItem> BuildRows() =>
    [
        new(LocalizedText.Get("guide.input.assign"), LocalizedText.Get("guide.action.assign")),
        new(LocalizedText.Get("guide.input.windowMenu"), LocalizedText.Get("guide.action.windowMenu")),
        new(LocalizedText.Get("guide.input.appMenu"), LocalizedText.Get("guide.action.appMenu")),
        new(LocalizedText.Get("guide.input.appDragReorder"), LocalizedText.Get("guide.action.appDragReorder")),
        new(LocalizedText.Get("guide.input.appResize"), LocalizedText.Get("guide.action.appResize")),
        new(LocalizedText.Get("guide.input.activate"), LocalizedText.Get("guide.action.activate")),
        new(LocalizedText.Get("guide.input.menu"), LocalizedText.Get("guide.action.menu")),
        new(LocalizedText.Get("guide.input.reorder"), LocalizedText.Get("guide.action.reorder")),
        new(LocalizedText.Get("guide.input.fullscreen"), LocalizedText.Get("guide.action.fullscreen")),
        new(LocalizedText.Get("guide.input.exitFullscreen"), LocalizedText.Get("guide.action.exitFullscreen"))
    ];

    private sealed record GuideItem(string Input, string Action);

    private static string BuildVersionText()
    {
        Version? version = typeof(ShortcutGuideWindow).Assembly.GetName().Version;
        string versionText = version switch
        {
            null => "?",
            { Revision: > 0 } => version.ToString(),
            { Build: > 0 } => version.ToString(3),
            _ => version.ToString(2)
        };

        return string.Format(LocalizedText.Get("guide.version"), versionText);
    }

    private void FreezeMinimumSizeFromContent(ListView list)
    {
        list.UpdateLayout();

        if (FindScrollViewer(list) is ScrollViewer sv &&
            sv.ComputedVerticalScrollBarVisibility == Visibility.Visible)
        {
            double extraHeight = sv.ExtentHeight - sv.ViewportHeight;
            if (extraHeight > 0)
            {
                Height += extraHeight;
                list.UpdateLayout();
            }
        }

        MinWidth = ActualWidth;
        MinHeight = ActualHeight;
        SizeToContent = SizeToContent.Manual;
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject root)
    {
        int childCount = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < childCount; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is ScrollViewer viewer)
                return viewer;

            ScrollViewer? found = FindScrollViewer(child);
            if (found != null)
                return found;
        }

        return null;
    }

    private static DataTemplate BuildNormalTextCellTemplate(string bindingPath)
    {
        var template = new DataTemplate();
        var factory = new FrameworkElementFactory(typeof(TextBlock));
        factory.SetBinding(TextBlock.TextProperty, new Binding(bindingPath));
        factory.SetValue(TextBlock.FontWeightProperty, FontWeights.Normal);
        factory.SetValue(TextBlock.FontStyleProperty, FontStyles.Normal);
        template.VisualTree = factory;
        return template;
    }
}
