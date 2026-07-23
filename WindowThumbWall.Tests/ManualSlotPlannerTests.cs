namespace WindowThumbWall.Tests;

public sealed class ManualSlotPlannerTests
{
    [Fact]
    public void GetAddTarget_UsesFirstEmptySlot()
    {
        int result = ManualSlotPlanner.GetAddTarget(
            [(IntPtr)10, IntPtr.Zero, (IntPtr)30],
            (IntPtr)20);

        Assert.Equal(1, result);
    }

    [Fact]
    public void GetAddTarget_AppendsWhenEverySlotIsOccupied()
    {
        int result = ManualSlotPlanner.GetAddTarget(
            [(IntPtr)10, (IntPtr)20],
            (IntPtr)30);

        Assert.Equal(2, result);
    }

    [Fact]
    public void GetAddTarget_RejectsAlreadyMonitoredOrInvalidWindow()
    {
        IReadOnlyList<IntPtr> slots = [(IntPtr)10, IntPtr.Zero];

        Assert.Equal(-1, ManualSlotPlanner.GetAddTarget(slots, (IntPtr)10));
        Assert.Equal(-1, ManualSlotPlanner.GetAddTarget(slots, IntPtr.Zero));
    }

    [Fact]
    public void CanRemove_OnlyAcceptsExistingSlotIndexes()
    {
        Assert.True(ManualSlotPlanner.CanRemove(0, 2));
        Assert.True(ManualSlotPlanner.CanRemove(1, 2));
        Assert.False(ManualSlotPlanner.CanRemove(-1, 2));
        Assert.False(ManualSlotPlanner.CanRemove(2, 2));
    }
}
