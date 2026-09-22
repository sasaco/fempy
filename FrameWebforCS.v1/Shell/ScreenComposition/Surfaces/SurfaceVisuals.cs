using FrameWebforCS.Shell.ScreenComposition.Core;

namespace FrameWebforCS.Shell.ScreenComposition.Surfaces;

internal static class SurfaceVisuals
{
    internal static Color WorkspaceMask { get; } = Color.Black;

    internal static Color Header { get; } = Color.FromArgb(30, 37, 44);

    internal static Color HeaderText { get; } = Color.White;

    internal static Color Accent { get; } = Color.FromArgb(80, 149, 252);

    internal static Color AccentHover { get; } = Color.FromArgb(60, 126, 226);

    internal static Color Card { get; } = Color.FromArgb(86, 88, 92);

    internal static Color Canvas { get; } = Color.FromArgb(86, 88, 92);

    internal static Color Border { get; } = Color.FromArgb(112, 114, 118);

    internal static Button CreateActionButton(string name, string text)
    {
        Button button = new()
        {
            AutoSize = true,
            BackColor = Accent,
            Cursor = Cursors.Hand,
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            Margin = new Padding(4),
            MinimumSize = new Size(92, 34),
            Name = name,
            Padding = new Padding(12, 4, 12, 4),
            Text = text,
            UseVisualStyleBackColor = false,
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = AccentHover;
        return button;
    }

    internal static Button CreateSecondaryButton(string name, string text)
    {
        Button button = CreateActionButton(name, text);
        button.BackColor = Color.FromArgb(96, 105, 122);
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(71, 79, 94);
        return button;
    }
}

public abstract class FloatingCardSurface : UserControl, IFloatingRouteSurface
{
    private const int DefaultCardWidth = 430;
    private const int HeaderHeight = 26;
    private const int MinimumCardWidth = 360;
    private const int MinimumCardHeight = 280;
    private const int ResizeGripSize = 18;
    private readonly Panel _card = new()
    {
        BackColor = SurfaceVisuals.Card,
        BorderStyle = BorderStyle.FixedSingle,
        Name = "RouteCard",
        Size = new Size(DefaultCardWidth, 620),
    };
    private readonly Panel _header = new()
    {
        BackColor = SurfaceVisuals.Header,
        Cursor = Cursors.SizeAll,
        Dock = DockStyle.Top,
        Height = HeaderHeight,
        Name = "RouteCardHeader",
    };
    private readonly Label _title = new()
    {
        AutoEllipsis = true,
        Dock = DockStyle.Fill,
        Font = new Font(SystemFonts.MessageBoxFont ?? Control.DefaultFont, FontStyle.Bold),
        ForeColor = SurfaceVisuals.HeaderText,
        Name = "RouteCardTitle",
        Padding = new Padding(9, 0, 4, 0),
        TextAlign = ContentAlignment.MiddleLeft,
    };
    private readonly Button _close = new()
    {
        Cursor = Cursors.Hand,
        Dock = DockStyle.Right,
        FlatStyle = FlatStyle.Flat,
        Font = new Font((SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily, 9, FontStyle.Regular),
        ForeColor = Color.White,
        Name = "RouteCardCloseButton",
        Size = new Size(32, HeaderHeight),
        Text = "×",
        UseVisualStyleBackColor = false,
    };
    private readonly Panel _body = new()
    {
        BackColor = SurfaceVisuals.Card,
        Dock = DockStyle.Fill,
        Name = "RouteCardBody",
        Padding = Padding.Empty,
    };
    private readonly Label _resizeGrip = new()
    {
        Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
        BackColor = Color.Transparent,
        Cursor = Cursors.SizeNWSE,
        Font = new Font((SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily, 8),
        ForeColor = Color.FromArgb(110, 118, 130),
        Name = "RouteCardResizeGrip",
        Size = new Size(ResizeGripSize, ResizeGripSize),
        Text = "◢",
        TextAlign = ContentAlignment.BottomRight,
    };
    private Point _pointerOrigin;
    private Rectangle _cardOrigin;
    private bool _hasManualCardBounds;
    private bool _moving;
    private bool _resizing;
    private bool _synchronizingCardBounds;

    protected FloatingCardSurface()
    {
        BackColor = Color.Transparent;
        Dock = DockStyle.Fill;
        Name = "FloatingRouteSurface";
        _close.FlatAppearance.BorderSize = 0;
        _close.FlatAppearance.MouseOverBackColor = Color.FromArgb(52, 61, 70);
        _header.Controls.Add(_title);
        _header.Controls.Add(_close);
        _card.Controls.Add(_body);
        _card.Controls.Add(_header);
        _card.Controls.Add(_resizeGrip);
        Controls.Add(_card);
        _header.MouseDown += BeginMove;
        _header.MouseMove += ContinueMove;
        _header.MouseUp += EndPointerOperation;
        _title.MouseDown += BeginMove;
        _title.MouseMove += ContinueMove;
        _title.MouseUp += EndPointerOperation;
        _resizeGrip.MouseDown += BeginResize;
        _resizeGrip.MouseMove += ContinueResize;
        _resizeGrip.MouseUp += EndPointerOperation;
        _close.Click += (_, _) => Request(ScreenCommandKind.CloseRoute);
        _card.SizeChanged += (_, _) => PositionResizeGrip();
        PositionResizeGrip();
    }

    public Panel CardPanel => _card;

    public Button CloseButton => _close;

    public Control FloatingCard => _card;

    public event EventHandler<ScreenCommandRequestedEventArgs>? CommandRequested;

    public Panel BodyPanel => _body;

    public string CardTitle
    {
        get => _title.Text;
        set => _title.Text = value;
    }

    protected void Request(ScreenCommandKind command) =>
        CommandRequested?.Invoke(this, new ScreenCommandRequestedEventArgs(command));

    protected override void OnParentChanged(EventArgs eventArgs)
    {
        base.OnParentChanged(eventArgs);
        SynchronizeCardBounds();
    }

    protected override void OnSizeChanged(EventArgs eventArgs)
    {
        base.OnSizeChanged(eventArgs);
        SynchronizeCardBounds();
    }

    protected override void OnVisibleChanged(EventArgs eventArgs)
    {
        base.OnVisibleChanged(eventArgs);
        SynchronizeCardBounds();
    }

    protected override void OnLayout(LayoutEventArgs eventArgs)
    {
        base.OnLayout(eventArgs);
        SynchronizeCardBounds();
    }

    private void BeginMove(object? sender, MouseEventArgs eventArgs)
    {
        if (eventArgs.Button != MouseButtons.Left)
        {
            return;
        }

        _moving = true;
        CapturePointer((Control)sender!, eventArgs);
    }

    private void ContinueMove(object? sender, MouseEventArgs eventArgs)
    {
        if (!_moving)
        {
            return;
        }

        Point delta = PointerDelta((Control)sender!, eventArgs);
        _hasManualCardBounds = true;
        _card.Location = new Point(_cardOrigin.Left + delta.X, _cardOrigin.Top + delta.Y);
        ConstrainCard();
    }

    private void BeginResize(object? sender, MouseEventArgs eventArgs)
    {
        if (eventArgs.Button != MouseButtons.Left)
        {
            return;
        }

        _resizing = true;
        CapturePointer((Control)sender!, eventArgs);
    }

    private void ContinueResize(object? sender, MouseEventArgs eventArgs)
    {
        if (!_resizing)
        {
            return;
        }

        Point delta = PointerDelta((Control)sender!, eventArgs);
        _hasManualCardBounds = true;
        int availableWidth = Math.Max(MinimumCardWidth, ClientSize.Width - _cardOrigin.Left);
        int availableHeight = Math.Max(MinimumCardHeight, ClientSize.Height - _cardOrigin.Top);
        _card.Size = new Size(
            Math.Clamp(_cardOrigin.Width + delta.X, MinimumCardWidth, availableWidth),
            Math.Clamp(_cardOrigin.Height + delta.Y, MinimumCardHeight, availableHeight));
    }

    private void CapturePointer(Control source, MouseEventArgs eventArgs)
    {
        _pointerOrigin = PointToClient(source.PointToScreen(eventArgs.Location));
        _cardOrigin = _card.Bounds;
    }

    private Point PointerDelta(Control source, MouseEventArgs eventArgs)
    {
        Point current = PointToClient(source.PointToScreen(eventArgs.Location));
        return new Point(current.X - _pointerOrigin.X, current.Y - _pointerOrigin.Y);
    }

    private void EndPointerOperation(object? sender, MouseEventArgs eventArgs)
    {
        _moving = false;
        _resizing = false;
    }

    private void ConstrainCard()
    {
        if (ClientSize.Width <= 0 || ClientSize.Height <= 0)
        {
            return;
        }

        int width = Math.Min(_card.Width, Math.Max(MinimumCardWidth, ClientSize.Width));
        int height = Math.Min(_card.Height, Math.Max(MinimumCardHeight, ClientSize.Height));
        int x = Math.Clamp(_card.Left, 0, Math.Max(0, ClientSize.Width - width));
        int y = Math.Clamp(_card.Top, 0, Math.Max(0, ClientSize.Height - height));
        _card.Bounds = new Rectangle(x, y, width, height);
    }

    private void SynchronizeCardBounds()
    {
        if (_synchronizingCardBounds || ClientSize.Width <= 0 || ClientSize.Height <= 0)
        {
            return;
        }

        _synchronizingCardBounds = true;
        try
        {
            if (!_hasManualCardBounds)
            {
                int width = Math.Min(DefaultCardWidth, ClientSize.Width);
                _card.Bounds = new Rectangle(
                    Math.Max(0, ClientSize.Width - width),
                    0,
                    width,
                    ClientSize.Height);
                return;
            }

            ConstrainCard();
        }
        finally
        {
            _synchronizingCardBounds = false;
        }
    }

    private void PositionResizeGrip()
    {
        _resizeGrip.Location = new Point(
            Math.Max(0, _card.ClientSize.Width - ResizeGripSize),
            Math.Max(_header.Bottom, _card.ClientSize.Height - ResizeGripSize));
        _resizeGrip.BringToFront();
    }
}
