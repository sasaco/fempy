using FrameWebforCS.Shell.ScreenComposition.Core;

namespace FrameWebforCS.Shell.ScreenComposition.Surfaces;

public abstract class OverlaySurfaceBase : UserControl, IFrameWebSurface
{
    private readonly Size _requestedDialogSize;
    private readonly Panel _dialog = new()
    {
        BackColor = SurfaceVisuals.Card,
        BorderStyle = BorderStyle.None,
        Name = "OverlayDialog",
    };
    private readonly Panel _header = new()
    {
        BackColor = SurfaceVisuals.Header,
        Dock = DockStyle.Top,
        Height = 29,
        Name = "OverlayHeader",
    };
    private readonly Panel _content = new()
    {
        BackColor = SurfaceVisuals.Card,
        Dock = DockStyle.Fill,
        Name = "OverlayContent",
    };
    private readonly Label _title = new()
    {
        Dock = DockStyle.Fill,
        Font = new Font((SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily, 9, FontStyle.Bold),
        ForeColor = Color.White,
        Name = "OverlayTitle",
        Padding = new Padding(9, 0, 4, 0),
        TextAlign = ContentAlignment.MiddleLeft,
    };
    private readonly Button _close = new()
    {
        Dock = DockStyle.Right,
        FlatStyle = FlatStyle.Flat,
        Font = new Font((SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily, 9, FontStyle.Regular),
        ForeColor = Color.White,
        Name = "OverlayCloseButton",
        Size = new Size(32, 29),
        Text = "×",
        UseVisualStyleBackColor = false,
    };

    protected OverlaySurfaceBase(Size dialogSize)
    {
        _requestedDialogSize = dialogSize;
        BackColor = Color.FromArgb(204, SurfaceVisuals.WorkspaceMask);
        Dock = DockStyle.Fill;
        Name = "FrameWebOverlay";
        TabStop = true;
        _dialog.Size = dialogSize;
        _close.FlatAppearance.BorderSize = 0;
        _close.FlatAppearance.MouseOverBackColor = Color.FromArgb(52, 61, 70);
        _header.Controls.Add(_title);
        _header.Controls.Add(_close);
        _dialog.Controls.Add(_content);
        _dialog.Controls.Add(_header);
        Controls.Add(_dialog);
        _close.Click += (_, _) => Request(ScreenCommandKind.CloseOverlay);
        SizeChanged += (_, _) => CenterDialog();
        CenterDialog();
    }

    public event EventHandler<ScreenCommandRequestedEventArgs>? CommandRequested;

    public Panel DialogPanel => _dialog;

    public Panel HeaderPanel => _header;

    protected Panel ContentPanel => _content;

    public Button CloseButton => _close;

    public string OverlayTitle
    {
        get => _title.Text;
        set => _title.Text = value;
    }

    protected void Request(ScreenCommandKind command) =>
        CommandRequested?.Invoke(this, new ScreenCommandRequestedEventArgs(command));

    protected override void OnVisibleChanged(EventArgs eventArgs)
    {
        base.OnVisibleChanged(eventArgs);
        if (Visible)
        {
            SelectNextControl(this, forward: true, tabStopOnly: true, nested: true, wrap: true);
        }
    }

    private void CenterDialog()
    {
        int width = Math.Min(_requestedDialogSize.Width, Math.Max(1, ClientSize.Width - 32));
        int height = Math.Min(_requestedDialogSize.Height, Math.Max(1, ClientSize.Height - 32));
        _dialog.Bounds = new Rectangle(
            Math.Max(0, (ClientSize.Width - width) / 2),
            Math.Max(0, (ClientSize.Height - height) / 2),
            width,
            height);
    }
}
