using WeifenLuo.WinFormsUI.Docking;

namespace PDF_Manager.Shell;

public sealed class MainForm : Form
{
    private readonly DockPanel _dockPanel;
    private readonly VS2015LightTheme _theme;
    private bool _disposed;

    public MainForm()
    {
        Text = "FrameWeb Desktop";
        ClientSize = new Size(1200, 800);
        MinimumSize = new Size(640, 480);
        StartPosition = FormStartPosition.CenterScreen;

        _theme = new VS2015LightTheme();
        _dockPanel = new DockPanel
        {
            Dock = DockStyle.Fill,
            DocumentStyle = DocumentStyle.DockingWindow,
            Theme = _theme,
        };

        Controls.Add(_dockPanel);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _dockPanel.Dispose();
            _theme.Dispose();
            _disposed = true;
        }

        base.Dispose(disposing);
    }
}
