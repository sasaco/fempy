using FrameWebforCsharp.Resources;

namespace FrameWebforCsharp.Shell.ScreenComposition.Core;

public sealed class NavigationRequestedEventArgs(PrimaryNavigationId navigation) : EventArgs
{
    public PrimaryNavigationId Navigation { get; } = navigation;
}

public sealed class PrimaryNavigationControl : UserControl
{
    private const int ExpandedWidth = 124;
    private const int CollapsedWidth = 56;

    private readonly FlowLayoutPanel _items = new()
    {
        AutoScroll = true,
        BackColor = Color.Transparent,
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.TopDown,
        Name = "PrimaryNavigationItems",
        Padding = new Padding(4, 8, 4, 42),
        WrapContents = false,
    };
    private readonly Button _toggle = new()
    {
        BackColor = Color.Transparent,
        Dock = DockStyle.Bottom,
        FlatAppearance = { BorderSize = 0, MouseOverBackColor = Color.FromArgb(220, 224, 228) },
        FlatStyle = FlatStyle.Flat,
        ForeColor = Color.FromArgb(32, 45, 56),
        Height = 34,
        Name = "PrimaryNavigationToggleButton",
        Text = "<",
        UseVisualStyleBackColor = false,
    };
    private readonly Dictionary<PrimaryNavigationId, Button> _buttons = [];
    private readonly LocalizationService? _localization;
    private bool _expanded = true;

    public PrimaryNavigationControl()
        : this(null)
    {
    }

    public PrimaryNavigationControl(LocalizationService? localization)
    {
        _localization = localization;
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.White;
        Dock = DockStyle.Left;
        Name = "PrimaryNavigation";
        Width = ExpandedWidth;
        Controls.Add(_items);
        Controls.Add(_toggle);

        foreach (PrimaryNavigationDefinition definition in AngularScreenManifest.PrimaryNavigation)
        {
            if (definition.Id is PrimaryNavigationId.Elements or PrimaryNavigationId.Displacements)
            {
                _items.Controls.Add(CreateSeparator($"{definition.Id}Separator"));
            }

            Button button = CreateButton(definition);
            _buttons.Add(definition.Id, button);
            _items.Controls.Add(button);
        }

        _toggle.Click += OnToggleClick;
        if (_localization is not null)
        {
            _localization.CultureChanged += OnCultureChanged;
        }

        ApplyExpandedState();
    }

    public event EventHandler<NavigationRequestedEventArgs>? NavigationRequested;

    public IReadOnlyDictionary<PrimaryNavigationId, Button> Buttons => _buttons;

    public Button ToggleButton => _toggle;

    public bool IsExpanded
    {
        get => _expanded;
        set
        {
            if (_expanded == value)
            {
                return;
            }

            _expanded = value;
            ApplyExpandedState();
        }
    }

    public void ApplyState(ScreenRouteState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        PrimaryNavigationId? active = state.Route is ScreenRouteId route
            ? AngularScreenManifest.GetRoute(route).Navigation
            : null;
        foreach (PrimaryNavigationDefinition definition in AngularScreenManifest.PrimaryNavigation)
        {
            Button button = _buttons[definition.Id];
            bool isCalculate = definition.Id == PrimaryNavigationId.Calculate;
            button.Enabled = !definition.RequiresResults || state.ResultsEnabled;
            button.Visible = !definition.ThreeDimensionalOnly || state.Dimension == ViewDimension.ThreeDimensional;
            button.BackColor = definition.Id == active
                ? ScreenCompositionStyle.AccentColor
                : isCalculate
                    ? ScreenCompositionStyle.HeaderBackColor
                    : Color.FromArgb(217, 217, 217);
            button.ForeColor = button.Enabled
                ? isCalculate || definition.Id == active ? Color.White : Color.FromArgb(32, 45, 56)
                : Color.FromArgb(160, 160, 160);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toggle.Click -= OnToggleClick;
            if (_localization is not null)
            {
                _localization.CultureChanged -= OnCultureChanged;
            }

            foreach (Button button in _buttons.Values)
            {
                button.Click -= OnButtonClick;
                button.Region?.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    private static Control CreateSeparator(string name)
    {
        int bottomMargin = name.StartsWith(nameof(PrimaryNavigationId.Elements), StringComparison.Ordinal)
            ? 16
            : 8;
        return new Panel
        {
            BackColor = Color.FromArgb(194, 194, 194),
            Height = 2,
            Margin = new Padding(4, 8, 4, bottomMargin),
            Name = name,
            Width = 108,
        };
    }

    private Button CreateButton(PrimaryNavigationDefinition definition)
    {
        bool isCalculate = definition.Id == PrimaryNavigationId.Calculate;
        Button button = new NavigationGlyphButton(definition.Id)
        {
            AutoEllipsis = false,
            BackColor = isCalculate ? ScreenCompositionStyle.HeaderBackColor : Color.FromArgb(217, 217, 217),
            Cursor = Cursors.Hand,
            FlatAppearance =
            {
                BorderSize = 0,
                MouseDownBackColor = isCalculate
                    ? ScreenCompositionStyle.HeaderBackColor
                    : ScreenCompositionStyle.SelectedBackColor,
                MouseOverBackColor = isCalculate
                    ? ScreenCompositionStyle.HeaderBackColor
                    : ScreenCompositionStyle.SelectedBackColor,
            },
            FlatStyle = FlatStyle.Flat,
            ForeColor = isCalculate ? Color.White : Color.FromArgb(32, 45, 56),
            Height = isCalculate ? 40 : 32,
            Margin = new Padding(0, 0, 0, 8),
            Name = $"Navigation{definition.Id}Button",
            Padding = new Padding(28, 0, 0, 0),
            Tag = definition.Id,
            Text = GetNavigationText(definition.Id),
            TextAlign = ContentAlignment.MiddleLeft,
            UseVisualStyleBackColor = false,
            Width = 116,
        };
        button.Region = RoundedRegion(button.Width, button.Height, isCalculate ? 4 : 16);
        button.Click += OnButtonClick;
        return button;
    }

    private static Region RoundedRegion(int width, int height, int radius)
    {
        using System.Drawing.Drawing2D.GraphicsPath path = new();
        int diameter = radius * 2;
        path.AddArc(0, 0, diameter, diameter, 180, 90);
        path.AddArc(width - diameter, 0, diameter, diameter, 270, 90);
        path.AddArc(width - diameter, height - diameter, diameter, diameter, 0, 90);
        path.AddArc(0, height - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return new Region(path);
    }

    private void ApplyExpandedState()
    {
        Width = _expanded ? ExpandedWidth : CollapsedWidth;
        _toggle.Text = _expanded ? "<" : ">";
        foreach ((PrimaryNavigationId id, Button button) in _buttons)
        {
            bool isCalculate = id == PrimaryNavigationId.Calculate;
            button.Width = _expanded ? 116 : isCalculate ? 40 : 32;
            string localizedText = GetNavigationText(id);
            button.Text = _expanded ? localizedText : string.Empty;
            button.Padding = _expanded ? new Padding(28, 0, 0, 0) : Padding.Empty;
            button.AccessibleName = localizedText;
            button.Region?.Dispose();
            button.Region = RoundedRegion(button.Width, button.Height, isCalculate ? 4 : 16);
        }

        foreach (Panel separator in _items.Controls.OfType<Panel>())
        {
            separator.Width = _expanded ? 108 : 32;
        }
    }

    private void OnToggleClick(object? sender, EventArgs eventArgs) => IsExpanded = !IsExpanded;

    private void OnCultureChanged(object? sender, EventArgs eventArgs) => ApplyExpandedState();

    private string GetNavigationText(PrimaryNavigationId navigation) => navigation switch
    {
        PrimaryNavigationId.Calculate => Localized("Calculate", "計算", "计算"),
        PrimaryNavigationId.Elements => Localized("Material", "材料", "材料"),
        PrimaryNavigationId.Nodes => Localized("Node", "節点", "节点"),
        PrimaryNavigationId.Supports => Localized("Support", "支点", "支点"),
        PrimaryNavigationId.Members => Localized("Member", "部材", "构件"),
        PrimaryNavigationId.Panel => Localized("Panel", "パネル（β版）", "面板"),
        PrimaryNavigationId.Joints => Localized("Joint", "結合", "连接"),
        PrimaryNavigationId.NoticePoints => Localized("Location", "着目点", "焦距点"),
        PrimaryNavigationId.MemberSprings => Localized("Spring", "バネ", "弹簧"),
        PrimaryNavigationId.Loads => Localized("Load", "荷重", "载重"),
        PrimaryNavigationId.Define => Localized("Combination", "組合せ", "组合"),
        PrimaryNavigationId.Displacements => Localized("Displacement", "変位", "位移"),
        PrimaryNavigationId.Reactions => Localized("Reaction", "支点反力", "支座反力"),
        PrimaryNavigationId.SectionForces => Localized("Member Section Force", "部材断面力", "构件截面力"),
        _ => throw new ArgumentOutOfRangeException(nameof(navigation), navigation, "Unknown navigation item."),
    };

    private string Localized(string english, string japanese, string chinese) =>
        _localization?.Language switch
        {
            UiLanguage.Japanese => japanese,
            UiLanguage.Chinese => chinese,
            _ => english,
        };

    private void OnButtonClick(object? sender, EventArgs eventArgs)
    {
        if (sender is Button { Tag: PrimaryNavigationId navigation })
        {
            NavigationRequested?.Invoke(this, new NavigationRequestedEventArgs(navigation));
        }
    }

    private sealed class NavigationGlyphButton(PrimaryNavigationId navigation) : Button
    {
        private readonly PrimaryNavigationId _navigation = navigation;

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            Graphics graphics = eventArgs.Graphics;
            graphics.Clear(Color.White);
            System.Drawing.Drawing2D.GraphicsState graphicsState = graphics.Save();
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            int radius = _navigation == PrimaryNavigationId.Calculate ? 4 : Height / 2;
            using (System.Drawing.Drawing2D.GraphicsPath background = RoundedPath(ClientRectangle, radius))
            using (SolidBrush backgroundBrush = new(BackColor))
            {
                graphics.FillPath(backgroundBrush, background);
            }

            if (!string.IsNullOrEmpty(Text))
            {
                Rectangle textBounds = new(28, 0, Math.Max(0, Width - 30), Height);
                TextFormatFlags flags = TextFormatFlags.Left
                    | TextFormatFlags.VerticalCenter
                    | TextFormatFlags.NoPadding
                    | TextFormatFlags.SingleLine;
                Font? fittedFont = null;
                Font textFont = Font;
                if (TextRenderer.MeasureText(Text, textFont, Size.Empty, flags).Width > textBounds.Width)
                {
                    fittedFont = new Font(textFont.FontFamily, Math.Max(7f, textFont.Size - 1.25f), textFont.Style);
                    textFont = fittedFont;
                }

                TextRenderer.DrawText(
                    graphics,
                    Text,
                    textFont,
                    textBounds,
                    Enabled ? ForeColor : Color.FromArgb(160, 160, 160),
                    flags);
                fittedFont?.Dispose();
            }

            float x = string.IsNullOrEmpty(Text) ? (Width - 20) / 2f : 9f;
            float y = (Height - 20) / 2f;
            graphics.TranslateTransform(x, y);

            Color color = Enabled ? ForeColor : Color.FromArgb(160, 160, 160);
            using Pen pen = new(color, 1.8f)
            {
                StartCap = System.Drawing.Drawing2D.LineCap.Round,
                EndCap = System.Drawing.Drawing2D.LineCap.Round,
                LineJoin = System.Drawing.Drawing2D.LineJoin.Round,
            };
            using SolidBrush brush = new(color);
            DrawGlyph(graphics, pen, brush, _navigation);
            graphics.Restore(graphicsState);

            if (Focused && ShowFocusCues)
            {
                ControlPaint.DrawFocusRectangle(graphics, Rectangle.Inflate(ClientRectangle, -3, -3));
            }
        }

        private static System.Drawing.Drawing2D.GraphicsPath RoundedPath(Rectangle bounds, int radius)
        {
            System.Drawing.Drawing2D.GraphicsPath path = new();
            float diameter = Math.Min(Math.Min(bounds.Width, bounds.Height), radius * 2f);
            RectangleF arc = new(bounds.Left, bounds.Top, diameter, diameter);
            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static void DrawGlyph(
            Graphics graphics,
            Pen pen,
            Brush brush,
            PrimaryNavigationId navigation)
        {
            switch (navigation)
            {
                case PrimaryNavigationId.Calculate:
                    graphics.DrawRoundedRectangle(pen, new RectangleF(3, 1, 14, 18), 2);
                    graphics.DrawRectangle(pen, 6, 4, 8, 4);
                    foreach (PointF point in new[] { new PointF(7, 11), new PointF(11, 11), new PointF(7, 15), new PointF(11, 15) })
                    {
                        graphics.FillRectangle(brush, point.X, point.Y, 2, 2);
                    }

                    break;
                case PrimaryNavigationId.Elements:
                    graphics.DrawLine(pen, 2, 5, 18, 5);
                    graphics.DrawLine(pen, 2, 15, 18, 15);
                    graphics.DrawLine(pen, 10, 5, 10, 15);
                    break;
                case PrimaryNavigationId.Nodes:
                    graphics.DrawLine(pen, 4, 15, 10, 4);
                    graphics.DrawLine(pen, 10, 4, 17, 14);
                    DrawNode(graphics, pen, 4, 15);
                    DrawNode(graphics, pen, 10, 4);
                    DrawNode(graphics, pen, 17, 14);
                    break;
                case PrimaryNavigationId.Supports:
                    graphics.DrawLine(pen, 10, 3, 10, 10);
                    graphics.DrawPolygon(pen, [new PointF(10, 10), new PointF(4, 17), new PointF(16, 17)]);
                    graphics.DrawLine(pen, 2, 19, 18, 19);
                    break;
                case PrimaryNavigationId.Members:
                    graphics.DrawLine(pen, 3, 16, 17, 4);
                    graphics.DrawLine(pen, 3, 13, 17, 1);
                    DrawNode(graphics, pen, 3, 15);
                    DrawNode(graphics, pen, 17, 3);
                    break;
                case PrimaryNavigationId.Panel:
                    graphics.DrawRectangle(pen, 3, 3, 14, 14);
                    graphics.DrawLine(pen, 3, 3, 17, 17);
                    graphics.DrawLine(pen, 17, 3, 3, 17);
                    break;
                case PrimaryNavigationId.Joints:
                    graphics.DrawLine(pen, 2, 16, 9, 10);
                    graphics.DrawLine(pen, 18, 16, 11, 10);
                    graphics.DrawLine(pen, 10, 9, 10, 2);
                    graphics.FillEllipse(brush, 7.5f, 7.5f, 5, 5);
                    break;
                case PrimaryNavigationId.NoticePoints:
                    graphics.DrawEllipse(pen, 3, 3, 14, 14);
                    graphics.DrawLine(pen, 10, 0, 10, 20);
                    graphics.DrawLine(pen, 0, 10, 20, 10);
                    graphics.FillEllipse(brush, 8, 8, 4, 4);
                    break;
                case PrimaryNavigationId.MemberSprings:
                    graphics.DrawLine(pen, 1, 10, 4, 10);
                    graphics.DrawLines(pen, [new PointF(4, 10), new PointF(6, 5), new PointF(9, 15), new PointF(12, 5), new PointF(15, 15), new PointF(17, 10)]);
                    graphics.DrawLine(pen, 17, 10, 19, 10);
                    break;
                case PrimaryNavigationId.Loads:
                    graphics.DrawLine(pen, 10, 1, 10, 17);
                    graphics.DrawLine(pen, 10, 17, 5, 11);
                    graphics.DrawLine(pen, 10, 17, 15, 11);
                    graphics.DrawLine(pen, 3, 19, 17, 19);
                    break;
                case PrimaryNavigationId.Define:
                    DrawList(graphics, pen, brush, includeArrow: false);
                    break;
                case PrimaryNavigationId.Displacements:
                    graphics.DrawLine(pen, 2, 10, 18, 10);
                    graphics.DrawLine(pen, 2, 10, 7, 5);
                    graphics.DrawLine(pen, 2, 10, 7, 15);
                    graphics.DrawLine(pen, 18, 10, 13, 5);
                    graphics.DrawLine(pen, 18, 10, 13, 15);
                    break;
                case PrimaryNavigationId.Reactions:
                    graphics.DrawPolygon(pen, [new PointF(10, 8), new PointF(4, 15), new PointF(16, 15)]);
                    graphics.DrawLine(pen, 2, 17, 18, 17);
                    graphics.DrawLine(pen, 10, 10, 10, 1);
                    graphics.DrawLine(pen, 10, 1, 6, 5);
                    graphics.DrawLine(pen, 10, 1, 14, 5);
                    break;
                case PrimaryNavigationId.SectionForces:
                    graphics.DrawLine(pen, 2, 18, 2, 2);
                    graphics.DrawLine(pen, 2, 18, 18, 18);
                    graphics.DrawBezier(pen, 3, 15, 7, 2, 13, 18, 18, 5);
                    break;
                default:
                    DrawList(graphics, pen, brush, includeArrow: true);
                    break;
            }
        }

        private static void DrawNode(Graphics graphics, Pen pen, float x, float y) =>
            graphics.DrawEllipse(pen, x - 2, y - 2, 4, 4);

        private static void DrawList(Graphics graphics, Pen pen, Brush brush, bool includeArrow)
        {
            for (int index = 0; index < 3; index++)
            {
                float y = 4 + (index * 6);
                graphics.FillEllipse(brush, 2, y - 1, 2, 2);
                graphics.DrawLine(pen, 7, y, 18, y);
            }

            if (includeArrow)
            {
                graphics.DrawLine(pen, 14, 15, 18, 15);
            }
        }
    }
}

internal static class GraphicsExtensions
{
    public static void DrawRoundedRectangle(this Graphics graphics, Pen pen, RectangleF bounds, float radius)
    {
        float diameter = radius * 2;
        using System.Drawing.Drawing2D.GraphicsPath path = new();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        graphics.DrawPath(pen, path);
    }
}
