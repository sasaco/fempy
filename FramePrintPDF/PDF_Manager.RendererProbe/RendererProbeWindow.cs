using WeifenLuo.WinFormsUI.Docking;

namespace PDF_Manager.RendererProbe;

internal sealed class RendererProbeShell : Form
{
    private readonly DockPanel _dockPanel;
    private readonly VS2015LightTheme _theme;
    private bool _disposed;

    public RendererProbeShell(bool verificationMode)
    {
        ProbeDiagnostics.WindowOpened();
        Text = "FrameWeb Renderer Probe";
        ClientSize = new Size(640, 480);
        MinimumSize = new Size(320, 240);
        StartPosition = verificationMode ? FormStartPosition.Manual : FormStartPosition.CenterScreen;
        ShowInTaskbar = !verificationMode;
        if (verificationMode)
        {
            Location = new Point(-20_000, -20_000);
        }

        _theme = new VS2015LightTheme();
        _dockPanel = new DockPanel
        {
            Dock = DockStyle.Fill,
            DocumentStyle = DocumentStyle.DockingWindow,
            Theme = _theme,
        };
        Controls.Add(_dockPanel);

        Document = new RendererProbeDocument();
        Document.Show(_dockPanel, DockState.Document);
    }

    public RendererProbeDocument Document { get; }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            Document.Dispose();
            _dockPanel.Dispose();
            _theme.Dispose();
            _disposed = true;
            ProbeDiagnostics.WindowClosed();
        }

        base.Dispose(disposing);
    }
}

internal sealed class RendererProbeDocument : DockContent
{
    private bool _disposed;

    public RendererProbeDocument()
    {
        ProbeDiagnostics.WindowOpened();
        Text = "Known OpenGL Frame";
        TabText = Text;
        HideOnClose = false;
        Renderer = new OpenGlViewportLifecycle();
        Controls.Add(Renderer.Control);
    }

    public OpenGlViewportLifecycle Renderer { get; }

    public void EnsureRendererReady()
    {
        Renderer.SetModel(ProbeSceneModel.KnownFrame);
        Renderer.Initialize();
        Renderer.Resize(Renderer.Control.ClientSize);
        Renderer.RequestRender();
    }

    protected override void OnShown(EventArgs eventArgs)
    {
        base.OnShown(eventArgs);
        EnsureRendererReady();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            Controls.Remove(Renderer.Control);
            Renderer.Dispose();
            _disposed = true;
            ProbeDiagnostics.WindowClosed();
        }

        base.Dispose(disposing);
    }
}
