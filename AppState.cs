using System.IO;
using System.Text.Json;

namespace WindowThumbWall;

internal sealed class AppState
{
    internal const string StatePathOverrideEnvironmentVariable = "WINDOWTHUMBWALL_STATE_PATH";

    public List<SlotState> Slots { get; set; } = [];
    public List<string> AutoAddApps { get; set; } = [];
    public WindowGeometry? Geometry { get; set; }
    public bool IsFullScreen { get; set; }
    public double LeftPanelWidth { get; set; }
    public double AppListHeight { get; set; }
    public bool EnableOsNotificationAttention { get; set; }

    internal static AppState Load()
    {
        string statePath = GetStatePath();
        try
        {
            if (!File.Exists(statePath)) return new AppState();
            var json = File.ReadAllText(statePath);
            return JsonSerializer.Deserialize<AppState>(json) ?? new AppState();
        }
        catch
        {
            return new AppState();
        }
    }

    internal void Save()
    {
        string statePath = GetStatePath();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(statePath, json);
        }
        catch
        {
            // Silently ignore save errors.
        }
    }

    internal static string GetStatePath()
    {
        string? overridePath = Environment.GetEnvironmentVariable(StatePathOverrideEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overridePath))
            return overridePath;

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WindowThumbWall", "state.json");
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
