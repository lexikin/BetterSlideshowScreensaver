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

        switch (firstArg)
        {
            case "c":
                Application.Run(new ConfigForm());
                break;

            case "s":
                ScreensaverController.Run();
                break;

            case "p":
                if (args.Length > 1 && IntPtr.TryParse(args[1], out var hwnd))
                {
                    ScreensaverController.RunPreview(hwnd);
                }
                break;

            default:
                ScreensaverController.Run();
                break;
        }
    }
}
