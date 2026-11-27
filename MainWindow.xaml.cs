using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace WindowThumbWall;

public partial class MainWindow : Window
{
    private const int AutoAddMaintenanceIntervalTicks = 4;
    private const string SlotDragFormat = "WindowThumbWall.SlotIndex";
    private const double DeadspaceWeight = 1.0;
    private const double DistortionWeight = 0.9;
    private const double SlotTitleFontSize = 12;
    private static readonly Thickness CellMargin = new(2);
    private static readonly Thickness CellBorderThickness = new(1);
    private static readonly Thickness SlotLabelPadding = new(6, 3, 6, 3);
    private static readonly TimeSpan ThumbnailUpdateMinInterval = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan ThumbnailRefreshDebounce = TimeSpan.FromMilliseconds(160);
    private static readonly TimeSpan ThumbnailRefreshMinInterval = TimeSpan.FromMilliseconds(1500);

    private IntPtr _mainHwnd;

    private readonly List<Border> _cellBorders = [];
    private readonly List<TextBlock> _cellLabels = [];
    private readonly List<Border> _cellHitLayers = [];
    private readonly List<ThumbHost> _cellHosts = [];
    private readonly List<ThumbnailSlot> _slots = [];

    private Point _dragStartPoint;
    private Guid? _dragSourceSlotId;
    private bool _dragMoved;
    private Border? _dropPreviewLayer;
    private Window? _dragGhost;
    private ShortcutGuideWindow? _shortcutGuideWindow;

    private bool _isFullScreen;
    private bool _isLeftPanelCollapsed;
    private WindowChoice? _windowChoice;
    private sealed record WindowChoice(Guid SlotId);
    private readonly WindowSlotRules _slotRules = new();
    private readonly HashSet<ThumbnailSlot> _pinsNeedingChoice = [];
    private sealed record SpecificWindowRow(Guid RuleId, Guid? SlotId, string Label);
    private WindowStyle _savedWindowStyle;
    private WindowState _savedWindowState;
    private GridLength _savedLeftColWidth;
    private GridLength _savedSplitterColWidth;

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(1000) };
    private readonly DispatcherTimer _stateSaveTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private readonly DispatcherTimer _thumbnailSettleTimer = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private readonly DispatcherTimer _thumbnailThrottleTimer = new();
    private readonly DispatcherTimer _thumbnailRefreshTimer = new() { Interval = ThumbnailRefreshDebounce };
    private readonly List<WindowInfo> _windowCache = [];
    private readonly TaskbarWindowOrder _taskbarWindowOrder = new();
    private readonly ObservableCollection<AutoAddAppEntry> _autoAddApps = [];
    private readonly HashSet<string> _autoAddAppSet = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _appDisplayNameCache = new(StringComparer.OrdinalIgnoreCase);
    private Point _appListDragStartPoint;
    private string? _appListDragSourceProcessName;
    private int _autoAddMaintenanceTick;
    private bool _notificationAttentionRequested;
    private bool _notificationAttentionEnabled;

    private uint _shellHookMsgId;
    private readonly HashSet<IntPtr> _flashingWindows = [];
    private readonly List<AttentionVisualState> _slotAttentionVisualStates = [];
    private IntPtr _activeSourceHwnd;
    private static readonly SolidColorBrush NormalBorderBrush =
        new(Color.FromRgb(0x55, 0x55, 0x55));
    private static readonly SolidColorBrush ActiveBorderBrush = new(Colors.White);
    private static readonly Thickness ActiveBorderThickness = new(2);
    private static readonly Thickness AttentionBorderThickness = new(3);
    static MainWindow()
    {
        NormalBorderBrush.Freeze();
        ActiveBorderBrush.Freeze();
    }

    private AppState? _pendingRestore;
    private readonly AppState _state;
    private readonly bool _canSaveState;
    private string? _stateNoticeKey;
    private bool _stateConflict;
    private bool _stateTrackingEnabled;
    private bool _gridRebuildPending;
    private bool _thumbnailUpdatePending;
    private bool _thumbnailUpdateRequestedWhilePending;
    private int _lastGridItemCount = -1;
    private int _lastGridRows = -1;
    private int _lastGridCols = -1;
    private long _lastThumbnailUpdateTicks;
    private readonly Dictionary<IntPtr, ThumbnailBinding> _thumbnailRefreshPendingWindows = [];
    private readonly Dictionary<IntPtr, long> _lastThumbnailRefreshTicks = [];

    public MainWindow()
    {
        InitializeComponent();
        if (string.Equals(Environment.GetEnvironmentVariable("WINDOWTHUMBWALL_DEVELOPMENT_NO_ACTIVATE"),
            "1", StringComparison.Ordinal))
        {
            ShowActivated = false;
        }
        ApplyLocalization();

        // Restore window geometry before the window is shown.
        var load = AppState.LoadWithStatus();
        _state = load.State;
        _canSaveState = load.CanSave;
        _pendingRestore = _state;
        _slotRules.Restore(_state.SpecificWindowRules);
        if (!_canSaveState)
            SetStateNotice("state.loadFailed");
        _notificationAttentionRequested = _pendingRestore.EnableOsNotificationAttention;
        _notificationAttentionEnabled = _notificationAttentionRequested && SupportsNotificationAttentionRuntime();
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
        InitializeNotificationFollowUpTimer();
        _stateSaveTimer.Tick += StateSaveTimer_Tick;
        _thumbnailSettleTimer.Tick += ThumbnailSettleTimer_Tick;
        _thumbnailThrottleTimer.Tick += ThumbnailThrottleTimer_Tick;
        _thumbnailRefreshTimer.Tick += ThumbnailRefreshTimer_Tick;
        Loaded += OnLoaded;
        Closed += OnClosed;
        LocationChanged += OnLocationChanged;
        SizeChanged += OnSizeChanged;
        LeftPanel.SizeChanged += OnPanelSizeChanged;
        AppList.SizeChanged += OnPanelSizeChanged;
        ThumbGrid.SizeChanged += OnThumbGridSizeChanged;
        WindowList.MouseDoubleClick += WindowList_DoubleClick;
        WindowList.PreviewMouseRightButtonDown += WindowList_RightClick;
        AppList.PreviewMouseRightButtonDown += AppList_RightClick;
        AppList.PreviewMouseLeftButtonDown += AppList_PreviewMouseLeftButtonDown;
        AppList.PreviewMouseMove += AppList_PreviewMouseMove;
        AppList.DragOver += AppList_DragOver;
        AppList.Drop += AppList_Drop;
        FilterBox.TextChanged += FilterBox_TextChanged;
        AppList.ItemsSource = _autoAddApps;
    }

    // Lifecycle

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _mainHwnd = new WindowInteropHelper(this).Handle;

        // Register for shell hook messages (flash / activation).
        _shellHookMsgId = NativeMethods.RegisterWindowMessage("SHELLHOOK");
        NativeMethods.RegisterShellHookWindow(_mainHwnd);
        HwndSource.FromHwnd(_mainHwnd)?.AddHook(WndProc);

        RefreshWindowList();
        RestorePanelLayout();
        RestoreAutoAddApps();
        RestoreSlots();
        SyncActiveSourceWindow(forceRefresh: true);
        if (_notificationAttentionEnabled)
            InitializeNotificationListenerAsync();
        _timer.Start();
        _stateTrackingEnabled = true;
        if (_state.NeedsMigrationBackup)
            RequestStateSave();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        DisposeNotificationListener();
        _timer.Stop();
        _stateSaveTimer.Stop();
        _thumbnailSettleTimer.Stop();
        _thumbnailThrottleTimer.Stop();
        _thumbnailRefreshTimer.Stop();
        _stateTrackingEnabled = false;
        NativeMethods.DeregisterShellHookWindow(_mainHwnd);
        SaveState();
        foreach (var slot in _slots) slot.Clear();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RequestStateSave();
        RequestGridRebuild();
    }

    private void OnLocationChanged(object? sender, EventArgs e) => RequestStateSave();

    private void OnPanelSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.WidthChanged || e.HeightChanged)
        {
            RequestStateSave();
            RequestGridRebuild();
        }
    }

    private void OnThumbGridSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!e.WidthChanged && !e.HeightChanged)
            return;

        RequestGridRebuild();
        ScheduleThumbnailSettle();
    }

    // State persistence

    private void SaveState()
    {
        if (!_canSaveState || _pendingRestore != null)
            return;

        CaptureCurrentState();
        AppStateSaveStatus result = _state.SaveWithStatus();
        _stateConflict = result == AppStateSaveStatus.SourceChanged;
        SetStateNotice(result switch
        {
            AppStateSaveStatus.Saved => null,
            AppStateSaveStatus.SourceChanged => "state.saveConflict",
            _ => "state.saveFailed"
        });
    }

    private void CaptureCurrentState()
    {
        var bounds = (WindowState == WindowState.Maximized || _isFullScreen)
            ? RestoreBounds
            : new Rect(Left, Top, Width, Height);

        _state.IsFullScreen = _isFullScreen;
        _state.IsLeftPanelCollapsed = _isLeftPanelCollapsed;
        _state.Geometry = WindowGeometry.FromBounds(bounds.Left, bounds.Top, bounds.Width, bounds.Height,
            !_isFullScreen && WindowState == WindowState.Maximized);
        _state.LeftPanelWidth = GetPersistedLength(
            _isFullScreen || _isLeftPanelCollapsed ? _savedLeftColWidth : LeftColumnDefinition.Width,
            LeftPanel.ActualWidth);
        _state.AppListHeight = GetPersistedLength(AppListRowDefinition.Height, AppList.ActualHeight);
        _state.EnableOsNotificationAttention = _notificationAttentionRequested;
        _state.Slots = _slots.Select(slot => slot.Intent).OfType<SlotState>().ToList();
        _state.SpecificWindowRules = _slotRules.All.ToList();
        _state.AutoAddApps = _autoAddApps.Select(app => app.ProcessName).ToList();
    }

    private void SetStateNotice(string? key)
    {
        _stateNoticeKey = key;
        UpdateStateNotice();
    }

    private void UpdateStateNotice()
    {
        StateStatusPanel.Visibility = _stateNoticeKey == null ? Visibility.Collapsed : Visibility.Visible;
        StateNotice.Visibility = StateStatusPanel.Visibility;
        StateNotice.Text = _stateNoticeKey == null ? string.Empty : LocalizedText.Get(_stateNoticeKey);
        StateRetryButton.Visibility = _stateNoticeKey is "state.saveFailed" or "state.saveConflict"
            or "state.copyFailed"
            ? Visibility.Visible : Visibility.Collapsed;
        StateRetryButton.Content = LocalizedText.Get(_stateConflict ? "state.saveCopy" : "state.retrySave");
    }

    private void StateRetryButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_stateConflict)
        {
            SaveRuleChange();
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = "WindowThumbWall-recovery.json",
            Filter = "JSON (*.json)|*.json",
            OverwritePrompt = false
        };
        if (dialog.ShowDialog(this) != true)
            return;
        CaptureCurrentState();
        SetStateNotice(_state.TrySaveRecoveryCopy(dialog.FileName)
            ? "state.copySaved" : "state.copyFailed");
    }

    private void RequestStateSave()
    {
        if (!_stateTrackingEnabled) return;
        _stateSaveTimer.Stop();
        _stateSaveTimer.Start();
    }

    private void StateSaveTimer_Tick(object? sender, EventArgs e)
    {
        _stateSaveTimer.Stop();
        SaveState();
    }

    private void SaveRuleChange()
    {
        if (!_stateTrackingEnabled) return;
        _stateSaveTimer.Stop();
        SaveState();
    }

    private void RestorePanelLayout()
    {
        if (_pendingRestore == null) return;

        if (_pendingRestore.LeftPanelWidth > 120)
            LeftColumnDefinition.Width = new GridLength(_pendingRestore.LeftPanelWidth);

        if (_pendingRestore.AppListHeight > 80)
            AppListRowDefinition.Height = new GridLength(_pendingRestore.AppListHeight);

        SetLeftPanelCollapsed(_pendingRestore.IsLeftPanelCollapsed);
    }

    private static double GetPersistedLength(GridLength gridLength, double actualFallback)
    {
        if (gridLength.IsAbsolute && gridLength.Value > 0)
            return gridLength.Value;
        return double.IsFinite(actualFallback) && actualFallback > 0 ? actualFallback : 0;
    }

    private void RestoreAutoAddApps()
    {
        if (_pendingRestore is not { AutoAddApps.Count: > 0 } state) return;

        foreach (var app in state.AutoAddApps)
            AddAppToAutoList(app);
    }

    private void RestoreSlots()
    {
        if (_pendingRestore is not AppState state)
        {
            _pendingRestore = null;
            return;
        }

        var attachedRules = state.Slots.Select(slot => slot.RuleId).OfType<Guid>().ToHashSet();
        var intents = state.Slots.Concat(_slotRules.All.Where(rule => !attachedRules.Contains(rule.RuleId))
            .Select(WindowSlotRules.CreatePlaceholder)).ToList();
        var matchStates = intents.Select(_slotRules.GetMatchState).ToArray();
        var candidates = CaptureRestoreCandidates(matchStates);
        var needsChoice = new HashSet<int>();
        var matches = WindowRestoreMatcher.Match(matchStates, candidates, needsChoice);
        var reserved = matches.Values.Select(match => match.Handle).ToHashSet();
        var legacy = PlanLegacyBindings(intents, candidates, reserved, needsChoice);
        for (int i = 0; i < intents.Count; i++)
        {
            SlotState intent = intents[i];
            WindowInfo? match = matches.GetValueOrDefault(i) ?? legacy.GetValueOrDefault(i);
            if (intent.IsAutomatic && match == null)
                continue;

            int idx = AddSlot(intent.SlotId);
            if (match == null || !AssignSlot(idx, match, intent))
            {
                if (intent.IsAutomatic)
                    RemoveSlot(idx);
                else
                    SetUnresolvedSlot(idx, intent, needsChoice.Contains(i) || intent.Identity == null);
            }
        }
        RefreshSpecificWindowsList();

        if (state.IsFullScreen && _slots.Count > 0)
            ToggleFullScreen();

        _pendingRestore = null;
    }

    // Keyboard

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape when _windowChoice != null:
                EndWindowChoice();
                e.Handled = true;
                break;
            case Key.Enter when e.OriginalSource != CollapseLeftPanelButton &&
                e.OriginalSource != ExpandLeftPanelButton && e.OriginalSource != StateRetryButton &&
                (_isFullScreen || !FilterBox.IsFocused):
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

    // Dynamic grid

    private (int rows, int cols) CalcGridSize(int count)
    {
        if (count <= 0) return (1, 1);

        double width = Math.Max(ThumbGrid.ActualWidth, 1);
        double height = Math.Max(ThumbGrid.ActualHeight, 1);

        if (width <= 1 || height <= 1)
        {
            int fallbackCols = (int)Math.Ceiling(Math.Sqrt(count));
            int fallbackRows = (int)Math.Ceiling((double)count / fallbackCols);
            return (fallbackRows, fallbackCols);
        }

        double titleBarHeight = EstimateTitleBarHeight();
        double horizontalChrome = CellMargin.Left + CellMargin.Right + CellBorderThickness.Left + CellBorderThickness.Right;
        double verticalChrome = CellMargin.Top + CellMargin.Bottom + CellBorderThickness.Top + CellBorderThickness.Bottom;
        double fallbackAspectRatio =
            Math.Max(width - horizontalChrome, 0.01) /
            Math.Max(height - verticalChrome - titleBarHeight, 0.01);
        var aspectRatios = CollectActiveAspectRatios(fallbackAspectRatio);

        return GridLayoutScorer.ChooseGrid(
            count,
            width,
            height,
            aspectRatios,
            titleBarHeight,
            horizontalChrome,
            verticalChrome,
            DeadspaceWeight,
            DistortionWeight);
    }

    private List<double> CollectActiveAspectRatios(double fallback)
    {
        var ratios = new List<double>();
        for (int i = 0; i < _slots.Count; i++)
        {
            if (!_slots[i].IsOccupied) continue;
            double ratio = NativeMethods.GetWindowAspectRatio(_slots[i].SourceHwnd, fallback);
            if (ratio > 0.01)
                ratios.Add(ratio);
        }

        if (ratios.Count == 0)
            ratios.Add(Math.Max(fallback, 0.01));

        return ratios;
    }

    private double EstimateTitleBarHeight()
    {
        double measuredHeight = 0;
        for (int i = 0; i < _cellLabels.Count; i++)
        {
            measuredHeight = Math.Max(measuredHeight, _cellLabels[i].ActualHeight);
        }

        if (measuredHeight > 0)
            return measuredHeight;

        var probe = new TextBlock
        {
            Text = LocalizedText.Get("slot.empty"),
            Padding = SlotLabelPadding,
            FontSize = SlotTitleFontSize,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return Math.Max(probe.DesiredSize.Height, 0.01);
    }

    private int AddSlot(Guid? slotId = null)
    {
        int idx = _cellBorders.Count;

        var label = new TextBlock
        {
            Text = LocalizedText.Get("slot.empty"),
            Foreground = Brushes.LightGray,
            Padding = SlotLabelPadding,
            FontSize = SlotTitleFontSize,
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
        hitLayer.SizeChanged += CellLayout_SizeChanged;

        var cellRoot = new Grid();
        cellRoot.Children.Add(panel);
        cellRoot.Children.Add(hitLayer);

        var border = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
            BorderThickness = CellBorderThickness,
            Margin = CellMargin,
            Background = Brushes.Black,
            Child = cellRoot,
            Tag = idx,
            Cursor = Cursors.Arrow
        };
        border.SizeChanged += CellLayout_SizeChanged;
        host.SizeChanged += CellLayout_SizeChanged;
        ThumbGrid.Children.Add(border);
        _cellBorders.Add(border);
        _cellLabels.Add(label);
        _cellHitLayers.Add(hitLayer);
        _cellHosts.Add(host);
        _slotAttentionVisualStates.Add(AttentionVisualState.None);

        RebuildGrid();

        // Force layout so BuildWindowCore runs and the HWND is ready.
        ThumbGrid.UpdateLayout();

        _slots.Add(new ThumbnailSlot(host, _mainHwnd, slotId));
        return idx;
    }

    private void RemoveSlot(int idx)
    {
        if (!ManualSlotPlanner.CanRemove(idx, _slots.Count)) return;

        IntPtr sourceHwnd = _slots[idx].SourceHwnd;
        ClearWindowAttentionState(sourceHwnd);
        _pinsNeedingChoice.Remove(_slots[idx]);
        if (_windowChoice?.SlotId == _slots[idx].SlotId)
            EndWindowChoice();

        _slots[idx].Clear();
        _cellBorders[idx].SizeChanged -= CellLayout_SizeChanged;
        _cellHitLayers[idx].SizeChanged -= CellLayout_SizeChanged;
        _cellHosts[idx].SizeChanged -= CellLayout_SizeChanged;
        ThumbGrid.Children.Remove(_cellBorders[idx]);
        _cellHosts[idx].Dispose();

        _cellBorders.RemoveAt(idx);
        _cellLabels.RemoveAt(idx);
        _cellHitLayers.RemoveAt(idx);
        _cellHosts.RemoveAt(idx);
        _slots.RemoveAt(idx);
        RefreshSpecificWindowsList();
        _slotAttentionVisualStates.RemoveAt(idx);
        SyncActiveSourceWindow(forceRefresh: true);

        for (int i = 0; i < _cellBorders.Count; i++)
        {
            _cellBorders[i].Tag = i;
            _cellHitLayers[i].Tag = i;
        }

        RebuildGrid();
        RequestStateSave();
        QueueNotificationAttentionSync();
    }

    private void RebuildGrid()
    {
        int count = _cellBorders.Count;
        if (count == 0)
        {
            if (_lastGridItemCount != 0 || _lastGridRows != 0 || _lastGridCols != 0)
            {
                ThumbGrid.RowDefinitions.Clear();
                ThumbGrid.ColumnDefinitions.Clear();
                _lastGridItemCount = 0;
                _lastGridRows = 0;
                _lastGridCols = 0;
            }
            return;
        }

        var (rows, cols) = CalcGridSize(count);

        bool gridShapeChanged = _lastGridItemCount != count || _lastGridRows != rows || _lastGridCols != cols;
        if (gridShapeChanged)
        {
            ThumbGrid.RowDefinitions.Clear();
            ThumbGrid.ColumnDefinitions.Clear();

            for (int r = 0; r < rows; r++)
                ThumbGrid.RowDefinitions.Add(new RowDefinition());
            for (int c = 0; c < cols; c++)
                ThumbGrid.ColumnDefinitions.Add(new ColumnDefinition());

            _lastGridItemCount = count;
            _lastGridRows = rows;
            _lastGridCols = cols;
        }

        for (int i = 0; i < count; i++)
        {
            Grid.SetRow(_cellBorders[i], i / cols);
            Grid.SetColumn(_cellBorders[i], i % cols);
            Grid.SetRowSpan(_cellBorders[i], 1);
            Grid.SetColumnSpan(_cellBorders[i], 1);
        }

        RequestThumbnailUpdate();
        ScheduleThumbnailSettle();
    }

    private void RequestGridRebuild()
    {
        if (_gridRebuildPending || _cellBorders.Count == 0) return;
        _gridRebuildPending = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Render, () =>
        {
            _gridRebuildPending = false;
            RebuildGrid();
        });
    }

    // Timer

    private void Timer_Tick(object? sender, EventArgs e)
    {
        RefreshWindowList();
        ValidateSlots();
        SyncMonitoredSlotTitles();

        _autoAddMaintenanceTick++;
        if (_autoAddMaintenanceTick >= AutoAddMaintenanceIntervalTicks)
        {
            _autoAddMaintenanceTick = 0;
            RetrySpecificWindowRules();
            RefreshAutoAddAppDisplayNames();
            AutoAddWindowsForRegisteredApps();
        }
        RecoverMissingThumbnailRegistrations();
        SyncActiveSourceWindow();
        CheckFlashState();
    }

    // Window list

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
                Title = NativeMethods.GetWindowTitle(hWnd),
                ProcessName = NativeMethods.GetProcessName(hWnd),
                Identity = WindowIdentity.CaptureLive(hWnd)
            });
            return true;
        }, IntPtr.Zero);
        _taskbarWindowOrder.ObserveWindowsInZOrder(_windowCache.Select(window => window.Handle));
        foreach (WindowInfo window in _windowCache)
            window.ObservedLifetime = _taskbarWindowOrder.GetLifetime(window.Handle);
        ApplyFilter();
    }

    private void SyncMonitoredSlotTitles()
    {
        if (_slots.Count == 0) return;

        var titleByHandle = new Dictionary<IntPtr, string>();
        foreach (var item in _windowCache)
            titleByHandle[item.Handle] = item.Title;

        bool changed = false;
        for (int i = 0; i < _slots.Count; i++)
        {
            if (!_slots[i].IsOccupied) continue;

            string latestTitle = titleByHandle.TryGetValue(_slots[i].SourceHwnd, out string? titleFromList)
                ? titleFromList
                : NativeMethods.GetWindowTitle(_slots[i].SourceHwnd);

            if (string.IsNullOrWhiteSpace(latestTitle)) continue;
            if (string.Equals(_slots[i].SourceTitle, latestTitle, StringComparison.Ordinal)) continue;

            _slots[i].UpdateSourceTitle(latestTitle);
            _cellLabels[i].Text = latestTitle;
            changed = true;
        }

        if (changed)
            RefreshSpecificWindowsList();
    }

    private void FilterBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        string filter = FilterBox.Text.Trim();
        var items = string.IsNullOrEmpty(filter)
            ? _windowCache.ToList()
            : _windowCache
                .Where(w =>
                    w.Title.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    w.ProcessName.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();

        var sel = WindowList.SelectedItem as WindowInfo;
        WindowList.ItemsSource = items;
        if (sel != null)
            WindowList.SelectedItem = items.FirstOrDefault(w => w.Handle == sel.Handle);
    }

    private void WindowList_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left ||
            FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject)?.DataContext is not WindowInfo info)
            return;

        WindowInfo? selectedWindow = CaptureCurrentWindow(info);
        if (_windowChoice is WindowChoice choice)
        {
            if (selectedWindow == null || !ResolveWindowChoice(choice, selectedWindow))
                ShowRuleNotice("rule.cannotChoose");
        }
        else if (selectedWindow != null)
        {
            AddWindowToMonitor(selectedWindow);
        }
    }

    private bool PinWindowToMonitor(WindowInfo info)
    {
        if (string.IsNullOrWhiteSpace(info.ProcessName) || !IsCurrentWindow(info))
            return false;

        ThumbnailSlot? existingSlot = _slots.FirstOrDefault(slot =>
            slot.IsOccupied && slot.SourceHwnd == info.Handle);
        if (existingSlot != null)
        {
            SlotState intent = existingSlot.Intent!;
            intent.ProcessName = info.ProcessName;
            intent.Title = info.Title;
            intent.Identity = info.Identity;
            _slotRules.SetRule(intent);
            RequestStateSave();
        }
        else if (!AddWindowToMonitor(info, isPinned: true))
        {
            return false;
        }

        bool replaced = _autoAddAppSet.Contains(info.ProcessName);
        RemoveAppFromAutoList(info.ProcessName);
        RefreshSpecificWindowsList();
        SaveRuleChange();
        if (replaced)
            ShowRuleNotice("rule.replacedApp", info.ProcessName);
        return true;
    }

    private bool ResolveWindowChoice(WindowChoice choice, WindowInfo info)
    {
        int slotIndex = FindSlotIndex(choice.SlotId);
        if (!ReferenceEquals(_windowChoice, choice) || slotIndex < 0 ||
            !IsCurrentWindow(info) || _slots[slotIndex].Intent is not SlotState previous)
            return false;

        int existingIndex = FindSlotIndexBySourceHwnd(info.Handle);
        if ((previous.RuleId != null && _autoAddAppSet.Contains(info.ProcessName)) ||
            (existingIndex >= 0 && existingIndex != slotIndex && _slots[existingIndex].Intent?.IsAutomatic != true))
            return false;

        SlotState replacement = WindowSlotRules.CreateIntent(choice.SlotId, info.ProcessName, info.Title,
            info.Identity!, isAutomatic: false, ruleId: previous.RuleId);
        if (!AssignSlot(slotIndex, info, replacement))
            return false;

        if (replacement.RuleId != null)
            _slotRules.SetRule(replacement);

        EndWindowChoice();
        if (existingIndex >= 0 && existingIndex != slotIndex)
        {
            RemoveSlot(existingIndex);
            RequestThumbnailRegistrationRefresh(info.Handle, bypassCooldown: true);
        }
        _pinsNeedingChoice.Remove(_slots[FindSlotIndex(choice.SlotId)]);
        RefreshSpecificWindowsList();
        SaveRuleChange();
        return true;
    }

    private WindowInfo? CaptureCurrentWindow(WindowInfo info)
    {
        if (!IsCurrentWindow(info))
            return null;

        return new WindowInfo
        {
            Handle = info.Handle,
            Title = NativeMethods.GetWindowTitle(info.Handle),
            ProcessName = info.ProcessName,
            Identity = WindowIdentity.Capture(info.Handle),
            ObservedLifetime = info.ObservedLifetime
        };
    }

    private bool IsCurrentWindow(WindowInfo info) =>
        info.Handle != IntPtr.Zero && info.ObservedLifetime is long lifetime &&
        _taskbarWindowOrder.GetLifetime(info.Handle) == lifetime &&
        NativeMethods.IsAltTabWindow(info.Handle) &&
        info.Identity?.IsSameLiveProcessAndClass(info.Handle) == true &&
        string.Equals(info.ProcessName, NativeMethods.GetProcessName(info.Handle), StringComparison.OrdinalIgnoreCase);

    private void RegisterAppRule(WindowInfo info)
    {
        if (string.IsNullOrWhiteSpace(info.ProcessName))
        {
            ShowRuleNotice("rule.cannotAdd");
            return;
        }

        AddAppToAutoList(info.ProcessName, ResolveDisplayNameFromWindow(info.Handle, info.ProcessName));
        bool replaced = false;
        foreach (WindowRuleState rule in _slotRules.All.Where(rule =>
                     string.Equals(rule.ProcessName, info.ProcessName, StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            SlotState? intent = _slots.FirstOrDefault(slot => slot.Intent?.RuleId == rule.RuleId)?.Intent;
            replaced |= _slotRules.RemoveRule(rule.RuleId, intent);
        }

        if (replaced)
        {
            RefreshSpecificWindowsList();
            RequestStateSave();
            ShowRuleNotice("rule.replacedSpecific", info.ProcessName);
        }
        SaveRuleChange();
    }

    private IReadOnlyList<WindowInfo> CaptureRestoreCandidates(IEnumerable<SlotState> slots)
    {
        var processes = slots.Select(slot => slot.ProcessName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return _windowCache.Where(window => processes.Contains(window.ProcessName))
            .Select(CaptureCurrentWindow)
            .OfType<WindowInfo>()
            .ToArray();
    }

    private Dictionary<int, WindowInfo> PlanLegacyBindings(
        IReadOnlyList<SlotState> intents, IReadOnlyList<WindowInfo> windows,
        HashSet<IntPtr> reserved, HashSet<int> needsChoice)
    {
        var matches = new Dictionary<int, WindowInfo>();
        for (int index = 0; index < intents.Count; index++)
        {
            SlotState intent = intents[index];
            if (!_autoAddAppSet.Contains(intent.ProcessName) || needsChoice.Contains(index))
                continue;

            WindowInfo[] candidates = windows.Where(window => WindowSlotRules.IsLegacyAppMatch(intent, window)).ToArray();
            if (candidates.Length == 1 && !WindowSlotRules.HasConflictingLegacyLifetime(intent, candidates[0]))
                matches.Add(index, candidates[0]);
            else if (candidates.Length > 0)
                needsChoice.Add(index);
        }

        var result = new Dictionary<int, WindowInfo>();
        foreach (var group in matches.GroupBy(match => match.Value.Handle))
        {
            if (group.Count() == 1 && reserved.Add(group.Key))
            {
                var match = group.Single();
                result.Add(match.Key, match.Value);
            }
            else
            {
                foreach (var match in group)
                    needsChoice.Add(match.Key);
            }
        }
        return result;
    }

    private void RetrySpecificWindowRules()
    {
        var pending = _slots.Select((slot, index) => (Slot: slot, State: slot.UnresolvedState, Index: index))
            .Where(item => item.State != null && item.Slot.SlotId != _windowChoice?.SlotId)
            .Select(item => (State: item.State!, item.Index)).ToArray();
        if (pending.Length == 0)
            return;

        SlotState[] states = pending.Select(item => item.State).ToArray();
        SlotState[] matchStates = states.Select(_slotRules.GetMatchState).ToArray();
        var candidates = CaptureRestoreCandidates(matchStates);
        var reserved = _slots.Where(slot => slot.IsOccupied).Select(slot => slot.SourceHwnd).ToHashSet();
        var needsChoice = new HashSet<int>();
        var matches = WindowRestoreMatcher.Match(matchStates, candidates, needsChoice, reserved);
        reserved.UnionWith(matches.Values.Select(match => match.Handle));
        var legacy = PlanLegacyBindings(states, candidates, reserved, needsChoice);
        for (int i = 0; i < pending.Length; i++)
        {
            int index = pending[i].Index;
            WindowInfo? match = matches.GetValueOrDefault(i) ?? legacy.GetValueOrDefault(i);
            if (match != null)
            {
                if (FindSlotIndexBySourceHwnd(match.Handle) < 0 && TryResolveSlot(index, match))
                    continue;
                needsChoice.Add(i);
            }

            UpdateWaitingStatus(index, needsChoice.Contains(i) || states[i].Identity == null);
        }
        RefreshSpecificWindowsList();
    }

    private bool TryResolveSlot(int index, WindowInfo info)
    {
        SlotState saved = _slots[index].UnresolvedState!;
        if (!AssignSlot(index, info, saved))
            return false;

        _pinsNeedingChoice.Remove(_slots[index]);
        RefreshSpecificWindowsList();
        RequestStateSave();
        return true;
    }

    private void UpdateWaitingStatus(int index, bool needsChoice)
    {
        ThumbnailSlot slot = _slots[index];
        SlotState saved = slot.UnresolvedState!;
        if (needsChoice)
            _pinsNeedingChoice.Add(slot);
        else
            _pinsNeedingChoice.Remove(slot);

        string title = string.IsNullOrWhiteSpace(saved.Title) ? saved.ProcessName : saved.Title;
        _cellLabels[index].Text = string.Format(
            LocalizedText.Get(needsChoice ? "slot.unresolved" : "slot.waiting"), title);
    }

    private void RefreshSpecificWindowsList()
    {
        Guid? selected = (SpecificWindowsList.SelectedItem as SpecificWindowRow)?.RuleId;
        var attached = _slots.Select((slot, index) => (slot, index))
            .Where(item => item.slot.Intent?.RuleId != null)
            .Select(item => new SpecificWindowRow(item.slot.Intent!.RuleId!.Value, item.slot.SlotId, _cellLabels[item.index].Text))
            .ToList();
        var attachedIds = attached.Select(row => row.RuleId).ToHashSet();
        SpecificWindowRow[] rows = attached.Concat(_slotRules.All.Where(rule => !attachedIds.Contains(rule.RuleId))
            .Select(rule => new SpecificWindowRow(rule.RuleId, null, rule.TitleAnchor))).ToArray();
        if (SpecificWindowsList.ItemsSource is SpecificWindowRow[] current && current.SequenceEqual(rows))
            return;

        SpecificWindowsList.ItemsSource = rows;
        SpecificWindowsList.SelectedItem = rows.FirstOrDefault(row => row.RuleId == selected);
    }

    private void SpecificWindowsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var row = SpecificWindowsList.SelectedItem as SpecificWindowRow;
        for (int i = 0; i < _slots.Count; i++)
            _cellLabels[i].Background = _slots[i].SlotId == row?.SlotId ? Brushes.DimGray : null;
    }

    private void SpecificWindowsList_RightClick(object sender, MouseButtonEventArgs e)
    {
        var item = FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (item?.DataContext is not SpecificWindowRow row)
            return;

        item.IsSelected = true;
        var menu = new ContextMenu { PlacementTarget = item };
        if (row.SlotId is Guid slotId)
        {
            var chooseItem = new MenuItem { Header = LocalizedText.Get("menu.chooseWindow") };
            chooseItem.Click += (_, _) => BeginWindowChoice(slotId);
            menu.Items.Add(chooseItem);
        }
        else
        {
            var showItem = new MenuItem { Header = LocalizedText.Get("menu.showRegisteredWindow") };
            showItem.Click += (_, _) => RestoreRuleSlot(row.RuleId);
            menu.Items.Add(showItem);
        }

        var removeItem = new MenuItem { Header = LocalizedText.Get("menu.removeAutoAdd") };
        removeItem.Click += (_, _) => RemoveSpecificWindowRule(row.RuleId);
        menu.Items.Add(removeItem);
        menu.IsOpen = true;
        e.Handled = true;
    }

    private void RestoreRuleSlot(Guid ruleId)
    {
        WindowRuleState? rule = _slotRules.All.FirstOrDefault(rule => rule.RuleId == ruleId);
        if (rule == null || _slots.Any(slot => slot.Intent?.RuleId == ruleId))
            return;

        SlotState intent = WindowSlotRules.CreatePlaceholder(rule);
        SetUnresolvedSlot(AddSlot(intent.SlotId), intent);
        RetrySpecificWindowRules();
        SaveRuleChange();
    }

    private void RemoveSpecificWindowRule(Guid ruleId)
    {
        SlotState? intent = _slots.FirstOrDefault(slot => slot.Intent?.RuleId == ruleId)?.Intent;
        if (!_slotRules.RemoveRule(ruleId, intent))
            return;
        RefreshSpecificWindowsList();
        SaveRuleChange();
    }

    private void BeginWindowChoice(Guid slotId)
    {
        int index = FindSlotIndex(slotId);
        if (index < 0 || _slots[index].Intent == null)
            return;

        if (_isFullScreen)
            ToggleFullScreen();
        SetLeftPanelCollapsed(false);
        _windowChoice = new WindowChoice(slotId);
        FilterLabel.Text = LocalizedText.Get("label.chooseWindowForSlot");
        RuleNotice.Visibility = Visibility.Collapsed;
        FilterBox.Focus();
    }

    private void EndWindowChoice()
    {
        _windowChoice = null;
        FilterLabel.Text = LocalizedText.Get("label.filter");
        RuleNotice.Visibility = Visibility.Collapsed;
    }

    private void ShowRuleNotice(string key, params object[] args)
    {
        RuleNotice.Text = string.Format(LocalizedText.Get(key), args);
        RuleNotice.Visibility = Visibility.Visible;
    }

    private bool AddWindowToMonitor(WindowInfo info, int? insertIndex = null, bool isPinned = false)
    {
        // Skip if already assigned.
        foreach (var slot in _slots)
            if (slot.IsOccupied && slot.SourceHwnd == info.Handle) return false;

        if (insertIndex is int targetIndex)
        {
            targetIndex = Math.Clamp(targetIndex, 0, _slots.Count);
            int sourceIndex = AddSlot();
            if (!AssignNewSlot(sourceIndex, info, isPinned, isAutomatic: true))
            {
                RemoveSlot(sourceIndex);
                return false;
            }

            if (sourceIndex != targetIndex)
                InsertSlot(sourceIndex, targetIndex);
            RequestStateSave();
            return true;
        }

        HashSet<int> reservedEmptySlots = _slots
            .Select((slot, index) => (slot, index))
            .Where(item => item.slot.IsUnresolved)
            .Select(item => item.index)
            .ToHashSet();
        int target = ManualSlotPlanner.GetAddTarget(
            _slots.Select(slot => slot.IsOccupied ? slot.SourceHwnd : IntPtr.Zero).ToList(),
            info.Handle,
            reservedEmptySlots);
        if (target < 0)
            return false;

        // No free slot -> add a new one.
        bool appended = target == _slots.Count;
        if (appended)
            target = AddSlot();

        if (AssignNewSlot(target, info, isPinned))
        {
            RequestStateSave();
            return true;
        }

        if (appended)
            RemoveSlot(target);
        return false;
    }

    private static T? FindVisualParent<T>(DependencyObject? source) where T : DependencyObject
    {
        while (source != null && source is not T)
            source = VisualTreeHelper.GetParent(source);

        return source as T;
    }

    private void WindowList_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox list) return;
        var item = FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (item == null) return;

        item.IsSelected = true;
        if (list.SelectedItem is not WindowInfo info) return;

        WindowInfo? choiceInfo = CaptureCurrentWindow(info);
        if (choiceInfo == null)
            return;

        var menu = new ContextMenu();
        if (_windowChoice is WindowChoice choice)
        {
            var useItem = new MenuItem { Header = LocalizedText.Get("button.useWindow") };
            useItem.Click += (_, _) =>
            {
                if (!ResolveWindowChoice(choice, choiceInfo))
                    ShowRuleNotice("rule.cannotChoose");
            };
            menu.Items.Add(useItem);
            var cancelItem = new MenuItem { Header = LocalizedText.Get("button.cancel") };
            cancelItem.Click += (_, _) => { if (ReferenceEquals(_windowChoice, choice)) EndWindowChoice(); };
            menu.Items.Add(cancelItem);
            menu.PlacementTarget = item;
            menu.IsOpen = true;
            e.Handled = true;
            return;
        }
        var addToMonitorItem = new MenuItem { Header = LocalizedText.Get("menu.addToMonitor") };
        addToMonitorItem.Click += (_, _) =>
        {
            if (!PinWindowToMonitor(choiceInfo))
                ShowRuleNotice("rule.cannotAdd");
        };

        var addAppItem = new MenuItem { Header = LocalizedText.Get("menu.addApp") };
        addAppItem.Click += (_, _) =>
        {
            if (IsCurrentWindow(choiceInfo))
                RegisterAppRule(choiceInfo);
        };

        menu.Items.Add(addToMonitorItem);
        menu.Items.Add(addAppItem);
        menu.PlacementTarget = item;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private void AppList_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox list) return;
        var item = FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (item == null) return;

        item.IsSelected = true;
        if (list.SelectedItem is not AutoAddAppEntry app) return;
        int index = _autoAddApps.IndexOf(app);

        var menu = new ContextMenu();
        var moveUpItem = new MenuItem { Header = LocalizedText.Get("menu.moveUp"), IsEnabled = index > 0 };
        moveUpItem.Click += (_, _) => MoveAutoApp(index, index - 1);

        var moveDownItem = new MenuItem
        {
            Header = LocalizedText.Get("menu.moveDown"),
            IsEnabled = index >= 0 && index < _autoAddApps.Count - 1
        };
        moveDownItem.Click += (_, _) => MoveAutoApp(index, index + 1);

        var removeItem = new MenuItem { Header = LocalizedText.Get("menu.removeAutoAdd") };
        removeItem.Click += (_, _) =>
        {
            RemoveAppFromAutoList(app.ProcessName);
            SaveRuleChange();
        };
        menu.Items.Add(moveUpItem);
        menu.Items.Add(moveDownItem);
        menu.Items.Add(removeItem);
        menu.PlacementTarget = item;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private void AddAppToAutoList(string processName, string? displayName = null)
    {
        if (string.IsNullOrWhiteSpace(processName)) return;

        if (!string.IsNullOrWhiteSpace(displayName))
            _appDisplayNameCache[processName] = displayName;

        if (!_autoAddAppSet.Add(processName))
        {
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                int existingIndex = _autoAddApps
                    .Select((app, idx) => new { app, idx })
                    .FirstOrDefault(x => x.app.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase))
                    ?.idx ?? -1;
                if (existingIndex >= 0 && _autoAddApps[existingIndex].DisplayName != displayName)
                {
                    _autoAddApps[existingIndex].DisplayName = displayName;
                    RequestStateSave();
                }
            }
            return;
        }

        _autoAddApps.Add(new AutoAddAppEntry
        {
            ProcessName = processName,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? processName : displayName
        });
        RequestStateSave();
    }

    private void RemoveAppFromAutoList(string processName)
    {
        if (!_autoAddAppSet.Remove(processName)) return;

        _appDisplayNameCache.Remove(processName);

        AutoAddAppEntry? existing = _autoAddApps.FirstOrDefault(a =>
            a.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            _autoAddApps.Remove(existing);
            RequestStateSave();
        }
    }

    private void MoveAutoApp(int sourceIndex, int targetIndex)
    {
        if (sourceIndex < 0 || sourceIndex >= _autoAddApps.Count) return;
        if (targetIndex < 0 || targetIndex >= _autoAddApps.Count) return;
        if (sourceIndex == targetIndex) return;

        AutoAddAppEntry item = _autoAddApps[sourceIndex];
        _autoAddApps.RemoveAt(sourceIndex);
        _autoAddApps.Insert(targetIndex, item);
        SaveRuleChange();
    }

    private void AppList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _appListDragStartPoint = e.GetPosition(AppList);
        var item = FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject);
        _appListDragSourceProcessName = (item?.DataContext as AutoAddAppEntry)?.ProcessName;
    }

    private void AppList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (_appListDragSourceProcessName == null) return;

        Point current = e.GetPosition(AppList);
        Vector delta = current - _appListDragStartPoint;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        string dragSource = _appListDragSourceProcessName;
        _appListDragSourceProcessName = null;
        DragDrop.DoDragDrop(AppList, new DataObject("WindowThumbWall.AppListItem", dragSource), DragDropEffects.Move);
    }

    private void AppList_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent("WindowThumbWall.AppListItem"))
            e.Effects = DragDropEffects.Move;
        else
            e.Effects = DragDropEffects.None;

        e.Handled = true;
    }

    private void AppList_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("WindowThumbWall.AppListItem")) return;
        if (e.Data.GetData("WindowThumbWall.AppListItem") is not string sourceApp) return;

        int sourceIndex = _autoAddApps
            .Select((app, idx) => new { app.ProcessName, idx })
            .FirstOrDefault(x => x.ProcessName.Equals(sourceApp, StringComparison.OrdinalIgnoreCase))
            ?.idx ?? -1;
        if (sourceIndex < 0) return;

        int targetIndex = _autoAddApps.Count - 1;
        var targetItem = FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (targetItem != null)
        {
            int targetItemIndex = AppList.ItemContainerGenerator.IndexFromContainer(targetItem);
            Point pos = e.GetPosition(targetItem);
            targetIndex = pos.Y <= targetItem.ActualHeight / 2 ? targetItemIndex : targetItemIndex + 1;
            if (sourceIndex < targetIndex)
                targetIndex--;
            targetIndex = Math.Clamp(targetIndex, 0, _autoAddApps.Count - 1);
        }

        MoveAutoApp(sourceIndex, targetIndex);
    }

    private void AutoAddWindowsForRegisteredApps()
    {
        if (_autoAddAppSet.Count == 0) return;

        Dictionary<string, int> nextInsertIndexByProcess = BuildAutoAddInsertIndexes();
        IReadOnlyList<WindowInfo> windowsInTaskbarOrder = _taskbarWindowOrder.Order(
            _windowCache,
            window => window.Handle);
        HashSet<IntPtr> monitoredHandles = _slots
            .Where(slot => slot.IsOccupied)
            .Select(slot => slot.SourceHwnd)
            .ToHashSet();
        IReadOnlyList<WindowInfo> pendingWindows = AutoAddWindowPlanner.GetPendingWindows(
            _autoAddApps,
            windowsInTaskbarOrder.Where(window => !_slots.Any(slot =>
                slot.UnresolvedState is SlotState intent && WindowSlotRules.IsLegacyAppMatch(intent, window))),
            monitoredHandles);

        foreach (WindowInfo info in pendingWindows)
        {
            if (!nextInsertIndexByProcess.TryGetValue(info.ProcessName, out int insertIndex))
                continue;

            WindowInfo? candidate = CaptureCurrentWindow(info);
            if (candidate == null || !AddWindowToMonitor(candidate, insertIndex))
                continue;

            AutoAddWindowPlanner.AdvanceInsertIndexes(
                _autoAddApps,
                nextInsertIndexByProcess,
                info.ProcessName,
                insertIndex,
                _slots.Count);
        }
    }

    private Dictionary<string, int> BuildAutoAddInsertIndexes()
    {
        var hwndToProcess = _windowCache
            .GroupBy(window => window.Handle)
            .ToDictionary(group => group.Key, group => group.First().ProcessName);
        var nextInsertIndexByProcess = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int lastMatchingSlotIndex = -1;

        foreach (var app in _autoAddApps)
        {
            for (int i = lastMatchingSlotIndex + 1; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (!slot.IsOccupied && !slot.IsUnresolved)
                    continue;

                string? slotProcess = slot.IsOccupied
                    ? slot.SourceProcessName
                    : slot.UnresolvedState?.ProcessName;
                if (string.IsNullOrWhiteSpace(slotProcess) &&
                    !hwndToProcess.TryGetValue(slot.SourceHwnd, out slotProcess))
                {
                    continue;
                }

                if (slotProcess!.Equals(app.ProcessName, StringComparison.OrdinalIgnoreCase))
                    lastMatchingSlotIndex = i;
            }

            nextInsertIndexByProcess[app.ProcessName] = lastMatchingSlotIndex + 1;
        }

        return nextInsertIndexByProcess;
    }

    private void RefreshAutoAddAppDisplayNames()
    {
        var representativeWindows = new Dictionary<string, IntPtr>(StringComparer.OrdinalIgnoreCase);
        foreach (var window in _windowCache)
        {
            if (string.IsNullOrWhiteSpace(window.ProcessName) || representativeWindows.ContainsKey(window.ProcessName))
                continue;

            representativeWindows[window.ProcessName] = window.Handle;
        }

        for (int i = 0; i < _autoAddApps.Count; i++)
        {
            AutoAddAppEntry entry = _autoAddApps[i];
            if (!representativeWindows.TryGetValue(entry.ProcessName, out IntPtr hWnd))
                continue;

            if (!_appDisplayNameCache.TryGetValue(entry.ProcessName, out string? displayName) ||
                string.IsNullOrWhiteSpace(displayName))
            {
                displayName = ResolveDisplayNameFromWindow(hWnd, entry.ProcessName);
                _appDisplayNameCache[entry.ProcessName] = displayName;
            }

            if (displayName == entry.DisplayName) continue;

            entry.DisplayName = displayName;
        }
    }

    private string ResolveDisplayNameFromWindow(IntPtr hWnd, string fallbackProcessName)
    {
        string displayName = NativeMethods.GetAppDisplayName(hWnd);
        return string.IsNullOrWhiteSpace(displayName) ? fallbackProcessName : displayName;
    }

    // Slot validation

    private void ValidateSlots()
    {
        bool changed = false;
        for (int i = _slots.Count - 1; i >= 0; i--)
        {
            ThumbnailSlot slot = _slots[i];
            if (!slot.IsOccupied || slot.IsSourceWindowValid(_taskbarWindowOrder))
                continue;

            IntPtr sourceHwnd = slot.SourceHwnd;
            ClearWindowAttentionState(sourceHwnd);
            if (slot.Intent is { IsAutomatic: false } intent)
            {
                if (slot.Identity is WindowIdentity identity)
                    intent.LastBindingIdentity = identity.WithoutHandle();
                SetUnresolvedSlot(i, intent);
                RequestStateSave();
            }
            else
                RemoveSlot(i);
            changed = true;
        }

        if (changed)
        {
            SyncActiveSourceWindow(forceRefresh: true);
            QueueNotificationAttentionSync();
        }
    }

    private void UpdateAllThumbnails()
    {
        foreach (var slot in _slots)
            slot.UpdateThumbnail();
    }

    private bool AssignNewSlot(int idx, WindowInfo info, bool isPinned, bool isAutomatic = false)
    {
        if (info.Identity is not WindowIdentity identity)
            return false;

        SlotState intent = WindowSlotRules.CreateIntent(_slots[idx].SlotId, info.ProcessName, info.Title, identity, isAutomatic);
        if (!AssignSlot(idx, info, intent))
            return false;
        if (isPinned)
            _slotRules.SetRule(intent);
        return true;
    }

    private bool AssignSlot(int idx, WindowInfo info, SlotState intent)
    {
        if (info.ObservedLifetime is not long lifetime || !IsCurrentWindow(info))
            return false;

        IntPtr previousSource = _slots[idx].SourceHwnd;
        var binding = new ThumbnailBinding(info.Handle, lifetime);
        if (!_slots[idx].Assign(binding, info.Title, info.ProcessName, info.Identity!, intent))
            return false;

        if (previousSource != IntPtr.Zero && previousSource != info.Handle)
            ClearWindowAttentionState(previousSource);
        _cellLabels[idx].Text = info.Title;
        _cellLabels[idx].Foreground = Brushes.White;
        _cellBorders[idx].BorderBrush = NormalBorderBrush;
        _cellBorders[idx].ToolTip = null;
        RequestThumbnailRegistrationRefresh(info.Handle, bypassCooldown: true);
        SyncActiveSourceWindow(forceRefresh: true);
        UpdateSlotAttentionVisual(idx);
        QueueNotificationAttentionSync();
        return true;
    }

    private void SetUnresolvedSlot(int idx, SlotState saved, bool needsChoice = false)
    {
        _slots[idx].SetUnresolved(saved);
        UpdateWaitingStatus(idx, needsChoice);
        RefreshSpecificWindowsList();
        _cellLabels[idx].Foreground = Brushes.Orange;
        _cellBorders[idx].ToolTip = LocalizedText.Get("menu.chooseWindow");
        UpdateSlotAttentionVisual(idx);
    }

    private void ClearWindowAttentionState(IntPtr sourceHwnd)
    {
        ClearNotificationAttentionGroupsForWindow(sourceHwnd);
        _flashingWindows.Remove(sourceHwnd);
        _thumbnailRefreshPendingWindows.Remove(sourceHwnd);
        _lastThumbnailRefreshTicks.Remove(sourceHwnd);
    }

    private void RecoverMissingThumbnailRegistrations()
    {
        foreach (var slot in _slots)
        {
            if (!slot.IsOccupied || slot.HasRegistration)
                continue;

            RequestThumbnailRegistrationRefresh(slot.SourceHwnd, bypassCooldown: true);
        }
    }

    private int FindSlotIndexBySourceHwnd(IntPtr sourceHwnd)
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i].Binding is ThumbnailBinding binding && binding.Handle == sourceHwnd &&
                binding.IsObservedCurrent(_taskbarWindowOrder))
                return i;
        }

        return -1;
    }

    private int FindSlotIndex(Guid slotId) => _slots.FindIndex(slot => slot.SlotId == slotId);

    private void RequestThumbnailRegistrationRefresh(IntPtr sourceHwnd, bool bypassCooldown = false)
    {
        if (sourceHwnd == IntPtr.Zero)
            return;

        int index = FindSlotIndexBySourceHwnd(sourceHwnd);
        if (index < 0 || _slots[index].Binding is not ThumbnailBinding binding ||
            !binding.IsObservedCurrent(_taskbarWindowOrder))
            return;

        if (!bypassCooldown &&
            _lastThumbnailRefreshTicks.TryGetValue(sourceHwnd, out long lastRefreshTicks))
        {
            long nowTicks = Stopwatch.GetTimestamp();
            long minIntervalTicks = (long)(ThumbnailRefreshMinInterval.TotalSeconds * Stopwatch.Frequency);
            if (nowTicks - lastRefreshTicks < minIntervalTicks)
                return;
        }

        if (_thumbnailRefreshPendingWindows.TryGetValue(sourceHwnd, out ThumbnailBinding pending) &&
            pending == binding)
            return;
        _thumbnailRefreshPendingWindows[sourceHwnd] = binding;

        _thumbnailRefreshTimer.Stop();
        _thumbnailRefreshTimer.Start();
    }

    private void ThumbnailRefreshTimer_Tick(object? sender, EventArgs e)
    {
        _thumbnailRefreshTimer.Stop();
        if (_thumbnailRefreshPendingWindows.Count == 0)
            return;

        ThumbnailBinding[] pendingWindows = _thumbnailRefreshPendingWindows.Values.ToArray();
        _thumbnailRefreshPendingWindows.Clear();

        bool refreshedAny = false;
        long refreshTicks = Stopwatch.GetTimestamp();
        foreach (ThumbnailBinding pending in pendingWindows)
        {
            int idx = FindSlotIndexBySourceHwnd(pending.Handle);
            if (idx < 0 || _slots[idx].Binding != pending || !pending.IsObservedCurrent(_taskbarWindowOrder))
                continue;

            _lastThumbnailRefreshTicks[pending.Handle] = refreshTicks;
            if (_slots[idx].RefreshRegistration(pending, _taskbarWindowOrder))
                refreshedAny = true;
        }

        if (refreshedAny)
            ScheduleThumbnailSettle();
    }

    private void RequestThumbnailUpdate()
    {
        if (_thumbnailUpdatePending)
        {
            _thumbnailUpdateRequestedWhilePending = true;
            return;
        }

        long nowTicks = Stopwatch.GetTimestamp();
        long elapsedTicks = nowTicks - _lastThumbnailUpdateTicks;
        long minIntervalTicks = (long)(ThumbnailUpdateMinInterval.TotalSeconds * Stopwatch.Frequency);

        if (_lastThumbnailUpdateTicks != 0 && elapsedTicks < minIntervalTicks)
        {
            _thumbnailUpdateRequestedWhilePending = true;
            ScheduleThumbnailThrottle(minIntervalTicks - elapsedTicks);
            return;
        }

        _thumbnailUpdatePending = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Render, () =>
        {
            _thumbnailUpdatePending = false;
            _thumbnailThrottleTimer.Stop();
            UpdateAllThumbnails();
            _lastThumbnailUpdateTicks = Stopwatch.GetTimestamp();

            if (_thumbnailUpdateRequestedWhilePending)
            {
                _thumbnailUpdateRequestedWhilePending = false;
                RequestThumbnailUpdate();
            }
        });
    }

    private void ScheduleThumbnailThrottle(long remainingTicks)
    {
        double remainingMs = remainingTicks * 1000.0 / Stopwatch.Frequency;
        if (remainingMs < 1)
            remainingMs = 1;

        _thumbnailThrottleTimer.Stop();
        _thumbnailThrottleTimer.Interval = TimeSpan.FromMilliseconds(remainingMs);
        _thumbnailThrottleTimer.Start();
    }

    private void ScheduleThumbnailSettle()
    {
        _thumbnailSettleTimer.Stop();
        _thumbnailSettleTimer.Start();
    }

    private void ThumbnailSettleTimer_Tick(object? sender, EventArgs e)
    {
        _thumbnailSettleTimer.Stop();
        RequestThumbnailUpdate();
    }

    private void ThumbnailThrottleTimer_Tick(object? sender, EventArgs e)
    {
        _thumbnailThrottleTimer.Stop();
        if (!_thumbnailUpdateRequestedWhilePending)
            return;

        _thumbnailUpdateRequestedWhilePending = false;
        RequestThumbnailUpdate();
    }

    private void CellLayout_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!e.WidthChanged && !e.HeightChanged)
            return;

        ScheduleThumbnailSettle();
    }

    // Cell interaction (click / menu / drag reorder)

    private void ActivateSlotWindow(int idx)
    {
        if (idx < 0 || idx >= _slots.Count)
            return;

        ThumbnailSlot slot = _slots[idx];
        if (!slot.IsSourceWindowValid(_taskbarWindowOrder))
            return;

        IntPtr hwnd = slot.SourceHwnd;
        if (NativeMethods.IsIconic(hwnd))
            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
        if (slot.SourceHwnd == hwnd && slot.IsSourceWindowValid(_taskbarWindowOrder))
            NativeMethods.SetForegroundWindow(hwnd);
    }

    private void Cell_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { Tag: int idx } || idx >= _slots.Count) return;
        _dragSourceSlotId = _slots[idx].SlotId;
        _dragStartPoint = e.GetPosition(this);
        _dragMoved = false;
        ((UIElement)sender).CaptureMouse();
        e.Handled = true;
    }

    private void Cell_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragSourceSlotId is not Guid slotId || sender is not UIElement element) return;
        if (e.LeftButton != MouseButtonState.Pressed) return;

        Point current = e.GetPosition(this);
        Vector delta = current - _dragStartPoint;
        if (!_dragMoved &&
            Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        _dragMoved = true;
        int sourceIndex = FindSlotIndex(slotId);
        _dragSourceSlotId = null;
        element.ReleaseMouseCapture();
        if (sourceIndex < 0)
            return;

        ShowDragGhost(sourceIndex, current);
        SetDropPreviewLayer(null);

        DragDrop.DoDragDrop(element, new DataObject(SlotDragFormat, slotId.ToString("D")), DragDropEffects.Move);

        HideDragGhost();
        SetDropPreviewLayer(null);
    }

    private void Cell_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border) return;
        ((UIElement)sender).ReleaseMouseCapture();

        if (!_dragMoved && _dragSourceSlotId is Guid slotId)
        {
            int index = FindSlotIndex(slotId);
            if (index >= 0)
                ActivateSlotWindow(index);
        }

        _dragSourceSlotId = null;
        _dragMoved = false;
        e.Handled = true;
    }

    private void Cell_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { Tag: int idx } || idx >= _slots.Count) return;

        Guid slotId = _slots[idx].SlotId;
        var menu = new ContextMenu();
        if (_slots[idx].Intent != null)
        {
            var chooseWindowItem = new MenuItem { Header = LocalizedText.Get("menu.chooseWindow") };
            chooseWindowItem.Click += (_, _) => BeginWindowChoice(slotId);
            menu.Items.Add(chooseWindowItem);
        }

        var clearItem = new MenuItem { Header = LocalizedText.Get("menu.unassign"), IsEnabled = idx < _slots.Count };
        clearItem.Click += (_, _) => RemoveSlot(FindSlotIndex(slotId));

        var exitFullScreenItem = new MenuItem { Header = LocalizedText.Get("menu.exitFullscreen"), IsEnabled = _isFullScreen };
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
        if (e.Data.GetData(SlotDragFormat) is not string value || !Guid.TryParse(value, out Guid slotId) ||
            sender is not Border { Tag: int targetIndex })
            return;

        int sourceIndex = FindSlotIndex(slotId);
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
        RefreshSpecificWindowsList();
        MoveItem(_slotAttentionVisualStates, sourceIndex, targetIndex);

        for (int idx = 0; idx < _cellBorders.Count; idx++)
        {
            _cellBorders[idx].Tag = idx;
            _cellHitLayers[idx].Tag = idx;
        }

        RebuildGrid();
        RequestStateSave();
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
            ShowActivated = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Topmost = true,
            IsHitTestVisible = false,
            Content = ghostBorder,
            Opacity = 0.85
        };

        new WindowInteropHelper(_dragGhost).EnsureHandle();
        if (UpdateDragGhostPosition(windowPoint))
            _dragGhost.Show();
        else
            HideDragGhost();
    }

    private bool UpdateDragGhostPosition(Point windowPoint)
    {
        if (_dragGhost == null) return false;
        Point screen = PointToScreen(windowPoint);
        IntPtr hwnd = new WindowInteropHelper(_dragGhost).Handle;
        // PointToScreen produces device pixels. Hand those directly to Win32 so
        // WPF does not interpret the desktop coordinates as device-independent units.
        return NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, (int)screen.X + 12, (int)screen.Y + 12,
            0, 0, NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
    }

    private void HideDragGhost()
    {
        if (_dragGhost == null) return;
        _dragGhost.Close();
        _dragGhost = null;
    }

    // Flash detection (shell hook)

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == (int)_shellHookMsgId)
        {
            int shellEvent = wParam.ToInt32();
            IntPtr targetHwnd = lParam;

            if (shellEvent == NativeMethods.HSHELL_WINDOWCREATED)
            {
                _taskbarWindowOrder.ObserveCreated(targetHwnd);
            }
            else if (shellEvent == NativeMethods.HSHELL_WINDOWDESTROYED)
            {
                OnWindowDestroyed(targetHwnd);
            }
            else if (shellEvent == NativeMethods.HSHELL_FLASH)
            {
                OnWindowFlash(targetHwnd);
            }
            else if (shellEvent == NativeMethods.HSHELL_REDRAW)
            {
                OnWindowRedraw(targetHwnd);
            }
            else if ((shellEvent & 0x7FFF) == NativeMethods.HSHELL_WINDOWACTIVATED)
            {
                OnWindowActivated(targetHwnd);
            }
        }
        return IntPtr.Zero;
    }

    private void OnWindowDestroyed(IntPtr hwnd)
    {
        int index = FindSlotIndexBySourceHwnd(hwnd);
        _taskbarWindowOrder.ObserveDestroyed(hwnd);
        _thumbnailRefreshPendingWindows.Remove(hwnd);
        _lastThumbnailRefreshTicks.Remove(hwnd);

        if (index < 0)
            return;

        _slots[index].ReleaseRegistration();
        Dispatcher.BeginInvoke(DispatcherPriority.Normal, ValidateSlots);
    }

    private void OnWindowFlash(IntPtr hwnd)
    {
        int index = FindSlotIndexBySourceHwnd(hwnd);
        if (index >= 0 && _flashingWindows.Add(hwnd))
            UpdateSlotAttentionVisual(index);
    }

    private void OnWindowActivated(IntPtr hwnd)
    {
        SetActiveSourceWindow(hwnd);
        if (FindSlotIndexBySourceHwnd(hwnd) < 0)
            return;

        ClearNotificationAttentionGroupsForWindow(hwnd);
        if (_flashingWindows.Remove(hwnd))
        {
            int slotIndex = FindSlotIndexBySourceHwnd(hwnd);
            if (slotIndex >= 0)
                UpdateSlotAttentionVisual(slotIndex);
        }

        RequestThumbnailRegistrationRefresh(hwnd);
    }

    private void OnWindowRedraw(IntPtr hwnd) => RequestThumbnailRegistrationRefresh(hwnd);

    private void CheckFlashState()
    {
        if (_flashingWindows.Count == 0)
            return;

        IntPtr fg = NativeMethods.GetForegroundWindow();
        if (!_flashingWindows.Remove(fg))
            return;

        int slotIndex = FindSlotIndexBySourceHwnd(fg);
        if (slotIndex >= 0)
            UpdateSlotAttentionVisual(slotIndex);
    }

    private static void StartFlashBorder(Border border, Color color)
    {
        var brush = new SolidColorBrush(color);
        var anim = new ColorAnimation
        {
            From = color,
            To = Color.FromArgb(0x40, color.R, color.G, color.B),
            Duration = TimeSpan.FromMilliseconds(400),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever
        };
        brush.BeginAnimation(SolidColorBrush.ColorProperty, anim);
        border.BorderBrush = brush;
        border.BorderThickness = AttentionBorderThickness;
    }

    private void StartFlashBorder(int idx, Color color) => StartFlashBorder(_cellBorders[idx], color);

    private void ShowActiveBorder(int idx)
    {
        _cellBorders[idx].BorderBrush = ActiveBorderBrush;
        _cellBorders[idx].BorderThickness = ActiveBorderThickness;
    }

    private void StopFlashBorder(int idx)
    {
        _cellBorders[idx].BorderBrush = NormalBorderBrush;
        _cellBorders[idx].BorderThickness = CellBorderThickness;
    }

    private void SyncActiveSourceWindow(bool forceRefresh = false) =>
        SetActiveSourceWindow(NativeMethods.GetForegroundWindow(), forceRefresh);

    private void SetActiveSourceWindow(IntPtr hwnd, bool forceRefresh = false)
    {
        IntPtr nextActiveSource = FindSlotIndexBySourceHwnd(hwnd) >= 0
            ? hwnd
            : IntPtr.Zero;

        if (!forceRefresh && nextActiveSource == _activeSourceHwnd)
            return;

        IntPtr previousActiveSource = _activeSourceHwnd;
        _activeSourceHwnd = nextActiveSource;

        if (previousActiveSource != IntPtr.Zero)
        {
            int previousIndex = FindSlotIndexBySourceHwnd(previousActiveSource);
            if (previousIndex >= 0)
                UpdateSlotAttentionVisual(previousIndex);
        }

        if (nextActiveSource != IntPtr.Zero && nextActiveSource != previousActiveSource)
        {
            int nextIndex = FindSlotIndexBySourceHwnd(nextActiveSource);
            if (nextIndex >= 0)
                UpdateSlotAttentionVisual(nextIndex);
        }
    }

    // Sidebar and fullscreen

    private void FullScreenButton_Click(object sender, RoutedEventArgs e) => ToggleFullScreen();

    private void CollapseLeftPanelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_windowChoice != null)
            EndWindowChoice();

        SetLeftPanelCollapsed(true);
    }

    private void ExpandLeftPanelButton_Click(object sender, RoutedEventArgs e) =>
        SetLeftPanelCollapsed(false);

    private void SetLeftPanelCollapsed(bool collapsed)
    {
        if (_isLeftPanelCollapsed == collapsed)
            return;

        if (collapsed)
            RememberExpandedSidebar();

        _isLeftPanelCollapsed = collapsed;
        ApplySidebarLayout();
        RequestStateSave();
    }

    private void RememberExpandedSidebar()
    {
        _savedLeftColWidth = LeftColumnDefinition.Width;
        _savedSplitterColWidth = RootGrid.ColumnDefinitions[1].Width;
    }

    private void ApplySidebarLayout()
    {
        bool expanded = !_isFullScreen && !_isLeftPanelCollapsed;
        bool showExpandButton = !_isFullScreen && _isLeftPanelCollapsed;
        LeftPanel.Visibility = Splitter.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        ExpandLeftPanelButton.Visibility = showExpandButton ? Visibility.Visible : Visibility.Collapsed;
        RootGrid.ColumnDefinitions[1].Width = expanded ? _savedSplitterColWidth : GridLength.Auto;
        // The collapsed column sizes itself to the expand button; fullscreen has no rail.
        LeftColumnDefinition.Width = expanded ? _savedLeftColWidth
            : showExpandButton ? GridLength.Auto : new GridLength(0);
    }

    private void ToggleFullScreen()
    {
        if (_isFullScreen)
        {
            WindowStyle = _savedWindowStyle;
            WindowState = _savedWindowState;
            _isFullScreen = false;
        }
        else
        {
            _savedWindowStyle = WindowStyle;
            _savedWindowState = WindowState;
            if (!_isLeftPanelCollapsed)
                RememberExpandedSidebar();

            if (WindowState == WindowState.Maximized)
                WindowState = WindowState.Normal;
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;
            _isFullScreen = true;
        }
        ApplySidebarLayout();
        RequestThumbnailUpdate();
        ScheduleThumbnailSettle();
        RequestStateSave();
    }
}
