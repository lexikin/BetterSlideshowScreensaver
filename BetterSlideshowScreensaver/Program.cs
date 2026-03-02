using Microsoft.Win32;

namespace BetterSlideshowScreensaver;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        // Self-install to System32 if not already there
        Installer.TryInstallToSystem32();

        // Check for stale pending-browse marker (< 24h) before handling normal args.
        // Covers the case where the /browse process was killed before it could show the form.
        var stale = PendingBrowse.PeekStale(TimeSpan.FromHours(24));
        if (stale != null)
        {
            // Consume it so we don't loop
            PendingBrowse.LoadAndDelete();
            if (SignalExistingInstance(stale.ShowHistory))
                return;
            var config = ScreensaverConfig.Load();
            var trayCtx = new TrayApplicationContext(config, stale.FolderPath, stale.ImagePath);
            if (stale.ShowHistory)
                trayCtx.ShowHistory();
            Application.Run(trayCtx);
            return;
        }

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

            case "browse":
                HandleBrowse();
                break;

            case "install":
                Installer.RunElevatedInstall();
                return;

            default:
                ScreensaverController.Run();
                break;
        }
    }

    private static void HandleBrowse()
    {
        var pending = PendingBrowse.LoadAndDelete();
        if (pending == null) return;

        // If another tray instance is already running, signal it and exit
        if (SignalExistingInstance(pending.ShowHistory))
            return;

        var config = ScreensaverConfig.Load();
        var trayCtx = new TrayApplicationContext(config, pending.FolderPath, pending.ImagePath);

        if (NativeMethods.IsDesktopLocked())
        {
            if (pending.ShowHistory)
            {
                // Wait for desktop unlock, then show the history window
                SystemEvents.SessionSwitch += (s, e) =>
                {
                    if (e.Reason == SessionSwitchReason.SessionUnlock)
                        trayCtx.ShowHistory();
                };
            }
            Application.Run(trayCtx);
        }
        else
        {
            if (pending.ShowHistory)
                trayCtx.ShowHistory();
            Application.Run(trayCtx);
        }
    }

    /// <summary>
    /// Tries to acquire the tray mutex. If already held by another instance,
    /// signals it to show history (if requested) and returns true.
    /// If acquired, creates the show-history event and keeps the mutex alive.
    /// </summary>
    private static bool SignalExistingInstance(bool showHistory)
    {
        var mutex = new Mutex(true, TrayApplicationContext.MutexName, out var createdNew);
        if (createdNew)
        {
            // We own the mutex — create the event for future signals and keep both alive
            _ = new EventWaitHandle(false, EventResetMode.AutoReset,
                TrayApplicationContext.ShowHistoryEventName);
            // Don't dispose mutex — it must stay alive for the process lifetime
            return false;
        }

        // Another instance owns the mutex — signal it
        mutex.Dispose();
        if (showHistory)
        {
            try
            {
                var evt = EventWaitHandle.OpenExisting(TrayApplicationContext.ShowHistoryEventName);
                evt.Set();
                evt.Dispose();
            }
            catch (WaitHandleCannotBeOpenedException) { }
        }
        return true;
    }
}
