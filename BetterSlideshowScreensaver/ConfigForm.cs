namespace BetterSlideshowScreensaver;

public class ConfigForm : Form
{
    private readonly TextBox _folderPathTextBox;
    private readonly ComboBox _monitorModeCombo;
    private readonly NumericUpDown _intervalNumeric;

    public ConfigForm()
    {
        Text = "Grenzer's Less Shitty Slideshow Screensaver - Settings";
        Icon = AppIcon.Load();
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(480, 230);

        var folderLabel = new Label { Text = "Image Folder:", Location = new Point(12, 18), AutoSize = true };
        _folderPathTextBox = new TextBox { Location = new Point(120, 15), Width = 270 };
        var browseButton = new Button { Text = "...", Location = new Point(396, 14), Width = 30 };
        browseButton.Click += BrowseButton_Click;

        var modeLabel = new Label { Text = "Monitor Mode:", Location = new Point(12, 53), AutoSize = true };
        _monitorModeCombo = new ComboBox
        {
            Location = new Point(120, 50),
            Width = 200,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _monitorModeCombo.Items.AddRange(new object[] { "Primary Only", "Same Image", "Main + Previous" });

        var intervalLabel = new Label { Text = "Slide Interval (s):", Location = new Point(12, 88), AutoSize = true };
        _intervalNumeric = new NumericUpDown
        {
            Location = new Point(120, 85),
            Width = 80,
            Minimum = 1,
            Maximum = 300,
            Value = 8
        };

        var tipLabel = new Label
        {
            Text = "Tip: Press Ctrl while the screensaver is running to open the file browser.",
            Location = new Point(12, 123),
            AutoSize = false,
            Size = new Size(450, 20),
            ForeColor = SystemColors.GrayText
        };

        var okButton = new Button { Text = "OK", Location = new Point(296, 185), Width = 80, DialogResult = DialogResult.OK };
        var cancelButton = new Button { Text = "Cancel", Location = new Point(384, 185), Width = 80 };
        okButton.Click += OkButton_Click;
        cancelButton.Click += (_, _) => Close();

        AcceptButton = okButton;
        CancelButton = cancelButton;

        Controls.AddRange(new Control[]
        {
            folderLabel, _folderPathTextBox, browseButton,
            modeLabel, _monitorModeCombo,
            intervalLabel, _intervalNumeric,
            tipLabel,
            okButton, cancelButton
        });

        LoadConfig();
    }

    private void LoadConfig()
    {
        var config = ScreensaverConfig.Load();
        _folderPathTextBox.Text = config.ImageFolderPath;
        _monitorModeCombo.SelectedIndex = (int)config.MonitorMode;
        _intervalNumeric.Value = Math.Clamp(config.SlideIntervalSeconds, 1, 300);
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
        var config = new ScreensaverConfig
        {
            ImageFolderPath = _folderPathTextBox.Text,
            MonitorMode = (MultiMonitorMode)_monitorModeCombo.SelectedIndex,
            SlideIntervalSeconds = (int)_intervalNumeric.Value
        };
        config.Save();
        Close();
    }
}
