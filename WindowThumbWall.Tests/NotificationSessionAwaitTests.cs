namespace WindowThumbWall.Tests;

public sealed class NotificationSessionAwaitTests
{
    [Fact]
    public async Task DelayedInitializationResult_IsRejectedAfterDisable()
    {
        object session = new();
        object? currentSession = session;
        TaskCompletionSource<string> delayedResult = new(TaskCreationOptions.RunContinuationsAsynchronously);
        string? appliedValue = null;

        Task<NotificationSessionResult<string>> pending = NotificationSessionAwait.AwaitAsync(
            session,
            delayedResult.Task,
            token => ReferenceEquals(currentSession, token));

        currentSession = null;
        delayedResult.SetResult("baseline");
        NotificationSessionResult<string> result = await pending;
        if (result.IsCurrent)
            appliedValue = result.Value;

        Assert.False(result.IsCurrent);
        Assert.Null(appliedValue);
    }

    [Fact]
    public async Task DelayedSyncResult_IsRejectedAfterClose()
    {
        object session = new();
        object? currentSession = session;
        TaskCompletionSource<int> delayedResult = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int collectionUpdates = 0;

        Task<NotificationSessionResult<int>> pending = NotificationSessionAwait.AwaitAsync(
            session,
            delayedResult.Task,
            token => ReferenceEquals(currentSession, token));

        currentSession = null;
        delayedResult.SetResult(3);
        NotificationSessionResult<int> result = await pending;
        if (result.IsCurrent)
            collectionUpdates += result.Value;

        Assert.False(result.IsCurrent);
        Assert.Equal(0, collectionUpdates);
    }

    [Fact]
    public async Task ReEnabledSessionAcceptsOnlyItsOwnDelayedResult()
    {
        object oldSession = new();
        object newSession = new();
        object? currentSession = oldSession;
        TaskCompletionSource<string> oldOperation = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<string> newOperation = new(TaskCreationOptions.RunContinuationsAsynchronously);

        Task<NotificationSessionResult<string>> oldPending = NotificationSessionAwait.AwaitAsync(
            oldSession,
            oldOperation.Task,
            token => ReferenceEquals(currentSession, token));
        currentSession = newSession;
        Task<NotificationSessionResult<string>> newPending = NotificationSessionAwait.AwaitAsync(
            newSession,
            newOperation.Task,
            token => ReferenceEquals(currentSession, token));

        oldOperation.SetResult("stale");
        newOperation.SetResult("current");
        NotificationSessionResult<string> oldResult = await oldPending;
        NotificationSessionResult<string> newResult = await newPending;

        Assert.False(oldResult.IsCurrent);
        Assert.True(newResult.IsCurrent);
        Assert.Equal("current", newResult.Value);
    }
}
