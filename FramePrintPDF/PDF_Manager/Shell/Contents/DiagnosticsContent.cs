using PDF_Manager.Core.Shell;
using PDF_Manager.Resources;
using WeifenLuo.WinFormsUI.Docking;

namespace PDF_Manager.Shell.Contents;

public sealed class DiagnosticsContent : ShellDockContent
{
    private readonly ListBox _messages = new()
    {
        BorderStyle = BorderStyle.None,
        Dock = DockStyle.Fill,
        Name = "DiagnosticsMessages",
    };

    private readonly ProgressBar _progress = new()
    {
        Dock = DockStyle.Bottom,
        Height = 5,
        MarqueeAnimationSpeed = 30,
        Name = "OperationProgress",
        Style = ProgressBarStyle.Blocks,
    };

    private readonly Label _status = new()
    {
        AutoEllipsis = true,
        Dock = DockStyle.Bottom,
        Height = 28,
        Name = "OperationStatus",
        Padding = new Padding(6),
    };

    private string? _statusResourceKey;

    public DiagnosticsContent(DocumentKey contentKey, LocalizationService localization)
        : base(contentKey, localization)
    {
        DockAreas = DockAreas.DockBottom | DockAreas.DockTop | DockAreas.Float;
        ShowHint = WeifenLuo.WinFormsUI.Docking.DockState.DockBottom;
        Controls.Add(_messages);
        Controls.Add(_status);
        Controls.Add(_progress);
        SetStatusResource("StatusReady");
        ApplyLocalization();
    }

    public ListBox Messages => _messages;

    public ProgressBar Progress => _progress;

    public Label StatusLabel => _status;

    public void SetBusy(bool isBusy)
    {
        _progress.Style = isBusy ? ProgressBarStyle.Marquee : ProgressBarStyle.Blocks;
        _progress.MarqueeAnimationSpeed = isBusy ? 30 : 0;
        if (isBusy)
        {
            SetStatusResource("StatusWorking");
        }
    }

    public void SetStatusResource(string resourceKey)
    {
        _statusResourceKey = resourceKey;
        _status.Text = Localization[resourceKey];
    }

    public void Report(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        _messages.Items.Add(message);
        _messages.SelectedIndex = _messages.Items.Count - 1;
        _statusResourceKey = null;
        _status.Text = message;
    }

    public override void ApplyLocalization()
    {
        Text = Localization["PaneDiagnostics"];
        TabText = Text;
        if (_statusResourceKey is not null)
        {
            _status.Text = Localization[_statusResourceKey];
        }
    }
}
