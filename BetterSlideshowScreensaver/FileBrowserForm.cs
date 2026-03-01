using System.Collections.Concurrent;
using System.Collections.Specialized;

namespace BetterSlideshowScreensaver;

public class FileBrowserForm : Form
{
    private ThumbnailPanel? _panel;
    private readonly ScreensaverConfig _config;
    private readonly ContextMenuStrip _contextMenu;
    private readonly StatusStrip _statusStrip;
    private readonly ToolStripStatusLabel _statusLabel;
    private readonly Label _loadingLabel;
    private readonly SplitContainer _splitContainer;
    private readonly List<(string Path, DateTime ShownAt)> _history;
    private readonly ListView _recentList;

    public FileBrowserForm(string folderPath, string selectedImagePath, ScreensaverConfig config,
        List<(string Path, DateTime ShownAt)>? history = null)
    {
        _config = config;
        _history = history ?? new List<(string Path, DateTime ShownAt)>();
        Text = "Grenzer's Less Shitty Slideshow Screensaver - File Browser";
        Icon = AppIcon.Load();
        Size = new Size(1200, 800);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;

        _contextMenu = BuildContextMenu();

        _loadingLabel = new Label
        {
            Text = "Scanning images...",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font(Font.FontFamily, 14f),
            ForeColor = SystemColors.GrayText
        };

        _statusStrip = new StatusStrip();
        _statusLabel = new ToolStripStatusLabel("Scanning...");
        _statusStrip.Items.Add(_statusLabel);

        // --- SplitContainer: thumbnail grid (left) + recent images (right) ---
        _splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            FixedPanel = FixedPanel.Panel2,
            BorderStyle = BorderStyle.None
        };

        // Loading label goes in left panel initially
        _splitContainer.Panel1.Controls.Add(_loadingLabel);

        // --- Recent Images panel (right side) ---
        var headerLabel = new Label
        {
            Text = "Recent Images",
            Dock = DockStyle.Top,
            Font = new Font(Font.FontFamily, 10f, FontStyle.Bold),
            Padding = new Padding(4, 6, 4, 6),
            AutoSize = false,
            Height = 30,
            BackColor = SystemColors.ControlLight
        };

        _recentList = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            HeaderStyle = ColumnHeaderStyle.None,
            BackColor = SystemColors.ControlLight,
            MultiSelect = false
        };
        _recentList.Columns.Add("Image / Time ago", -1);

        PopulateRecentList();

        _recentList.ItemActivate += (_, _) => NavigateToRecentItem();
        _recentList.Click += (_, _) => NavigateToRecentItem();

        _splitContainer.Panel2.BackColor = SystemColors.ControlLight;
        _splitContainer.Panel2.Controls.Add(_recentList);
        _splitContainer.Panel2.Controls.Add(headerLabel);

        Controls.Add(_splitContainer);
        Controls.Add(_statusStrip);

        // Set splitter distance after the layout is ready
        _splitContainer.SplitterDistance = Math.Max(100, 1200 - 250);

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
                Close();
            else if (e.KeyCode == Keys.C && e.Control)
            {
                CopySelected();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Delete)
            {
                ToggleExcludeSelected();
                e.Handled = true;
            }
        };

        Shown += (_, _) =>
        {
            TopMost = true;
            Activate();
            TopMost = false;

            // Scan in background so the window appears instantly
            Task.Run(() => ImageScanner.ScanAndSort(folderPath))
                .ContinueWith(t =>
                {
                    var images = t.Result;
                    _loadingLabel.Dispose();

                    _panel = new ThumbnailPanel(images, config)
                    {
                        Dock = DockStyle.Fill,
                        ContextMenuStrip = _contextMenu
                    };
                    _panel.ItemDoubleClicked += path =>
                        System.Diagnostics.Process.Start(
                            new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });

                    _splitContainer.Panel1.Controls.Add(_panel);
                    _statusLabel.Text = $"{images.Count} images";
                    _panel.ScrollToImage(selectedImagePath);
                    _panel.Focus();
                }, TaskScheduler.FromCurrentSynchronizationContext());
        };

        // Auto-size the recent list column once layout is done
        _recentList.Resize += (_, _) =>
        {
            if (_recentList.Columns.Count > 0)
                _recentList.Columns[0].Width = _recentList.ClientSize.Width;
        };
    }

    private void PopulateRecentList()
    {
        _recentList.Items.Clear();

        // Dedupe: keep only the latest occurrence per path, then reverse for most-recent-first
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var deduped = new List<(string Path, DateTime ShownAt)>();
        for (var i = _history.Count - 1; i >= 0; i--)
        {
            if (seen.Add(_history[i].Path))
                deduped.Add(_history[i]);
        }

        foreach (var (path, shownAt) in deduped)
        {
            var item = new ListViewItem(Path.GetFileName(path));
            item.SubItems.Add(FormatTimeAgo(shownAt));
            item.Tag = path;
            item.ToolTipText = path;
            _recentList.Items.Add(item);
        }
    }

    private void NavigateToRecentItem()
    {
        if (_panel == null || _recentList.SelectedItems.Count == 0) return;
        var path = _recentList.SelectedItems[0].Tag as string;
        if (!string.IsNullOrEmpty(path))
            _panel.ScrollToImage(path);
    }

    private static string FormatTimeAgo(DateTime shownAt)
    {
        var elapsed = DateTime.Now - shownAt;
        if (elapsed.TotalSeconds < 60) return $"{(int)elapsed.TotalSeconds}s ago";
        if (elapsed.TotalMinutes < 60) return $"{(int)elapsed.TotalMinutes}m ago";
        return $"{(int)elapsed.TotalHours}h ago";
    }

    private ContextMenuStrip BuildContextMenu()
    {
        var menu = new ContextMenuStrip();
        var openItem = new ToolStripMenuItem("Open", null, (_, _) => OpenSelected());
        var openInExplorerItem = new ToolStripMenuItem("Open in Explorer", null, (_, _) => OpenInExplorer());
        var copyItem = new ToolStripMenuItem("Copy", null, (_, _) => CopySelected());
        var copyPathItem = new ToolStripMenuItem("Copy Path", null, (_, _) => CopyPathSelected());
        var separator = new ToolStripSeparator();
        var excludeItem = new ToolStripMenuItem("Exclude") { Name = "excludeItem" };
        excludeItem.Click += (_, _) => ToggleExcludeSelected();

        menu.Items.AddRange(new ToolStripItem[] { openItem, openInExplorerItem, copyItem, copyPathItem, separator, excludeItem });

        menu.Opening += (_, e) =>
        {
            if (_panel == null) { e.Cancel = true; return; }
            var selected = _panel.GetSelectedPaths();
            if (selected.Count == 0) { e.Cancel = true; return; }
            excludeItem.Text = _config.IsExcluded(selected[0]) ? "Include" : "Exclude";
        };

        return menu;
    }

    private void OpenSelected()
    {
        if (_panel == null) return;
        foreach (var path in _panel.GetSelectedPaths())
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
    }

    private void CopySelected()
    {
        if (_panel == null) return;
        var paths = _panel.GetSelectedPaths();
        if (paths.Count == 0) return;
        var sc = new StringCollection();
        foreach (var p in paths) sc.Add(p);
        Clipboard.SetFileDropList(sc);
    }

    private void CopyPathSelected()
    {
        if (_panel == null) return;
        var paths = _panel.GetSelectedPaths();
        if (paths.Count == 0) return;
        Clipboard.SetText(string.Join(Environment.NewLine, paths));
    }

    private void OpenInExplorer()
    {
        if (_panel == null) return;
        var paths = _panel.GetSelectedPaths();
        if (paths.Count == 0) return;
        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{paths[0]}\"");
    }

    private void ToggleExcludeSelected()
    {
        if (_panel == null) return;
        foreach (var path in _panel.GetSelectedPaths())
            _config.ToggleExclusion(path);
        _config.Save();
        _panel.Invalidate();
    }

    private class ThumbnailPanel : Control
    {
        private const int ThumbSize = 128;
        private const int CellPad = 8;
        private const int TextHeight = 20;
        private const int CellWidth = ThumbSize + CellPad * 2;
        private const int CellHeight = ThumbSize + TextHeight + CellPad * 2;
        private const int MaxCacheSize = 500;

        private readonly List<string> _images;
        private readonly ScreensaverConfig _config;
        private readonly HashSet<int> _selected = new();
        private int _focusedIndex = -1;
        private int _shiftAnchor = -1;

        private readonly VScrollBar _scrollBar;
        private readonly ConcurrentDictionary<int, Bitmap> _thumbCache = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly ManualResetEventSlim _loadSignal = new(false);
        private volatile int _visibleFirst = -1;
        private volatile int _visibleLast = -1;
        private volatile bool _thumbnailsUpdated;
        private readonly System.Windows.Forms.Timer _repaintTimer;

        public event Action<string>? ItemDoubleClicked;

        public ThumbnailPanel(List<string> images, ScreensaverConfig config)
        {
            _images = images;
            _config = config;

            DoubleBuffered = true;
            SetStyle(ControlStyles.Selectable | ControlStyles.UserPaint
                | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);

            _scrollBar = new VScrollBar { Dock = DockStyle.Right };
            _scrollBar.Scroll += (_, _) => { QueueVisibleThumbnails(); Invalidate(); };
            Controls.Add(_scrollBar);

            _repaintTimer = new System.Windows.Forms.Timer { Interval = 150 };
            _repaintTimer.Tick += (_, _) =>
            {
                if (_thumbnailsUpdated)
                {
                    _thumbnailsUpdated = false;
                    Invalidate();
                }
            };
            _repaintTimer.Start();

            StartBackgroundLoader();
        }

        private int Columns => Math.Max(1, (Width - _scrollBar.Width) / CellWidth);
        private int Rows => (_images.Count + Columns - 1) / Columns;
        private int LeftMargin => (Width - _scrollBar.Width - Columns * CellWidth) / 2;

        public void ScrollToImage(string path)
        {
            var idx = _images.FindIndex(
                p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            if (idx < 0) return;

            _selected.Clear();
            _selected.Add(idx);
            _focusedIndex = idx;
            _shiftAnchor = idx;

            EnsureVisible(idx);
            QueueVisibleThumbnails();
            Invalidate();
        }

        public List<string> GetSelectedPaths() =>
            _selected.OrderBy(i => i).Select(i => _images[i]).ToList();

        private void EnsureVisible(int index)
        {
            var row = index / Columns;
            var y = row * CellHeight;
            var viewH = ClientSize.Height;
            var maxScroll = Math.Max(0, _scrollBar.Maximum - _scrollBar.LargeChange + 1);

            if (y < _scrollBar.Value)
                _scrollBar.Value = Math.Min(y, maxScroll);
            else if (y + CellHeight > _scrollBar.Value + viewH)
                _scrollBar.Value = Math.Min(y + CellHeight - viewH, maxScroll);
        }

        private void UpdateScrollRange()
        {
            var totalH = Rows * CellHeight;
            var viewH = Math.Max(1, ClientSize.Height);
            _scrollBar.Maximum = Math.Max(0, totalH);
            _scrollBar.LargeChange = viewH;
            _scrollBar.SmallChange = CellHeight;
            var maxScroll = Math.Max(0, _scrollBar.Maximum - _scrollBar.LargeChange + 1);
            if (_scrollBar.Value > maxScroll)
                _scrollBar.Value = maxScroll;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateScrollRange();
            QueueVisibleThumbnails();
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            var delta = e.Delta > 0 ? -CellHeight : CellHeight;
            var maxScroll = Math.Max(0, _scrollBar.Maximum - _scrollBar.LargeChange + 1);
            _scrollBar.Value = Math.Clamp(_scrollBar.Value + delta, 0, maxScroll);
            QueueVisibleThumbnails();
            Invalidate();
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Left: case Keys.Right: case Keys.Up: case Keys.Down:
                    return true;
            }
            return base.IsInputKey(keyData);
        }

        private int HitTest(int x, int y)
        {
            var col = (x - LeftMargin) / CellWidth;
            var row = (y + _scrollBar.Value) / CellHeight;
            if (col < 0 || col >= Columns || row < 0) return -1;
            var idx = row * Columns + col;
            return idx >= 0 && idx < _images.Count ? idx : -1;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus();
            var idx = HitTest(e.X, e.Y);

            if (e.Button == MouseButtons.Left)
            {
                if (idx < 0)
                {
                    if (!ModifierKeys.HasFlag(Keys.Control))
                        _selected.Clear();
                }
                else if (ModifierKeys.HasFlag(Keys.Control))
                {
                    if (!_selected.Remove(idx)) _selected.Add(idx);
                    _shiftAnchor = idx;
                }
                else if (ModifierKeys.HasFlag(Keys.Shift) && _shiftAnchor >= 0)
                {
                    _selected.Clear();
                    var lo = Math.Min(_shiftAnchor, idx);
                    var hi = Math.Max(_shiftAnchor, idx);
                    for (var i = lo; i <= hi; i++) _selected.Add(i);
                }
                else
                {
                    _selected.Clear();
                    _selected.Add(idx);
                    _shiftAnchor = idx;
                }
                _focusedIndex = idx;
            }
            else if (e.Button == MouseButtons.Right && idx >= 0)
            {
                if (!_selected.Contains(idx))
                {
                    _selected.Clear();
                    _selected.Add(idx);
                    _shiftAnchor = idx;
                    _focusedIndex = idx;
                }
            }

            Invalidate();
            base.OnMouseDown(e);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            var idx = HitTest(e.X, e.Y);
            if (idx >= 0) ItemDoubleClicked?.Invoke(_images[idx]);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            var cols = Columns;
            var newFocus = _focusedIndex;

            switch (e.KeyCode)
            {
                case Keys.Left: newFocus = Math.Max(0, _focusedIndex - 1); break;
                case Keys.Right: newFocus = Math.Min(_images.Count - 1, _focusedIndex + 1); break;
                case Keys.Up: newFocus = Math.Max(0, _focusedIndex - cols); break;
                case Keys.Down: newFocus = Math.Min(_images.Count - 1, _focusedIndex + cols); break;
                case Keys.Home: newFocus = 0; break;
                case Keys.End: newFocus = _images.Count - 1; break;
                case Keys.Enter:
                    if (_focusedIndex >= 0) ItemDoubleClicked?.Invoke(_images[_focusedIndex]);
                    e.Handled = true;
                    return;
                case Keys.A when e.Control:
                    _selected.Clear();
                    for (var i = 0; i < _images.Count; i++) _selected.Add(i);
                    Invalidate();
                    e.Handled = true;
                    return;
                default:
                    base.OnKeyDown(e);
                    return;
            }

            if (newFocus != _focusedIndex && newFocus >= 0)
            {
                if (e.Shift && _shiftAnchor >= 0)
                {
                    _selected.Clear();
                    var lo = Math.Min(_shiftAnchor, newFocus);
                    var hi = Math.Max(_shiftAnchor, newFocus);
                    for (var i = lo; i <= hi; i++) _selected.Add(i);
                }
                else if (!e.Control)
                {
                    _selected.Clear();
                    _selected.Add(newFocus);
                    _shiftAnchor = newFocus;
                }
                _focusedIndex = newFocus;
                EnsureVisible(newFocus);
                QueueVisibleThumbnails();
                Invalidate();
                e.Handled = true;
            }

            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            if (_images.Count == 0) return;

            var scrollY = _scrollBar.Value;
            var cols = Columns;
            var leftMargin = LeftMargin;
            var firstRow = scrollY / CellHeight;
            var lastRow = (scrollY + ClientSize.Height) / CellHeight;

            using var selectedBrush = new SolidBrush(Color.FromArgb(60, SystemColors.Highlight));
            using var focusPen = new Pen(SystemColors.Highlight)
                { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
            using var excludedBrush = new SolidBrush(Color.Gray);
            using var italicFont = new Font(Font, FontStyle.Italic);
            using var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            };

            for (var row = firstRow; row <= lastRow; row++)
            {
                for (var col = 0; col < cols; col++)
                {
                    var idx = row * cols + col;
                    if (idx >= _images.Count) break;

                    var x = leftMargin + col * CellWidth + CellPad;
                    var y = row * CellHeight - scrollY + CellPad;

                    // Selection background
                    if (_selected.Contains(idx))
                        e.Graphics.FillRectangle(selectedBrush,
                            x - 2, y - 2, ThumbSize + 4, ThumbSize + TextHeight + CellPad + 4);

                    // Thumbnail or placeholder
                    if (_thumbCache.TryGetValue(idx, out var thumb))
                        e.Graphics.DrawImage(thumb, x, y, ThumbSize, ThumbSize);
                    else
                        e.Graphics.FillRectangle(Brushes.LightGray, x, y, ThumbSize, ThumbSize);

                    // Filename
                    var isExcluded = _config.IsExcluded(_images[idx]);
                    var textBrush = isExcluded ? excludedBrush : SystemBrushes.ControlText;
                    var font = isExcluded ? italicFont : Font;
                    var textRect = new RectangleF(
                        x - CellPad / 2f, y + ThumbSize + 2, CellWidth - CellPad, TextHeight);
                    e.Graphics.DrawString(Path.GetFileName(_images[idx]), font, textBrush, textRect, sf);

                    // Focus rectangle
                    if (idx == _focusedIndex && Focused)
                        e.Graphics.DrawRectangle(focusPen,
                            x - 3, y - 3, ThumbSize + 6, ThumbSize + TextHeight + CellPad + 6);
                }
            }
        }

        // --- Thumbnail loading (visible range only) ---

        private void QueueVisibleThumbnails()
        {
            if (_images.Count == 0) return;
            var scrollY = _scrollBar.Value;
            var cols = Columns;
            var firstRow = Math.Max(0, scrollY / CellHeight - 2); // 2-row buffer
            var lastRow = (scrollY + ClientSize.Height) / CellHeight + 2;

            _visibleFirst = Math.Max(0, firstRow * cols);
            _visibleLast = Math.Min(_images.Count - 1, (lastRow + 1) * cols - 1);
            _loadSignal.Set();
        }

        private void StartBackgroundLoader()
        {
            var token = _cts.Token;
            Task.Run(() =>
            {
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        _loadSignal.Wait(token);
                        _loadSignal.Reset();

                        var first = _visibleFirst;
                        var last = _visibleLast;

                        for (var i = first; i <= last && !token.IsCancellationRequested; i++)
                        {
                            if (i < _visibleFirst || i > _visibleLast) break;
                            if (_thumbCache.ContainsKey(i)) continue;

                            var thumb = CreateThumbnail(_images[i]);
                            if (thumb == null) continue;

                            if (_thumbCache.Count >= MaxCacheSize)
                                EvictFarthest(i);

                            _thumbCache[i] = thumb;
                            _thumbnailsUpdated = true;
                        }
                    }
                }
                catch (OperationCanceledException) { }
            }, token);
        }

        private void EvictFarthest(int currentIdx)
        {
            var farthest = -1;
            var maxDist = -1;
            foreach (var key in _thumbCache.Keys)
            {
                var dist = Math.Abs(key - currentIdx);
                if (dist > maxDist) { maxDist = dist; farthest = key; }
            }
            if (farthest >= 0)
                _thumbCache.TryRemove(farthest, out _);
        }

        private static Bitmap? CreateThumbnail(string path)
        {
            try
            {
                var bytes = File.ReadAllBytes(path);
                using var ms = new MemoryStream(bytes);
                using var original = Image.FromStream(ms);

                var thumb = new Bitmap(ThumbSize, ThumbSize);
                using var g = Graphics.FromImage(thumb);
                g.Clear(Color.Black);
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

                var ratioX = (double)ThumbSize / original.Width;
                var ratioY = (double)ThumbSize / original.Height;
                var ratio = Math.Min(ratioX, ratioY);
                var newW = (int)(original.Width * ratio);
                var newH = (int)(original.Height * ratio);
                var x = (ThumbSize - newW) / 2;
                var y = (ThumbSize - newH) / 2;

                g.DrawImage(original, x, y, newW, newH);
                return thumb;
            }
            catch
            {
                return null;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cts.Cancel();
                _loadSignal.Set();
                _repaintTimer.Stop();
                _repaintTimer.Dispose();
                _cts.Dispose();
                _loadSignal.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
