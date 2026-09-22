using System.Resources;
using FrameWebforCS.Core.Documents;
using FrameWebforCS.Resources;
using FrameWebforCS.Shell.ScreenComposition.Core;

namespace FrameWebforCS.Shell.ScreenComposition.Surfaces;

public sealed class PresetOverlayControl : OverlaySurfaceBase, IPresetOverlaySurface
{
    private static readonly ResourceManager PresetImages =
        new("FrameWebforCS.Resources.Strings", typeof(LocalizationService).Assembly);
    private readonly LocalizationService _localization;
    private readonly FlowLayoutPanel _tiles = new()
    {
        AutoScroll = true,
        BackColor = SurfaceVisuals.Card,
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.LeftToRight,
        Name = "PresetOverlayTiles",
        Padding = Padding.Empty,
        WrapContents = true,
    };
    private readonly Button _cancel;
    private readonly Button _open;
    private readonly Dictionary<BuiltInProjectPreset, Button> _buttons = [];
    private bool _disposed;

    public PresetOverlayControl(LocalizationService localization)
        : base(new Size(1062, 540))
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        Name = "PresetOverlay";
        Panel footer = new()
        {
            Dock = DockStyle.Bottom,
            BackColor = SurfaceVisuals.Card,
            Height = 40,
            Name = "PresetOverlayActions",
            Padding = new Padding(4),
        };
        FlowLayoutPanel actions = new()
        {
            AutoSize = true,
            Dock = DockStyle.Right,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
        };
        _cancel = SurfaceVisuals.CreateSecondaryButton("PresetCancelButton", string.Empty);
        _open = SurfaceVisuals.CreateActionButton("PresetOpenButton", string.Empty);
        _open.Enabled = false;
        actions.Controls.Add(_cancel);
        actions.Controls.Add(_open);
        footer.Controls.Add(actions);
        foreach (BuiltInProjectPresetDescriptor descriptor in ProjectDocumentPresets.Catalog.OrderBy(value => value.SortOrder))
        {
            Button button = CreatePresetTile(descriptor);
            _buttons.Add(descriptor.Id, button);
            _tiles.Controls.Add(button);
        }

        ContentPanel.Controls.Add(_tiles);
        ContentPanel.Controls.Add(footer);
        _cancel.Click += (_, _) => Request(ScreenCommandKind.CloseOverlay);
        _open.Click += (_, _) => Request(ScreenCommandKind.OpenPreset);
        _localization.CultureChanged += OnCultureChanged;
        ApplyLocalization();
    }

    public BuiltInProjectPreset? SelectedPreset { get; private set; }

    public IReadOnlyDictionary<BuiltInProjectPreset, Button> PresetButtons => _buttons;

    public Button OpenButton => _open;

    public Button CancelButton => _cancel;

    public void SelectPreset(BuiltInProjectPreset preset)
    {
        if (!_buttons.ContainsKey(preset))
        {
            throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unknown built-in preset.");
        }

        SelectedPreset = preset;
        _open.Enabled = true;
        foreach ((BuiltInProjectPreset id, Button button) in _buttons)
        {
            bool selected = id == preset;
            button.BackColor = selected ? Color.FromArgb(64, 67, 71) : Color.FromArgb(63, 66, 73);
            button.FlatAppearance.BorderColor = selected ? SurfaceVisuals.Accent : SurfaceVisuals.Border;
            button.FlatAppearance.BorderSize = selected ? 3 : 1;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _localization.CultureChanged -= OnCultureChanged;
            foreach (Button button in _buttons.Values)
            {
                button.Image?.Dispose();
                button.Image = null;
            }
        }

        base.Dispose(disposing);
    }

    private Button CreatePresetTile(BuiltInProjectPresetDescriptor descriptor)
    {
        Button button = new()
        {
            BackColor = Color.FromArgb(63, 66, 73),
            Cursor = Cursors.Hand,
            FlatStyle = FlatStyle.Flat,
            Font = new Font((SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily, 10, FontStyle.Bold),
            ForeColor = Color.White,
            Image = LoadPresetImage(descriptor.Id),
            ImageAlign = ContentAlignment.TopCenter,
            Margin = new Padding(20),
            Name = $"Preset{descriptor.Id}Button",
            Padding = new Padding(4),
            Size = new Size(314, 275),
            Tag = descriptor.Id,
            TextAlign = ContentAlignment.BottomCenter,
            TextImageRelation = TextImageRelation.ImageAboveText,
            UseVisualStyleBackColor = false,
        };
        button.FlatAppearance.BorderColor = SurfaceVisuals.Border;
        button.Click += (_, _) => SelectPreset(descriptor.Id);
        return button;
    }

    private void ApplyLocalization()
    {
        OverlayTitle = _localization["SurfacePreset"];
        _cancel.Text = _localization["PrintCancel"];
        _open.Text = _localization["SurfaceOpenPreset"];
        foreach ((BuiltInProjectPreset preset, Button button) in _buttons)
        {
            button.Text = _localization[PresetResourceKey(preset)];
            button.AccessibleName = button.Text;
        }
    }

    private static string PresetResourceKey(BuiltInProjectPreset preset) => preset switch
    {
        BuiltInProjectPreset.RamenViaduct => "SurfacePresetRamenViaduct",
        BuiltInProjectPreset.ConcreteTBeamBridge => "SurfacePresetConcreteTBeamBridge",
        BuiltInProjectPreset.UShapedRetainingWall => "SurfacePresetRetainingWall",
        BuiltInProjectPreset.PortalPier => "SurfacePresetPortalPier",
        _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, null),
    };

    private static Bitmap LoadPresetImage(BuiltInProjectPreset preset)
    {
        string resourceKey = preset switch
        {
            BuiltInProjectPreset.RamenViaduct => "SurfacePresetRamenImage",
            BuiltInProjectPreset.ConcreteTBeamBridge => "SurfacePresetConcreteTBeamImage",
            BuiltInProjectPreset.UShapedRetainingWall => "SurfacePresetRetainingWallImage",
            BuiltInProjectPreset.PortalPier => "SurfacePresetPortalPierImage",
            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, null),
        };
        Bitmap source = PresetImages.GetObject(resourceKey) as Bitmap
            ?? throw new MissingManifestResourceException($"The preset image '{resourceKey}' is missing.");
        return new Bitmap(source, new Size(306, 220));
    }

    private void OnCultureChanged(object? sender, EventArgs eventArgs) => ApplyLocalization();
}
