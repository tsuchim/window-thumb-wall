namespace WindowThumbWall.Tests;

public sealed class WindowEnumerationPolicyTests
{
    [Fact]
    public void ShouldInclude_RejectsOwnedStandardDialog()
    {
        WindowCandidateData window = new(
            IsVisible: true,
            ExStyle: 0,
            HasOwner: true,
            ClassName: "#32770",
            Title: "Open");

        bool included = WindowEnumerationPolicy.ShouldInclude(window);

        Assert.False(included);
    }

    [Fact]
    public void ShouldInclude_KeepsOwnedNonDialogWindow()
    {
        WindowCandidateData window = new(
            IsVisible: true,
            ExStyle: 0,
            HasOwner: true,
            ClassName: "Chrome_WidgetWin_1",
            Title: "Settings");

        bool included = WindowEnumerationPolicy.ShouldInclude(window);

        Assert.True(included);
    }

    [Fact]
    public void ShouldInclude_RejectsToolWindow()
    {
        WindowCandidateData window = new(
            IsVisible: true,
            ExStyle: (nint)NativeMethods.WS_EX_TOOLWINDOW,
            HasOwner: false,
            ClassName: "SomeWindowClass",
            Title: "Tool");

        bool included = WindowEnumerationPolicy.ShouldInclude(window);

        Assert.False(included);
    }

    [Fact]
    public void ShouldInclude_RejectsUntitledWindow()
    {
        WindowCandidateData window = new(
            IsVisible: true,
            ExStyle: 0,
            HasOwner: false,
            ClassName: "ApplicationFrameWindow",
            Title: "");

        bool included = WindowEnumerationPolicy.ShouldInclude(window);

        Assert.False(included);
    }
}
