namespace BetterSlideshowScreensaver;

public static class ImageScanner
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".tif", ".webp"
    };

    public static List<string> ScanAndSort(string folderPath, IReadOnlyCollection<string>? excludedFiles = null)
    {
        if (!Directory.Exists(folderPath))
            return new List<string>();

        var excluded = excludedFiles is { Count: > 0 }
            ? new HashSet<string>(excludedFiles, StringComparer.OrdinalIgnoreCase)
            : null;

        var files = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
            .Where(f => ImageExtensions.Contains(Path.GetExtension(f)))
            .Where(f => excluded == null || !excluded.Contains(f))
            .Select(f => (Path: f, Time: File.GetLastWriteTimeUtc(f)))
            .ToList();

        files.Sort((a, b) => a.Time.CompareTo(b.Time));

        return files.Select(f => f.Path).ToList();
    }
}
