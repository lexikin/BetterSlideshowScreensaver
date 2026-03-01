namespace BetterSlideshowScreensaver;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        if (args.Length == 0)
        {
            ScreensaverController.Run();
            return;
        }

        var firstArg = args[0].ToLowerInvariant().TrimStart('-', '/');

        // Windows passes /c:HWND for configure — strip everything after the flag letter
        var flag = firstArg.Split(':')[0];

        switch (flag)
        {
            case "c":
                Application.Run(new ConfigForm());
                break;

            case "s":
                ScreensaverController.Run();
                break;

            case "p":
                IntPtr hwnd = IntPtr.Zero;
                var parts = firstArg.Split(':');
                if (parts.Length > 1 && IntPtr.TryParse(parts[1], out hwnd)) { }
                else if (args.Length > 1 && IntPtr.TryParse(args[1], out hwnd)) { }

                if (hwnd != IntPtr.Zero)
                    ScreensaverController.RunPreview(hwnd);
                break;

            default:
                ScreensaverController.Run();
                break;
        }
    }
}
