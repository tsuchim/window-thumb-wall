namespace WindowThumbWall.Tests;

public sealed class AutoAddWindowPlannerTests
{
    [Fact]
    public void GetPendingWindows_PreservesTaskbarOrderWithinSameApp()
    {
        AutoAddAppEntry[] apps = [App("Code")];
        WindowInfo[] windowsInTaskbarOrder =
        [
            Window(11, "Code", "first"),
            Window(22, "Code", "second"),
            Window(33, "Code", "third")
        ];

        IReadOnlyList<WindowInfo> result = AutoAddWindowPlanner.GetPendingWindows(
            apps,
            windowsInTaskbarOrder,
            new HashSet<IntPtr>());

        Assert.Equal([(IntPtr)11, (IntPtr)22, (IntPtr)33], result.Select(window => window.Handle));
    }

    [Fact]
    public void GetPendingWindows_LeavesAlreadyMonitoredWindowsOutOfAutomaticChanges()
    {
        AutoAddAppEntry[] apps = [App("Code")];
        WindowInfo[] windowsInTaskbarOrder =
        [
            Window(11, "Code", "first"),
            Window(22, "Code", "second"),
            Window(33, "Code", "third")
        ];

        IReadOnlyList<WindowInfo> result = AutoAddWindowPlanner.GetPendingWindows(
            apps,
            windowsInTaskbarOrder,
            new HashSet<IntPtr> { (IntPtr)11, (IntPtr)22 });

        WindowInfo pending = Assert.Single(result);
        Assert.Equal((IntPtr)33, pending.Handle);
    }

    [Fact]
    public void GetPendingWindows_UsesRegisteredAppOrderBetweenApps()
    {
        AutoAddAppEntry[] apps = [App("Code"), App("Notepad")];
        WindowInfo[] windowsInTaskbarOrder =
        [
            Window(101, "Notepad", "note"),
            Window(11, "Code", "first"),
            Window(22, "Code", "second")
        ];

        IReadOnlyList<WindowInfo> result = AutoAddWindowPlanner.GetPendingWindows(
            apps,
            windowsInTaskbarOrder,
            new HashSet<IntPtr>());

        Assert.Equal([(IntPtr)11, (IntPtr)22, (IntPtr)101], result.Select(window => window.Handle));
    }

    [Fact]
    public void GetPendingWindows_IsIdempotentWhenNoWindowBecomesMonitored()
    {
        AutoAddAppEntry[] apps = [App("Code")];
        WindowInfo[] windows = [Window(11, "Code", "first"), Window(22, "Code", "second")];

        IReadOnlyList<WindowInfo> first = AutoAddWindowPlanner.GetPendingWindows(apps, windows, new HashSet<IntPtr>());
        IReadOnlyList<WindowInfo> second = AutoAddWindowPlanner.GetPendingWindows(apps, windows, new HashSet<IntPtr>());

        Assert.Equal([(IntPtr)11, (IntPtr)22], first.Select(window => window.Handle));
        Assert.Equal([(IntPtr)11, (IntPtr)22], second.Select(window => window.Handle));
    }

    [Fact]
    public void GetPendingWindows_MatchesProcessNamesWithoutCaseSensitivity()
    {
        IReadOnlyList<WindowInfo> result = AutoAddWindowPlanner.GetPendingWindows(
            [App("CODE")],
            [Window(11, "code", "first")],
            new HashSet<IntPtr>());

        Assert.Equal([(IntPtr)11], result.Select(window => window.Handle));
    }

    [Fact]
    public void GetPendingWindows_ExcludesInvalidAndDuplicateCandidates()
    {
        IReadOnlyList<WindowInfo> result = AutoAddWindowPlanner.GetPendingWindows(
            [App("Code")],
            [Window(0, "Code", "invalid"), Window(11, "Code", "first"), Window(11, "Code", "duplicate"), Window(22, "", "missing")],
            new HashSet<IntPtr>());

        Assert.Equal([(IntPtr)11], result.Select(window => window.Handle));
    }

    [Fact]
    public void GetPendingWindows_ReturnsEmptyForEmptyCandidates()
    {
        IReadOnlyList<WindowInfo> result = AutoAddWindowPlanner.GetPendingWindows(
            [App("Code")],
            [],
            new HashSet<IntPtr>());

        Assert.Empty(result);
    }

    [Fact]
    public void GetPendingWindows_PreservesExistingMonitoredSlotOrderByNotPlanningThoseHandles()
    {
        IReadOnlyList<WindowInfo> result = AutoAddWindowPlanner.GetPendingWindows(
            [App("Code")],
            [Window(11, "Code", "first"), Window(22, "Code", "second"), Window(33, "Code", "third")],
            new HashSet<IntPtr> { (IntPtr)22, (IntPtr)11 });

        Assert.Equal([(IntPtr)33], result.Select(window => window.Handle));
    }

    [Fact]
    public void AdvanceInsertIndexes_KeepsLaterRegisteredAppsAfterEarlierInsertedApp()
    {
        AutoAddAppEntry[] apps = [App("Code"), App("Notepad")];
        var indexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Code"] = 0,
            ["Notepad"] = 0
        };

        AutoAddWindowPlanner.AdvanceInsertIndexes(apps, indexes, "Code", 0, 1);

        Assert.Equal(1, indexes["Code"]);
        Assert.Equal(1, indexes["Notepad"]);
    }

    private static AutoAddAppEntry App(string processName) => new()
    {
        ProcessName = processName,
        DisplayName = processName
    };

    private static WindowInfo Window(long handle, string processName, string title) => new()
    {
        Handle = (IntPtr)handle,
        ProcessName = processName,
        Title = title
    };
}
