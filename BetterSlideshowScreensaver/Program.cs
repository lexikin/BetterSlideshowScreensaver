namespace BetterSlideshowScreensaver;

static class Program
{
    // The single-instance mutex and the show-history event are held for the entire
    // lifetime of the tray process. They MUST be rooted in static fields — if they
    // were locals they would become GC-eligible the moment the method returned, and
    // their finalizers would close the underlying named handles (silently dropping
    // single-instance protection and destroying the event other processes signal).
    private static Mutex? _trayMutex;
    private static EventWaitHandle? _trayEvent;

    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        // Self-install to System32 if not already there (no-op when already running
        // from System32, e.g. the OS-launched screensaver or the autostart tray).
        // Returns true only when the setup dialog just ran and was accepted — i.e. a
        // manual first-run of the downloaded binary. Open Settings straight away so the
        // user can pick an image folder, instead of falling through to launching the
        // screensaver with nothing configured.
        if (Installer.TryInstallToSystem32())
        {
            Application.Run(new ConfigForm());
            return;
        }

        var firstArg = args.Length > 0 ? args[0].ToLowerInvariant().TrimStart('-', '/') : "";

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

            case "tray":
                RunTray();
                break;

            case "install":
                Installer.RunElevatedInstall();
                return;

            default:
                ScreensaverController.Run();
                break;
        }
    }

    /// <summary>
    /// Runs the persistent tray process (started at logon and at install). Enforces a
    /// single instance: if another tray already owns the mutex, signals it to pick up
    /// any pending marker and exits.
    /// </summary>
    private static void RunTray()
    {
        _trayMutex = new Mutex(initiallyOwned: true, TrayApplicationContext.MutexName, out var createdNew);
        if (!createdNew)
        {
            // Another tray is already running — hand off and exit.
            TrayLauncher.SignalExisting();
            return;
        }

        _trayEvent = new EventWaitHandle(false, EventResetMode.AutoReset,
            TrayApplicationContext.ShowHistoryEventName);

        // Re-assert the logon autostart entry (self-healing after an in-place update).
        if (Installer.IsRunningFromSystem32)
            Installer.RegisterTrayAutostart();

        var config = ScreensaverConfig.Load();
        Application.Run(new TrayApplicationContext(config, _trayEvent));

        GC.KeepAlive(_trayMutex);
        GC.KeepAlive(_trayEvent);
    }
}
