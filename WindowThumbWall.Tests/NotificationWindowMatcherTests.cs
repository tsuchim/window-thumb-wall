namespace WindowThumbWall.Tests;

public sealed class NotificationWindowMatcherTests
{
    [Fact]
    public void Resolve_ReturnsUniqueMatch_WhenStrongTokenMatchesSingleWindow()
    {
        NotificationSignal signal = new(
            AppUserModelId: string.Empty,
            AppDisplayName: "Codex",
            NotificationTexts: ["Build failed for repo WindowThumbWall on branch release-123"]);

        NotificationMatchResult result = NotificationWindowMatcher.Resolve(signal,
        [
            NewWindow((IntPtr)1, "WindowThumbWall - release-123", "codex", @"C:\Apps\codex.exe", ""),
            NewWindow((IntPtr)2, "Slack", "slack", @"C:\Apps\slack.exe", "")
        ]);

        Assert.Equal(NotificationMatchKind.Unique, result.Kind);
        Assert.Equal([(IntPtr)1], result.CandidateHandles);
    }

    [Fact]
    public void Resolve_ReturnsUniqueMatch_WhenAumidHasSingleVisibleWindow()
    {
        NotificationSignal signal = new(
            AppUserModelId: "OpenAI.Codex_123!App",
            AppDisplayName: "Codex",
            NotificationTexts: ["New notification"]);

        NotificationMatchResult result = NotificationWindowMatcher.Resolve(signal,
        [
            NewWindow((IntPtr)1, "Session A", "codex", @"C:\Apps\codex.exe", "OpenAI.Codex_123!App"),
            NewWindow((IntPtr)2, "Browser", "msedge", @"C:\Apps\msedge.exe", "Microsoft.MSEdge")
        ]);

        Assert.Equal(NotificationMatchKind.Unique, result.Kind);
        Assert.Equal([(IntPtr)1], result.CandidateHandles);
    }

    [Fact]
    public void Resolve_UsesTitleTokensWithinAumidCandidates()
    {
        NotificationSignal signal = new(
            AppUserModelId: "OpenAI.Codex_123!App",
            AppDisplayName: "Codex",
            NotificationTexts: ["Job session-8842 finished"]);

        NotificationMatchResult result = NotificationWindowMatcher.Resolve(signal,
        [
            NewWindow((IntPtr)1, "Codex session-8842", "codex", @"C:\Apps\codex.exe", "OpenAI.Codex_123!App"),
            NewWindow((IntPtr)2, "Codex session-9911", "codex", @"C:\Apps\codex.exe", "OpenAI.Codex_123!App")
        ]);

        Assert.Equal(NotificationMatchKind.Unique, result.Kind);
        Assert.Equal([(IntPtr)1], result.CandidateHandles);
    }

    [Fact]
    public void Resolve_ReturnsAmbiguousCandidates_WhenMultipleWindowsRemain()
    {
        NotificationSignal signal = new(
            AppUserModelId: "OpenAI.Codex_123!App",
            AppDisplayName: "Codex",
            NotificationTexts: ["Build completed"]);

        NotificationMatchResult result = NotificationWindowMatcher.Resolve(signal,
        [
            NewWindow((IntPtr)1, "Codex build", "codex", @"C:\Apps\codex.exe", "OpenAI.Codex_123!App"),
            NewWindow((IntPtr)2, "Codex build", "codex", @"C:\Apps\codex.exe", "OpenAI.Codex_123!App"),
            NewWindow((IntPtr)3, "Slack", "slack", @"C:\Apps\slack.exe", "")
        ]);

        Assert.Equal(NotificationMatchKind.Ambiguous, result.Kind);
        Assert.Equal([(IntPtr)1, (IntPtr)2], result.CandidateHandles);
    }

    [Fact]
    public void Resolve_UsesExecutableHints_AsFinalReductionStage()
    {
        NotificationSignal signal = new(
            AppUserModelId: string.Empty,
            AppDisplayName: "Codex",
            NotificationTexts: ["Session task-447 requires attention"]);

        NotificationMatchResult result = NotificationWindowMatcher.Resolve(signal,
        [
            NewWindow((IntPtr)1, "task-447", "codex", @"C:\Apps\codex.exe", ""),
            NewWindow((IntPtr)2, "task-447", "slack", @"C:\Apps\slack.exe", ""),
            NewWindow((IntPtr)3, "task-912", "codex", @"C:\Apps\codex.exe", "")
        ]);

        Assert.Equal(NotificationMatchKind.Unique, result.Kind);
        Assert.Equal([(IntPtr)1], result.CandidateHandles);
    }

    [Fact]
    public void Resolve_ChoosesNarrowestTitleTokenInsteadOfFirstHit()
    {
        NotificationSignal signal = new(
            AppUserModelId: string.Empty,
            AppDisplayName: "Codex",
            NotificationTexts: ["WindowThumbWall session-8842 completed"]);

        NotificationMatchResult result = NotificationWindowMatcher.Resolve(signal,
        [
            NewWindow((IntPtr)1, "WindowThumbWall session-8842", "codex", @"C:\Apps\codex.exe", ""),
            NewWindow((IntPtr)2, "WindowThumbWall session-9911", "codex", @"C:\Apps\codex.exe", ""),
            NewWindow((IntPtr)3, "Slack", "slack", @"C:\Apps\slack.exe", "")
        ]);

        Assert.Equal(NotificationMatchKind.Unique, result.Kind);
        Assert.Equal([(IntPtr)1], result.CandidateHandles);
    }

    [Fact]
    public void Resolve_DoesNotUsePathSubstringAsTitleTokenMatch()
    {
        NotificationSignal signal = new(
            AppUserModelId: string.Empty,
            AppDisplayName: string.Empty,
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
            NotificationTexts: ["New notification"]);

        NotificationMatchResult result = NotificationWindowMatcher.Resolve(signal,
        [
            NewWindow((IntPtr)1, "task alpha", "codex", @"C:\Apps\codex.exe", ""),
            NewWindow((IntPtr)2, "task beta", "codex", @"C:\Apps\codex.exe", ""),
            NewWindow((IntPtr)3, "PowerShell_7.6.0.0", "pwsh", @"C:\Program Files\PowerShell\7\pwsh.exe", "")
        ]);

        Assert.Equal(NotificationMatchKind.Ambiguous, result.Kind);
        Assert.Equal([(IntPtr)1, (IntPtr)2], result.CandidateHandles);
    }

    [Fact]
    public void Resolve_ReturnsNone_WhenSourceAppDoesNotMatchAnyCandidate()
    {
        NotificationSignal signal = new(
            AppUserModelId: "Microsoft.WSL",
            AppDisplayName: "WSL",
            NotificationTexts: ["Windows drive performance tip"]);

        NotificationMatchResult result = NotificationWindowMatcher.Resolve(signal,
        [
            NewWindow((IntPtr)1, "Windows Input Experience", "TextInputHost", @"C:\Windows\SystemApps\TextInputHost.exe", ""),
            NewWindow((IntPtr)2, "repo-a - Visual Studio Code", "Code", @"C:\Apps\Code.exe", "")
        ]);

        Assert.Equal(NotificationMatchKind.None, result.Kind);
        Assert.Empty(result.CandidateHandles);
    }

    [Fact]
    public void Resolve_UsesSourceAppReduction_EvenWhenTitleDoesNotHelp()
    {
        NotificationSignal signal = new(
            AppUserModelId: string.Empty,
            AppDisplayName: "Codex",
            NotificationTexts: ["Attention required"]);

        NotificationMatchResult result = NotificationWindowMatcher.Resolve(signal,
        [
            NewWindow((IntPtr)1, "Codex", "Codex", @"C:\Apps\Codex.exe", ""),
            NewWindow((IntPtr)2, "PowerShell_7.6.0.0", "pwsh", @"C:\Program Files\PowerShell\7\pwsh.exe", "")
        ]);

        Assert.Equal(NotificationMatchKind.Unique, result.Kind);
        Assert.Equal([(IntPtr)1], result.CandidateHandles);
    }

    [Fact]
    public void Resolve_ReturnsAmbiguousCandidates_AfterExecutableReductionWhenTextDoesNotNarrow()
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

    private static NotificationWindowCandidate NewWindow(
        IntPtr handle,
        string title,
        string processName,
        string executablePath,
        string appUserModelId) =>
        new(handle, title, processName, executablePath, appUserModelId);
}
