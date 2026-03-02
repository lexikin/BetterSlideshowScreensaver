using System.Text.Json;
using System.Text.Json.Serialization;

namespace BetterSlideshowScreensaver;

public enum MultiMonitorMode
{
    PrimaryOnly,
    SameImage,
    MainPlusPrevious
}

public class ScreensaverConfig
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BetterSlideshowScreensaver");

    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string ImageFolderPath { get; set; } = "";
    public MultiMonitorMode MonitorMode { get; set; } = MultiMonitorMode.PrimaryOnly;
    public int SlideIntervalSeconds { get; set; } = 8;
    public List<string> ExcludedFiles { get; set; } = new();
    public List<string> DisabledMonitors { get; set; } = new();

    public bool IsExcluded(string filePath) =>
        ExcludedFiles.Any(e => string.Equals(e, filePath, StringComparison.OrdinalIgnoreCase));

    public void ToggleExclusion(string filePath)
    {
        var index = ExcludedFiles.FindIndex(e => string.Equals(e, filePath, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
            ExcludedFiles.RemoveAt(index);
        else
            ExcludedFiles.Add(filePath);
    }

    public bool IsMonitorDisabled(string deviceName) =>
        DisabledMonitors.Any(d => string.Equals(d, deviceName, StringComparison.OrdinalIgnoreCase));

    public void ToggleMonitor(string deviceName)
    {
        var index = DisabledMonitors.FindIndex(d => string.Equals(d, deviceName, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
            DisabledMonitors.RemoveAt(index);
        else
            DisabledMonitors.Add(deviceName);
    }

    public static ScreensaverConfig Load()
    {
        if (!File.Exists(ConfigPath))
            return new ScreensaverConfig();

        try
        {
            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<ScreensaverConfig>(json, JsonOptions) ?? new ScreensaverConfig();
        }
        catch
        {
            return new ScreensaverConfig();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(ConfigDir);
        var json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(ConfigPath, json);
    }

    // --- Persistent history ---

    private static readonly string HistoryPath = Path.Combine(ConfigDir, "history.json");
    private const int MaxHistoryEntries = 500;

    public static List<(string Path, DateTime ShownAt)> LoadHistory()
    {
        if (!File.Exists(HistoryPath))
            return new List<(string, DateTime)>();

        try
        {
            var json = File.ReadAllText(HistoryPath);
            var entries = JsonSerializer.Deserialize<List<HistoryEntry>>(json, JsonOptions);
            return entries?.Select(e => (e.Path, e.ShownAt)).ToList()
                   ?? new List<(string, DateTime)>();
        }
        catch
        {
            return new List<(string, DateTime)>();
        }
    }

    public static void SaveHistory(List<(string Path, DateTime ShownAt)> history)
    {
        Directory.CreateDirectory(ConfigDir);
        var trimmed = history.Count > MaxHistoryEntries
            ? history.Skip(history.Count - MaxHistoryEntries).ToList()
            : history;
        var entries = trimmed.Select(h => new HistoryEntry { Path = h.Path, ShownAt = h.ShownAt }).ToList();
        var json = JsonSerializer.Serialize(entries, JsonOptions);
        File.WriteAllText(HistoryPath, json);
    }

    private class HistoryEntry
    {
        public string Path { get; set; } = "";
        public DateTime ShownAt { get; set; }
    }
}

// --- Pending browse marker ---

public class PendingBrowse
{
    private static readonly string MarkerPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BetterSlideshowScreensaver", "pending-browse.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string ImagePath { get; set; } = "";
    public string FolderPath { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public bool ShowHistory { get; set; }

    public static void Save(string imagePath, string folderPath, bool showHistory = false)
    {
        var dir = Path.GetDirectoryName(MarkerPath)!;
        Directory.CreateDirectory(dir);
        var marker = new PendingBrowse
        {
            ImagePath = imagePath,
            FolderPath = folderPath,
            Timestamp = DateTime.UtcNow,
            ShowHistory = showHistory
        };
        var json = JsonSerializer.Serialize(marker, JsonOptions);
        File.WriteAllText(MarkerPath, json);
    }

    public static PendingBrowse? LoadAndDelete()
    {
        if (!File.Exists(MarkerPath))
            return null;

        try
        {
            var json = File.ReadAllText(MarkerPath);
            File.Delete(MarkerPath);
            return JsonSerializer.Deserialize<PendingBrowse>(json, JsonOptions);
        }
        catch
        {
            try { File.Delete(MarkerPath); } catch { }
            return null;
        }
    }

    public static PendingBrowse? PeekStale(TimeSpan maxAge)
    {
        if (!File.Exists(MarkerPath))
            return null;

        try
        {
            var json = File.ReadAllText(MarkerPath);
            var marker = JsonSerializer.Deserialize<PendingBrowse>(json, JsonOptions);
            if (marker != null && DateTime.UtcNow - marker.Timestamp < maxAge)
                return marker;
            File.Delete(MarkerPath);
            return null;
        }
        catch
        {
            try { File.Delete(MarkerPath); } catch { }
            return null;
        }
    }
}
