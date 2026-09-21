using PDF_Manager.Resources;

namespace PDF_Manager.Shell.ScreenComposition.Core;

public sealed class ControlPanelRequestedEventArgs(bool isOpen) : EventArgs
{
    public bool IsOpen { get; } = isOpen;
}

public sealed class OptionalHeaderControl : UserControl
{
    private static readonly Color HeaderBackColor = Color.FromArgb(230, 230, 230);
    private static readonly Color ButtonBackColor = Color.FromArgb(34, 34, 34);

    private readonly ScreenRouteController _controller;
    private readonly LocalizationService? _localization;
    private readonly TableLayoutPanel _layout = new()
    {
        BackColor = HeaderBackColor,
        ColumnCount = 3,
        Dock = DockStyle.Fill,
        Margin = Padding.Empty,
        Name = "OptionalHeaderLayout",
        Padding = Padding.Empty,
        RowCount = 1,
    };
    private readonly FlowLayoutPanel _dimensionArea = new()
    {
        Anchor = AnchorStyles.Left,
        AutoSize = true,
        BackColor = HeaderBackColor,
        FlowDirection = FlowDirection.LeftToRight,
        Margin = new Padding(14, 5, 0, 5),
        Name = "DimensionSelector",
        WrapContents = false,
    };
    private readonly FlowLayoutPanel _centerArea = new()
    {
        Anchor = AnchorStyles.None,
        AutoSize = true,
        BackColor = HeaderBackColor,
        FlowDirection = FlowDirection.LeftToRight,
        Margin = Padding.Empty,
        Name = "OptionalHeaderCenter",
        WrapContents = false,
    };
    private readonly FlowLayoutPanel _controlArea = new()
    {
        Anchor = AnchorStyles.Right,
        AutoSize = true,
        BackColor = HeaderBackColor,
        FlowDirection = FlowDirection.LeftToRight,
        Margin = new Padding(0, 7, 16, 6),
        Name = "ControlArea",
        WrapContents = false,
    };
    private readonly Button _twoDimensional = CreateButton("Dimension2DButton", "2D", 50, 30);
    private readonly Button _threeDimensional = CreateButton("Dimension3DButton", "3D", 50, 30);
    private readonly Panel _pager = new()
    {
        BackColor = Color.Transparent,
        Margin = Padding.Empty,
        Name = "PageSelector",
        Size = new Size(124, 27),
    };
    private readonly Button _previous = CreateButton("PreviousPageButton", "▲", 27, 13);
    private readonly PageSelectorLabel _page = new()
    {
        AutoSize = false,
        BackColor = ButtonBackColor,
        ForeColor = Color.White,
        Location = new Point(28, 0),
        Margin = Padding.Empty,
        Name = "PageIndicator",
        Width = 96,
        Height = 27,
    };
    private readonly Button _next = CreateButton("NextPageButton", "▼", 27, 14);
    private readonly Button _control = CreateControlButton();
    private readonly List<Button> _contextButtons = [];
    private bool _controlOpen;

    public OptionalHeaderControl(ScreenRouteController controller)
        : this(controller, null)
    {
    }

    public OptionalHeaderControl(ScreenRouteController controller, LocalizationService? localization)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        _localization = localization;
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = HeaderBackColor;
        Dock = DockStyle.Top;
        Height = 40;
        MinimumSize = new Size(0, 40);
        MaximumSize = new Size(0, 40);
        Name = "OptionalHeader";

        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 124));
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        _layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _layout.Controls.Add(_dimensionArea, 0, 0);
        _layout.Controls.Add(_centerArea, 1, 0);
        _layout.Controls.Add(_controlArea, 2, 0);
        _twoDimensional.Margin = new Padding(0, 0, 2, 0);
        _dimensionArea.Controls.Add(_twoDimensional);
        _dimensionArea.Controls.Add(_threeDimensional);
        _controlArea.Controls.Add(_control);
        _previous.AccessibleName = "Previous page";
        _previous.Font = new Font((SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily, 5.5f);
        _previous.Location = Point.Empty;
        _next.AccessibleName = "Next page";
        _next.Font = new Font((SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily, 5.5f);
        _next.Location = new Point(0, 13);
        _pager.Controls.Add(_previous);
        _pager.Controls.Add(_next);
        _pager.Controls.Add(_page);
        Controls.Add(_layout);

        _twoDimensional.Tag = ViewDimension.TwoDimensional;
        _threeDimensional.Tag = ViewDimension.ThreeDimensional;
        _twoDimensional.Click += OnDimensionClick;
        _threeDimensional.Click += OnDimensionClick;
        _previous.Click += OnPreviousClick;
        _next.Click += OnNextClick;
        _control.Click += OnControlClick;
        if (_localization is not null)
        {
            _localization.CultureChanged += OnCultureChanged;
        }

        ApplyLocalization();
        ApplyState(_controller.State);
    }

    public event EventHandler<ControlPanelRequestedEventArgs>? ControlPanelRequested;

    public IReadOnlyList<Button> ContextButtons => _contextButtons;
    public Button TwoDimensionalButton => _twoDimensional;
    public Button ThreeDimensionalButton => _threeDimensional;
    public Button PreviousPageButton => _previous;
    public Button NextPageButton => _next;
    public Label PageIndicator => _page;
    public Button ControlButton => _control;

    public bool IsControlPanelOpen
    {
        get => _controlOpen;
        set
        {
            _controlOpen = value;
            SetSelected(_control, value, useAccentBackground: false);
        }
    }

    public void ApplyState(ScreenRouteState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        SuspendLayout();
        try
        {
            _centerArea.Controls.Clear();
            ClearContextButtons();
            Visible = true;

            if (state.Route.HasValue)
            {
                AddContextButtons(state);
                _centerArea.Controls.Add(_pager);
            }

            SetSelected(_twoDimensional, state.Dimension == ViewDimension.TwoDimensional, useAccentBackground: false);
            SetSelected(_threeDimensional, state.Dimension == ViewDimension.ThreeDimensional, useAccentBackground: false);
            _page.Text = Localized(
                $"Page {state.Page.Index + 1}",
                $"{state.Page.Index + 1}ページ",
                $"第{state.Page.Index + 1}页");
            _page.AccessibleName = $"{_page.Text} ({state.Page.Index + 1} / {state.Page.Count})";
            _previous.Enabled = state.Page.CanMovePrevious;
            _next.Enabled = state.Page.CanMoveNext;
        }
        finally
        {
            ResumeLayout(performLayout: true);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _twoDimensional.Click -= OnDimensionClick;
            _threeDimensional.Click -= OnDimensionClick;
            _previous.Click -= OnPreviousClick;
            _next.Click -= OnNextClick;
            _control.Click -= OnControlClick;
            _control.Image?.Dispose();
            _previous.Font?.Dispose();
            _next.Font?.Dispose();
            if (_localization is not null)
            {
                _localization.CultureChanged -= OnCultureChanged;
            }

            ClearContextButtons();
        }

        base.Dispose(disposing);
    }

    private static Button CreateButton(string name, string text, int width, int height) => new()
    {
        AutoSize = false,
        BackColor = ButtonBackColor,
        Cursor = Cursors.Hand,
        FlatAppearance = { BorderSize = 0, MouseOverBackColor = Color.FromArgb(54, 54, 54) },
        FlatStyle = FlatStyle.Flat,
        ForeColor = Color.White,
        Height = height,
        Margin = Padding.Empty,
        Name = name,
        Text = text,
        UseVisualStyleBackColor = false,
        Width = width,
    };

    private static Button CreateControlButton() => new()
    {
        AutoSize = false,
        BackColor = ButtonBackColor,
        Cursor = Cursors.Hand,
        FlatAppearance = { BorderSize = 0, MouseOverBackColor = Color.FromArgb(54, 54, 54) },
        FlatStyle = FlatStyle.Flat,
        ForeColor = Color.White,
        Height = 27,
        Image = CreateControlGlyph(),
        ImageAlign = ContentAlignment.MiddleLeft,
        Margin = Padding.Empty,
        Name = "ControlButton",
        Padding = new Padding(7, 0, 4, 0),
        Text = "Control",
        TextImageRelation = TextImageRelation.ImageBeforeText,
        UseVisualStyleBackColor = false,
        Width = 102,
    };

    private static Bitmap CreateControlGlyph()
    {
        Bitmap image = new(18, 18);
        using Graphics graphics = Graphics.FromImage(image);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using Pen pen = new(Color.White, 1.25f)
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap = System.Drawing.Drawing2D.LineCap.Round,
        };
        using SolidBrush brush = new(Color.White);
        foreach ((float y, float knobX) in new[] { (4f, 6f), (9f, 12f), (14f, 8f) })
        {
            graphics.DrawLine(pen, 2, y, 16, y);
            graphics.FillEllipse(brush, knobX - 2, y - 2, 4, 4);
        }

        return image;
    }

    private static void SetSelected(Button button, bool selected, bool useAccentBackground = true)
    {
        button.BackColor = selected && useAccentBackground
            ? ScreenCompositionStyle.AccentColor
            : ButtonBackColor;
        button.ForeColor = selected && !useAccentBackground
            ? ScreenCompositionStyle.AccentColor
            : Color.White;
    }

    private void AddContextButtons(ScreenRouteState state)
    {
        foreach ((ScreenRouteContext context, string text) in GetContextOptions(state))
        {
            Button button = CreateButton(
                $"Context{context}Button",
                text,
                Math.Max(72, TextRenderer.MeasureText(text, Font).Width + 18),
                27);
            button.Margin = new Padding(0, 0, 16, 0);
            button.Tag = context;
            button.Click += OnContextClick;
            SetSelected(button, context == state.Context);
            _contextButtons.Add(button);
            _centerArea.Controls.Add(button);
        }
    }

    private IReadOnlyList<(ScreenRouteContext Context, string Text)> GetContextOptions(
        ScreenRouteState state)
    {
        return state.Context switch
        {
            ScreenRouteContext.Members or ScreenRouteContext.RigidZone =>
                [(ScreenRouteContext.Members, Localized("Member Data", "部材データ", "构件输入")),
                 (ScreenRouteContext.RigidZone, Localized("Rigid Zone", "剛域", "硬区"))],
            ScreenRouteContext.LoadNames or ScreenRouteContext.Loads =>
                [(ScreenRouteContext.LoadNames, Localized("Name of Load", "荷重名称", "载重名称")),
                 (ScreenRouteContext.Loads, Localized("Load Strength", "荷重強度", "载重负荷"))],
            ScreenRouteContext.Define or ScreenRouteContext.Combine or ScreenRouteContext.Pickup =>
                [(ScreenRouteContext.Define, "DEFINE"),
                 (ScreenRouteContext.Combine, Localized("COMBINE", "コンバイン", "组合")),
                 (ScreenRouteContext.Pickup, Localized("PICKUP", "ピックアップ", "聚集"))],
            ScreenRouteContext.BasicResult or ScreenRouteContext.CombinedResult or ScreenRouteContext.PickupResult =>
                [(ScreenRouteContext.BasicResult, Localized("Basic Case", "基本ケース", "基本载重案例")),
                 (ScreenRouteContext.CombinedResult, Localized("COMBINE", "コンバイン", "组合")),
                 (ScreenRouteContext.PickupResult, Localized("PICKUP", "ピックアップ", "聚集"))],
            _ => [],
        };
    }

    private string Localized(string english, string japanese, string chinese) =>
        _localization?.Language switch
        {
            UiLanguage.Japanese => japanese,
            UiLanguage.Chinese => chinese,
            _ => english,
        };

    private void ApplyLocalization() =>
        _control.Text = Localized("Control", "コントロール", "控制");

    private void OnCultureChanged(object? sender, EventArgs eventArgs)
    {
        ApplyLocalization();
        ApplyState(_controller.State);
    }

    private void ClearContextButtons()
    {
        foreach (Button button in _contextButtons)
        {
            button.Click -= OnContextClick;
            button.Dispose();
        }

        _contextButtons.Clear();
    }

    private void OnDimensionClick(object? sender, EventArgs eventArgs)
    {
        if (sender is Button { Tag: ViewDimension dimension })
        {
            _controller.SetDimension(dimension);
        }
    }

    private void OnContextClick(object? sender, EventArgs eventArgs)
    {
        if (sender is Button { Tag: ScreenRouteContext context })
        {
            _controller.SelectContext(context);
        }
    }

    private void OnControlClick(object? sender, EventArgs eventArgs)
    {
        IsControlPanelOpen = !IsControlPanelOpen;
        ControlPanelRequested?.Invoke(this, new ControlPanelRequestedEventArgs(IsControlPanelOpen));
    }

    private void OnPreviousClick(object? sender, EventArgs eventArgs) => _controller.MovePage(-1);
    private void OnNextClick(object? sender, EventArgs eventArgs) => _controller.MovePage(1);

    private sealed class PageSelectorLabel : Label
    {
        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            Graphics graphics = eventArgs.Graphics;
            graphics.Clear(BackColor);
            using Pen pen = new(ForeColor, 1.15f);
            using SolidBrush brush = new(ForeColor);
            for (int index = 0; index < 3; index++)
            {
                float y = 8 + (index * 5);
                graphics.FillEllipse(brush, 9, y - 1, 2, 2);
                graphics.DrawLine(pen, 14, y, 23, y);
            }

            TextRenderer.DrawText(
                graphics,
                Text,
                Font,
                new Rectangle(30, 0, Math.Max(0, Width - 34), Height),
                ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        }
    }
}
