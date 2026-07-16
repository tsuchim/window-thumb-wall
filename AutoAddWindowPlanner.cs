namespace WindowThumbWall;

internal static class AutoAddWindowPlanner
{
    internal static IReadOnlyList<WindowInfo> GetPendingWindows(
        IEnumerable<AutoAddAppEntry> registeredApps,
        IEnumerable<WindowInfo> windowsInTaskbarOrder,
        IReadOnlySet<IntPtr> monitoredHandles)
    {
        List<WindowInfo> orderedWindows = windowsInTaskbarOrder.ToList();
        var pending = new List<WindowInfo>();
        var plannedHandles = new HashSet<IntPtr>();

        foreach (AutoAddAppEntry app in registeredApps)
        {
            foreach (WindowInfo window in orderedWindows)
            {
                if (window.Handle == IntPtr.Zero ||
                    string.IsNullOrWhiteSpace(window.ProcessName) ||
                    monitoredHandles.Contains(window.Handle) ||
                    !window.ProcessName.Equals(app.ProcessName, StringComparison.OrdinalIgnoreCase) ||
                    !plannedHandles.Add(window.Handle))
                    continue;

                pending.Add(window);
            }
        }

        return pending;
    }

    internal static void AdvanceInsertIndexes(
        IReadOnlyList<AutoAddAppEntry> registeredApps,
        IDictionary<string, int> nextInsertIndexByProcess,
        string insertedProcessName,
        int insertedAt,
        int slotCount)
    {
        nextInsertIndexByProcess[insertedProcessName] = Math.Min(insertedAt + 1, slotCount);

        int insertedAppIndex = registeredApps
            .Select((app, index) => new { app, index })
            .FirstOrDefault(item => item.app.ProcessName.Equals(insertedProcessName, StringComparison.OrdinalIgnoreCase))
            ?.index ?? -1;

        for (int i = insertedAppIndex + 1; i < registeredApps.Count; i++)
        {
            string processName = registeredApps[i].ProcessName;
            if (nextInsertIndexByProcess.TryGetValue(processName, out int nextIndex) && nextIndex >= insertedAt)
                nextInsertIndexByProcess[processName] = nextIndex + 1;
        }
    }
}
