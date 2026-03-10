namespace WindowThumbWall;

internal readonly record struct WindowCandidateData(
    bool IsVisible,
    nint ExStyle,
    bool HasOwner,
    string ClassName,
    string Title);

internal static class WindowEnumerationPolicy
{
    internal static bool ShouldInclude(WindowCandidateData window)
    {
        if (!window.IsVisible) return false;
        if ((window.ExStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0) return false;
        if (IsLikelyOwnedStandardDialog(window)) return false;
        return !string.IsNullOrWhiteSpace(window.Title);
    }

    internal static bool IsLikelyOwnedStandardDialog(WindowCandidateData window)
    {
        return window.HasOwner
            && string.Equals(window.ClassName, "#32770", StringComparison.Ordinal);
    }
}
