using System.Runtime.InteropServices;
using PDF_Manager.Core.Abstractions;
using PDF_Manager.Resources;

namespace PDF_Manager.Shell.Printing;

public sealed class PrintPreviewState
{
    public PrintPreviewState(
        PrintPreviewResult preview,
        int selectedPageIndex = 0)
    {
        Preview = preview ?? throw new ArgumentNullException(nameof(preview));
        if (selectedPageIndex < 0 || selectedPageIndex >= preview.PageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(selectedPageIndex));
        }

        SelectedPageIndex = selectedPageIndex;
    }

    public PrintPreviewResult Preview { get; }

    public PrintPreviewCapture SelectedPageCapture => SelectedPage.RenderedPageCapture;

    public PrintPreviewCapture RenderedPageCapture => SelectedPageCapture;

    public string SelectedPageTextContent => SelectedPage.TextContent;

    public int PageCount => Preview.PageCount;

    public int SelectedPageIndex { get; }

    public PrintPreviewPage SelectedPage => Preview.Pages[SelectedPageIndex];

    public IReadOnlyList<PrintContentSection> SelectedSections => SelectedPage.Sections;

    public PrintPreviewState SelectPage(int pageIndex) =>
        new(Preview, pageIndex);
}

public sealed class PrintPageSetupSelection
{
    public PrintPageSetupSelection(
        PrintPageSettings pageSettings,
        IEnumerable<PrintContentSection> sections)
    {
        PageSettings = pageSettings ?? throw new ArgumentNullException(nameof(pageSettings));
        PrintContentSection[] values = sections?.ToArray()
            ?? throw new ArgumentNullException(nameof(sections));
        if (values.Length == 0 ||
            values.Any(value => !Enum.IsDefined(value)) ||
            values.Distinct().Count() != values.Length)
        {
            throw new ArgumentException("At least one unique print section is required.", nameof(sections));
        }

        Sections = Array.AsReadOnly(values);
    }

    public PrintPageSettings PageSettings { get; }

    public IReadOnlyList<PrintContentSection> Sections { get; }
}

public interface IPrintDialogService
{
    PrintPageSetupSelection? ShowPageSetup(
        IWin32Window owner,
        LocalizationService localization,
        PrintPageSetupSelection current);

    void ShowPreview(
        IWin32Window owner,
        LocalizationService localization,
        PrintPreviewState preview);
}

public sealed class WinFormsPrintDialogService : IPrintDialogService
{
    public PrintPageSetupSelection? ShowPageSetup(
        IWin32Window owner,
        LocalizationService localization,
        PrintPageSetupSelection current)
    {
        using PrintPageSetupDialog dialog = new(localization, current);
        return dialog.ShowDialog(owner) == DialogResult.OK ? dialog.Selection : null;
    }

    public void ShowPreview(
        IWin32Window owner,
        LocalizationService localization,
        PrintPreviewState preview)
    {
        using PrintPreviewDialog dialog = new(localization, preview);
        dialog.ShowDialog(owner);
    }
}

public sealed class PrintPageSetupDialog : Form
{
    private readonly LocalizationService _localization;
    private readonly ComboBox _paperSize = CreateDropDown("PrintPaperSizeSelector");
    private readonly ComboBox _orientation = CreateDropDown("PrintOrientationSelector");
    private readonly ComboBox _layout = CreateDropDown("PrintLayoutSelector");
    private readonly NumericUpDown _scale = new()
    {
        DecimalPlaces = 0,
        Increment = 10,
        Maximum = 400,
        Minimum = 25,
        Name = "PrintScaleSelector",
    };
    private readonly CheckBox _pageNumbers = new() { AutoSize = true, Name = "PrintPageNumbersCheckBox" };
    private readonly CheckedListBox _sections = new()
    {
        CheckOnClick = true,
        Dock = DockStyle.Fill,
        Name = "PrintSectionSelector",
    };

    public PrintPageSetupDialog(
        LocalizationService localization,
        PrintPageSetupSelection current)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        ArgumentNullException.ThrowIfNull(current);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(520, 520);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "PrintPageSetupDialog";
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;

        TableLayoutPanel fields = new()
        {
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            RowCount = 7,
        };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        fields.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        AddField(fields, 0, "PrintPaperSize", _paperSize);
        AddField(fields, 1, "PrintOrientation", _orientation);
        AddField(fields, 2, "PrintScale", _scale);
        AddField(fields, 3, "PrintLayout", _layout);
        AddField(fields, 4, "PrintPageNumbers", _pageNumbers);
        Label sectionsLabel = CreateLabel(_localization["PrintSections"]);
        fields.Controls.Add(sectionsLabel, 0, 5);
        fields.SetColumnSpan(sectionsLabel, 2);
        fields.Controls.Add(_sections, 0, 6);
        fields.SetColumnSpan(_sections, 2);

        FlowLayoutPanel buttons = new()
        {
            AutoSize = true,
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(12),
        };
        Button ok = new()
        {
            AutoSize = true,
            DialogResult = DialogResult.OK,
            Name = "PrintPageSetupOkButton",
            Text = _localization["PrintApply"],
        };
        Button cancel = new()
        {
            AutoSize = true,
            DialogResult = DialogResult.Cancel,
            Name = "PrintPageSetupCancelButton",
            Text = _localization["PrintCancel"],
        };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        Controls.Add(fields);
        Controls.Add(buttons);
        AcceptButton = ok;
        CancelButton = cancel;

        Selection = current;
        Populate(current);
        Text = _localization["PrintPageSetupTitle"];
        FormClosing += OnFormClosing;
    }

    public PrintPageSetupSelection Selection { get; private set; } = new(
        PrintPageSettings.Default,
        Enum.GetValues<PrintContentSection>());

    private static ComboBox CreateDropDown(string name) => new()
    {
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDownList,
        Name = name,
    };

    private static Label CreateLabel(string text) => new()
    {
        Anchor = AnchorStyles.Left,
        AutoSize = true,
        Text = text,
    };

    private void AddField(TableLayoutPanel fields, int row, string resourceKey, Control control)
    {
        fields.Controls.Add(CreateLabel(_localization[resourceKey]), 0, row);
        fields.Controls.Add(control, 1, row);
    }

    private void Populate(PrintPageSetupSelection current)
    {
        _paperSize.Items.AddRange([_localization["PrintPaperA4"], _localization["PrintPaperA3"]]);
        _orientation.Items.AddRange(
            [_localization["PrintPortrait"], _localization["PrintLandscape"]]);
        _layout.Items.AddRange(
            [_localization["PrintLayoutContinuous"], _localization["PrintLayoutSectionPerPage"]]);
        _paperSize.SelectedIndex = (int)current.PageSettings.PaperSize;
        _orientation.SelectedIndex = (int)current.PageSettings.Orientation;
        _layout.SelectedIndex = (int)current.PageSettings.Layout;
        _scale.Value = (decimal)(current.PageSettings.Scale * 100);
        _pageNumbers.Checked = current.PageSettings.ShowPageNumbers;

        HashSet<PrintContentSection> selected = current.Sections.ToHashSet();
        foreach (PrintContentSection section in Enum.GetValues<PrintContentSection>())
        {
            _sections.Items.Add(
                new SectionItem(section, _localization[SectionResourceKey(section)]),
                selected.Contains(section));
        }
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs eventArgs)
    {
        if (DialogResult != DialogResult.OK)
        {
            return;
        }

        PrintContentSection[] sections = _sections.CheckedItems
            .OfType<SectionItem>()
            .Select(item => item.Section)
            .ToArray();
        if (sections.Length == 0)
        {
            MessageBox.Show(
                this,
                _localization["PrintSectionRequired"],
                _localization["PrintPageSetupTitle"],
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            eventArgs.Cancel = true;
            return;
        }

        PrintPageSettings previous = Selection.PageSettings;
        Selection = new PrintPageSetupSelection(
            new PrintPageSettings(
                (PrintPaperSize)_paperSize.SelectedIndex,
                (PrintPageOrientation)_orientation.SelectedIndex,
                previous.LeftMarginMillimetres,
                previous.TopMarginMillimetres,
                previous.RightMarginMillimetres,
                previous.BottomMarginMillimetres,
                (double)_scale.Value / 100,
                (PrintLayoutChoice)_layout.SelectedIndex,
                _pageNumbers.Checked),
            sections);
    }

    internal static string SectionResourceKey(PrintContentSection section) => section switch
    {
        PrintContentSection.ProjectSummary => "PrintSectionProjectSummary",
        PrintContentSection.InputTables => "PrintSectionInputTables",
        PrintContentSection.ModelDiagram => "PrintSectionModelDiagram",
        PrintContentSection.LoadDiagram => "PrintSectionLoadDiagram",
        PrintContentSection.DisplacementResults => "PrintSectionDisplacements",
        PrintContentSection.ReactionResults => "PrintSectionReactions",
        PrintContentSection.MemberForceResults => "PrintSectionMemberForces",
        PrintContentSection.ResultDiagram => "PrintSectionResultDiagram",
        _ => throw new ArgumentOutOfRangeException(nameof(section), section, null),
    };

    private sealed record SectionItem(PrintContentSection Section, string Display)
    {
        public override string ToString() => Display;
    }
}

public sealed class PrintPreviewDialog : Form
{
    private readonly LocalizationService _localization;
    private readonly PictureBox _preview = new()
    {
        BackColor = Color.White,
        BorderStyle = BorderStyle.FixedSingle,
        Dock = DockStyle.Fill,
        Name = "PrintPreviewImage",
        SizeMode = PictureBoxSizeMode.Zoom,
    };
    private readonly Label _page = new() { AutoSize = true, Name = "PrintPreviewPageLabel" };
    private readonly ListBox _sections = new() { Dock = DockStyle.Right, Name = "PrintPreviewSectionList", Width = 230 };
    private readonly TextBox _content = new()
    {
        AcceptsReturn = true,
        AcceptsTab = true,
        Dock = DockStyle.Fill,
        Multiline = true,
        Name = "PrintPreviewContentTextBox",
        ReadOnly = true,
        ScrollBars = ScrollBars.Both,
        TabStop = true,
        WordWrap = false,
    };
    private readonly Button _previous = new() { AutoSize = true, Name = "PrintPreviewPreviousButton" };
    private readonly Button _next = new() { AutoSize = true, Name = "PrintPreviewNextButton" };
    private PrintPreviewState _state;

    public PrintPreviewDialog(LocalizationService localization, PrintPreviewState state)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _state = state ?? throw new ArgumentNullException(nameof(state));
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(920, 680);
        MinimizeBox = false;
        Name = "PrintPreviewDialog";
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = _localization["PrintPreviewTitle"];

        FlowLayoutPanel navigation = new()
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            Padding = new Padding(8),
        };
        _previous.Text = _localization["ResultPrevious"];
        _next.Text = _localization["ResultNext"];
        _content.AccessibleName = _localization["PrintPreviewContent"];
        _previous.Click += (_, _) => SelectPage(_state.SelectedPageIndex - 1);
        _next.Click += (_, _) => SelectPage(_state.SelectedPageIndex + 1);
        navigation.Controls.Add(_previous);
        navigation.Controls.Add(_next);
        navigation.Controls.Add(_page);
        GroupBox contentGroup = new()
        {
            Dock = DockStyle.Fill,
            Text = _localization["PrintPreviewContent"],
        };
        contentGroup.Controls.Add(_content);
        TableLayoutPanel previewLayout = new()
        {
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            RowCount = 2,
        };
        previewLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 65));
        previewLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 35));
        previewLayout.Controls.Add(_preview, 0, 0);
        previewLayout.Controls.Add(contentGroup, 0, 1);
        Controls.Add(previewLayout);
        Controls.Add(_sections);
        Controls.Add(navigation);

        RefreshPage();
    }

    public PrintPreviewState CurrentState => _state;

    public PictureBox PreviewImage => _preview;

    public Label PageLabel => _page;

    public TextBox ContentTextBox => _content;

    public ListBox SectionList => _sections;

    public Button PreviousButton => _previous;

    public Button NextButton => _next;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _preview.Image?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void SelectPage(int pageIndex)
    {
        if (pageIndex < 0 || pageIndex >= _state.PageCount)
        {
            return;
        }

        _state = _state.SelectPage(pageIndex);
        RefreshPage();
    }

    private void RefreshPage()
    {
        Bitmap nextImage = CaptureBitmap(_state.SelectedPageCapture);
        Image? previousImage = _preview.Image;
        _preview.Image = nextImage;
        previousImage?.Dispose();
        _page.Text = string.Format(
            _localization.Culture,
            _localization["PrintPreviewPageCount"],
            _state.SelectedPageIndex + 1,
            _state.PageCount);
        _content.Text = _state.SelectedPageTextContent;
        _content.SelectionStart = 0;
        _content.SelectionLength = 0;
        _sections.Items.Clear();
        foreach (PrintContentSection section in _state.SelectedPage.Sections)
        {
            _sections.Items.Add(_localization[PrintPageSetupDialog.SectionResourceKey(section)]);
        }

        _previous.Enabled = _state.SelectedPageIndex > 0;
        _next.Enabled = _state.SelectedPageIndex + 1 < _state.PageCount;
    }

    private static Bitmap CaptureBitmap(PrintPreviewCapture capture)
    {
        Bitmap bitmap = new(capture.Width, capture.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        ReadOnlySpan<byte> rgb = capture.Rgb24.Span;
        System.Drawing.Imaging.BitmapData data = bitmap.LockBits(
            new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            System.Drawing.Imaging.ImageLockMode.WriteOnly,
            System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        try
        {
            byte[] bgrRow = new byte[checked(capture.Width * 3)];
            for (int y = 0; y < capture.Height; y++)
            {
                int sourceRow = checked(y * capture.Width * 3);
                for (int x = 0; x < capture.Width; x++)
                {
                    int source = sourceRow + (x * 3);
                    int destination = x * 3;
                    bgrRow[destination] = rgb[source + 2];
                    bgrRow[destination + 1] = rgb[source + 1];
                    bgrRow[destination + 2] = rgb[source];
                }

                Marshal.Copy(bgrRow, 0, data.Scan0 + (y * data.Stride), bgrRow.Length);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return bitmap;
    }
}
