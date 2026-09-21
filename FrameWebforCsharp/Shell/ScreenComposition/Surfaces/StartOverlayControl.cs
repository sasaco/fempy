using FrameWebforCsharp.Resources;
using FrameWebforCsharp.Shell.ScreenComposition.Core;

namespace FrameWebforCsharp.Shell.ScreenComposition.Surfaces;

public sealed class StartOverlayControl : OverlaySurfaceBase
{
    private readonly LocalizationService _localization;
    private readonly Button _newProject = CreateTile("StartNewProjectButton", StartTileGlyph.ControlPoint, 174);
    private readonly Button _openProject = CreateTile("StartOpenProjectButton", StartTileGlyph.FolderOpen, 224);
    private readonly Button _preset = CreateTile("StartPresetButton", StartTileGlyph.ViewModule, 242);

    public StartOverlayControl(LocalizationService localization)
        : base(new Size(760, 400))
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        Name = "StartOverlay";
        DialogPanel.BackColor = Color.Transparent;
        ContentPanel.BackColor = Color.Transparent;
        FlowLayoutPanel tiles = new()
        {
            BackColor = SurfaceVisuals.Card,
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            Height = 166,
            Name = "StartOverlayTiles",
            Padding = Padding.Empty,
            WrapContents = false,
        };
        tiles.Controls.Add(_newProject);
        tiles.Controls.Add(_openProject);
        tiles.Controls.Add(_preset);
        ContentPanel.Controls.Add(tiles);
        _newProject.Click += (_, _) => Request(ScreenCommandKind.NewProject);
        _openProject.Click += (_, _) => Request(ScreenCommandKind.OpenProject);
        _preset.Click += (_, _) => Request(ScreenCommandKind.ShowPreset);
        _localization.CultureChanged += OnCultureChanged;
        ApplyLocalization();
    }

    public Button NewProjectButton => _newProject;

    public Button OpenProjectButton => _openProject;

    public Button PresetButton => _preset;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _localization.CultureChanged -= OnCultureChanged;
        }

        base.Dispose(disposing);
    }

    private static Button CreateTile(string name, StartTileGlyph glyph, int width) => new StartTileButton(glyph)
    {
        Cursor = Cursors.Hand,
        Margin = new Padding(20),
        Name = name,
        Size = new Size(width, 126),
    };

    private void ApplyLocalization()
    {
        OverlayTitle = "Start menu";
        ApplyTileText(_newProject, _localization["SurfaceStartNewProject"]);
        ApplyTileText(_openProject, _localization["SurfaceStartOpenProject"]);
        ApplyTileText(_preset, _localization["SurfaceStartOpenPreset"]);
    }

    private static void ApplyTileText(Button tile, string label)
    {
        tile.Text = label;
        tile.AccessibleName = label;
    }

    private void OnCultureChanged(object? sender, EventArgs eventArgs) => ApplyLocalization();

    private sealed class StartTileButton : Button
    {
        private readonly StartTileGlyph _glyph;

        internal StartTileButton(StartTileGlyph glyph)
        {
            _glyph = glyph;
            BackColor = Color.FromArgb(221, 221, 221);
            FlatAppearance.BorderSize = 0;
            FlatStyle = FlatStyle.Flat;
            ForeColor = SurfaceVisuals.Header;
            UseVisualStyleBackColor = false;
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            eventArgs.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            eventArgs.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            eventArgs.Graphics.Clear(Parent?.BackColor ?? SurfaceVisuals.Card);
            using System.Drawing.Drawing2D.GraphicsPath shape = RoundedRectangle(ClientRectangle, 8);
            using SolidBrush background = new(BackColor);
            using Font labelFont = new((SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily, 12, FontStyle.Bold);
            eventArgs.Graphics.FillPath(background, shape);
            Color foreground = Enabled ? ForeColor : Color.FromArgb(130, ForeColor);
            DrawGlyph(eventArgs.Graphics, foreground);
            TextRenderer.DrawText(
                eventArgs.Graphics,
                Text,
                labelFont,
                new Rectangle(6, 92, Math.Max(1, Width - 12), 25),
                foreground,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            if (Focused && ShowFocusCues)
            {
                ControlPaint.DrawFocusRectangle(eventArgs.Graphics, Rectangle.Inflate(ClientRectangle, -5, -5));
            }
        }

        private void DrawGlyph(Graphics graphics, Color foreground)
        {
            using SolidBrush glyphBrush = new(foreground);
            using SolidBrush cutoutBrush = new(BackColor);
            switch (_glyph)
            {
                case StartTileGlyph.ControlPoint:
                    DrawControlPoint(graphics, glyphBrush, cutoutBrush);
                    break;
                case StartTileGlyph.FolderOpen:
                    DrawFolderOpen(graphics, glyphBrush, cutoutBrush);
                    break;
                case StartTileGlyph.ViewModule:
                    DrawViewModule(graphics, glyphBrush);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported start tile glyph: {_glyph}.");
            }
        }

        private void DrawControlPoint(Graphics graphics, Brush glyphBrush, Brush cutoutBrush)
        {
            Rectangle bounds = new((Width - 42) / 2, 30, 42, 42);
            graphics.FillEllipse(glyphBrush, bounds);
            graphics.FillEllipse(cutoutBrush, Rectangle.Inflate(bounds, -5, -5));
            graphics.FillRectangle(glyphBrush, bounds.X + 10, bounds.Y + 19, 22, 5);
            graphics.FillRectangle(glyphBrush, bounds.X + 19, bounds.Y + 10, 5, 22);
        }

        private void DrawFolderOpen(Graphics graphics, Brush glyphBrush, Brush cutoutBrush)
        {
            int x = (Width - 74) / 2;
            const int y = 22;
            using System.Drawing.Drawing2D.GraphicsPath folder = new();
            folder.AddLine(x + 5, y + 2, x + 29, y + 2);
            folder.AddLine(x + 29, y + 2, x + 37, y + 10);
            folder.AddLine(x + 37, y + 10, x + 68, y + 10);
            folder.AddBezier(x + 68, y + 10, x + 72, y + 10, x + 74, y + 13, x + 74, y + 17);
            folder.AddLine(x + 74, y + 17, x + 74, y + 44);
            folder.AddBezier(x + 74, y + 44, x + 74, y + 48, x + 71, y + 50, x + 67, y + 50);
            folder.AddLine(x + 67, y + 50, x + 7, y + 50);
            folder.AddBezier(x + 7, y + 50, x + 3, y + 50, x, y + 47, x, y + 43);
            folder.AddLine(x, y + 43, x, y + 9);
            folder.AddBezier(x, y + 9, x, y + 5, x + 2, y + 2, x + 5, y + 2);
            folder.CloseFigure();
            graphics.FillPath(glyphBrush, folder);
            graphics.FillRectangle(cutoutBrush, x + 7, y + 18, 60, 25);
        }

        private void DrawViewModule(Graphics graphics, Brush glyphBrush)
        {
            int x = (Width - 67) / 2;
            const int y = 22;
            const int cellWidth = 20;
            const int cellHeight = 23;
            const int gap = 4;
            for (int row = 0; row < 2; row++)
            {
                for (int column = 0; column < 3; column++)
                {
                    graphics.FillRectangle(
                        glyphBrush,
                        x + (column * (cellWidth + gap)),
                        y + (row * (cellHeight + gap)),
                        cellWidth,
                        cellHeight);
                }
            }
        }

        private static System.Drawing.Drawing2D.GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
        {
            System.Drawing.Drawing2D.GraphicsPath path = new();
            int diameter = radius * 2;
            Rectangle arc = new(bounds.X, bounds.Y, diameter, diameter);
            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter - 1;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter - 1;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.X;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    private enum StartTileGlyph
    {
        ControlPoint,
        FolderOpen,
        ViewModule,
    }
}
