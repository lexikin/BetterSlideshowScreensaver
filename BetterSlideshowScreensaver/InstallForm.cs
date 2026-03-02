namespace BetterSlideshowScreensaver;

public class InstallForm : Form
{
    private readonly CheckBox? _installCheck;
    private readonly CheckBox? _activeCheck;

    public bool InstallChecked => _installCheck?.Checked ?? false;
    public bool SetActiveChecked => _activeCheck?.Checked ?? false;

    public InstallForm(bool showInstall, bool showSetActive)
    {
        Text = "Better Slideshow Screensaver";
        Icon = AppIcon.Load();
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(16);

        var layout = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            Dock = DockStyle.Fill
        };

        var heading = new Label
        {
            Text = "Screensaver Setup",
            Font = new Font(Font.FontFamily, 12f, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8)
        };
        layout.Controls.Add(heading);

        var path = new Label
        {
            Text = Installer.InstalledPath,
            ForeColor = SystemColors.GrayText,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 12)
        };
        layout.Controls.Add(path);

        if (showInstall)
        {
            _installCheck = new CheckBox
            {
                Text = Installer.IsInstalled
                    ? "Update screensaver in System32 (newer version available)"
                    : "Install screensaver to System32",
                Checked = true,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            };
            layout.Controls.Add(_installCheck);
        }

        if (showSetActive)
        {
            _activeCheck = new CheckBox
            {
                Text = "Set as active screensaver",
                Checked = true,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 12)
            };
            layout.Controls.Add(_activeCheck);
        }

        var note = new Label
        {
            Text = showInstall ? "Admin privileges may be required." : "",
            ForeColor = SystemColors.GrayText,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8)
        };
        if (showInstall) layout.Controls.Add(note);

        var buttonPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            Margin = new Padding(0, 4, 0, 0)
        };

        var okButton = new Button { Text = "OK", Width = 80, DialogResult = DialogResult.OK };
        var cancelButton = new Button { Text = "Cancel", Width = 80, DialogResult = DialogResult.Cancel };
        cancelButton.Margin = new Padding(8, 0, 0, 0);
        buttonPanel.Controls.Add(okButton);
        buttonPanel.Controls.Add(cancelButton);
        layout.Controls.Add(buttonPanel);

        AcceptButton = okButton;
        CancelButton = cancelButton;

        Controls.Add(layout);
    }
}
