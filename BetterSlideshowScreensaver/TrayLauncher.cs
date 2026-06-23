using System.Diagnostics;

namespace BetterSlideshowScreensaver;

/// <summary>
/// Locates, signals, or starts the persistent tray process. The tray hosts the
/// notify icon, history window and auto-updater, and outlives any single screensaver
/// run. The screensaver never spawns UI itself — it hands off to the tray via a
/// pending-browse marker plus the named show-history event.
/// </summary>
public static class TrayLauncher
{
    /// <summary>True if a tray process is currently running (owns the named event).</summary>
    public static bool IsRunning()
    {
        try
        {
            using var evt = EventWaitHandle.OpenExisting(TrayApplicationContext.ShowHistoryEventName);
            return true;
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            return false;
        }
    }

    /// <summary>Starts the tray if it is not already running.</summary>
    public static void EnsureRunning()
    {
        if (!IsRunning())
            Start();
    }

    /// <summary>
    /// Signals the running tray to pick up the pending-browse marker (and show history
    /// if the marker requests it). If no tray is running, starts one — it reads the
    /// marker on startup. Either way no UI is created in the calling process.
    /// </summary>
    public static void EnsureRunningAndSignal()
    {
        if (SignalExisting())
            return;

        // No tray running — start one; it consumes the marker on startup.
        Start();
    }

    /// <summary>Signals an already-running tray. Returns false if none is running.</summary>
    public static bool SignalExisting()
    {
        try
        {
            using var evt = EventWaitHandle.OpenExisting(TrayApplicationContext.ShowHistoryEventName);
            evt.Set();
            return true;
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            return false;
        }
    }

    private static void Start()
    {
        try
        {
            // Prefer the stable System32 copy so the persistent tray never pins a
            // build/worktree path open.
            var path = Installer.IsInstalled ? Installer.InstalledPath : Environment.ProcessPath!;
            Process.Start(path, "/tray");
        }
        catch
        {
            // Best effort — a missing tray is non-fatal; it will be retried on the
            // next screensaver run or logon.
        }
    }
}
