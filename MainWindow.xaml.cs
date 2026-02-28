using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace WindowThumbWall;

public partial class MainWindow : Window
{
    private IntPtr _mainHwnd;

    private readonly List<Border> _cellBorders = [];
    private readonly List<TextBlock> _cellLabels = [];
    private readonly List<ThumbHost> _cellHosts = [];
    private readonly List<ThumbnailSlot> _slots = [];

    private int _maximizedIndex = -1;

    private bool _isFullScreen;
    private WindowStyle _savedWindowStyle;
    private WindowState _savedWindowState;
    private GridLength _savedLeftColWidth;
    private GridLength _savedSplitterColWidth;

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly List<WindowInfo> _windowCache = [];

    public MainWindow()
    {
        InitializeComponent();

        _timer.Tick += Timer_Tick;
        Loaded += OnLoaded;
        Closed += OnClosed;
        SizeChanged += OnSizeChanged;
        WindowList.MouseDoubleClick += WindowList_DoubleClick;
        FilterBox.TextChanged += FilterBox_TextChanged;
    }

    // ── Lifecycle ────────────────────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _mainHwnd = new WindowInteropHelper(this).Handle;
        _timer.Start();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _timer.Stop();
        foreach (var slot in _slots) slot.Clear();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) =>
        Dispatcher.BeginInvoke(DispatcherPriority.Render, UpdateAllThumbnails);

    // ── Keyboard ─────────────────────────────────────────────────

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter when _isFullScreen || !FilterBox.IsFocused:
                ToggleFullScreen();
                e.Handled = true;
                break;
            case Key.Escape when _isFullScreen:
                ToggleFullScreen();
                e.Handled = true;
                break;
        }
        base.OnPreviewKeyDown(e);
    }

    // ── Dynamic grid ─────────────────────────────────────────────

    private static (int rows, int cols) CalcGridSize(int count)
    {
        if (count <= 0) return (1, 1);
        int cols = (int)Math.Ceiling(Math.Sqrt(count));
        int rows = (int)Math.Ceiling((double)count / cols);
        return (rows, cols);
    }

    private int AddSlot()
    {
        if (_maximizedIndex >= 0) RestoreGrid();

        int idx = _cellBorders.Count;

        var label = new TextBlock
        {
            Text = "(empty)",
            Foreground = Brushes.LightGray,
            Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
            Padding = new Thickness(6, 3, 6, 3),
            FontSize = 12,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        DockPanel.SetDock(label, Dock.Top);

        var host = new ThumbHost();

        var panel = new DockPanel();
        panel.Children.Add(label);
        panel.Children.Add(host);

        var border = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(2),
            Background = Brushes.Black,
            Child = panel,
            Tag = idx,
            Cursor = Cursors.Hand
        };
        border.MouseLeftButtonDown += Cell_LeftClick;
        border.MouseRightButtonDown += Cell_RightClick;

        ThumbGrid.Children.Add(border);
        _cellBorders.Add(border);
        _cellLabels.Add(label);
        _cellHosts.Add(host);

        RebuildGrid();

        // Force layout so BuildWindowCore runs and the HWND is ready.
        ThumbGrid.UpdateLayout();

        _slots.Add(new ThumbnailSlot(host, _mainHwnd));
        return idx;
    }

    private void RemoveSlot(int idx)
    {
        if (_maximizedIndex >= 0) RestoreGrid();

        _slots[idx].Clear();
        ThumbGrid.Children.Remove(_cellBorders[idx]);
        _cellHosts[idx].Dispose();

        _cellBorders.RemoveAt(idx);
        _cellLabels.RemoveAt(idx);
        _cellHosts.RemoveAt(idx);
        _slots.RemoveAt(idx);

        for (int i = 0; i < _cellBorders.Count; i++)
            _cellBorders[i].Tag = i;

        RebuildGrid();
    }

    private void RebuildGrid()
    {
        int count = _cellBorders.Count;
        ThumbGrid.RowDefinitions.Clear();
        ThumbGrid.ColumnDefinitions.Clear();

        if (count == 0) return;

        var (rows, cols) = CalcGridSize(count);

        for (int r = 0; r < rows; r++)
            ThumbGrid.RowDefinitions.Add(new RowDefinition());
        for (int c = 0; c < cols; c++)
            ThumbGrid.ColumnDefinitions.Add(new ColumnDefinition());

        for (int i = 0; i < count; i++)
        {
            Grid.SetRow(_cellBorders[i], i / cols);
            Grid.SetColumn(_cellBorders[i], i % cols);
            Grid.SetRowSpan(_cellBorders[i], 1);
            Grid.SetColumnSpan(_cellBorders[i], 1);
        }

        Dispatcher.BeginInvoke(DispatcherPriority.Render, UpdateAllThumbnails);
    }

    // ── Timer ────────────────────────────────────────────────────

    private void Timer_Tick(object? sender, EventArgs e)
    {
        RefreshWindowList();
        ValidateSlots();
        UpdateAllThumbnails();
    }

    // ── Window list ──────────────────────────────────────────────

    private void RefreshWindowList()
    {
        _windowCache.Clear();
        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (hWnd == _mainHwnd) return true;
            if (!NativeMethods.IsAltTabWindow(hWnd)) return true;
            _windowCache.Add(new WindowInfo
            {
                Handle = hWnd,
                Title = NativeMethods.GetWindowTitle(hWnd)
            });
            return true;
        }, IntPtr.Zero);
        ApplyFilter();
    }

    private void FilterBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        string filter = FilterBox.Text.Trim();
        var items = string.IsNullOrEmpty(filter)
            ? _windowCache.ToList()
            : _windowCache
                .Where(w => w.Title.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();

        var sel = WindowList.SelectedItem as WindowInfo;
        WindowList.ItemsSource = items;
        if (sel != null)
            WindowList.SelectedItem = items.FirstOrDefault(w => w.Handle == sel.Handle);
    }

    private void WindowList_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (WindowList.SelectedItem is not WindowInfo info) return;

        // Skip if already assigned.
        foreach (var slot in _slots)
            if (slot.IsOccupied && slot.SourceHwnd == info.Handle) return;

        // First free slot.
        int target = -1;
        for (int i = 0; i < _slots.Count; i++)
        {
            if (!_slots[i].IsOccupied)
            {
                target = i;
                break;
            }
        }

        // No free slot → add a new one.
        if (target == -1)
            target = AddSlot();

        if (_slots[target].Assign(info.Handle, info.Title))
            _cellLabels[target].Text = info.Title;
    }

    // ── Slot validation ──────────────────────────────────────────

    private void ValidateSlots()
    {
        for (int i = _slots.Count - 1; i >= 0; i--)
        {
            if (!_slots[i].CheckValid())
                RemoveSlot(i);
        }
    }

    private void UpdateAllThumbnails()
    {
        foreach (var slot in _slots)
            slot.UpdateThumbnail();
    }

    // ── Maximize / Restore ───────────────────────────────────────

    private void Cell_LeftClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { Tag: int idx }) return;

        if (_maximizedIndex == idx)
            RestoreGrid();
        else if (_maximizedIndex >= 0)
            RestoreGrid();
        else if (idx < _slots.Count && _slots[idx].IsOccupied)
            MaximizeCell(idx);
    }

    private void MaximizeCell(int idx)
    {
        var (rows, cols) = CalcGridSize(_cellBorders.Count);
        _maximizedIndex = idx;

        for (int i = 0; i < _cellBorders.Count; i++)
        {
            if (i == idx)
            {
                Grid.SetRow(_cellBorders[i], 0);
                Grid.SetColumn(_cellBorders[i], 0);
                Grid.SetRowSpan(_cellBorders[i], rows);
                Grid.SetColumnSpan(_cellBorders[i], cols);
            }
            else
            {
                _cellBorders[i].Visibility = Visibility.Collapsed;
            }
        }
        Dispatcher.BeginInvoke(DispatcherPriority.Render, UpdateAllThumbnails);
    }

    private void RestoreGrid()
    {
        _maximizedIndex = -1;
        for (int i = 0; i < _cellBorders.Count; i++)
            _cellBorders[i].Visibility = Visibility.Visible;
        RebuildGrid();
    }

    private void Cell_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { Tag: int idx }) return;
        if (idx < _slots.Count)
            RemoveSlot(idx);
    }

    // ── Fullscreen ───────────────────────────────────────────────

    private void ToggleFullScreen()
    {
        if (_isFullScreen)
        {
            WindowStyle = _savedWindowStyle;
            WindowState = _savedWindowState;
            LeftPanel.Visibility = Visibility.Visible;
            Splitter.Visibility = Visibility.Visible;
            RootGrid.ColumnDefinitions[0].Width = _savedLeftColWidth;
            RootGrid.ColumnDefinitions[1].Width = _savedSplitterColWidth;
            _isFullScreen = false;
        }
        else
        {
            _savedWindowStyle = WindowStyle;
            _savedWindowState = WindowState;
            _savedLeftColWidth = RootGrid.ColumnDefinitions[0].Width;
            _savedSplitterColWidth = RootGrid.ColumnDefinitions[1].Width;

            LeftPanel.Visibility = Visibility.Collapsed;
            Splitter.Visibility = Visibility.Collapsed;
            RootGrid.ColumnDefinitions[0].Width = new GridLength(0);
            RootGrid.ColumnDefinitions[1].Width = new GridLength(0);

            if (WindowState == WindowState.Maximized)
                WindowState = WindowState.Normal;
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;
            _isFullScreen = true;
        }
        Dispatcher.BeginInvoke(DispatcherPriority.Render, UpdateAllThumbnails);
    }
}