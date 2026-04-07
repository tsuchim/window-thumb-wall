namespace WindowThumbWall.Tests;

public sealed class NotificationWindowMatcherTests
{
    [Fact]
    public void Resolve_ReturnsNone_WhenSignalHasNoSourceAppIdentity()
    {
        NotificationSignal signal = new(
            AppUserModelId: string.Empty,
            AppDisplayName: string.Empty,
            NotificationTexts: ["Job session-8842 finished"]);

        NotificationMatchResult result = NotificationWindowMatcher.Resolve(signal,
        [
            NewWindow((IntPtr)1, "Codex session-8842", "codex", @"C:\Apps\codex.exe", "")
        ]);

        Assert.Equal(NotificationMatchKind.None, result.Kind);
        Assert.Empty(result.CandidateHandles);
    }

    [Fact]
    public void Resolve_ReturnsNone_WhenSourceAppDoesNotMatchAnyMonitoredWindow()
    {
        NotificationSignal signal = new(
            AppUserModelId: "OpenAI.Codex_123!App",
            AppDisplayName: "Codex",
            NotificationTexts: ["Build finished"]);

        NotificationMatchResult result = NotificationWindowMatcher.Resolve(signal,
        [
            NewWindow((IntPtr)1, "repo-a - Visual Studio Code", "Code", @"C:\Apps\Code.exe", ""),
            NewWindow((IntPtr)2, "Browser", "msedge", @"C:\Apps\msedge.exe", "Microsoft.MSEdge")
        ]);

        Assert.Equal(NotificationMatchKind.None, result.Kind);
        Assert.Empty(result.CandidateHandles);
    }

    [Fact]
    public void Resolve_ReturnsUnique_WhenSourceAppHasSingleMonitoredWindow()
    {
        NotificationSignal signal = new(
            AppUserModelId: "OpenAI.Codex_123!App",
            AppDisplayName: "Codex",
            NotificationTexts: ["Attention required"]);

        NotificationMatchResult result = NotificationWindowMatcher.Resolve(signal,
        [
            NewWindow((IntPtr)1, "Codex", "codex", @"C:\Apps\codex.exe", "OpenAI.Codex_123!App")
        ]);

        Assert.Equal(NotificationMatchKind.Unique, result.Kind);
        Assert.Equal([(IntPtr)1], result.CandidateHandles);
    }

    [Fact]
    public void Resolve_UsesTitleTokensOnlyWithinSameAppCandidates()
    {
        NotificationSignal signal = new(
            AppUserModelId: string.Empty,
            AppDisplayName: "Visual Studio Code",
            NotificationTexts: ["README.md session-8842 finished"]);

        NotificationMatchResult result = NotificationWindowMatcher.Resolve(signal,
        [
            NewWindow((IntPtr)1, "README.md session-8842 - Visual Studio Code", "Code", @"C:\Apps\Code.exe", ""),
            NewWindow((IntPtr)2, "README.md session-9911 - Visual Studio Code", "Code", @"C:\Apps\Code.exe", ""),
            NewWindow((IntPtr)3, "README.md session-8842 - Codex", "codex", @"C:\Apps\codex.exe", "")
        ]);

        Assert.Equal(NotificationMatchKind.Unique, result.Kind);
        Assert.Equal([(IntPtr)1], result.CandidateHandles);
    }

    [Fact]
    public void Resolve_ChoosesNarrowestNonEmptyTitleTokenCandidateSet()
    {
        NotificationSignal signal = new(
            AppUserModelId: "OpenAI.Codex_123!App",
            AppDisplayName: "Codex",
            NotificationTexts: ["WindowThumbWall session-8842 completed"]);

        NotificationMatchResult result = NotificationWindowMatcher.Resolve(signal,
        [
            NewWindow((IntPtr)1, "WindowThumbWall session-8842", "codex", @"C:\Apps\codex.exe", "OpenAI.Codex_123!App"),
            NewWindow((IntPtr)2, "WindowThumbWall session-9911", "codex", @"C:\Apps\codex.exe", "OpenAI.Codex_123!App"),
            NewWindow((IntPtr)3, "WindowThumbWall backlog", "codex", @"C:\Apps\codex.exe", "OpenAI.Codex_123!App")
        ]);

        Assert.Equal(NotificationMatchKind.Unique, result.Kind);
        Assert.Equal([(IntPtr)1], result.CandidateHandles);
    }

    [Fact]
    public void Resolve_ReturnsAmbiguous_WhenMultipleSameAppWindowsRemain()
    {
        NotificationSignal signal = new(
            AppUserModelId: string.Empty,
            AppDisplayName: "Visual Studio Code",
            NotificationTexts: ["Attention required"]);

        NotificationMatchResult result = NotificationWindowMatcher.Resolve(signal,
        [
            NewWindow((IntPtr)1, "repo-a - Visual Studio Code", "Code", @"C:\Apps\Code.exe", ""),
            NewWindow((IntPtr)2, "repo-b - Visual Studio Code", "Code", @"C:\Apps\Code.exe", ""),
            NewWindow((IntPtr)3, "Browser", "msedge", @"C:\Apps\msedge.exe", "")
        ]);

        Assert.Equal(NotificationMatchKind.Ambiguous, result.Kind);
        Assert.Equal([(IntPtr)1, (IntPtr)2], result.CandidateHandles);
    }

    [Fact]
    public void Resolve_DoesNotUsePathLikeTitleTokensFromDifferentApps()
    {
        NotificationSignal signal = new(
            AppUserModelId: string.Empty,
            AppDisplayName: "Codex",
            NotificationTexts: ["WindowThumbWall release-123 finished"]);

        NotificationMatchResult result = NotificationWindowMatcher.Resolve(signal,
        [
            NewWindow((IntPtr)1, "Codex release-123", "codex", @"C:\Apps\codex.exe", ""),
            NewWindow((IntPtr)2, @"C:\Users\me\Github\WindowThumbWall\packaging", "pwsh", @"C:\Program Files\PowerShell\7\pwsh.exe", "")
        ]);

        Assert.Equal(NotificationMatchKind.Unique, result.Kind);
        Assert.Equal([(IntPtr)1], result.CandidateHandles);
    }

    [Fact]
    public void Resolve_ReducesByExactExecutableHintOnly()
    {
        NotificationSignal signal = new(
            AppUserModelId: string.Empty,
            AppDisplayName: "Codex",
            NotificationTexts: ["Attention required"]);

        NotificationMatchResult result = NotificationWindowMatcher.Resolve(signal,
        [
            NewWindow((IntPtr)1, "task alpha", "codex", @"C:\Apps\codex.exe", ""),
            NewWindow((IntPtr)2, "task beta", "codex-helper", @"C:\Apps\codex-helper.exe", ""),
            NewWindow((IntPtr)3, "task gamma", "mycodex", @"C:\Apps\mycodex.exe", "")
        ]);

        Assert.Equal(NotificationMatchKind.Unique, result.Kind);
        Assert.Equal([(IntPtr)1], result.CandidateHandles);
    }

    [Fact]
    public void Resolve_DoesNotCrossIntoDifferentAppEvenWhenTitleMatches()
    {
        NotificationSignal signal = new(
            AppUserModelId: string.Empty,
            AppDisplayName: "Visual Studio Code",
            NotificationTexts: ["session-8842 finished"]);

        NotificationMatchResult result = NotificationWindowMatcher.Resolve(signal,
        [
            NewWindow((IntPtr)1, "Codex session-8842", "codex", @"C:\Apps\codex.exe", ""),
            NewWindow((IntPtr)2, "repo-a - Visual Studio Code", "Code", @"C:\Apps\Code.exe", ""),
            NewWindow((IntPtr)3, "repo-b - Visual Studio Code", "Code", @"C:\Apps\Code.exe", "")
        ]);

        Assert.Equal(NotificationMatchKind.Ambiguous, result.Kind);
        Assert.Equal([(IntPtr)2, (IntPtr)3], result.CandidateHandles);
    }

    private static NotificationWindowCandidate NewWindow(
        IntPtr handle,
        string title,
        string processName,
        string executablePath,
        string appUserModelId) =>
        new(handle, title, processName, executablePath, appUserModelId);
}
