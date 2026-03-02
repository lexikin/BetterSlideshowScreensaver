using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace BetterSlideshowScreensaver;

public static class AutoUpdater
{
    private const string RepoApiUrl =
        "https://api.github.com/repos/lexikin/BetterSlideshowScreensaver/releases/latest";

    private static readonly HttpClient Http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("BetterSlideshowScreensaver", "1.0"));
        return client;
    }

    public static string GetCurrentTag()
    {
        return Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "dev";
    }

    public static async Task RunUpdateCycleAsync(NotifyIcon notifyIcon)
    {
        try
        {
            var currentTag = GetCurrentTag();
            if (currentTag == "dev") return;

            var (latestTag, assetUrl) = await CheckForUpdateAsync();
            if (latestTag == null || assetUrl == null || latestTag == currentTag) return;

            var tempPath = await DownloadUpdateAsync(assetUrl);
            if (tempPath == null) return;

            if (ApplyUpdate(tempPath))
            {
                notifyIcon.ShowBalloonTip(5000, "Screensaver Updated",
                    $"Screensaver updated to {latestTag}", ToolTipIcon.Info);
            }
        }
        catch
        {
            // Silently ignore update errors
        }
    }

    private static async Task<(string? tag, string? assetUrl)> CheckForUpdateAsync()
    {
        var response = await Http.GetAsync(RepoApiUrl);
        if (!response.IsSuccessStatusCode) return (null, null);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var tag = root.GetProperty("tag_name").GetString();

        string? assetUrl = null;
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString();
            if (name != null && name.EndsWith(".scr", StringComparison.OrdinalIgnoreCase))
            {
                assetUrl = asset.GetProperty("browser_download_url").GetString();
                break;
            }
        }

        return (tag, assetUrl);
    }

    private static async Task<string?> DownloadUpdateAsync(string url)
    {
        var response = await Http.GetAsync(url);
        if (!response.IsSuccessStatusCode) return null;

        var tempPath = Path.Combine(Path.GetTempPath(), "BetterSlideshowScreensaver_update.scr");
        await using var fs = File.Create(tempPath);
        await response.Content.CopyToAsync(fs);
        return tempPath;
    }

    private static bool ApplyUpdate(string tempPath)
    {
        try
        {
            return Installer.CopyToSystem32(tempPath);
        }
        finally
        {
            try { File.Delete(tempPath); } catch { }
        }
    }
}
