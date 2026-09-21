using PDF_Manager.Shell.Contents;

namespace PDF_Manager.Shell.ScreenComposition.Core;

public sealed class WorkspaceControl : UserControl
{
    private readonly ProjectDocumentContent _documentHost;
    private readonly Control _viewportSurface;
    private readonly Button _home = new HomeGlyphButton
    {
        AccessibleName = "Home",
        Anchor = AnchorStyles.Top | AnchorStyles.Left,
        BackColor = Color.Transparent,
        Cursor = Cursors.Hand,
        FlatAppearance =
        {
            BorderSize = 0,
            MouseDownBackColor = Color.FromArgb(220, 225, 230),
            MouseOverBackColor = Color.White,
        },
        FlatStyle = FlatStyle.Flat,
        Location = new Point(79, 20),
        Name = "WorkspaceHomeButton",
        Size = new Size(28, 28),
        TabStop = false,
        UseVisualStyleBackColor = false,
    };
    private bool _disposed;

    public WorkspaceControl(ProjectDocumentContent documentHost)
    {
        _documentHost = documentHost ?? throw new ArgumentNullException(nameof(documentHost));
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = ScreenCompositionStyle.WorkspaceBackColor;
        Dock = DockStyle.Fill;
        Name = "Workspace";

        _viewportSurface = documentHost.DetachViewportHost();
        if (_viewportSurface.IsDisposed)
        {
            throw new ObjectDisposedException(nameof(documentHost), "The viewport surface is already disposed.");
        }

        _viewportSurface.Parent?.Controls.Remove(_viewportSurface);
        _viewportSurface.Dock = DockStyle.Fill;
        Controls.Add(_viewportSurface);
        _viewportSurface.Controls.Add(_home);
        _home.BringToFront();
        _home.Click += OnHomeClick;
    }

    public ProjectDocumentContent DocumentHost => _documentHost;

    public Control ViewportSurface => _viewportSurface;

    public Button HomeButton => _home;

    public int VisibleViewportCount =>
        Controls.Contains(_viewportSurface) && _viewportSurface.Visible ? 1 : 0;

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _home.Click -= OnHomeClick;
            _viewportSurface.Controls.Remove(_home);
            _home.Dispose();
            Controls.Remove(_viewportSurface);
        }

        base.Dispose(disposing);
    }

    private void OnHomeClick(object? sender, EventArgs eventArgs) => _documentHost.Home();

    private sealed class HomeGlyphButton : Button
    {
        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            base.OnPaint(eventArgs);
            Graphics graphics = eventArgs.Graphics;
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            Color color = Enabled ? Color.FromArgb(32, 45, 56) : SystemColors.GrayText;
            using Pen pen = new(color, 1.6f)
            {
                StartCap = System.Drawing.Drawing2D.LineCap.Round,
                EndCap = System.Drawing.Drawing2D.LineCap.Round,
                LineJoin = System.Drawing.Drawing2D.LineJoin.Round,
            };
            graphics.DrawLines(pen, [
                new PointF(8, 15), new PointF(16, 8), new PointF(24, 15),
            ]);
            graphics.DrawLines(pen, [
                new PointF(10, 14), new PointF(10, 23), new PointF(22, 23), new PointF(22, 14),
            ]);
            graphics.DrawRectangle(pen, 15, 17, 3, 6);
        }
    }
}
