namespace WindowThumbWall.Tests;

public sealed class TaskbarWindowOrderTests
{
    [Fact]
    public void Order_UsesReverseZOrderForWindowsThatPredateTracking()
    {
        var order = new TaskbarWindowOrder();
        TestWindow[] windowsInZOrder =
        [
            new((IntPtr)3),
            new((IntPtr)2),
            new((IntPtr)1)
        ];

        IReadOnlyList<TestWindow> result = order.Order(windowsInZOrder, window => window.Handle);

        Assert.Equal([(IntPtr)1, (IntPtr)2, (IntPtr)3], result.Select(window => window.Handle));
    }

    [Fact]
    public void Order_AppendsNewlyCreatedWindowAfterExistingWindows()
    {
        var order = new TaskbarWindowOrder();
        order.ObserveCreated((IntPtr)10);
        order.ObserveCreated((IntPtr)20);
        order.ObserveCreated((IntPtr)30);

        TestWindow[] windowsInCurrentZOrder =
        [
            new((IntPtr)30),
            new((IntPtr)10),
            new((IntPtr)20)
        ];

        IReadOnlyList<TestWindow> result = order.Order(windowsInCurrentZOrder, window => window.Handle);

        Assert.Equal([(IntPtr)10, (IntPtr)20, (IntPtr)30], result.Select(window => window.Handle));
    }

    [Fact]
    public void ObserveDestroyed_RemovesOldPositionBeforeHandleIsObservedAgain()
    {
        var order = new TaskbarWindowOrder();
        order.ObserveCreated((IntPtr)10);
        order.ObserveCreated((IntPtr)20);
        order.ObserveDestroyed((IntPtr)10);
        order.ObserveCreated((IntPtr)10);

        TestWindow[] windowsInZOrder =
        [
            new((IntPtr)10),
            new((IntPtr)20)
        ];

        IReadOnlyList<TestWindow> result = order.Order(windowsInZOrder, window => window.Handle);

        Assert.Equal([(IntPtr)20, (IntPtr)10], result.Select(window => window.Handle));
    }

    [Fact]
    public void ObserveCreated_IgnoresDuplicateNotifications()
    {
        var order = new TaskbarWindowOrder();
        order.ObserveCreated((IntPtr)10);
        order.ObserveCreated((IntPtr)10);
        order.ObserveCreated((IntPtr)20);

        IReadOnlyList<TestWindow> result = order.Order(
            new TestWindow[] { new((IntPtr)20), new((IntPtr)10) }, window => window.Handle);

        Assert.Equal([(IntPtr)10, (IntPtr)20], result.Select(window => window.Handle));
    }

    [Fact]
    public void ObserveDestroyed_IgnoresDuplicateNotifications()
    {
        var order = new TaskbarWindowOrder();
        order.ObserveCreated((IntPtr)10);
        order.ObserveDestroyed((IntPtr)10);
        order.ObserveDestroyed((IntPtr)10);
        order.ObserveCreated((IntPtr)20);

        IReadOnlyList<TestWindow> result = order.Order(
            new TestWindow[] { new((IntPtr)20) }, window => window.Handle);

        Assert.Equal([(IntPtr)20], result.Select(window => window.Handle));
    }

    [Fact]
    public void Order_AppendsUnknownWindowsAfterKnownWindowsDeterministically()
    {
        var order = new TaskbarWindowOrder();
        order.ObserveCreated((IntPtr)10);

        IReadOnlyList<TestWindow> first = order.Order(
            new TestWindow[] { new((IntPtr)30), new((IntPtr)20), new((IntPtr)10) }, window => window.Handle);
        IReadOnlyList<TestWindow> second = order.Order(
            new TestWindow[] { new((IntPtr)10), new((IntPtr)20), new((IntPtr)30) }, window => window.Handle);

        Assert.Equal([(IntPtr)10, (IntPtr)20, (IntPtr)30], first.Select(window => window.Handle));
        Assert.Equal([(IntPtr)10, (IntPtr)20, (IntPtr)30], second.Select(window => window.Handle));
    }

    [Fact]
    public void Order_IgnoresInvalidHandles()
    {
        var order = new TaskbarWindowOrder();
        order.ObserveCreated(IntPtr.Zero);
        order.ObserveCreated((IntPtr)10);

        IReadOnlyList<TestWindow> result = order.Order(
            new TestWindow[] { new(IntPtr.Zero), new((IntPtr)10) }, window => window.Handle);

        Assert.Equal([(IntPtr)10], result.Select(window => window.Handle));
    }

    [Fact]
    public void Order_DoesNotMutateCallerWindowOrder()
    {
        var order = new TaskbarWindowOrder();
        TestWindow[] windowsInZOrder =
        [
            new((IntPtr)2),
            new((IntPtr)1)
        ];

        _ = order.Order(windowsInZOrder, window => window.Handle);

        Assert.Equal([(IntPtr)2, (IntPtr)1], windowsInZOrder.Select(window => window.Handle));
    }

    private sealed record TestWindow(IntPtr Handle);
}
