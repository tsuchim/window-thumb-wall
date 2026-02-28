using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace WindowThumbWall;

public partial class MainWindow : Window
{
    private const string SlotDragFormat = "WindowThumbWall.SlotIndex";

    private IntPtr _mainHwnd;

    private readonly List<Border> _cellBorders = [];
    private readonly List<TextBlock> _cellLabels = [];
    private readonly List<Border> _cellHitLayers = [];
    private readonly List<ThumbHost> _cellHosts = [];
    private readonly List<ThumbnailSlot> _slots = [];

    private Point _dragStartPoint;
    private int _dragSourceIndex = -1;
    private bool _dragMoved;
    private Border? _dropPreviewLayer;
    private Window? _dragGhost;

    private bool _isFullScreen;
    private WindowStyle _savedWindowStyle;
    private WindowState _savedWindowState;
    private GridLength _savedLeftColWidth;
    private GridLength _savedSplitterColWidth;

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly List<WindowInfo> _windowCache = [];

    private uint _shellHookMsgId;
    private readonly HashSet<IntPtr> _flashingWindows = [];
    private static readonly SolidColorBrush NormalBorderBrush =
        new(Color.FromRgb(0x55, 0x55, 0x55));
    static MainWindow() => NormalBorderBrush.Freeze();

    private AppState? _pendingRestore;

    public MainWindow()
    {
        InitializeComponent();

        // Restore window geometry before the window is shown.
        _pendingRestore = AppState.Load();
        if (_pendingRestore.Geometry is { Width: > 0, Height: > 0 } geo)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = geo.Left;
            Top = geo.Top;
            Width = geo.Width;
            Height = geo.Height;
            if (geo.IsMaximized)
                WindowState = WindowState.Maximized;
        }

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

        // Register for shell hook messages (flash / activation).
        _shellHookMsgId = NativeMethods.RegisterWindowMessage("SHELLHOOK");
        NativeMethods.RegisterShellHookWindow(_mainHwnd);
        HwndSource.FromHwnd(_mainHwnd)?.AddHook(WndProc);

        RestoreSlots();
        _timer.Start();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _timer.Stop();
        NativeMethods.DeregisterShellHookWindow(_mainHwnd);
        SaveState();
        foreach (var slot in _slots) slot.Clear();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) =>
        Dispatcher.BeginInvoke(DispatcherPriority.Render, UpdateAllThumbnails);

    // ── State persistence ─────────────────────────────────────────

    private void SaveState()
    {
        var bounds = (WindowState == WindowState.Maximized || _isFullScreen)
            ? RestoreBounds
            : new Rect(Left, Top, Width, Height);

        var state = new AppState
        {
            IsFullScreen = _isFullScreen,
            Geometry = new WindowGeometry
            {
                Left = bounds.Left,
                Top = bounds.Top,
                Width = bounds.Width,
                Height = bounds.Height,
                IsMaximized = !_isFullScreen && WindowState == WindowState.Maximized
            }
        };

        foreach (var slot in _slots)
        {
            if (!slot.IsOccupied) continue;
            state.Slots.Add(new SlotState
            {
                ProcessName = NativeMethods.GetProcessName(slot.SourceHwnd),
                Title = slot.SourceTitle
            });
        }

        state.Save();
    }

    private void RestoreSlots()
    {
        if (_pendingRestore is not { Slots.Count: > 0 } state)
        {
            _pendingRestore = null;
            return;
        }

        // Enumerate all current windows.
        var allWindows = new List<(IntPtr Handle, string Title, string ProcessName)>();
        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (hWnd == _mainHwnd) return true;
            if (!NativeMethods.IsAltTabWindow(hWnd)) return true;
            allWindows.Add((hWnd, NativeMethods.GetWindowTitle(hWnd), NativeMethods.GetProcessName(hWnd)));
            return true;
        }, IntPtr.Zero);

        var usedHandles = new HashSet<IntPtr>();

        foreach (var saved in state.Slots)
        {
            // 1. Exact match: same process + same title.
            var match = allWindows.FirstOrDefault(w =>
                !usedHandles.Contains(w.Handle) &&
                w.ProcessName.Equals(saved.ProcessName, StringComparison.OrdinalIgnoreCase) &&
                w.Title == saved.Title);

            // 2. Fallback: same process name, any title.
            if (match.Handle == IntPtr.Zero)
            {
                match = allWindows.FirstOrDefault(w =>
                    !usedHandles.Contains(w.Handle) &&
                    w.ProcessName.Equals(saved.ProcessName, StringComparison.OrdinalIgnoreCase));
            }

            if (match.Handle == IntPtr.Zero) continue;

            usedHandles.Add(match.Handle);
            int idx = AddSlot();
            if (_slots[idx].Assign(match.Handle, match.Title))
                _cellLabels[idx].Text = match.Title;
        }

        if (state.IsFullScreen && _slots.Count > 0)
            ToggleFullScreen();

        _pendingRestore = null;
    }

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
        int idx = _cellBorders.Count;

        var label = new TextBlock
        {
            Text = "(empty)",
            Foreground = Brushes.LightGray,
            Padding = new Thickness(6, 3, 6, 3),
            FontSize = 12,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var titleBar = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
            Child = label,
            Cursor = Cursors.Arrow
        };
        DockPanel.SetDock(titleBar, Dock.Top);

        var host = new ThumbHost();

        var panel = new DockPanel();
        panel.Children.Add(titleBar);
        panel.Children.Add(host);

        var hitLayer = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(1, 255, 255, 255)),
            Tag = idx,
            Cursor = Cursors.Hand,
            AllowDrop = true
        };
        hitLayer.PreviewMouseLeftButtonDown += Cell_PreviewMouseLeftButtonDown;
        hitLayer.PreviewMouseMove += Cell_PreviewMouseMove;
        hitLayer.PreviewMouseLeftButtonUp += Cell_PreviewMouseLeftButtonUp;
        hitLayer.MouseRightButtonDown += Cell_RightClick;
        hitLayer.DragOver += Cell_DragOver;
        hitLayer.Drop += Cell_Drop;
        hitLayer.DragLeave += Cell_DragLeave;

        var cellRoot = new Grid();
        cellRoot.Children.Add(panel);
        cellRoot.Children.Add(hitLayer);

        var border = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(2),
            Background = Brushes.Black,
            Child = cellRoot,
            Tag = idx,
            Cursor = Cursors.Arrow
        };

        ThumbGrid.Children.Add(border);
        _cellBorders.Add(border);
        _cellLabels.Add(label);
        _cellHitLayers.Add(hitLayer);
        _cellHosts.Add(host);

        RebuildGrid();

        // Force layout so BuildWindowCore runs and the HWND is ready.
        ThumbGrid.UpdateLayout();

        _slots.Add(new ThumbnailSlot(host, _mainHwnd));
        return idx;
    }

    private void RemoveSlot(int idx)
    {
        _flashingWindows.Remove(_slots[idx].SourceHwnd);
        _slots[idx].Clear();
        ThumbGrid.Children.Remove(_cellBorders[idx]);
        _cellHosts[idx].Dispose();

        _cellBorders.RemoveAt(idx);
        _cellLabels.RemoveAt(idx);
        _cellHitLayers.RemoveAt(idx);
        _cellHosts.RemoveAt(idx);
        _slots.RemoveAt(idx);

        for (int i = 0; i < _cellBorders.Count; i++)
        {
            _cellBorders[i].Tag = i;
            _cellHitLayers[i].Tag = i;
        }

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
        CheckFlashState();
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

    // ── Cell interaction (click / menu / drag reorder) ───────────

    private void ActivateSlotWindow(int idx)
    {
        if (idx >= _slots.Count || !_slots[idx].IsOccupied) return;
        IntPtr hwnd = _slots[idx].SourceHwnd;
        if (NativeMethods.IsIconic(hwnd))
            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
        NativeMethods.SetForegroundWindow(hwnd);
    }

    private void Cell_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { Tag: int idx }) return;
        _dragSourceIndex = idx;
        _dragStartPoint = e.GetPosition(this);
        _dragMoved = false;
        ((UIElement)sender).CaptureMouse();
        e.Handled = true;
    }

    private void Cell_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragSourceIndex < 0 || sender is not UIElement element) return;
        if (e.LeftButton != MouseButtonState.Pressed) return;

        Point current = e.GetPosition(this);
        Vector delta = current - _dragStartPoint;
        if (!_dragMoved &&
            Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        _dragMoved = true;
        int sourceIndex = _dragSourceIndex;
        _dragSourceIndex = -1;
        element.ReleaseMouseCapture();

        ShowDragGhost(sourceIndex, current);
        SetDropPreviewLayer(null);

        DragDrop.DoDragDrop(element, new DataObject(SlotDragFormat, sourceIndex), DragDropEffects.Move);

        HideDragGhost();
        SetDropPreviewLayer(null);
    }

    private void Cell_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { Tag: int idx }) return;
        ((UIElement)sender).ReleaseMouseCapture();

        if (!_dragMoved)
            ActivateSlotWindow(idx);

        _dragSourceIndex = -1;
        _dragMoved = false;
        e.Handled = true;
    }

    private void Cell_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { Tag: int idx }) return;

        var menu = new ContextMenu();

        var clearItem = new MenuItem { Header = "選択解除", IsEnabled = idx < _slots.Count };
        clearItem.Click += (_, _) => { if (idx < _slots.Count) RemoveSlot(idx); };

        var exitFullScreenItem = new MenuItem { Header = "全画面解除", IsEnabled = _isFullScreen };
        exitFullScreenItem.Click += (_, _) => { if (_isFullScreen) ToggleFullScreen(); };

        menu.Items.Add(clearItem);
        menu.Items.Add(exitFullScreenItem);
        menu.PlacementTarget = (FrameworkElement)sender;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private void Cell_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(SlotDragFormat) || sender is not Border targetLayer)
        {
            e.Effects = DragDropEffects.None;
            return;
        }

        e.Effects = DragDropEffects.Move;
        SetDropPreviewLayer(targetLayer);
        UpdateDragGhostPosition(e.GetPosition(this));
        e.Handled = true;
    }

    private void Cell_DragLeave(object sender, DragEventArgs e)
    {
        if (sender == _dropPreviewLayer)
            SetDropPreviewLayer(null);
    }

    private void Cell_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(SlotDragFormat) || sender is not Border { Tag: int targetIndex })
            return;

        int sourceIndex = (int)e.Data.GetData(SlotDragFormat)!;
        if (sourceIndex >= 0 && sourceIndex < _slots.Count &&
            targetIndex >= 0 && targetIndex < _slots.Count &&
            sourceIndex != targetIndex)
        {
            InsertSlot(sourceIndex, targetIndex);
        }

        SetDropPreviewLayer(null);
        e.Handled = true;
    }

    private static void MoveItem<T>(List<T> list, int sourceIndex, int targetIndex)
    {
        var item = list[sourceIndex];
        list.RemoveAt(sourceIndex);
        list.Insert(targetIndex, item);
    }

    private void InsertSlot(int sourceIndex, int targetIndex)
    {
        if (sourceIndex < targetIndex)
            targetIndex--;

        MoveItem(_cellBorders, sourceIndex, targetIndex);
        MoveItem(_cellLabels, sourceIndex, targetIndex);
        MoveItem(_cellHitLayers, sourceIndex, targetIndex);
        MoveItem(_cellHosts, sourceIndex, targetIndex);
        MoveItem(_slots, sourceIndex, targetIndex);

        for (int idx = 0; idx < _cellBorders.Count; idx++)
        {
            _cellBorders[idx].Tag = idx;
            _cellHitLayers[idx].Tag = idx;
        }

        RebuildGrid();
    }

    private static readonly Brush DefaultHitLayerBrush =
        new SolidColorBrush(Color.FromArgb(1, 255, 255, 255));

    private static readonly Brush PreviewHitLayerBrush =
        new SolidColorBrush(Color.FromArgb(70, 80, 160, 255));

    private void SetDropPreviewLayer(Border? layer)
    {
        if (_dropPreviewLayer != null)
            _dropPreviewLayer.Background = DefaultHitLayerBrush;

        _dropPreviewLayer = layer;

        if (_dropPreviewLayer != null)
            _dropPreviewLayer.Background = PreviewHitLayerBrush;
    }

    private void ShowDragGhost(int sourceIndex, Point windowPoint)
    {
        HideDragGhost();

        var ghostBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(180, 20, 20, 20)),
            BorderBrush = Brushes.DodgerBlue,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10),
            Child = new TextBlock
            {
                Text = _cellLabels[sourceIndex].Text,
                Foreground = Brushes.White,
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Width = 240
            }
        };

        _dragGhost = new Window
        {
            Width = 270,
            Height = 48,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ShowInTaskbar = false,
            Topmost = true,
            IsHitTestVisible = false,
            Content = ghostBorder,
            Opacity = 0.85
        };

        UpdateDragGhostPosition(windowPoint);
        _dragGhost.Show();
    }

    private void UpdateDragGhostPosition(Point windowPoint)
    {
        if (_dragGhost == null) return;
        Point screen = PointToScreen(windowPoint);
        _dragGhost.Left = screen.X + 12;
        _dragGhost.Top = screen.Y + 12;
    }

    private void HideDragGhost()
    {
        if (_dragGhost == null) return;
        _dragGhost.Close();
        _dragGhost = null;
    }

    // ── Flash detection (shell hook) ─────────────────────────────

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == (int)_shellHookMsgId)
        {
            int shellEvent = wParam.ToInt32();
            IntPtr targetHwnd = lParam;

            if (shellEvent == NativeMethods.HSHELL_FLASH)
            {
                OnWindowFlash(targetHwnd);
            }
            else if ((shellEvent & 0x7FFF) == NativeMethods.HSHELL_WINDOWACTIVATED)
            {
                OnWindowActivated(targetHwnd);
            }
        }
        return IntPtr.Zero;
    }

    private void OnWindowFlash(IntPtr hwnd)
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i].IsOccupied && _slots[i].SourceHwnd == hwnd)
            {
                if (_flashingWindows.Add(hwnd))
                    StartFlashBorder(i);
                return;
            }
        }
    }

    private void OnWindowActivated(IntPtr hwnd)
    {
        if (_flashingWindows.Remove(hwnd))
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].IsOccupied && _slots[i].SourceHwnd == hwnd)
                {
                    StopFlashBorder(i);
                    return;
                }
            }
        }
    }

    private void CheckFlashState()
    {
        if (_flashingWindows.Count == 0) return;
        IntPtr fg = NativeMethods.GetForegroundWindow();
        if (_flashingWindows.Remove(fg))
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].IsOccupied && _slots[i].SourceHwnd == fg)
                {
                    StopFlashBorder(i);
                    return;
                }
            }
        }
    }

    private static void StartFlashBorder(Border border)
    {
        var brush = new SolidColorBrush(Colors.Red);
        var anim = new ColorAnimation
        {
            From = Colors.Red,
            To = Color.FromArgb(0x40, 0xFF, 0x00, 0x00),
            Duration = TimeSpan.FromMilliseconds(400),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };
        brush.BeginAnimation(SolidColorBrush.ColorProperty, anim);
        border.BorderBrush = brush;
        border.BorderThickness = new Thickness(3);
    }

    private void StartFlashBorder(int idx) => StartFlashBorder(_cellBorders[idx]);

    private void StopFlashBorder(int idx)
    {
        _cellBorders[idx].BorderBrush = NormalBorderBrush;
        _cellBorders[idx].BorderThickness = new Thickness(1);
    }

    // ── Fullscreen ───────────────────────────────────────────────

    private void FullScreenButton_Click(object sender, RoutedEventArgs e) => ToggleFullScreen();

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