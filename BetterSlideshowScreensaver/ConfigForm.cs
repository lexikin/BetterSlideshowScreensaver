namespace BetterSlideshowScreensaver;

public class ConfigForm : Form
{
    private readonly TextBox _folderPathTextBox;
    private readonly NumericUpDown _intervalNumeric;
    private readonly MonitorPreviewPanel _monitorPanel;

    public ConfigForm()
    {
        Text = "Grenzer's Less Shitty Slideshow Screensaver - Settings";
        Icon = AppIcon.Load();
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(480, 420);

        var folderLabel = new Label { Text = "Image Folder:", Location = new Point(12, 18), AutoSize = true };
        _folderPathTextBox = new TextBox { Location = new Point(120, 15), Width = 270 };
        var browseButton = new Button { Text = "...", Location = new Point(396, 14), Width = 30 };
        browseButton.Click += BrowseButton_Click;

        var intervalLabel = new Label { Text = "Slide Interval (s):", Location = new Point(12, 53), AutoSize = true };
        _intervalNumeric = new NumericUpDown
        {
            Location = new Point(120, 50),
            Width = 80,
            Minimum = 1,
            Maximum = 300,
            Value = 8
        };

        _monitorPanel = new MonitorPreviewPanel
        {
            Location = new Point(12, 85),
            Size = new Size(456, 250)
        };

        var tipLabel = new Label
        {
            Text = "Tip: Press Ctrl while the screensaver is running to get the current screensaver",
            Location = new Point(12, 345),
            AutoSize = false,
            Size = new Size(450, 20),
            ForeColor = SystemColors.GrayText
        };

        var okButton = new Button { Text = "OK", Location = new Point(296, 380), Width = 80, DialogResult = DialogResult.OK };
        var cancelButton = new Button { Text = "Cancel", Location = new Point(384, 380), Width = 80 };
        okButton.Click += OkButton_Click;
        cancelButton.Click += (_, _) => Close();

        AcceptButton = okButton;
        CancelButton = cancelButton;

        Controls.AddRange(new Control[]
        {
            folderLabel, _folderPathTextBox, browseButton,
            intervalLabel, _intervalNumeric,
            _monitorPanel,
            tipLabel,
            okButton, cancelButton
        });

        _folderPathTextBox.TextChanged += (_, _) => ScanAndUpdateImages();

        LoadConfig();
    }

    private void LoadConfig()
    {
        var config = ScreensaverConfig.Load();
        _folderPathTextBox.Text = config.ImageFolderPath;
        _intervalNumeric.Value = Math.Clamp(config.SlideIntervalSeconds, 1, 300);
        _monitorPanel.SetDisabledMonitors(config.DisabledMonitors);
        ScanAndUpdateImages();
    }

    private void ScanAndUpdateImages()
    {
        var folder = _folderPathTextBox.Text;
        if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
        {
            var images = ImageScanner.ScanAndSort(folder);
            _monitorPanel.SetImages(images);
        }
        else
        {
            _monitorPanel.SetImages(new List<string>());
        }
    }

    private void BrowseButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select the folder containing your images",
            ShowNewFolderButton = false
        };

        if (!string.IsNullOrEmpty(_folderPathTextBox.Text) && Directory.Exists(_folderPathTextBox.Text))
            dialog.SelectedPath = _folderPathTextBox.Text;

        if (dialog.ShowDialog(this) == DialogResult.OK)
            _folderPathTextBox.Text = dialog.SelectedPath;
    }

    private void OkButton_Click(object? sender, EventArgs e)
    {
        var config = ScreensaverConfig.Load();
        config.ImageFolderPath = _folderPathTextBox.Text;
        config.SlideIntervalSeconds = (int)_intervalNumeric.Value;
        config.DisabledMonitors = _monitorPanel.GetDisabledMonitors();
        config.Save();
        Close();
    }

    private class MonitorPreviewPanel : Panel
    {
        private readonly Screen[] _screens;
        private readonly HashSet<string> _disabledSet = new(StringComparer.OrdinalIgnoreCase);
        private Image? _bezelImage;
        private Rectangle[] _monitorRects = Array.Empty<Rectangle>();
        private Rectangle[] _screenRects = Array.Empty<Rectangle>();
        private int _hoveredIndex = -1;
        private List<string> _images = new();
        private int _currentImageIndex;
        private Image?[] _previewImages = Array.Empty<Image?>();
        private readonly System.Windows.Forms.Timer _previewTimer;
        private static readonly Random Rng = new();

        public MonitorPreviewPanel()
        {
            _screens = Screen.AllScreens;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

            LoadBezelImage();

            _previewTimer = new System.Windows.Forms.Timer { Interval = 3000 };
            _previewTimer.Tick += (_, _) => AdvancePreview();
            _previewTimer.Start();
        }

        private void LoadBezelImage()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "monitor.png");
            if (File.Exists(path))
            {
                try
                {
                    var bytes = File.ReadAllBytes(path);
                    _bezelImage = Image.FromStream(new MemoryStream(bytes));
                }
                catch { }
            }
        }

        public void SetDisabledMonitors(List<string> disabled)
        {
            _disabledSet.Clear();
            foreach (var d in disabled)
                _disabledSet.Add(d);
            Invalidate();
        }

        public List<string> GetDisabledMonitors() => _disabledSet.ToList();

        public void SetImages(List<string> images)
        {
            _images = images;
            _currentImageIndex = images.Count > 0 ? Rng.Next(images.Count) : 0;
            DisposePreviewImages();
            if (_images.Count > 0)
                LoadPreviewImages();
            Invalidate();
        }

        private void DisposePreviewImages()
        {
            foreach (var img in _previewImages)
                img?.Dispose();
            _previewImages = new Image?[_screens.Length];
        }

        private static Image? LoadImageFromFile(string path)
        {
            try
            {
                var bytes = File.ReadAllBytes(path);
                return Image.FromStream(new MemoryStream(bytes));
            }
            catch
            {
                return null;
            }
        }

        private void LoadPreviewImages()
        {
            if (_images.Count == 0) return;
            for (var i = 0; i < _screens.Length; i++)
            {
                _previewImages[i]?.Dispose();
                var idx = (_currentImageIndex + i) % _images.Count;
                _previewImages[i] = LoadImageFromFile(_images[idx]);
            }
        }

        private void AdvancePreview()
        {
            if (_images.Count == 0) return;
            _currentImageIndex = (_currentImageIndex + 1) % _images.Count;
            LoadPreviewImages();
            Invalidate();
        }

        private void ComputeLayout()
        {
            if (_screens.Length == 0) return;

            // Get bounding box of all screens in virtual-screen coords
            var minX = _screens.Min(s => s.Bounds.Left);
            var minY = _screens.Min(s => s.Bounds.Top);
            var maxX = _screens.Max(s => s.Bounds.Right);
            var maxY = _screens.Max(s => s.Bounds.Bottom);

            var totalWidth = maxX - minX;
            var totalHeight = maxY - minY;

            var padding = 20;
            var availW = Width - padding * 2;
            var availH = Height - padding * 2;

            // Scale proportionally to fit panel
            var scaleX = (double)availW / totalWidth;
            var scaleY = (double)availH / totalHeight;
            var scale = Math.Min(scaleX, scaleY);

            // Center the arrangement
            var scaledW = (int)(totalWidth * scale);
            var scaledH = (int)(totalHeight * scale);
            var offsetX = padding + (availW - scaledW) / 2;
            var offsetY = padding + (availH - scaledH) / 2;

            _monitorRects = new Rectangle[_screens.Length];
            _screenRects = new Rectangle[_screens.Length];

            for (var i = 0; i < _screens.Length; i++)
            {
                var b = _screens[i].Bounds;
                var x = offsetX + (int)((b.Left - minX) * scale);
                var y = offsetY + (int)((b.Top - minY) * scale);
                var w = (int)(b.Width * scale);
                var h = (int)(b.Height * scale);

                // Inset each monitor slightly so there's a gap between them
                const int gap = 4;
                _monitorRects[i] = new Rectangle(x + gap, y + gap, w - gap * 2, h - gap * 2);

                // Inner screen area from bezel proportions
                // monitor.png is 164x148, screen area ~(12,8)->(152,118)
                var mr = _monitorRects[i];
                var sx = mr.X + (int)(mr.Width * 0.073);
                var sy = mr.Y + (int)(mr.Height * 0.054);
                var sw = (int)(mr.Width * (0.927 - 0.073));
                var sh = (int)(mr.Height * (0.797 - 0.054));
                _screenRects[i] = new Rectangle(sx, sy, sw, sh);
            }
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            ComputeLayout();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.Clear(BackColor);

            if (_monitorRects.Length == 0)
                ComputeLayout();

            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

            for (var i = 0; i < _screens.Length; i++)
            {
                var bezelRect = _monitorRects[i];
                var screenRect = _screenRects[i];
                var disabled = _disabledSet.Contains(_screens[i].DeviceName);

                // Draw bezel image
                if (_bezelImage != null)
                    e.Graphics.DrawImage(_bezelImage, bezelRect);
                else
                    e.Graphics.FillRectangle(Brushes.DimGray, bezelRect);

                // Fill screen area
                var monitorImage = i < _previewImages.Length ? _previewImages[i] : null;
                if (disabled || monitorImage == null)
                {
                    e.Graphics.FillRectangle(Brushes.Black, screenRect);
                }
                else
                {
                    e.Graphics.FillRectangle(Brushes.Black, screenRect);
                    var fitRect = CalculateFitRect(monitorImage.Width, monitorImage.Height,
                        screenRect.Width, screenRect.Height);
                    fitRect.Offset(screenRect.Location);
                    e.Graphics.SetClip(screenRect);
                    e.Graphics.DrawImage(monitorImage, fitRect);
                    e.Graphics.ResetClip();
                }

                // Draw monitor number
                var numberStr = (i + 1).ToString();
                using var font = new Font("Segoe UI", Math.Max(screenRect.Height * 0.3f, 10f), FontStyle.Bold);
                var textSize = e.Graphics.MeasureString(numberStr, font);
                var textX = screenRect.X + (screenRect.Width - textSize.Width) / 2;
                var textY = screenRect.Y + (screenRect.Height - textSize.Height) / 2;
                e.Graphics.DrawString(numberStr, font, new SolidBrush(Color.FromArgb(120, 255, 255, 255)),
                    textX, textY);

                // Hover overlay
                if (_hoveredIndex == i)
                {
                    using var overlayBrush = new SolidBrush(Color.FromArgb(140, 0, 0, 0));
                    e.Graphics.FillRectangle(overlayBrush, screenRect);

                    var label = disabled ? "Enable" : "Disable";
                    using var hoverFont = new Font("Segoe UI", Math.Max(screenRect.Height * 0.15f, 9f), FontStyle.Bold);
                    var labelSize = e.Graphics.MeasureString(label, hoverFont);
                    var lx = screenRect.X + (screenRect.Width - labelSize.Width) / 2;
                    var ly = screenRect.Y + (screenRect.Height - labelSize.Height) / 2;
                    e.Graphics.DrawString(label, hoverFont, Brushes.White, lx, ly);
                }
            }
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

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var newHover = -1;
            for (var i = 0; i < _monitorRects.Length; i++)
            {
                if (_monitorRects[i].Contains(e.Location))
                {
                    newHover = i;
                    break;
                }
            }

            if (newHover != _hoveredIndex)
            {
                _hoveredIndex = newHover;
                Cursor = _hoveredIndex >= 0 ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoveredIndex != -1)
            {
                _hoveredIndex = -1;
                Cursor = Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            for (var i = 0; i < _monitorRects.Length; i++)
            {
                if (_monitorRects[i].Contains(e.Location))
                {
                    var deviceName = _screens[i].DeviceName;
                    if (_disabledSet.Contains(deviceName))
                        _disabledSet.Remove(deviceName);
                    else
                        _disabledSet.Add(deviceName);
                    Invalidate();
                    break;
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _previewTimer.Stop();
                _previewTimer.Dispose();
                _bezelImage?.Dispose();
                foreach (var img in _previewImages)
                    img?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
