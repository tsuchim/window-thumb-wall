namespace WindowThumbWall;

internal static class ManualSlotPlanner
{
    // Returns an existing empty slot, the append position, or -1 for an invalid
    // or already monitored source window.
    internal static int GetAddTarget(IReadOnlyList<IntPtr> sourceHandles, IntPtr sourceHandle)
    {
        if (sourceHandle == IntPtr.Zero || sourceHandles.Contains(sourceHandle))
            return -1;

        for (int i = 0; i < sourceHandles.Count; i++)
        {
            if (sourceHandles[i] == IntPtr.Zero)
                return i;
        }

        return sourceHandles.Count;
    }

    internal static bool CanRemove(int slotIndex, int slotCount) =>
        slotIndex >= 0 && slotIndex < slotCount;
}
