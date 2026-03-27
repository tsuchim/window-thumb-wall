using System.IO;
using System.Text;
using System.Windows.Controls;
using System.Windows.Media;
using Windows.Foundation.Metadata;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace WindowThumbWall;

internal enum AttentionVisualState
{
    None,
    Red,
    Orange
}

internal sealed record NotificationAttentionGroup(
    uint NotificationId,
    AttentionVisualState VisualState,
    IReadOnlyList<IntPtr> CandidateHandles);

public partial class MainWindow
{
    private static readonly Color AttentionRed = Colors.Red;
    private static readonly Color AttentionOrange = Color.FromRgb(0xFF, 0x8C, 0x00);
    private const string NotificationDiagnosticsEnvironmentVariable = "WINDOWTHUMBWALL_NOTIFICATION_DIAGNOSTICS";
    private const int NotificationDiagnosticsMaxCharacters = 160;
    private const long NotificationDiagnosticsMaxBytes = 256 * 1024;
    private static readonly string NotificationDiagnosticsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WindowThumbWall",
        "logs",
        "notification-attention.log");

    private readonly Dictionary<uint, NotificationAttentionGroup> _notificationAttentionGroups = [];
    private readonly HashSet<uint> _knownNotificationIds = [];
    private readonly HashSet<IntPtr> _notificationResolvedWindows = [];
    private readonly HashSet<IntPtr> _notificationAmbiguousWindows = [];

    private UserNotificationListener? _notificationListener;
    private bool _notificationListenerInitialized;
    private bool _notificationSyncInProgress;
    private bool _notificationSyncQueued;

    private void SetNotificationAttentionEnabled(bool enabled)
    {
        if (_notificationAttentionEnabled == enabled)
            return;

        _notificationAttentionEnabled = enabled;
        if (enabled)
        {
            AppendNotificationDiagnostic("OS notification attention enabled.");
            if (IsLoaded)
                InitializeNotificationListenerAsync();
        }
        else
        {
            AppendNotificationDiagnostic("OS notification attention disabled.");
            ResetNotificationAttentionState();
        }

        RequestStateSave();
    }

    private void ResetNotificationAttentionState()
    {
        DisposeNotificationListener();
        _notificationListener = null;
        _notificationListenerInitialized = false;
        _notificationSyncQueued = false;
        _notificationSyncInProgress = false;
        _knownNotificationIds.Clear();
        _notificationAttentionGroups.Clear();
        _notificationResolvedWindows.Clear();
        _notificationAmbiguousWindows.Clear();
        UpdateAllSlotAttentionVisuals();
    }

    private async void InitializeNotificationListenerAsync()
    {
        if (!_notificationAttentionEnabled)
            return;

        if (_notificationListenerInitialized)
            return;

        _notificationListenerInitialized = true;

        if (!ApiInformation.IsTypePresent("Windows.UI.Notifications.Management.UserNotificationListener"))
        {
            AppendNotificationDiagnostic("Notification listener type is not present on this system.");
            return;
        }

        try
        {
            _notificationListener = UserNotificationListener.Current;
            UserNotificationListenerAccessStatus accessStatus = _notificationListener.GetAccessStatus();
            AppendNotificationDiagnostic($"Notification listener access status before request: {accessStatus}");
            if (accessStatus != UserNotificationListenerAccessStatus.Allowed)
                accessStatus = await _notificationListener.RequestAccessAsync();

            if (accessStatus != UserNotificationListenerAccessStatus.Allowed)
            {
                AppendNotificationDiagnostic($"Notification listener access denied: {accessStatus}");
                return;
            }

            IReadOnlyList<UserNotification> currentNotifications =
                await _notificationListener.GetNotificationsAsync(NotificationKinds.Toast);
            _knownNotificationIds.Clear();
            foreach (UserNotification notification in currentNotifications)
                _knownNotificationIds.Add(notification.Id);

            _notificationAttentionGroups.Clear();
            if (currentNotifications.Count > 0)
            {
                List<NotificationWindowCandidate> candidates = CaptureNotificationWindowCandidates();
                AppendNotificationSnapshot("initialize", currentNotifications, candidates);
                foreach (UserNotification notification in currentNotifications)
                    AddNotificationAttentionGroup(notification, candidates);
            }
            else
            {
                AppendNotificationDiagnostic("Notification initialization completed with zero current toast notifications.");
            }

            RebuildNotificationAttentionIndex();

            _notificationListener.NotificationChanged += NotificationListener_NotificationChanged;
        }
        catch (Exception ex)
        {
            AppendNotificationDiagnostic($"Notification listener initialization failed: {ex}");
            _notificationListener = null;
        }
    }

    private void DisposeNotificationListener()
    {
        if (_notificationListener != null)
            _notificationListener.NotificationChanged -= NotificationListener_NotificationChanged;
    }

    private void NotificationListener_NotificationChanged(
        UserNotificationListener sender,
        UserNotificationChangedEventArgs args)
    {
        if (!_notificationAttentionEnabled)
            return;

        Dispatcher.BeginInvoke(async () => await SyncNotificationAttentionAsync());
    }

    private async Task SyncNotificationAttentionAsync()
    {
        if (!_notificationAttentionEnabled)
            return;

        if (_notificationListener == null)
            return;

        if (_notificationSyncInProgress)
        {
            _notificationSyncQueued = true;
            return;
        }

        _notificationSyncInProgress = true;
        try
        {
            do
            {
                _notificationSyncQueued = false;

                IReadOnlyList<UserNotification> notifications =
                    await _notificationListener.GetNotificationsAsync(NotificationKinds.Toast);
                HashSet<uint> currentIds = notifications.Select(static notification => notification.Id).ToHashSet();

                _notificationAttentionGroups.Clear();
                if (notifications.Count > 0)
                {
                    List<NotificationWindowCandidate> candidates = CaptureNotificationWindowCandidates();
                    AppendNotificationSnapshot("sync", notifications, candidates);
                    foreach (UserNotification notification in notifications)
                        AddNotificationAttentionGroup(notification, candidates);
                }
                else
                {
                    AppendNotificationDiagnostic("Notification sync completed with zero current toast notifications.");
                }

                _knownNotificationIds.Clear();
                foreach (uint currentId in currentIds)
                    _knownNotificationIds.Add(currentId);

                RebuildNotificationAttentionIndex();
            }
            while (_notificationSyncQueued);
        }
        catch (Exception ex)
        {
            AppendNotificationDiagnostic($"Notification sync failed: {ex}");
        }
        finally
        {
            _notificationSyncInProgress = false;
        }
    }

    private List<NotificationWindowCandidate> CaptureNotificationWindowCandidates()
    {
        List<NotificationWindowCandidate> candidates = [];
        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (hWnd == _mainHwnd)
                return true;
            if (!NativeMethods.IsAltTabWindow(hWnd))
                return true;

            candidates.Add(new NotificationWindowCandidate(
                Handle: hWnd,
                Title: NativeMethods.GetWindowTitle(hWnd),
                ProcessName: NativeMethods.GetProcessName(hWnd),
                ExecutablePath: NativeMethods.GetProcessImagePath(hWnd),
                AppUserModelId: NativeMethods.GetWindowAppUserModelId(hWnd)));
            return true;
        }, IntPtr.Zero);

        return candidates;
    }

    private void AddNotificationAttentionGroup(
        UserNotification notification,
        IReadOnlyList<NotificationWindowCandidate> candidates)
    {
        NotificationSignal signal = BuildNotificationSignal(notification, out _);
        NotificationMatchResult result = NotificationWindowMatcher.Resolve(signal, candidates);
        if (result.Kind == NotificationMatchKind.None || result.CandidateHandles.Count == 0)
            return;

        AttentionVisualState state = result.Kind == NotificationMatchKind.Unique
            ? AttentionVisualState.Red
            : AttentionVisualState.Orange;

        _notificationAttentionGroups[notification.Id] = new NotificationAttentionGroup(
            notification.Id,
            state,
            result.CandidateHandles);
    }

    private static NotificationSignal BuildNotificationSignal(
        UserNotification notification,
        out string appInfoStatus)
    {
        List<string> texts = [];
        string appUserModelId = string.Empty;
        string appDisplayName = string.Empty;
        appInfoStatus = "available";

        try
        {
            NotificationBinding? binding =
                notification.Notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric);
            if (binding != null)
            {
                foreach (var element in binding.GetTextElements())
                {
                    if (!string.IsNullOrWhiteSpace(element.Text))
                        texts.Add(element.Text);
                }
            }
        }
        catch
        {
            // Keep processing with whatever metadata is available.
        }

        try
        {
            var appInfo = notification.AppInfo;
            if (appInfo != null)
            {
                appUserModelId = appInfo.AppUserModelId ?? string.Empty;
                appDisplayName = appInfo.DisplayInfo?.DisplayName ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            appInfoStatus = $"unavailable:{ex.GetType().Name}";
        }

        return new NotificationSignal(
            AppUserModelId: appUserModelId,
            AppDisplayName: appDisplayName,
            NotificationTexts: texts);
    }

    private void AppendNotificationSnapshot(
        string phase,
        IReadOnlyList<UserNotification> notifications,
        IReadOnlyList<NotificationWindowCandidate> candidates)
    {
        if (!ShouldWriteNotificationDiagnostics())
            return;

        StringBuilder builder = new();
        int monitoredCount = _slots.Count(static slot => slot.IsOccupied);
        builder.AppendLine(
            $"Notification snapshot phase={phase} notifications={notifications.Count} candidates={candidates.Count} monitored={monitoredCount}");

        if (monitoredCount > 0)
        {
            builder.AppendLine("Monitored slots:");
            foreach (ThumbnailSlot slot in _slots.Where(static slot => slot.IsOccupied))
            {
                builder.AppendLine(
                    $"  slot hwnd={FormatHandle(slot.SourceHwnd)} title=\"{SanitizeNotificationDiagnosticValue(slot.SourceTitle)}\" process=\"{SanitizeNotificationDiagnosticValue(slot.SourceProcessName)}\"");
            }
        }

        builder.AppendLine("Candidate windows:");
        foreach (NotificationWindowCandidate candidate in candidates)
        {
            bool monitored = _slots.Any(slot => slot.IsOccupied && slot.SourceHwnd == candidate.Handle);
            builder.AppendLine(
                $"  hwnd={FormatHandle(candidate.Handle)} monitored={monitored} title=\"{SanitizeNotificationDiagnosticValue(candidate.Title)}\" process=\"{SanitizeNotificationDiagnosticValue(candidate.ProcessName)}\" exe=\"{SanitizeNotificationDiagnosticValue(Path.GetFileName(candidate.ExecutablePath))}\" aumid=\"{SanitizeNotificationDiagnosticValue(candidate.AppUserModelId)}\"");
        }

        foreach (UserNotification notification in notifications)
        {
            NotificationSignal signal = BuildNotificationSignal(notification, out string appInfoStatus);
            NotificationMatchResult result = NotificationWindowMatcher.Resolve(signal, candidates);

            builder.AppendLine(
                $"Notification id={notification.Id} appInfo={appInfoStatus} app=\"{SanitizeNotificationDiagnosticValue(signal.AppDisplayName)}\" aumid=\"{SanitizeNotificationDiagnosticValue(signal.AppUserModelId)}\" texts=\"{string.Join(" | ", signal.NotificationTexts.Select(SanitizeNotificationDiagnosticValue))}\" result={result.Kind} handles=[{string.Join(", ", result.CandidateHandles.Select(FormatHandle))}]");

            foreach (IntPtr handle in result.CandidateHandles)
            {
                NotificationWindowCandidate? candidate = candidates.FirstOrDefault(window => window.Handle == handle);
                if (candidate != null)
                {
                    bool monitored = _slots.Any(slot => slot.IsOccupied && slot.SourceHwnd == handle);
                    builder.AppendLine(
                        $"    matched hwnd={FormatHandle(candidate.Handle)} monitored={monitored} title=\"{SanitizeNotificationDiagnosticValue(candidate.Title)}\" process=\"{SanitizeNotificationDiagnosticValue(candidate.ProcessName)}\" exe=\"{SanitizeNotificationDiagnosticValue(Path.GetFileName(candidate.ExecutablePath))}\" aumid=\"{SanitizeNotificationDiagnosticValue(candidate.AppUserModelId)}\"");
                }
            }
        }

        AppendNotificationDiagnostic(builder.ToString().TrimEnd());
    }

    private static string FormatHandle(IntPtr handle) => $"0x{handle.ToInt64():X}";

    private static bool ShouldWriteNotificationDiagnostics()
    {
        string? value = Environment.GetEnvironmentVariable(NotificationDiagnosticsEnvironmentVariable);
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizeNotificationDiagnosticValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string collapsed = string.Join(" ", value.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries));
        if (collapsed.Length <= NotificationDiagnosticsMaxCharacters)
            return collapsed;

        return collapsed[..(NotificationDiagnosticsMaxCharacters - 3)] + "...";
    }

    private static void RotateNotificationDiagnosticsIfNeeded()
    {
        if (!File.Exists(NotificationDiagnosticsPath))
            return;

        FileInfo logFile = new(NotificationDiagnosticsPath);
        if (logFile.Length < NotificationDiagnosticsMaxBytes)
            return;

        string archivedPath = NotificationDiagnosticsPath + ".1";
        if (File.Exists(archivedPath))
            File.Delete(archivedPath);

        File.Move(NotificationDiagnosticsPath, archivedPath);
    }

    private static void AppendNotificationDiagnostic(string message)
    {
        if (!ShouldWriteNotificationDiagnostics())
            return;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(NotificationDiagnosticsPath)!);
            RotateNotificationDiagnosticsIfNeeded();
            string line = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}] {message}{Environment.NewLine}";
            File.AppendAllText(NotificationDiagnosticsPath, line, Encoding.UTF8);
        }
        catch
        {
            // Diagnostics must never break attention handling.
        }
    }

    private void QueueNotificationAttentionSync()
    {
        if (!_notificationAttentionEnabled)
            return;

        if (_notificationListener == null)
            return;

        Dispatcher.BeginInvoke(async () => await SyncNotificationAttentionAsync());
    }

    private void ClearNotificationAttentionGroupsForWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || _notificationAttentionGroups.Count == 0)
            return;

        uint[] groupsToClear = _notificationAttentionGroups
            .Where(pair => pair.Value.CandidateHandles.Contains(hwnd))
            .Select(static pair => pair.Key)
            .ToArray();

        if (groupsToClear.Length == 0)
            return;

        foreach (uint groupId in groupsToClear)
            _notificationAttentionGroups.Remove(groupId);

        RebuildNotificationAttentionIndex();
    }

    private void RebuildNotificationAttentionIndex()
    {
        _notificationResolvedWindows.Clear();
        _notificationAmbiguousWindows.Clear();

        foreach (NotificationAttentionGroup group in _notificationAttentionGroups.Values)
        {
            foreach (IntPtr hwnd in group.CandidateHandles)
            {
                if (group.VisualState == AttentionVisualState.Red)
                {
                    _notificationResolvedWindows.Add(hwnd);
                    _notificationAmbiguousWindows.Remove(hwnd);
                }
                else if (!_notificationResolvedWindows.Contains(hwnd))
                {
                    _notificationAmbiguousWindows.Add(hwnd);
                }
            }
        }

        UpdateAllSlotAttentionVisuals();
    }

    private void UpdateAllSlotAttentionVisuals()
    {
        for (int i = 0; i < _slots.Count; i++)
            UpdateSlotAttentionVisual(i);
    }

    private void UpdateSlotAttentionVisual(int idx)
    {
        if (idx < 0 || idx >= _slots.Count || idx >= _slotAttentionVisualStates.Count)
            return;

        AttentionVisualState desiredState = GetDesiredSlotAttentionState(idx);
        if (_slotAttentionVisualStates[idx] == desiredState)
            return;

        _slotAttentionVisualStates[idx] = desiredState;
        switch (desiredState)
        {
            case AttentionVisualState.Red:
                StartFlashBorder(idx, AttentionRed);
                break;
            case AttentionVisualState.Orange:
                StartFlashBorder(idx, AttentionOrange);
                break;
            default:
                StopFlashBorder(idx);
                break;
        }
    }

    private AttentionVisualState GetDesiredSlotAttentionState(int idx)
    {
        if (idx < 0 || idx >= _slots.Count || !_slots[idx].IsOccupied)
            return AttentionVisualState.None;

        IntPtr hwnd = _slots[idx].SourceHwnd;
        if (_flashingWindows.Contains(hwnd) || _notificationResolvedWindows.Contains(hwnd))
            return AttentionVisualState.Red;

        return _notificationAmbiguousWindows.Contains(hwnd)
            ? AttentionVisualState.Orange
            : AttentionVisualState.None;
    }
}
