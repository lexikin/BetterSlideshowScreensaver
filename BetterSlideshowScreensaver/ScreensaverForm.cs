namespace BetterSlideshowScreensaver;

public class ScreensaverForm : Form
{
    private Image? _currentImage;
    private Point _initialMousePosition;
    private bool _mousePositionSet;
    private const int MouseMoveThreshold = 5;

    public event EventHandler? DismissRequested;
    public event EventHandler? CtrlDismissRequested;

    public ScreensaverForm(Screen screen)
    {
        SetupFullScreen(screen.Bounds);
    }

    public ScreensaverForm(IntPtr previewHwnd)
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;

        NativeMethods.SetParent(Handle, previewHwnd);
        NativeMethods.SetWindowLong(Handle, NativeMethods.GWL_STYLE,
            NativeMethods.GetWindowLong(Handle, NativeMethods.GWL_STYLE) | NativeMethods.WS_CHILD);

        NativeMethods.GetClientRect(previewHwnd, out var rect);
        ClientSize = new Size(rect.Right - rect.Left, rect.Bottom - rect.Top);
        Location = new Point(0, 0);

        SetupCommon();
    }

    private void SetupFullScreen(Rectangle bounds)
    {
        FormBorderStyle = FormBorderStyle.None;
        WindowState = FormWindowState.Normal;
        StartPosition = FormStartPosition.Manual;
        Bounds = bounds;
        TopMost = true;
        ShowInTaskbar = false;
        Cursor.Hide();

        SetupCommon();

        var lines = new List<string>();
        if (!Installer.IsInstalled || !Installer.IsUpToDate)
            lines.Add("Screensaver not installed");
        if (!Installer.IsActiveScreensaver())
            lines.Add("Not set as active screensaver");

        if (lines.Count > 0)
        {
            var warning = new Label
            {
                Text = string.Join("\n", lines),
                AutoSize = true,
                ForeColor = Color.FromArgb(180, Color.White),
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 9f),
                TextAlign = ContentAlignment.BottomRight,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            Controls.Add(warning);
            warning.BringToFront();
            Layout += (_, _) =>
            {
                warning.Location = new Point(
                    ClientSize.Width - warning.Width - 12,
                    ClientSize.Height - warning.Height - 8);
            };
        }

        KeyDown += OnKeyDown;
        MouseMove += OnMouseMove;
        MouseClick += (_, _) => DismissRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SetupCommon()
    {
        BackColor = Color.Black;
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    public void ShowImage(Image? image)
    {
        var old = _currentImage;
        _currentImage = image;
        old?.Dispose();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.Clear(Color.Black);

        if (_currentImage == null)
            return;

        e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

        var destRect = CalculateFitRect(_currentImage.Width, _currentImage.Height, ClientSize.Width, ClientSize.Height);
        e.Graphics.DrawImage(_currentImage, destRect);
    }

    private static Rectangle CalculateFitRect(int imgWidth, int imgHeight, int areaWidth, int areaHeight)
    {
        var ratioX = (double)areaWidth / imgWidth;
        var ratioY = (double)areaHeight / imgHeight;
        var ratio = Math.Min(ratioX, ratioY);

        var newWidth = (int)(imgWidth * ratio);
        var newHeight = (int)(imgHeight * ratio);
        var x = (areaWidth - newWidth) / 2;
        var y = (areaHeight - newHeight) / 2;

        return new Rectangle(x, y, newWidth, newHeight);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.LControlKey || e.KeyCode == Keys.RControlKey)
            CtrlDismissRequested?.Invoke(this, EventArgs.Empty);
        else
            DismissRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        if (!_mousePositionSet)
        {
            _initialMousePosition = e.Location;
            _mousePositionSet = true;
            return;
        }

        var dx = Math.Abs(e.Location.X - _initialMousePosition.X);
        var dy = Math.Abs(e.Location.Y - _initialMousePosition.Y);

        if (dx > MouseMoveThreshold || dy > MouseMoveThreshold)
            DismissRequested?.Invoke(this, EventArgs.Empty);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _currentImage?.Dispose();
            _currentImage = null;
        }
        base.Dispose(disposing);
    }
}
