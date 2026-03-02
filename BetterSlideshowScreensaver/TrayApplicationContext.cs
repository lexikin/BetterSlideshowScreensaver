namespace BetterSlideshowScreensaver;

public class TrayApplicationContext : ApplicationContext
{
    public const string MutexName = "BetterSlideshowScreensaver_Tray";
    public const string ShowHistoryEventName = "BetterSlideshowScreensaver_ShowHistory";

    private readonly NotifyIcon _notifyIcon;
    private readonly ScreensaverConfig _config;
    private readonly string _folderPath;
    private readonly string _selectedImagePath;
    private HistoryForm? _historyForm;
    private readonly System.Windows.Forms.Timer _signalTimer;

    public TrayApplicationContext(ScreensaverConfig config, string folderPath, string selectedImagePath)
    {
        _config = config;
        _folderPath = folderPath;
        _selectedImagePath = selectedImagePath;

        // Poll for show-history signals from other instances
        var showHistoryEvent = EventWaitHandle.OpenExisting(ShowHistoryEventName);
        _signalTimer = new System.Windows.Forms.Timer { Interval = 500 };
        _signalTimer.Tick += (_, _) =>
        {
            if (showHistoryEvent.WaitOne(0))
                ShowHistory();
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
