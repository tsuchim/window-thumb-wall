namespace WindowThumbWall.Tests;

public sealed class NotificationAttentionPolicyTests
{
    [Fact]
    public void ShouldSuppressAmbiguousNotificationDueToFlash_ReturnsTrue_WhenSameAppHasFlashingWindow()
    {
        NotificationSignal signal = new(
            AppUserModelId: string.Empty,
            AppDisplayName: "Visual Studio Code",
            NotificationTexts: ["チャット: 新しい応答"]);

        bool suppressed = NotificationAttentionPolicy.ShouldSuppressAmbiguousNotificationDueToFlash(
            signal,
            [
                NewWindow((IntPtr)1, "repo-a - Visual Studio Code", "Code", @"C:\Apps\Code.exe", ""),
                NewWindow((IntPtr)2, "repo-b - Visual Studio Code", "Code", @"C:\Apps\Code.exe", ""),
                NewWindow((IntPtr)3, "Browser", "msedge", @"C:\Apps\msedge.exe", "")
            ],
            new HashSet<IntPtr> { (IntPtr)2 });

        Assert.True(suppressed);
    }

    [Fact]
    public void ShouldSuppressAmbiguousNotificationDueToFlash_ReturnsFalse_WhenNoSameAppWindowIsFlashing()
    {
        NotificationSignal signal = new(
            AppUserModelId: string.Empty,
            AppDisplayName: "Visual Studio Code",
            NotificationTexts: ["チャット: 新しい応答"]);

        bool suppressed = NotificationAttentionPolicy.ShouldSuppressAmbiguousNotificationDueToFlash(
            signal,
            [
                NewWindow((IntPtr)1, "repo-a - Visual Studio Code", "Code", @"C:\Apps\Code.exe", ""),
                NewWindow((IntPtr)2, "repo-b - Visual Studio Code", "Code", @"C:\Apps\Code.exe", ""),
                NewWindow((IntPtr)3, "Browser", "msedge", @"C:\Apps\msedge.exe", "")
            ],
            new HashSet<IntPtr> { (IntPtr)3 });

        Assert.False(suppressed);
    }

    [Fact]
    public void ShouldSuppressAmbiguousNotificationDueToFlash_ReturnsFalse_WhenSignalHasNoAppIdentity()
    {
        NotificationSignal signal = new(
            AppUserModelId: string.Empty,
            AppDisplayName: string.Empty,
            NotificationTexts: ["新しい通知"]);

        bool suppressed = NotificationAttentionPolicy.ShouldSuppressAmbiguousNotificationDueToFlash(
            signal,
            [
                NewWindow((IntPtr)1, "repo-a - Visual Studio Code", "Code", @"C:\Apps\Code.exe", "")
            ],
            new HashSet<IntPtr> { (IntPtr)1 });

        Assert.False(suppressed);
    }

    private static NotificationWindowCandidate NewWindow(
        IntPtr handle,
        string title,
        string processName,
        string executablePath,
        string appUserModelId) =>
        new(handle, title, processName, executablePath, appUserModelId);
}
