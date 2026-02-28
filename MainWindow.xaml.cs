using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace WindowThumbWall;

public partial class MainWindow : Window
{
    private const int SlotCount = 4;

    private IntPtr _mainHwnd;
    private readonly Border[] _cellBorders = new Border[SlotCount];
    private readonly TextBlock[] _cellLabels = new TextBlock[SlotCount];
    private readonly ThumbHost[] _cellHosts = new ThumbHost[SlotCount];
    private readonly ThumbnailSlot?[] _slots = new ThumbnailSlot[SlotCount];

    private int _nextSlot;
    private int _maximizedIndex = -1;

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
        BuildCells();

        // ThumbHosts need one layout pass before their HWNDs are ready.
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            for (int i = 0; i < SlotCount; i++)
                _slots[i] = new ThumbnailSlot(_cellHosts[i], _mainHwnd);
            _timer.Start();
        });
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _timer.Stop();
        for (int i = 0; i < SlotCount; i++)
            _slots[i]?.Clear();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) =>
        Dispatcher.BeginInvoke(DispatcherPriority.Render, UpdateAllThumbnails);

    // ── Build 2×2 cells ──────────────────────────────────────────

    private void BuildCells()
    {
        for (int i = 0; i < SlotCount; i++)
        {
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
                Tag = i,
                Cursor = Cursors.Hand
            };
            border.MouseLeftButtonDown += Cell_LeftClick;
            border.MouseRightButtonDown += Cell_RightClick;

            Grid.SetRow(border, i / 2);
            Grid.SetColumn(border, i % 2);
            ThumbGrid.Children.Add(border);

            _cellBorders[i] = border;
            _cellLabels[i] = label;
            _cellHosts[i] = host;
        }
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
        for (int i = 0; i < SlotCount; i++)
            if (_slots[i] is { IsOccupied: true } s && s.SourceHwnd == info.Handle) return;

        // First free slot.
        int target = -1;
        for (int i = 0; i < SlotCount; i++)
        {
            if (_slots[i] is not null && !_slots[i]!.IsOccupied)
            {
                target = i;
                break;
            }
        }

        // Round-robin overwrite when full.
        if (target == -1)
            target = _nextSlot % SlotCount;

        if (_slots[target]?.Assign(info.Handle, info.Title) == true)
        {
            _cellLabels[target].Text = info.Title;
            _nextSlot = (target + 1) % SlotCount;
        }
    }

    // ── Slot validation ──────────────────────────────────────────

    private void ValidateSlots()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            if (_slots[i] is not null && !_slots[i]!.CheckValid())
                _cellLabels[i].Text = "(empty)";
        }
    }

    private void UpdateAllThumbnails()
    {
        for (int i = 0; i < SlotCount; i++)
            _slots[i]?.UpdateThumbnail();
    }

    // ── Maximize / Restore ───────────────────────────────────────

    private void Cell_LeftClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { Tag: int idx }) return;

        if (_maximizedIndex == idx)
        {
            RestoreGrid();
        }
        else if (_maximizedIndex >= 0)
        {
            RestoreGrid();
        }
        else if (_slots[idx] is { IsOccupied: true })
        {
            MaximizeCell(idx);
        }
    }

    private void MaximizeCell(int idx)
    {
        _maximizedIndex = idx;
        for (int i = 0; i < SlotCount; i++)
        {
            if (i == idx)
            {
                Grid.SetRow(_cellBorders[i], 0);
                Grid.SetColumn(_cellBorders[i], 0);
                Grid.SetRowSpan(_cellBorders[i], 2);
                Grid.SetColumnSpan(_cellBorders[i], 2);
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
        for (int i = 0; i < SlotCount; i++)
        {
            Grid.SetRow(_cellBorders[i], i / 2);
            Grid.SetColumn(_cellBorders[i], i % 2);
            Grid.SetRowSpan(_cellBorders[i], 1);
            Grid.SetColumnSpan(_cellBorders[i], 1);
            _cellBorders[i].Visibility = Visibility.Visible;
        }
        Dispatcher.BeginInvoke(DispatcherPriority.Render, UpdateAllThumbnails);
    }

    private void Cell_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { Tag: int idx }) return;
        if (_slots[idx] is { IsOccupied: true })
        {
            _slots[idx]!.Clear();
            _cellLabels[idx].Text = "(empty)";
        }
    }
}