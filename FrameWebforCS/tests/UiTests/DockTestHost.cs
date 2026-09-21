using PDF_Manager.Core.Shell;
using PDF_Manager.Shell.Docking;
using WeifenLuo.WinFormsUI.Docking;

namespace PDF_Manager.UiTests;

internal sealed class DockTestHost : IDisposable
{
    private readonly VS2015LightTheme theme = new();
    private bool disposed;

    public DockTestHost()
    {
        Form = new Form
        {
            ClientSize = new Size(900, 650),
            ShowInTaskbar = false,
            StartPosition = FormStartPosition.Manual,
        };
        DockPanel = new DockPanel
        {
            Dock = DockStyle.Fill,
            DocumentStyle = DocumentStyle.DockingWindow,
            Theme = theme,
        };
        Form.Controls.Add(DockPanel);
        Registry = new DockContentRegistry(DockPanel);
        Form.Show();
        Application.DoEvents();
    }

    public Form Form { get; }

    public DockPanel DockPanel { get; }

    public DockContentRegistry Registry { get; }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        Registry.Dispose();
        Form.Close();
        Form.Dispose();
        theme.Dispose();
        disposed = true;
    }
}

internal sealed class TestDockContent : DockContent, IKeyedDockContent
{
    public TestDockContent(DocumentKey contentKey)
    {
        ContentKey = contentKey;
        Text = contentKey.Identifier;
        TabText = Text;
    }

    public DocumentKey ContentKey { get; }
}
