using FrameWebforCS.Resources;
using FrameWebforCS.Shell.ScreenComposition.Core;
using System.Drawing.Drawing2D;

namespace FrameWebforCS.Shell.ScreenComposition.Surfaces;

public sealed class OperationOverlayControl : OverlaySurfaceBase, IOperationOverlaySurface
{
    private readonly LocalizationService _localization;
    private readonly Label _message = new()
    {
        AutoEllipsis = true,
        Dock = DockStyle.Fill,
        Font = new Font(SystemFonts.MessageBoxFont ?? Control.DefaultFont, FontStyle.Regular),
        ForeColor = Color.White,
        Name = "OperationOverlayMessage",
        Padding = new Padding(24),
        TextAlign = ContentAlignment.MiddleCenter,
    };
    private readonly CircularSpinnerControl _progress = new();
    private readonly FlowLayoutPanel _actions = new()
    {
        AutoSize = true,
        Dock = DockStyle.Bottom,
        FlowDirection = FlowDirection.RightToLeft,
        Name = "OperationOverlayActions",
        Padding = new Padding(12),
    };
    private readonly Button _primary;
    private readonly Button _cancel;
    private ScreenOverlayKind _kind;
    private bool _disposed;

    public OperationOverlayControl(LocalizationService localization, ScreenOverlayKind kind)
        : base(new Size(520, 300))
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _primary = SurfaceVisuals.CreateActionButton("OperationOverlayPrimaryButton", string.Empty);
        _cancel = SurfaceVisuals.CreateSecondaryButton("OperationOverlayCancelButton", string.Empty);
        _actions.Controls.Add(_primary);
        _actions.Controls.Add(_cancel);
        ContentPanel.Controls.Add(_message);
        ContentPanel.Controls.Add(_actions);
        DialogPanel.Controls.Add(_progress);
        _progress.BringToFront();
        HeaderPanel.BringToFront();
        DialogPanel.SizeChanged += OnDialogSizeChanged;
        _primary.Click += (_, _) => Request(ScreenCommandKind.CloseOverlay);
        _cancel.Click += (_, _) => Request(ScreenCommandKind.CancelOperation);
        _localization.CultureChanged += OnCultureChanged;
        ApplyKind(kind);
    }

    public ScreenOverlayKind Kind => _kind;

    public string Message
    {
        get => _message.Text;
        set => _message.Text = value ?? string.Empty;
    }

    public void SetMessage(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        Message = message;
    }

    public ProgressBar Progress => _progress;

    public Control Spinner => _progress;

    public Button PrimaryButton => _primary;

    public Button CancelOperationButton => _cancel;

    public void ApplyKind(ScreenOverlayKind kind)
    {
        if (kind is not ScreenOverlayKind.Wait and not ScreenOverlayKind.Confirm and not ScreenOverlayKind.Alert)
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "An operation overlay kind is required.");
        }

        _kind = kind;
        Name = $"{kind}Overlay";
        bool isWait = kind == ScreenOverlayKind.Wait;
        DialogPanel.BackColor = isWait ? Color.Transparent : SurfaceVisuals.Card;
        HeaderPanel.Visible = !isWait;
        ContentPanel.Visible = !isWait;
        _message.Visible = !isWait;
        _progress.Visible = isWait;
        _actions.Visible = !isWait;
        _cancel.Visible = kind == ScreenOverlayKind.Confirm;
        _primary.Visible = kind is ScreenOverlayKind.Confirm or ScreenOverlayKind.Alert;
        CloseButton.Visible = !isWait;
        CenterSpinner();
        ApplyLocalization();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _localization.CultureChanged -= OnCultureChanged;
            DialogPanel.SizeChanged -= OnDialogSizeChanged;
        }

        base.Dispose(disposing);
    }

    private void ApplyLocalization()
    {
        OverlayTitle = _kind switch
        {
            ScreenOverlayKind.Wait => string.Empty,
            ScreenOverlayKind.Confirm => _localization["SurfaceConfirmTitle"],
            ScreenOverlayKind.Alert => _localization["ErrorTitle"],
            _ => throw new InvalidOperationException("The operation overlay kind is not initialized."),
        };
        _primary.Text = _localization["SurfaceOk"];
        _cancel.Text = _kind == ScreenOverlayKind.Wait
            ? _localization["MenuCancel"]
            : _localization["PrintCancel"];
        if (string.IsNullOrWhiteSpace(_message.Text))
        {
            _message.Text = _kind switch
            {
                ScreenOverlayKind.Wait => _localization["StatusWorking"],
                ScreenOverlayKind.Confirm => _localization["UnsavedChangesMessage"],
                ScreenOverlayKind.Alert => _localization["UnexpectedError"],
                _ => string.Empty,
            };
        }
    }

    private void OnCultureChanged(object? sender, EventArgs eventArgs) => ApplyLocalization();

    private void OnDialogSizeChanged(object? sender, EventArgs eventArgs) => CenterSpinner();

    private void CenterSpinner()
    {
        _progress.Location = new Point(
            Math.Max(0, (DialogPanel.ClientSize.Width - _progress.Width) / 2),
            Math.Max(0, (DialogPanel.ClientSize.Height - _progress.Height) / 2));
    }

    private sealed class CircularSpinnerControl : ProgressBar
    {
        private const int LogicalDiameter = 90;
        private const float LogicalStrokeWidth = 8f;

        internal CircularSpinnerControl()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.SupportsTransparentBackColor |
                ControlStyles.UserPaint,
                true);
            AccessibleName = "Wait spinner";
            BackColor = Color.Transparent;
            MarqueeAnimationSpeed = 24;
            Name = "OperationOverlaySpinner";
            Size = new Size(LogicalDiameter, LogicalDiameter);
            Style = ProgressBarStyle.Marquee;
            TabStop = false;
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            base.OnPaintBackground(eventArgs);
            Graphics graphics = eventArgs.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float scale = DeviceDpi / 96f;
            float stroke = LogicalStrokeWidth * scale;
            float diameter = Math.Max(1, Math.Min(ClientSize.Width, ClientSize.Height) - stroke - 2);
            RectangleF ring = new(
                (ClientSize.Width - diameter) / 2f,
                (ClientSize.Height - diameter) / 2f,
                diameter,
                diameter);
            using Pen pen = new(Color.White, stroke)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
            };
            graphics.DrawArc(pen, ring, -80, 290);
        }
    }
}
