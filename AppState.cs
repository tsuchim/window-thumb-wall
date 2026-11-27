using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WindowThumbWall;

internal sealed class AppState
{
    internal const string StatePathOverrideEnvironmentVariable = "WINDOWTHUMBWALL_STATE_PATH";
    internal const int CurrentSchemaVersion = 2;
    private static readonly TimeSpan StateWriteLockTimeout = TimeSpan.FromSeconds(5);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    [JsonIgnore]
    internal byte[]? SourceRevision { get; set; }
    [JsonIgnore]
    internal string? SourcePath { get; set; }
    [JsonIgnore]
    internal bool CanSaveSource { get; set; } = true;
    [JsonIgnore]
    internal bool NeedsMigrationBackup { get; set; }

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public List<SlotState> Slots { get; set; } = [];
    public List<WindowRuleState> SpecificWindowRules { get; set; } = [];
    public List<string> AutoAddApps { get; set; } = [];
    public WindowGeometry? Geometry { get; set; }
    public bool IsFullScreen { get; set; }
    public bool IsLeftPanelCollapsed { get; set; }
    public double LeftPanelWidth { get; set; }
    public double AppListHeight { get; set; }
    public bool EnableOsNotificationAttention { get; set; }

    internal static AppState Load() => Load(out _);

    internal static AppState Load(out bool canSave)
    {
        AppStateLoadResult result = LoadWithStatus();
        canSave = result.CanSave;
        return result.State;
    }

    internal static AppStateLoadResult LoadWithStatus()
    {
        string path;
        try
        {
            path = Path.GetFullPath(GetStatePath());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return FailedLoad(null, null, ex);
        }

        try
        {
            byte[]? bytes = ReadStateFile(path);
            if (bytes == null)
            {
                var missing = new AppState { SourcePath = path, SourceRevision = null };
                return new AppStateLoadResult(missing, AppStateLoadStatus.NotFound, CanSave: true, Migrated: false);
            }

            using var reader = new StreamReader(new MemoryStream(bytes, writable: false), detectEncodingFromByteOrderMarks: true);
            string json = reader.ReadToEnd();
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new JsonException("WindowThumbWall state must be an object.");

            JsonElement root = document.RootElement;
            bool hasVersion = root.TryGetProperty(nameof(SchemaVersion), out JsonElement versionElement);
            AppState state = JsonSerializer.Deserialize<AppState>(json, JsonOptions)
                ?? throw new JsonException("WindowThumbWall state is empty.");
            if (state.Slots == null || state.AutoAddApps == null || state.Slots.Any(slot => slot == null))
                throw new JsonException("WindowThumbWall state has an invalid layout.");

            bool migrated = !hasVersion;
            if (migrated)
            {
                MigrateLegacyState(state);
            }
            else
            {
                if (versionElement.ValueKind != JsonValueKind.Number ||
                    !versionElement.TryGetInt32(out int version) || version != CurrentSchemaVersion)
                    throw new JsonException("WindowThumbWall state has an unsupported schema version.");
                if (state.SpecificWindowRules == null)
                    throw new JsonException("WindowThumbWall state has an invalid rule list.");
                ValidateVersion2State(state);
            }

            state.SourcePath = path;
            state.SourceRevision = bytes;
            state.CanSaveSource = true;
            state.NeedsMigrationBackup = migrated;
            return new AppStateLoadResult(state, AppStateLoadStatus.Loaded, CanSave: true, Migrated: migrated);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return FailedLoad(path, null, ex);
        }
    }

    internal AppStateSaveStatus SaveWithStatus()
    {
        if (!CanSaveSource)
            return AppStateSaveStatus.Failed;

        string path;
        try
        {
            path = Path.GetFullPath(SourcePath ?? GetStatePath());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Trace.TraceWarning("Could not resolve WindowThumbWall state path: {0}", ex.GetType().Name);
            return AppStateSaveStatus.Failed;
        }

        string? tempPath = null;
        try
        {
            string? directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory))
                return AppStateSaveStatus.Failed;

            Directory.CreateDirectory(directory);
            using FileStream? writeLock = TryAcquireStateWriteLock(path);
            if (writeLock == null)
                return AppStateSaveStatus.Failed;

            byte[]? observed = ReadStateFile(path);
            if (!ByteArraysEqual(observed, SourceRevision))
                return AppStateSaveStatus.SourceChanged;

            if (SchemaVersion != CurrentSchemaVersion || SpecificWindowRules == null || Slots == null || AutoAddApps == null)
                return AppStateSaveStatus.Failed;
            ValidateVersion2State(this);

            byte[] data = JsonSerializer.SerializeToUtf8Bytes(this, JsonOptions);

            if (NeedsMigrationBackup && observed != null && !EnsureMigrationBackup(path, observed))
                return AppStateSaveStatus.Failed;

            tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllBytes(tempPath, data);
            File.Move(tempPath, path, overwrite: true);
            tempPath = null;
            SourceRevision = data;
            SourcePath = path;
            NeedsMigrationBackup = false;
            CanSaveSource = true;
            return AppStateSaveStatus.Saved;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            Trace.TraceWarning("Could not save WindowThumbWall state: {0}", ex.GetType().Name);
            return AppStateSaveStatus.Failed;
        }
        finally
        {
            if (tempPath != null)
            {
                try { File.Delete(tempPath); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Trace.TraceWarning("Could not remove temporary WindowThumbWall state: {0}", ex.GetType().Name);
                }
            }
        }
    }

    internal bool TrySaveRecoveryCopy(string path)
    {
        string? createdPath = null;
        try
        {
            if (SchemaVersion != CurrentSchemaVersion || SpecificWindowRules == null || Slots == null || AutoAddApps == null)
                return false;
            ValidateVersion2State(this);
            string fullPath = Path.GetFullPath(path);
            string? directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(directory))
                return false;
            Directory.CreateDirectory(directory);
            byte[] data = JsonSerializer.SerializeToUtf8Bytes(this, JsonOptions);
            using (var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                createdPath = fullPath;
                stream.Write(data);
                stream.Flush(flushToDisk: true);
            }
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            Trace.TraceWarning("Could not write WindowThumbWall recovery copy: {0}", ex.GetType().Name);
            if (createdPath != null)
            {
                try { File.Delete(createdPath); }
                catch (Exception cleanupError) when (cleanupError is IOException or UnauthorizedAccessException)
                {
                    Trace.TraceWarning("Could not remove incomplete WindowThumbWall recovery copy: {0}", cleanupError.GetType().Name);
                }
            }
            return false;
        }
    }

    // Compatibility wrapper for callers that create a new state object directly.
    // Checked read-modify-write callers should retain the state returned by LoadWithStatus.
    internal void Save()
    {
        if (SourcePath == null)
        {
            AppStateLoadResult latest = LoadWithStatus();
            if (!latest.CanSave)
            {
                Trace.TraceWarning("Could not save WindowThumbWall state after a failed load.");
                return;
            }
            SourcePath = latest.State.SourcePath;
            SourceRevision = latest.State.SourceRevision;
            NeedsMigrationBackup = latest.State.NeedsMigrationBackup;
        }

        SchemaVersion = CurrentSchemaVersion;
        SpecificWindowRules ??= [];
        AutoAddApps ??= [];
        Slots ??= [];
        EnsureCompatibilityIdsAndRules();
        _ = SaveWithStatus();
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

    private static void MigrateLegacyState(AppState state)
    {
        state.SchemaVersion = CurrentSchemaVersion;
        state.SpecificWindowRules = [];
        foreach (SlotState slot in state.Slots)
        {
            if (slot.SlotId == Guid.Empty)
                slot.SlotId = Guid.NewGuid();

            // Old data used IsPinned to represent an explicit specific-window rule.
            if (!slot.IsPinned)
            {
                slot.RuleId = null;
                slot.IsAutomatic = false;
                continue;
            }

            Guid ruleId = slot.RuleId is Guid existingRuleId && existingRuleId != Guid.Empty
                ? existingRuleId
                : Guid.NewGuid();
            slot.RuleId = ruleId;
            slot.LastBindingIdentity ??= slot.Identity;
            slot.IsAutomatic = false;
            state.SpecificWindowRules.Add(new WindowRuleState
            {
                RuleId = ruleId,
                ProcessName = slot.ProcessName,
                TitleAnchor = slot.Title,
                Identity = slot.LastBindingIdentity ?? slot.Identity
            });
        }
    }

    private void EnsureCompatibilityIdsAndRules()
    {
        foreach (SlotState slot in Slots)
            if (slot.SlotId == Guid.Empty)
                slot.SlotId = Guid.NewGuid();

        foreach (SlotState slot in Slots.Where(slot => slot.IsPinned && slot.RuleId == null))
        {
            Guid id = Guid.NewGuid();
            slot.RuleId = id;
            slot.LastBindingIdentity ??= slot.Identity;
            SpecificWindowRules.Add(new WindowRuleState
            {
                RuleId = id,
                ProcessName = slot.ProcessName,
                TitleAnchor = slot.Title,
                Identity = slot.LastBindingIdentity ?? slot.Identity
            });
        }
    }

    private static void ValidateVersion2State(AppState state)
    {
        if (state.Slots.Any(slot => slot == null) || state.SpecificWindowRules.Any(rule => rule == null))
            throw new JsonException("WindowThumbWall state has an invalid layout.");

        var slotIds = new HashSet<Guid>();
        foreach (SlotState slot in state.Slots)
        {
            if (slot.SlotId == Guid.Empty || !slotIds.Add(slot.SlotId))
                throw new JsonException("WindowThumbWall state has a missing or duplicate slot ID.");
        }

        var rulesById = new Dictionary<Guid, WindowRuleState>();
        foreach (WindowRuleState rule in state.SpecificWindowRules)
        {
            if (rule.RuleId == Guid.Empty || !rulesById.TryAdd(rule.RuleId, rule))
                throw new JsonException("WindowThumbWall state has a missing or duplicate rule ID.");
        }

        foreach (SlotState slot in state.Slots)
        {
            if (slot.RuleId is Guid ruleId && !rulesById.ContainsKey(ruleId))
                throw new JsonException("WindowThumbWall state refers to a missing rule.");
            if (slot.IsPinned && slot.RuleId == null)
                throw new JsonException("WindowThumbWall pinned slot has no rule reference.");
            if (!slot.IsPinned && slot.RuleId != null)
                throw new JsonException("WindowThumbWall unpinned slot has a rule reference.");
        }
    }

    private static bool EnsureMigrationBackup(string path, byte[] original)
    {
        string backupPath = path + ".pre-v2.bak";
        if (File.Exists(backupPath))
        {
            try { return ByteArraysEqual(File.ReadAllBytes(backupPath), original); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Trace.TraceWarning("Could not inspect WindowThumbWall migration backup: {0}", ex.GetType().Name);
                return false;
            }
        }

        string tempPath = backupPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(tempPath, original);
            try
            {
                File.Move(tempPath, backupPath, overwrite: false);
                return true;
            }
            catch (IOException) when (File.Exists(backupPath))
            {
                return ByteArraysEqual(File.ReadAllBytes(backupPath), original);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning("Could not preserve WindowThumbWall migration backup: {0}", ex.GetType().Name);
            return false;
        }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Trace.TraceWarning("Could not remove temporary WindowThumbWall backup: {0}", ex.GetType().Name);
            }
        }
    }

    private static AppStateLoadResult FailedLoad(string? path, byte[]? revision, Exception ex)
    {
        Trace.TraceWarning("Could not load WindowThumbWall state: {0}", ex.GetType().Name);
        var state = new AppState { SourcePath = path, SourceRevision = revision, CanSaveSource = false };
        return new AppStateLoadResult(state, AppStateLoadStatus.Failed, CanSave: false, Migrated: false);
    }

    private static byte[]? ReadStateFile(string path)
    {
        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            using var contents = new MemoryStream();
            file.CopyTo(contents);
            return contents.ToArray();
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    private static FileStream? TryAcquireStateWriteLock(string path)
    {
        // File sharing enforces the write lock across Windows sessions.
        string lockPath = path + ".write-lock";
        var elapsed = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                return new FileStream(lockPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1,
                    FileOptions.DeleteOnClose);
            }
            catch (IOException ex) when (IsWriteLockBusy(ex))
            {
                if (elapsed.Elapsed >= StateWriteLockTimeout)
                    return null;

                Thread.Sleep(50);
            }
        }
    }

    private static bool IsWriteLockBusy(IOException exception) =>
        (exception.HResult & 0xffff) is 32 or 33 or 80 or 183; // Windows sharing, lock, or file-exists errors.

    private static bool ByteArraysEqual(byte[]? left, byte[]? right) =>
        left == null ? right == null : right != null && left.AsSpan().SequenceEqual(right);

}

internal enum AppStateLoadStatus
{
    Loaded,
    NotFound,
    Failed
}

internal sealed record AppStateLoadResult(AppState State, AppStateLoadStatus Status, bool CanSave, bool Migrated);

internal enum AppStateSaveStatus
{
    Saved,
    SourceChanged,
    Failed
}

internal sealed class SlotState
{
    public Guid SlotId { get; set; }
    public Guid? RuleId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool IsPinned { get; set; }
    public bool IsAutomatic { get; set; }
    public WindowIdentity? Identity { get; set; }
    public WindowIdentity? LastBindingIdentity { get; set; }
}

internal sealed class WindowRuleState
{
    public Guid RuleId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string TitleAnchor { get; set; } = string.Empty;
    public WindowIdentity? Identity { get; set; }
}

internal sealed class WindowGeometry
{
    internal static WindowGeometry? FromBounds(double left, double top, double width, double height, bool isMaximized) =>
        double.IsFinite(left) && double.IsFinite(top) &&
        double.IsFinite(width) && width > 0 && double.IsFinite(height) && height > 0
            ? new WindowGeometry { Left = left, Top = top, Width = width, Height = height, IsMaximized = isMaximized }
            : null;

    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool IsMaximized { get; set; }
}
