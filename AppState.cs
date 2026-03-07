using System.IO;
using System.Text.Json;

namespace WindowThumbWall;

internal sealed class AppState
{
    public List<SlotState> Slots { get; set; } = [];
    public List<string> AutoAddApps { get; set; } = [];
    public WindowGeometry? Geometry { get; set; }
    public bool IsFullScreen { get; set; }
    public double LeftPanelWidth { get; set; }
    public double AppListHeight { get; set; }

    private static readonly string StatePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WindowThumbWall", "state.json");

    internal static AppState Load()
    {
        try
        {
            if (!File.Exists(StatePath)) return new AppState();
            var json = File.ReadAllText(StatePath);
            return JsonSerializer.Deserialize<AppState>(json) ?? new AppState();
        }
        catch
        {
            return new AppState();
        }
    }

    internal void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(StatePath, json);
        }
        catch
        {
            // Silently ignore save errors.
        }
    }
}

internal sealed class SlotState
{
    public string ProcessName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
}

internal sealed class WindowGeometry
{
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool IsMaximized { get; set; }
}
