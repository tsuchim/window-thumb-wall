namespace WindowThumbWall.Tests;

public sealed class NotificationObservationPolicyTests
{
    [Fact]
    public void ShouldEvaluate_ReturnsFalse_ForUnchangedBaselineNotificationWithoutAttentionGroup()
    {
        NotificationObservation current = new(
            new DateTimeOffset(2026, 4, 8, 12, 0, 0, TimeSpan.Zero),
            "same",
            IsBaseline: true);

        bool shouldEvaluate = NotificationObservationPolicy.ShouldEvaluate(
            100,
            current,
            new Dictionary<uint, NotificationObservation>
            {
                [100] = current
            },
            new Dictionary<uint, NotificationAttentionGroup>(),
            new HashSet<uint>());

        Assert.False(shouldEvaluate);
    }

    [Fact]
    public void ShouldEvaluate_ReturnsTrue_WhenBaselineNotificationChanged()
    {
        NotificationObservation known = new(
            new DateTimeOffset(2026, 4, 8, 12, 0, 0, TimeSpan.Zero),
            "before",
            IsBaseline: true);
        NotificationObservation current = known with { Fingerprint = "after" };

        bool shouldEvaluate = NotificationObservationPolicy.ShouldEvaluate(
            100,
            current,
            new Dictionary<uint, NotificationObservation>
            {
                [100] = known
            },
            new Dictionary<uint, NotificationAttentionGroup>(),
            new HashSet<uint>());

        Assert.True(shouldEvaluate);
    }

    [Fact]
    public void ShouldEvaluate_ReturnsTrue_ForUnchangedPostInitNotificationWithoutAttentionGroup()
    {
        NotificationObservation current = new(
            new DateTimeOffset(2026, 4, 8, 12, 0, 0, TimeSpan.Zero),
            "same",
            IsBaseline: false);

        bool shouldEvaluate = NotificationObservationPolicy.ShouldEvaluate(
            100,
            current,
            new Dictionary<uint, NotificationObservation>
            {
                [100] = current
            },
            new Dictionary<uint, NotificationAttentionGroup>(),
            new HashSet<uint>());

        Assert.True(shouldEvaluate);
    }

    [Fact]
    public void ShouldEvaluate_ReturnsFalse_ForUnchangedPostInitNotificationWithExistingAttentionGroup()
    {
        NotificationObservation current = new(
            new DateTimeOffset(2026, 4, 8, 12, 0, 0, TimeSpan.Zero),
            "same",
            IsBaseline: false);

        bool shouldEvaluate = NotificationObservationPolicy.ShouldEvaluate(
            100,
            current,
            new Dictionary<uint, NotificationObservation>
            {
                [100] = current
            },
            new Dictionary<uint, NotificationAttentionGroup>
            {
                [100] = new(100, AttentionVisualState.Red, [(IntPtr)1])
            },
            new HashSet<uint>());

        Assert.False(shouldEvaluate);
    }

    [Fact]
    public void ShouldEvaluate_ReturnsTrue_WhenCreationTimeChanged()
    {
        NotificationObservation known = new(
            new DateTimeOffset(2026, 4, 8, 12, 0, 0, TimeSpan.Zero),
            "same",
            IsBaseline: false);
        NotificationObservation current = known with
        {
            CreationTime = new DateTimeOffset(2026, 4, 8, 12, 0, 5, TimeSpan.Zero)
        };

        bool shouldEvaluate = NotificationObservationPolicy.ShouldEvaluate(
            100,
            current,
            new Dictionary<uint, NotificationObservation>
            {
                [100] = known
            },
            new Dictionary<uint, NotificationAttentionGroup>(),
            new HashSet<uint>());

        Assert.True(shouldEvaluate);
    }

    [Fact]
    public void ShouldEvaluate_ReturnsFalse_WhenNotificationWasDismissedByActivation()
    {
        NotificationObservation current = new(
            new DateTimeOffset(2026, 4, 8, 12, 0, 0, TimeSpan.Zero),
            "same",
            IsBaseline: false);

        bool shouldEvaluate = NotificationObservationPolicy.ShouldEvaluate(
            100,
            current,
            new Dictionary<uint, NotificationObservation>
            {
                [100] = current
            },
            new Dictionary<uint, NotificationAttentionGroup>(),
            new HashSet<uint> { 100 });

        Assert.False(shouldEvaluate);
    }

    [Fact]
    public void ShouldEvaluate_ReturnsTrue_ForPreviouslyUnseenNotification()
    {
        NotificationObservation current = new(
            new DateTimeOffset(2026, 4, 8, 12, 0, 0, TimeSpan.Zero),
            "same",
            IsBaseline: false);

        bool shouldEvaluate = NotificationObservationPolicy.ShouldEvaluate(
            100,
            current,
            new Dictionary<uint, NotificationObservation>(),
            new Dictionary<uint, NotificationAttentionGroup>(),
            new HashSet<uint>());

        Assert.True(shouldEvaluate);
    }
}
