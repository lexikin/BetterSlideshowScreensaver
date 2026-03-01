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
}
