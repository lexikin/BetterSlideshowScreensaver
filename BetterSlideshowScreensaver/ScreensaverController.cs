using System.Diagnostics;

namespace BetterSlideshowScreensaver;

public static class ScreensaverController
{
    private static List<string> _images = new();
    private static int _currentIndex;
    private static string _currentImagePath = "";
    private static readonly List<(ScreensaverForm Form, Screen Screen)> Forms = new();
    private static System.Windows.Forms.Timer? _timer;
    private static ScreensaverConfig _config = new();
    private static readonly Random Rng = new();
    private static List<(string Path, DateTime ShownAt)> _history = new();
    private static ApplicationContext? _appContext;

    public static void Run()
    {
        _config = ScreensaverConfig.Load();

        if (string.IsNullOrEmpty(_config.ImageFolderPath) || !Directory.Exists(_config.ImageFolderPath))
        {
            MessageBox.Show("Please configure an image folder first.", "Better Slideshow Screensaver",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            Application.Run(new ConfigForm());
            return;
        }

        _images = ImageScanner.ScanAndSort(_config.ImageFolderPath, _config.ExcludedFiles);

        if (_images.Count == 0)
        {
            MessageBox.Show("No images found in the configured folder.", "Better Slideshow Screensaver",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // Load persisted history from previous sessions
        _history = ScreensaverConfig.LoadHistory();

        _currentIndex = Rng.Next(_images.Count);

        CreateForms();
        ShowCurrentSlide();

        _timer = new System.Windows.Forms.Timer { Interval = _config.SlideIntervalSeconds * 1000 };
        _timer.Tick += (_, _) => AdvanceSlide();
        _timer.Start();

        // Use an ApplicationContext with no MainForm so we control the
        // lifetime ourselves. This lets us transition from the screensaver
        // forms to the file browser within a single message loop.
        _appContext = new ApplicationContext();
        Application.Run(_appContext);
    }

    public static void RunPreview(IntPtr previewHwnd)
    {
        _config = ScreensaverConfig.Load();

        var form = new ScreensaverForm(previewHwnd);
        Forms.Add((form, Screen.PrimaryScreen!));

        if (!string.IsNullOrEmpty(_config.ImageFolderPath) && Directory.Exists(_config.ImageFolderPath))
        {
            _images = ImageScanner.ScanAndSort(_config.ImageFolderPath, _config.ExcludedFiles);
            if (_images.Count > 0)
            {
                _currentIndex = Rng.Next(_images.Count);
                ShowCurrentSlide();

                _timer = new System.Windows.Forms.Timer { Interval = _config.SlideIntervalSeconds * 1000 };
                _timer.Tick += (_, _) => AdvanceSlide();
                _timer.Start();
            }
        }

        Application.Run(form);
    }

    private static void CreateForms()
    {
        var screens = Screen.AllScreens;
        // Put primary screen first
        var ordered = screens.OrderByDescending(s => s.Primary).ToArray();

        foreach (var screen in ordered)
        {
            var form = new ScreensaverForm(screen);
            form.DismissRequested += (_, _) => Dismiss(openFileBrowser: false);
            form.CtrlDismissRequested += (_, _) => Dismiss(openFileBrowser: true);
            Forms.Add((form, screen));
        }

        // Show all forms — we use ApplicationContext so nothing is auto-shown
        foreach (var (form, _) in Forms)
            form.Show();
    }

    private static void ShowCurrentSlide()
    {
        if (_images.Count == 0 || Forms.Count == 0)
            return;

        _currentImagePath = _images[_currentIndex];
        _history.Add((_currentImagePath, DateTime.Now));

        for (var i = 0; i < Forms.Count; i++)
        {
            var (form, screen) = Forms[i];
            if (_config.IsMonitorDisabled(screen.DeviceName))
                form.ShowImage(null);
            else
            {
                var idx = (_currentIndex + i) % _images.Count;
                form.ShowImage(LoadImage(_images[idx]));
            }
        }
    }

    private static void AdvanceSlide()
    {
        _currentIndex = (_currentIndex + 1) % _images.Count;

        // If we've wrapped around (completed full cycle), pick new random start
        if (_currentIndex == 0)
            _currentIndex = Rng.Next(_images.Count);

        ShowCurrentSlide();
    }

    private static Image? LoadImage(string path)
    {
        try
        {
            var bytes = File.ReadAllBytes(path);
            var ms = new MemoryStream(bytes);
            return Image.FromStream(ms);
        }
        catch
        {
            return null;
        }
    }

    private static void Dismiss(bool openFileBrowser)
    {
        _timer?.Stop();
        _timer?.Dispose();

        // Always persist history on dismiss
        ScreensaverConfig.SaveHistory(_history);

        foreach (var (form, _) in Forms)
        {
            if (!form.IsDisposed)
                form.Close();
        }

        // Cursor.Hide() was called once per screensaver form, so we
        // need a matching Cursor.Show() for each to restore visibility.
        for (var i = 0; i < Forms.Count; i++)
            Cursor.Show();

        // Always launch the tray process. If Ctrl was held, also show history.
        PendingBrowse.Save(_currentImagePath, _config.ImageFolderPath, showHistory: openFileBrowser);
        Process.Start(Environment.ProcessPath!, "/browse");

        _appContext?.ExitThread();
    }
}
