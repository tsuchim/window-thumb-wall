namespace WindowThumbWall;

internal static class NotificationAttentionPolicy
{
    internal static bool ShouldSuppressAmbiguousNotificationDueToFlash(
        NotificationSignal signal,
        IReadOnlyList<NotificationWindowCandidate> candidates,
        IReadOnlyCollection<IntPtr> flashingWindows)
    {
        ArgumentNullException.ThrowIfNull(signal);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(flashingWindows);

        if (flashingWindows.Count == 0 || !NotificationWindowMatcher.HasAppIdentity(signal))
            return false;

        List<NotificationWindowCandidate> appCompatible =
            NotificationWindowMatcher.NarrowByAppIdentity(candidates, signal);
        if (appCompatible.Count == 0)
            return false;

        return appCompatible.Any(window => flashingWindows.Contains(window.Handle));
    }
}
