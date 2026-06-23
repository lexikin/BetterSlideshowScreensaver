namespace BetterSlideshowScreensaver;

public class TrayApplicationContext : ApplicationContext
{
    public const string MutexName = "BetterSlideshowScreensaver_Tray";
    public const string ShowHistoryEventName = "BetterSlideshowScreensaver_ShowHistory";

    private readonly NotifyIcon _notifyIcon;
    private readonly ScreensaverConfig _config;
    private string _folderPath;
    private string _selectedImagePath = "";
    private HistoryForm? _historyForm;
    private readonly System.Windows.Forms.Timer _signalTimer;
    private readonly System.Windows.Forms.Timer _updateTimer;
    private readonly EventWaitHandle _showHistoryEvent;

    public TrayApplicationContext(ScreensaverConfig config, EventWaitHandle showHistoryEvent)
    {
        _config = config;
        _showHistoryEvent = showHistoryEvent;
        _folderPath = config.ImageFolderPath;

        // A marker may already be waiting (e.g. the tray was started by a screensaver
        // dismiss). Honor it now, including showing history if requested.
        ConsumePendingBrowse();

        // Poll for show-history signals from screensaver/other processes.
        _signalTimer = new System.Windows.Forms.Timer { Interval = 500 };
        _signalTimer.Tick += (_, _) =>
        {
            if (_showHistoryEvent.WaitOne(0))
                ConsumePendingBrowse();
        };
        _signalTimer.Start();

        _notifyIcon = new NotifyIcon
        {
            Icon = AppIcon.Load(),
            Text = "Better Slideshow Screensaver",
            Visible = true
        };
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                ShowHistory();
        };

        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("History", null, (_, _) => ShowHistory());
        trayMenu.Items.Add("Configure", null, (_, _) =>
        {
            using var configForm = new ConfigForm();
            configForm.ShowDialog(_historyForm);
        });
        trayMenu.Items.Add("Test", null, (_, _) =>
        {
            System.Diagnostics.Process.Start(Environment.ProcessPath!, "/s");
        });
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add("Exit", null, (_, _) =>
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            ExitThread();
        });
        _notifyIcon.ContextMenuStrip = trayMenu;

        // Auto-update: check immediately, then every 4 hours
        _ = Task.Run(() => AutoUpdater.RunUpdateCycleAsync(_notifyIcon));
        _updateTimer = new System.Windows.Forms.Timer { Interval = 4 * 60 * 60 * 1000 };
        _updateTimer.Tick += (_, _) => _ = Task.Run(() => AutoUpdater.RunUpdateCycleAsync(_notifyIcon));
        _updateTimer.Start();
    }

    /// <summary>
    /// Reads the pending-browse marker left by a screensaver dismiss, updates the
    /// folder/selected image the history window will open to, and shows history if the
    /// marker requests it (Ctrl-dismiss). No-op when there is no marker.
    /// </summary>
    private void ConsumePendingBrowse()
    {
        var pending = PendingBrowse.LoadAndDelete();
        if (pending == null)
            return;

        if (!string.IsNullOrEmpty(pending.FolderPath))
            _folderPath = pending.FolderPath;
        _selectedImagePath = pending.ImagePath;

        if (pending.ShowHistory)
            ShowHistory();
    }

    public void ShowHistory()
    {
        if (_historyForm == null || _historyForm.IsDisposed)
        {
            _historyForm = new HistoryForm(_folderPath, _selectedImagePath, _config);
            _historyForm.FormClosed += (_, _) => _historyForm = null;
            _historyForm.Show();
        }
        else
        {
            if (_historyForm.WindowState == FormWindowState.Minimized)
                _historyForm.WindowState = FormWindowState.Normal;
            _historyForm.Activate();
        }
    }
}
