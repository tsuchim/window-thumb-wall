using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace WindowThumbWall;

internal enum AttentionVisualState
{
    None,
    Active,
    Red,
    Orange
}

internal sealed record NotificationAttentionGroup(
    uint NotificationId,
    AttentionVisualState VisualState,
    IReadOnlyList<IntPtr> CandidateHandles);

internal sealed record NotificationObservation(
    DateTimeOffset CreationTime,
    string Fingerprint,
    bool IsBaseline);

internal static class NotificationObservationPolicy
{
    internal static bool ShouldEvaluate(
        uint notificationId,
        NotificationObservation currentObservation,
        IReadOnlyDictionary<uint, NotificationObservation> knownObservations,
        IReadOnlyDictionary<uint, NotificationAttentionGroup> attentionGroups,
        IReadOnlySet<uint> dismissedNotificationIds)
    {
        if (dismissedNotificationIds.Contains(notificationId))
            return false;

        if (!knownObservations.TryGetValue(notificationId, out NotificationObservation? knownObservation))
            return true;

        bool changed =
            knownObservation.CreationTime != currentObservation.CreationTime ||
            !string.Equals(knownObservation.Fingerprint, currentObservation.Fingerprint, StringComparison.Ordinal);
        if (changed)
            return true;

        return !knownObservation.IsBaseline && !attentionGroups.ContainsKey(notificationId);
    }
}

internal readonly record struct NotificationSessionResult<T>(bool IsCurrent, T Value);

internal static class NotificationSessionAwait
{
    internal static async Task<NotificationSessionResult<T>> AwaitAsync<T>(
        object session,
        Task<T> operation,
        Func<object, bool> isCurrent)
    {
        T value = await operation.ConfigureAwait(true);
        return new NotificationSessionResult<T>(isCurrent(session), value);
    }
}

public partial class MainWindow
{
    private static readonly Color AttentionRed = Colors.Red;
    private static readonly Color AttentionOrange = Color.FromRgb(0xFF, 0x8C, 0x00);
    private const string NotificationDiagnosticsEnvironmentVariable = "WINDOWTHUMBWALL_NOTIFICATION_DIAGNOSTICS";
    private const int NotificationDiagnosticsMaxCharacters = 160;
    private const long NotificationDiagnosticsMaxBytes = 256 * 1024;
    private const int NotificationFollowUpSyncCount = 5;
    private static string NotificationDiagnosticsPath => Path.Combine(
        Path.GetDirectoryName(Path.GetFullPath(AppState.GetStatePath()))!,
        "logs",
        "notification-attention.log");

    private readonly Dictionary<uint, NotificationAttentionGroup> _notificationAttentionGroups = [];
    private readonly Dictionary<uint, NotificationObservation> _notificationObservations = [];
    private readonly HashSet<uint> _dismissedNotificationIds = [];
    private readonly HashSet<IntPtr> _notificationResolvedWindows = [];
    private readonly HashSet<IntPtr> _notificationAmbiguousWindows = [];
    private readonly DispatcherTimer _notificationFollowUpTimer = new() { Interval = TimeSpan.FromSeconds(1) };

    private NotificationSession? _notificationSession;

    private sealed class NotificationSession
    {
        internal UserNotificationListener? Listener { get; set; }
        internal TypedEventHandler<UserNotificationListener, UserNotificationChangedEventArgs>? Handler { get; set; }
        internal bool Initialized { get; set; }
        internal bool Subscribed { get; set; }
        internal bool SyncInProgress { get; set; }
        internal bool SyncQueued { get; set; }
        internal int FollowUpRemainingTicks { get; set; }
    }

    private bool SupportsNotificationAttentionRuntime() => NativeMethods.HasCurrentPackageIdentity();

    private void InitializeNotificationFollowUpTimer() =>
        _notificationFollowUpTimer.Tick += NotificationFollowUpTimer_Tick;

    private void SetNotificationAttentionEnabled(bool enabled)
    {
        _notificationAttentionRequested = enabled;

        if (enabled && !SupportsNotificationAttentionRuntime())
        {
            MessageBox.Show(
                this,
                LocalizedText.Get("setting.osNotifications.requiresPackage.message"),
                LocalizedText.Get("setting.osNotifications.requiresPackage.title"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            enabled = false;
        }

        bool runtimeEnabled = enabled && SupportsNotificationAttentionRuntime();
        if (_notificationAttentionEnabled == runtimeEnabled)
        {
            RequestStateSave();
            return;
        }

        _notificationAttentionEnabled = runtimeEnabled;
        if (runtimeEnabled)
        {
            NotificationSession session = EnsureNotificationSession();
            AppendNotificationDiagnostic("OS notification attention enabled.");
            if (IsLoaded)
                InitializeNotificationListenerAsync(session);
        }
        else
        {
            ResetNotificationAttentionState();
            AppendNotificationDiagnostic("OS notification attention disabled.");
        }

        RequestStateSave();
    }

    private void ResetNotificationAttentionState()
    {
        DisposeNotificationListener();
        _notificationObservations.Clear();
        _dismissedNotificationIds.Clear();
        _notificationAttentionGroups.Clear();
        _notificationResolvedWindows.Clear();
        _notificationAmbiguousWindows.Clear();
        UpdateAllSlotAttentionVisuals();
    }

    private NotificationSession EnsureNotificationSession() =>
        _notificationSession ??= new NotificationSession();

    private void InitializeNotificationListenerAsync() =>
        InitializeNotificationListenerAsync(EnsureNotificationSession());

    private bool IsCurrentNotificationSession(object session) =>
        ReferenceEquals(_notificationSession, session) && _notificationAttentionEnabled;

    private async void InitializeNotificationListenerAsync(NotificationSession session)
    {
        if (!IsCurrentNotificationSession(session))
            return;

        if (session.Initialized)
            return;

        session.Initialized = true;

        if (!ApiInformation.IsTypePresent("Windows.UI.Notifications.Management.UserNotificationListener"))
        {
            AppendNotificationDiagnostic("Notification listener type is not present on this system.");
            return;
        }

        if (!SupportsNotificationAttentionRuntime())
        {
            AppendNotificationDiagnostic("Notification listener requires packaged app identity for this run.");
            return;
        }

        try
        {
            UserNotificationListener listener = UserNotificationListener.Current;
            session.Listener = listener;
            UserNotificationListenerAccessStatus accessStatus = listener.GetAccessStatus();
            AppendNotificationDiagnostic($"Notification listener access status before request: {accessStatus}");
            if (accessStatus != UserNotificationListenerAccessStatus.Allowed)
            {
                NotificationSessionResult<UserNotificationListenerAccessStatus> accessResult =
                    await NotificationSessionAwait.AwaitAsync(
                        session,
                        listener.RequestAccessAsync().AsTask(),
                        IsCurrentNotificationSession);
                if (!accessResult.IsCurrent)
                    return;
                accessStatus = accessResult.Value;
            }

            if (!IsCurrentNotificationSession(session))
                return;

            if (accessStatus != UserNotificationListenerAccessStatus.Allowed)
            {
                AppendNotificationDiagnostic($"Notification listener access denied: {accessStatus}");
                return;
            }

            NotificationSessionResult<IReadOnlyList<UserNotification>> baselineResult =
                await NotificationSessionAwait.AwaitAsync(
                    session,
                    listener.GetNotificationsAsync(NotificationKinds.Toast).AsTask(),
                    IsCurrentNotificationSession);
            if (!baselineResult.IsCurrent || !IsCurrentNotificationSession(session))
                return;
            IReadOnlyList<UserNotification> currentNotifications = baselineResult.Value;
            _notificationObservations.Clear();
            _dismissedNotificationIds.Clear();
            foreach (UserNotification notification in currentNotifications)
            {
                NotificationSignal signal = BuildNotificationSignal(notification, out _);
                _notificationObservations[notification.Id] = CreateNotificationObservation(notification, signal, isBaseline: true);
            }

            _notificationAttentionGroups.Clear();
            if (currentNotifications.Count > 0)
            {
                List<NotificationWindowCandidate> candidates = CaptureMonitoredNotificationWindowCandidates();
                AppendNotificationSnapshot("initialize-baseline", currentNotifications, candidates);
                AppendNotificationDiagnostic(
                    $"Notification initialization recorded {currentNotifications.Count} existing toast notification(s) without creating attention groups.");
            }
            else
            {
                AppendNotificationDiagnostic("Notification initialization completed with zero current toast notifications.");
            }

            RebuildNotificationAttentionIndex();

            if (!IsCurrentNotificationSession(session))
                return;
            session.Handler = (_, _) => Dispatcher.BeginInvoke(() =>
            {
                if (!IsCurrentNotificationSession(session))
                    return;
                ScheduleNotificationFollowUpSync(session);
                QueueNotificationAttentionSync(session);
            });
            listener.NotificationChanged += session.Handler;
            session.Subscribed = true;
        }
        catch (Exception ex)
        {
            if (IsCurrentNotificationSession(session))
                AppendNotificationDiagnostic($"Notification listener initialization failed: {ex}");
        }
    }

    private void DisposeNotificationListener()
    {
        NotificationSession? session = _notificationSession;
        _notificationSession = null;
        _notificationFollowUpTimer.Stop();
        if (session == null)
            return;

        session.FollowUpRemainingTicks = 0;
        if (session.Subscribed && session.Listener != null && session.Handler != null)
        {
            session.Listener.NotificationChanged -= session.Handler;
            session.Subscribed = false;
            session.Handler = null;
        }
    }

    private void NotificationFollowUpTimer_Tick(object? sender, EventArgs e)
    {
        NotificationSession? session = _notificationSession;
        if (session == null || !IsCurrentNotificationSession(session) || session.Listener == null || session.FollowUpRemainingTicks <= 0)
        {
            _notificationFollowUpTimer.Stop();
            if (session != null)
                session.FollowUpRemainingTicks = 0;
            return;
        }

        session.FollowUpRemainingTicks--;
        QueueNotificationAttentionSync(session);

        if (session.FollowUpRemainingTicks <= 0)
            _notificationFollowUpTimer.Stop();
    }

    private void ScheduleNotificationFollowUpSync(NotificationSession session)
    {
        if (!IsCurrentNotificationSession(session))
            return;
        session.FollowUpRemainingTicks = NotificationFollowUpSyncCount;
        if (!_notificationFollowUpTimer.IsEnabled)
            _notificationFollowUpTimer.Start();
    }

    private async Task SyncNotificationAttentionAsync(NotificationSession session)
    {
        if (!IsCurrentNotificationSession(session) || session.Listener == null)
            return;
        UserNotificationListener listener = session.Listener;

        if (session.SyncInProgress)
        {
            session.SyncQueued = true;
            return;
        }

        session.SyncInProgress = true;
        try
        {
            do
            {
                if (!IsCurrentNotificationSession(session))
                    return;
                session.SyncQueued = false;

                NotificationSessionResult<IReadOnlyList<UserNotification>> syncResult =
                    await NotificationSessionAwait.AwaitAsync(
                        session,
                        listener.GetNotificationsAsync(NotificationKinds.Toast).AsTask(),
                        IsCurrentNotificationSession);
                if (!syncResult.IsCurrent || !IsCurrentNotificationSession(session))
                    return;
                IReadOnlyList<UserNotification> notifications = syncResult.Value;
                HashSet<uint> currentIds = notifications.Select(static notification => notification.Id).ToHashSet();
                uint[] removedIds = _notificationObservations.Keys
                    .Where(id => !currentIds.Contains(id))
                    .ToArray();
                foreach (uint removedId in removedIds)
                {
                    _notificationAttentionGroups.Remove(removedId);
                    _notificationObservations.Remove(removedId);
                    _dismissedNotificationIds.Remove(removedId);
                }

                List<(UserNotification Notification, NotificationSignal Signal)> changedNotifications = [];
                foreach (UserNotification notification in notifications)
                {
                    NotificationSignal signal = BuildNotificationSignal(notification, out _);
                    NotificationObservation currentObservation;
                    if (_notificationObservations.TryGetValue(notification.Id, out NotificationObservation? knownObservation))
                    {
                        currentObservation = CreateNotificationObservation(
                            notification,
                            signal,
                            isBaseline: knownObservation.IsBaseline);

                        if (knownObservation.CreationTime != currentObservation.CreationTime ||
                            !string.Equals(knownObservation.Fingerprint, currentObservation.Fingerprint, StringComparison.Ordinal))
                        {
                            _dismissedNotificationIds.Remove(notification.Id);
                        }
                    }
                    else
                    {
                        currentObservation = CreateNotificationObservation(notification, signal, isBaseline: false);
                        _dismissedNotificationIds.Remove(notification.Id);
                    }

                    if (ShouldEvaluateNotification(notification.Id, currentObservation))
                    {
                        changedNotifications.Add((notification, signal));
                        currentObservation = currentObservation with { IsBaseline = false };
                    }

                    _notificationObservations[notification.Id] = currentObservation;
                }

                if (changedNotifications.Count > 0)
                {
                    List<NotificationWindowCandidate> candidates = CaptureMonitoredNotificationWindowCandidates();
                    AppendNotificationSnapshot(
                        "sync-delta",
                        changedNotifications.Select(static entry => entry.Notification).ToArray(),
                        candidates);
                    foreach ((UserNotification notification, NotificationSignal signal) in changedNotifications)
                    {
                        _notificationAttentionGroups.Remove(notification.Id);
                        AddNotificationAttentionGroup(notification, signal, candidates);
                    }
                }

                if (notifications.Count == 0)
                {
                    AppendNotificationDiagnostic("Notification sync completed with zero current toast notifications.");
                }
                else if (changedNotifications.Count == 0)
                {
                    AppendNotificationDiagnostic(
                        $"Notification sync observed no new or updated toast notifications. current={notifications.Count} removed={removedIds.Length}");
                }

                if (removedIds.Length > 0)
                {
                    AppendNotificationDiagnostic(
                        $"Notification sync removed attention groups for notification ids [{string.Join(", ", removedIds)}].");
                }

                RebuildNotificationAttentionIndex();
            }
            while (IsCurrentNotificationSession(session) && session.SyncQueued);
        }
        catch (Exception ex)
        {
            if (IsCurrentNotificationSession(session))
                AppendNotificationDiagnostic($"Notification sync failed: {ex}");
        }
        finally
        {
            if (IsCurrentNotificationSession(session))
                session.SyncInProgress = false;
        }
    }

    private List<NotificationWindowCandidate> CaptureMonitoredNotificationWindowCandidates()
    {
        List<NotificationWindowCandidate> candidates = [];
        foreach (ThumbnailSlot slot in _slots.Where(static slot => slot.IsOccupied))
        {
            candidates.Add(new NotificationWindowCandidate(
                Handle: slot.SourceHwnd,
                Title: NativeMethods.GetWindowTitle(slot.SourceHwnd),
                ProcessName: NativeMethods.GetProcessName(slot.SourceHwnd),
                ExecutablePath: NativeMethods.GetProcessImagePath(slot.SourceHwnd),
                AppUserModelId: NativeMethods.GetWindowAppUserModelId(slot.SourceHwnd)));
        }

        return candidates;
    }

    private void AddNotificationAttentionGroup(
        UserNotification notification,
        NotificationSignal signal,
        IReadOnlyList<NotificationWindowCandidate> candidates)
    {
        NotificationMatchResult result = NotificationWindowMatcher.Resolve(signal, candidates);
        if (result.Kind == NotificationMatchKind.None || result.CandidateHandles.Count == 0)
            return;

        if (result.Kind == NotificationMatchKind.Ambiguous)
        {
            if (NotificationAttentionPolicy.ShouldSuppressAmbiguousNotificationDueToFlash(
                    signal,
                    candidates,
                    _flashingWindows))
            {
                AppendNotificationDiagnostic(
                    $"Notification id={notification.Id} ambiguous attention suppressed because the same app already has an active flash signal.");
                return;
            }
        }

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

    private static string CreateNotificationFingerprint(NotificationSignal signal)
    {
        ArgumentNullException.ThrowIfNull(signal);
        return string.Join(
            "\u001F",
            signal.AppUserModelId,
            signal.AppDisplayName,
            string.Join("\u001E", signal.NotificationTexts));
    }

    private static NotificationObservation CreateNotificationObservation(
        UserNotification notification,
        NotificationSignal signal,
        bool isBaseline) =>
        new(
            notification.CreationTime,
            CreateNotificationFingerprint(signal),
            isBaseline);

    private bool ShouldEvaluateNotification(uint notificationId, NotificationObservation currentObservation)
        => NotificationObservationPolicy.ShouldEvaluate(
            notificationId,
            currentObservation,
            _notificationObservations,
            _notificationAttentionGroups,
            _dismissedNotificationIds);

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
        NotificationSession? session = _notificationSession;
        if (session == null)
            return;

        QueueNotificationAttentionSync(session);
    }

    private void QueueNotificationAttentionSync(NotificationSession session)
    {
        if (!IsCurrentNotificationSession(session) || !session.Subscribed)
            return;

        Dispatcher.BeginInvoke(async () =>
        {
            if (IsCurrentNotificationSession(session))
                await SyncNotificationAttentionAsync(session);
        });
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
        {
            _notificationAttentionGroups.Remove(groupId);
            _dismissedNotificationIds.Add(groupId);
        }

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
            case AttentionVisualState.Active:
                ShowActiveBorder(idx);
                break;
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

        if (_notificationAmbiguousWindows.Contains(hwnd))
            return AttentionVisualState.Orange;

        return hwnd == _activeSourceHwnd
            ? AttentionVisualState.Active
            : AttentionVisualState.None;
    }
}
