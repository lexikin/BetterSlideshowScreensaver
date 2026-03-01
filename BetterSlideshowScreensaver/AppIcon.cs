using System.Drawing;

namespace BetterSlideshowScreensaver;

public static class AppIcon
{
    private static Icon? _cached;

    public static Icon? Load()
    {
        if (_cached != null) return _cached;
        try
        {
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe))
                _cached = Icon.ExtractAssociatedIcon(exe);
        }
        catch
        {
            // Silently ignore — form will use default icon
        }
        return _cached;
    }
}
