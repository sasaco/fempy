using FrameWebforCS.Core.Abstractions;
using FrameWebforCS.Resources;
using FrameWebforCS.Shell.Printing;
using FrameWebforCS.Shell.ScreenComposition.Core;

namespace FrameWebforCS.Shell.ScreenComposition.Surfaces;

public sealed class PrintOverlayControl : OverlaySurfaceBase, IPrintOverlaySurface
{
    private readonly LocalizationService _localization;
    private readonly ComboBox _paperSize = Selector("PrintOverlayPaperSize");
    private readonly ComboBox _orientation = Selector("PrintOverlayOrientation");
    private readonly ComboBox _layout = Selector("PrintOverlayLayout");
    private readonly NumericUpDown _scale = new()
    {
        BackColor = Color.FromArgb(64, 67, 71),
        DecimalPlaces = 0,
        ForeColor = Color.White,
        Increment = 10,
        Maximum = 400,
        Minimum = 25,
        Name = "PrintOverlayScale",
        Value = 100,
    };
    private readonly CheckBox _pageNumbers = new()
    {
        AutoSize = true,
        ForeColor = Color.White,
        Name = "PrintOverlayPageNumbers",
    };
    private readonly CheckedListBox _sections = new()
    {
        CheckOnClick = true,
        BackColor = Color.FromArgb(64, 67, 71),
        BorderStyle = BorderStyle.None,
        Dock = DockStyle.Fill,
        ForeColor = Color.White,
        Name = "PrintOverlaySections",
    };
    private readonly PictureBox _preview = new()
    {
        BackColor = Color.FromArgb(229, 232, 236),
        BorderStyle = BorderStyle.FixedSingle,
        Dock = DockStyle.Fill,
        Name = "PrintOverlayPreview",
        SizeMode = PictureBoxSizeMode.Zoom,
    };
    private readonly Label _page = new() { AutoSize = true, ForeColor = Color.White, Name = "PrintOverlayPage" };
    private readonly Label _error = new()
    {
        AutoEllipsis = true,
        BackColor = Color.FromArgb(255, 235, 238),
        Dock = DockStyle.Top,
        ForeColor = Color.FromArgb(183, 28, 28),
        Height = 30,
        Name = "PrintOverlayError",
        Padding = new Padding(8),
        Visible = false,
    };
    private readonly Button _previous;
    private readonly Button _next;
    private readonly Button _cancel;
    private readonly Button _export;
    private PrintPreviewState? _previewState;
    private bool _modelDiagramSelected;
    private bool _updating;
    private bool _disposed;

    public PrintOverlayControl(LocalizationService localization)
        : base(new Size(1062, 540))
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        Name = "PrintOverlay";
        _previous = SurfaceVisuals.CreateSecondaryButton("PrintOverlayPrevious", string.Empty);
        _next = SurfaceVisuals.CreateSecondaryButton("PrintOverlayNext", string.Empty);
        _cancel = SurfaceVisuals.CreateSecondaryButton("PrintOverlayCancel", string.Empty);
        _export = SurfaceVisuals.CreateActionButton("PrintOverlayExport", "PDF");

        TableLayoutPanel content = new()
        {
            ColumnCount = 2,
            BackColor = SurfaceVisuals.Card,
            Dock = DockStyle.Fill,
            Name = "PrintOverlayContent",
            Padding = Padding.Empty,
            RowCount = 1,
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 290));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.Controls.Add(BuildSelectionPanel(), 0, 0);
        content.Controls.Add(BuildPreviewPanel(), 1, 0);
        Panel footer = BuildFooter();
        ContentPanel.Controls.Add(content);
        ContentPanel.Controls.Add(footer);

        _previous.Click += (_, _) => SelectPage((_previewState?.SelectedPageIndex ?? 0) - 1);
        _next.Click += (_, _) => SelectPage((_previewState?.SelectedPageIndex ?? 0) + 1);
        _cancel.Click += (_, _) => Request(ScreenCommandKind.CloseOverlay);
        _export.Click += (_, _) => Request(ScreenCommandKind.ExportPdf);
        _sections.ItemCheck += OnSectionItemCheck;
        _paperSize.SelectedIndexChanged += OnSettingsChanged;
        _orientation.SelectedIndexChanged += OnSettingsChanged;
        _layout.SelectedIndexChanged += OnSettingsChanged;
        _scale.ValueChanged += OnSettingsChanged;
        _pageNumbers.CheckedChanged += OnSettingsChanged;
        _localization.CultureChanged += OnCultureChanged;
        ApplyLocalization();
    }

    public PrintPageSetupSelection Selection
    {
        get
        {
            HashSet<PrintContentSection> selectedSections = _sections.CheckedItems
                .OfType<SectionItem>()
                .Select(item => item.Section)
                .ToHashSet();
            if (_modelDiagramSelected)
            {
                selectedSections.Add(PrintContentSection.ModelDiagram);
            }

            PrintContentSection[] sections = Enum.GetValues<PrintContentSection>()
                .Where(selectedSections.Contains)
                .ToArray();
            return new PrintPageSetupSelection(
                new PrintPageSettings(
                    (PrintPaperSize)_paperSize.SelectedIndex,
                    (PrintPageOrientation)_orientation.SelectedIndex,
                    PrintPageSettings.Default.LeftMarginMillimetres,
                    PrintPageSettings.Default.TopMarginMillimetres,
                    PrintPageSettings.Default.RightMarginMillimetres,
                    PrintPageSettings.Default.BottomMarginMillimetres,
                    (double)_scale.Value / 100,
                    (PrintLayoutChoice)_layout.SelectedIndex,
                    _pageNumbers.Checked),
                sections);
        }
    }

    public PrintPreviewState? CurrentPreview => _previewState;

    public CheckedListBox SectionSelector => _sections;

    public PictureBox PreviewImage => _preview;

    public Label PageLabel => _page;

    public Button PreviousButton => _previous;

    public Button NextButton => _next;

    public Button ExportButton => _export;

    public Button CancelButton => _cancel;

    public void SetSelection(PrintPageSetupSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        bool wasUpdating = _updating;
        _updating = true;
        try
        {
            _paperSize.SelectedIndex = (int)selection.PageSettings.PaperSize;
            _orientation.SelectedIndex = (int)selection.PageSettings.Orientation;
            _layout.SelectedIndex = (int)selection.PageSettings.Layout;
            _scale.Value = (decimal)(selection.PageSettings.Scale * 100);
            _pageNumbers.Checked = selection.PageSettings.ShowPageNumbers;
            PopulateSections(selection.Sections);
        }
        finally
        {
            _updating = wasUpdating;
        }

        UpdateExportAvailability();
    }

    public void SetPreview(PrintPreviewState? preview)
    {
        _previewState = preview;
        RefreshPreview();
    }

    public void SetError(string? message)
    {
        _error.Text = message ?? string.Empty;
        _error.Visible = !string.IsNullOrWhiteSpace(message);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _localization.CultureChanged -= OnCultureChanged;
            _preview.Image?.Dispose();
            _preview.Image = null;
        }

        base.Dispose(disposing);
    }

    private static ComboBox Selector(string name) => new()
    {
        BackColor = Color.FromArgb(64, 67, 71),
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDownList,
        FlatStyle = FlatStyle.Flat,
        ForeColor = Color.White,
        Name = name,
    };

    private Control BuildSelectionPanel()
    {
        TableLayoutPanel panel = new()
        {
            ColumnCount = 2,
            BackColor = SurfaceVisuals.Card,
            Dock = DockStyle.Fill,
            Name = "PrintOverlaySelection",
            Padding = new Padding(14, 14, 12, 0),
            RowCount = 7,
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int row = 0; row < 5; row++) panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        AddField(panel, 0, "PrintPaperSize", _paperSize);
        AddField(panel, 1, "PrintOrientation", _orientation);
        AddField(panel, 2, "PrintLayout", _layout);
        AddField(panel, 3, "PrintScale", _scale);
        AddField(panel, 4, "PrintPageNumbers", _pageNumbers);
        Label sections = ResourceLabel("PrintSections");
        panel.Controls.Add(sections, 0, 5);
        panel.SetColumnSpan(sections, 2);
        panel.Controls.Add(_sections, 0, 6);
        panel.SetColumnSpan(_sections, 2);
        return panel;
    }

    private Control BuildPreviewPanel()
    {
        Panel panel = new()
        {
            BackColor = SurfaceVisuals.Card,
            Dock = DockStyle.Fill,
            Name = "PrintOverlayPreviewArea",
            Padding = new Padding(12),
        };
        FlowLayoutPanel navigation = new()
        {
            AutoSize = true,
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.LeftToRight,
            Name = "PrintOverlayPreviewNavigation",
            Padding = new Padding(0, 8, 0, 0),
        };
        navigation.Controls.Add(_previous);
        navigation.Controls.Add(_page);
        navigation.Controls.Add(_next);
        panel.Controls.Add(_preview);
        panel.Controls.Add(navigation);
        panel.Controls.Add(_error);
        return panel;
    }

    private Panel BuildFooter()
    {
        Panel footer = new()
        {
            Dock = DockStyle.Bottom,
            BackColor = SurfaceVisuals.Card,
            Height = 40,
            Name = "PrintOverlayActions",
            Padding = new Padding(4),
        };
        FlowLayoutPanel buttons = new()
        {
            AutoSize = true,
            Dock = DockStyle.Right,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
        };
        buttons.Controls.Add(_cancel);
        buttons.Controls.Add(_export);
        footer.Controls.Add(buttons);
        return footer;
    }

    private void AddField(TableLayoutPanel panel, int row, string resourceKey, Control control)
    {
        panel.Controls.Add(ResourceLabel(resourceKey), 0, row);
        panel.Controls.Add(control, 1, row);
    }

    private Label ResourceLabel(string resourceKey) => new()
    {
        Anchor = AnchorStyles.Left,
        AutoSize = true,
        ForeColor = Color.White,
        Name = $"PrintOverlay{resourceKey}Label",
        Tag = resourceKey,
    };

    private void PopulateSections(IEnumerable<PrintContentSection> selectedSections)
    {
        HashSet<PrintContentSection> selected = selectedSections.ToHashSet();
        _modelDiagramSelected = selected.Contains(PrintContentSection.ModelDiagram);
        _sections.Items.Clear();
        foreach (PrintContentSection section in Enum.GetValues<PrintContentSection>()
                     .Where(section => section != PrintContentSection.ModelDiagram))
        {
            _sections.Items.Add(
                new SectionItem(section, _localization[PrintUiResources.SectionResourceKey(section)]),
                selected.Contains(section));
        }
    }

    private void SelectPage(int pageIndex)
    {
        if (_previewState is null || pageIndex < 0 || pageIndex >= _previewState.PageCount)
        {
            return;
        }

        _previewState = _previewState.SelectPage(pageIndex);
        RefreshPreview();
    }

    private void RefreshPreview()
    {
        Image? previous = _preview.Image;
        _preview.Image = _previewState is null
            ? null
            : PrintPreviewBitmap.Create(_previewState.SelectedPageCapture);
        previous?.Dispose();
        _previous.Enabled = _previewState?.SelectedPageIndex > 0;
        _next.Enabled = _previewState is not null && _previewState.SelectedPageIndex + 1 < _previewState.PageCount;
        _page.Text = _previewState is null
            ? _localization["PrintPreviewOutOfDate"]
            : string.Format(
                _localization.Culture,
                _localization["PrintPreviewPageCount"],
                _previewState.SelectedPageIndex + 1,
                _previewState.PageCount);
        UpdateExportAvailability();
    }

    private void UpdateExportAvailability()
    {
        _export.Enabled = _sections.CheckedItems.Count > 0 && _previewState is not null;
    }

    private void ApplyLocalization()
    {
        PrintPageSetupSelection current = TryReadSelection();
        _updating = true;
        try
        {
            OverlayTitle = _localization["MenuPrint"];
            _paperSize.Items.Clear();
            _paperSize.Items.AddRange([_localization["PrintPaperA4"], _localization["PrintPaperA3"]]);
            _orientation.Items.Clear();
            _orientation.Items.AddRange([_localization["PrintPortrait"], _localization["PrintLandscape"]]);
            _layout.Items.Clear();
            _layout.Items.AddRange([_localization["PrintLayoutContinuous"], _localization["PrintLayoutSectionPerPage"]]);
            foreach (Control control in Descendants(this))
            {
                if (control is Label { Tag: string resourceKey } label)
                {
                    label.Text = _localization[resourceKey];
                }
            }

            _pageNumbers.Text = _localization["PrintPageNumbers"];
            _previous.Text = _localization["ResultPrevious"];
            _next.Text = _localization["ResultNext"];
            _cancel.Text = _localization["PrintCancel"];
            _export.Text = _localization["SurfaceExportPdf"];
            SetSelection(current);
        }
        finally
        {
            _updating = false;
        }

        RefreshPreview();
    }

    private PrintPageSetupSelection TryReadSelection()
    {
        if (_paperSize.SelectedIndex < 0 || _orientation.SelectedIndex < 0 || _layout.SelectedIndex < 0 ||
            (_sections.CheckedItems.Count == 0 && !_modelDiagramSelected))
        {
            return new PrintPageSetupSelection(PrintPageSettings.Default, Enum.GetValues<PrintContentSection>());
        }

        return Selection;
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (Control descendant in Descendants(child)) yield return descendant;
        }
    }

    private void OnSectionItemCheck(object? sender, ItemCheckEventArgs eventArgs)
    {
        if (_updating)
        {
            return;
        }

        int checkedCount = _sections.CheckedItems.Count;
        if (eventArgs.CurrentValue != CheckState.Checked && eventArgs.NewValue == CheckState.Checked)
        {
            checkedCount++;
        }
        else if (eventArgs.CurrentValue == CheckState.Checked && eventArgs.NewValue != CheckState.Checked)
        {
            checkedCount--;
        }

        _export.Enabled = checkedCount > 0 && _previewState is not null;
        if (!IsHandleCreated)
        {
            return;
        }

        BeginInvoke(() =>
        {
            UpdateExportAvailability();
            Request(ScreenCommandKind.RefreshPrintPreview);
        });
    }

    private void OnSettingsChanged(object? sender, EventArgs eventArgs)
    {
        if (!_updating) Request(ScreenCommandKind.ConfigurePrint);
    }

    private void OnCultureChanged(object? sender, EventArgs eventArgs) => ApplyLocalization();

    private sealed record SectionItem(PrintContentSection Section, string Display)
    {
        public override string ToString() => Display;
    }
}
