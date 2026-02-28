namespace WindowThumbWall;

internal sealed class WindowInfo
{
    public required IntPtr Handle { get; init; }
    public required string Title { get; init; }
    public override string ToString() => Title;
}
