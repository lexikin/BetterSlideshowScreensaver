using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Win32;

namespace BetterSlideshowScreensaver;

public static class Installer
{
    private const string ScrName = "BetterSlideshowScreensaver.scr";

    public static string InstalledPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), ScrName);

    public static bool IsRunningFromSystem32 =>
        string.Equals(
            Path.GetDirectoryName(Environment.ProcessPath),
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            StringComparison.OrdinalIgnoreCase);

    public static bool IsInstalled =>
        File.Exists(InstalledPath);

    public static bool IsUpToDate
    {
        get
        {
            if (!IsInstalled) return false;
            try { return FilesMatch(Environment.ProcessPath!, InstalledPath); }
            catch { return false; }
        }
    }

    public static bool IsActiveScreensaver()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
            var value = key?.GetValue("SCRNSAVE.EXE") as string;
            return !string.IsNullOrEmpty(value) &&
                   string.Equals(value, InstalledPath, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static void SetAsActiveScreensaver()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", writable: true);
        key?.SetValue("SCRNSAVE.EXE", InstalledPath);
    }

    /// <summary>
    /// Shows an install/update form if the screensaver is not installed, outdated, or not active.
    /// </summary>
    public static bool TryInstallToSystem32()
    {
        if (IsRunningFromSystem32) return false;

        var needsCopy = !IsInstalled || !IsUpToDate;
        var needsActivate = !IsActiveScreensaver();

        if (!needsCopy && !needsActivate) return false;

        using var form = new InstallForm(needsCopy, needsActivate);
        if (form.ShowDialog() != DialogResult.OK) return false;

        if (form.InstallChecked)
        {
            if (!CopyToSystem32(Environment.ProcessPath!))
                return false;
        }

        if (form.SetActiveChecked && (IsInstalled || form.InstallChecked))
            SetAsActiveScreensaver();

        return true;
    }

    public static bool CopyToSystem32(string sourcePath)
    {
        // Try direct copy first (in case already elevated)
        try
        {
            File.Copy(sourcePath, InstalledPath, overwrite: true);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            // Need elevation — re-launch with runas
            return LaunchElevatedInstall(sourcePath);
        }
    }

    private static bool FilesMatch(string pathA, string pathB)
    {
        try
        {
            var hashA = HashFile(pathA);
            var hashB = HashFile(pathB);
            return hashA.SequenceEqual(hashB);
        }
        catch
        {
            return false; // if we can't read either file, treat as mismatch
        }
    }

    private static byte[] HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return SHA256.HashData(stream);
    }

    /// <summary>
    /// Called with /install flag from an elevated process.
    /// </summary>
    public static bool RunElevatedInstall()
    {
        try
        {
            // The source path is passed as the second arg
            var args = Environment.GetCommandLineArgs();
            if (args.Length < 3) return false;
            var sourcePath = args[2];
            File.Copy(sourcePath, InstalledPath, overwrite: true);
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Install failed: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private static bool LaunchElevatedInstall(string sourcePath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = Environment.ProcessPath!,
                Arguments = $"/install \"{sourcePath}\"",
                Verb = "runas",
                UseShellExecute = true
            };
            var proc = Process.Start(psi);
            proc?.WaitForExit();
            return proc?.ExitCode == 0;
        }
        catch
        {
            // User declined UAC or other error
            return false;
        }
    }
}
