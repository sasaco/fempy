using FrameWebforCsharp.Resources;

namespace FrameWebforCsharp.Shell.ScreenComposition.Core;

public sealed class UiLanguageRequestedEventArgs(UiLanguage language) : EventArgs
{
    public UiLanguage Language { get; } = language;
}

public sealed class HeaderMenuControl : UserControl
{
    private const string FrameWebProductVersion = "2.5.12";

    private readonly LocalizationService _localization;
    private readonly ToolStrip _left = CreateStrip("HeaderLeftActions");
    private readonly ToolStrip _right = CreateStrip("HeaderRightActions");
    private readonly Label _title = new CenteredTitleLabel
    {
        AutoEllipsis = true,
        BackColor = Color.Transparent,
        Dock = DockStyle.None,
        Font = new Font(SystemFonts.MessageBoxFont!, FontStyle.Bold),
        ForeColor = Color.White,
        Name = "HeaderProductTitle",
        TextAlign = ContentAlignment.MiddleCenter,
    };
    private readonly ToolStripLabel _logo = CreateLogo();
    private readonly ToolStripDropDownButton _file = new()
    {
        ForeColor = Color.White,
        Image = CreateHeaderIcon(HeaderIconKind.Document),
        ImageScaling = ToolStripItemImageScaling.None,
        Margin = new Padding(12, 0, 0, 0),
        Name = "HeaderFileMenu",
    };
    private readonly ToolStripMenuItem _new = new() { Name = "HeaderNewItem" };
    private readonly ToolStripMenuItem _open = new() { Name = "HeaderOpenItem" };
    private readonly ToolStripMenuItem _save = new() { Name = "HeaderSaveItem" };
    private readonly ToolStripMenuItem _saveAs = new() { Name = "HeaderSaveAsItem" };
    private readonly ToolStripMenuItem _preset = new() { Name = "HeaderPresetItem" };
    private readonly ToolStripButton _print = CreateButton("HeaderPrintButton", HeaderIconKind.Print);
    private readonly ToolStripDropDownButton _language = new()
    {
        ForeColor = Color.White,
        Image = CreateHeaderIcon(HeaderIconKind.Globe),
        ImageScaling = ToolStripItemImageScaling.None,
        Name = "HeaderLanguageMenu",
    };
    private readonly ToolStripMenuItem _japanese = new() { Name = "HeaderJapaneseItem" };
    private readonly ToolStripMenuItem _english = new() { Name = "HeaderEnglishItem" };
    private readonly ToolStripMenuItem _chinese = new() { Name = "HeaderChineseItem" };
    private readonly ToolStripButton _help = CreateButton("HeaderHelpButton", HeaderIconKind.Help);
    private readonly ToolStripButton _contact = CreateButton("HeaderContactButton", HeaderIconKind.Contact);
    private readonly ToolStripButton _login = CreateButton("HeaderLoginButton", HeaderIconKind.Login);

    public HeaderMenuControl(LocalizationService localization)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = ScreenCompositionStyle.HeaderBackColor;
        Dock = DockStyle.Top;
        Height = 44;
        MinimumSize = new Size(0, 44);
        MaximumSize = new Size(0, 44);
        Name = "HeaderMenu";

        _left.Dock = DockStyle.Left;
        _left.LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow;
        _left.Width = 260;
        _right.Dock = DockStyle.Right;
        _right.Width = 430;
        _right.LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow;
        _right.RightToLeft = RightToLeft.Yes;
        _file.DropDownItems.AddRange([_new, _open, _save, _saveAs, new ToolStripSeparator(), _preset]);
        _language.DropDownItems.AddRange([_japanese, _english, _chinese]);
        _left.Items.AddRange([_logo, _file, _print]);
        _right.Items.AddRange([_login, _contact, _help, _language]);
        Controls.Add(_title);
        Controls.Add(_right);
        Controls.Add(_left);
        _title.Bounds = new Rectangle(0, 0, ClientSize.Width, 44);
        _title.SendToBack();

        _new.Click += OnNewClick;
        _open.Click += OnOpenClick;
        _save.Click += OnSaveClick;
        _saveAs.Click += OnSaveAsClick;
        _preset.Click += OnPresetClick;
        _print.Click += OnPrintClick;
        _help.Click += OnHelpClick;
        _contact.Click += OnContactClick;
        _japanese.Click += OnJapaneseClick;
        _english.Click += OnEnglishClick;
        _chinese.Click += OnChineseClick;
        _localization.CultureChanged += OnCultureChanged;
        _login.Enabled = false;
        ApplyLocalization();
    }

    public event EventHandler<ScreenCommandRequestedEventArgs>? CommandRequested;
    public event EventHandler<UiLanguageRequestedEventArgs>? LanguageRequested;

    public ToolStripDropDownButton FileMenu => _file;
    public ToolStripButton PrintButton => _print;
    public ToolStripDropDownButton LanguageMenu => _language;
    public ToolStripButton HelpButton => _help;
    public ToolStripButton ContactButton => _contact;
    public ToolStripButton LoginButton => _login;
    public Label ProductTitle => _title;

    public void ApplyCommandState(bool canCreate, bool canOpen, bool canSave, bool canPrint)
    {
        _new.Enabled = canCreate;
        _open.Enabled = canOpen;
        _save.Enabled = canSave;
        _saveAs.Enabled = canSave;
        _preset.Enabled = canCreate;
        _print.Enabled = canPrint;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _new.Click -= OnNewClick;
            _open.Click -= OnOpenClick;
            _save.Click -= OnSaveClick;
            _saveAs.Click -= OnSaveAsClick;
            _preset.Click -= OnPresetClick;
            _print.Click -= OnPrintClick;
            _help.Click -= OnHelpClick;
            _contact.Click -= OnContactClick;
            _japanese.Click -= OnJapaneseClick;
            _english.Click -= OnEnglishClick;
            _chinese.Click -= OnChineseClick;
            _localization.CultureChanged -= OnCultureChanged;
            _logo.Image?.Dispose();
            _file.Image?.Dispose();
            _print.Image?.Dispose();
            _language.Image?.Dispose();
            _help.Image?.Dispose();
            _contact.Image?.Dispose();
            _login.Image?.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnSizeChanged(EventArgs eventArgs)
    {
        base.OnSizeChanged(eventArgs);
        _title.Bounds = new Rectangle(0, 0, ClientSize.Width, 44);
        _title.SendToBack();
    }

    private static ToolStrip CreateStrip(string name) => new()
    {
        AutoSize = false,
        BackColor = ScreenCompositionStyle.HeaderBackColor,
        CanOverflow = false,
        GripStyle = ToolStripGripStyle.Hidden,
        Name = name,
        Padding = new Padding(8, 7, 8, 6),
        Renderer = new HeaderToolStripRenderer(),
    };

    private static ToolStripButton CreateButton(string name, HeaderIconKind icon) => new()
    {
        AutoSize = true,
        ForeColor = Color.White,
        Image = CreateHeaderIcon(icon),
        ImageScaling = ToolStripItemImageScaling.None,
        Name = name,
    };

    private static Bitmap CreateHeaderIcon(HeaderIconKind icon)
    {
        Bitmap image = new(18, 18);
        using Graphics graphics = Graphics.FromImage(image);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using Pen pen = new(Color.White, 1.45f)
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap = System.Drawing.Drawing2D.LineCap.Round,
            LineJoin = System.Drawing.Drawing2D.LineJoin.Round,
        };
        using SolidBrush brush = new(Color.White);

        switch (icon)
        {
            case HeaderIconKind.Document:
                graphics.DrawPolygon(pen, [
                    new PointF(4, 2), new PointF(11, 2), new PointF(15, 6),
                    new PointF(15, 16), new PointF(4, 16),
                ]);
                graphics.DrawLine(pen, 11, 2, 11, 6);
                graphics.DrawLine(pen, 11, 6, 15, 6);
                graphics.DrawLine(pen, 7, 10, 12, 10);
                graphics.DrawLine(pen, 7, 13, 12, 13);
                break;
            case HeaderIconKind.Print:
                graphics.DrawRectangle(pen, 5, 2, 8, 5);
                graphics.DrawRoundedRectangle(pen, new RectangleF(2, 6, 14, 8), 2);
                graphics.FillEllipse(brush, 12.5f, 8.5f, 1.5f, 1.5f);
                graphics.FillRectangle(brush, 5, 11, 8, 5);
                graphics.DrawRectangle(Pens.White, 6, 12, 6, 3);
                break;
            case HeaderIconKind.Globe:
                graphics.DrawEllipse(pen, 2, 2, 14, 14);
                graphics.DrawEllipse(pen, 6, 2, 6, 14);
                graphics.DrawLine(pen, 2, 9, 16, 9);
                break;
            case HeaderIconKind.Help:
                graphics.DrawEllipse(pen, 2, 2, 14, 14);
                using (Font font = new((SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily, 9, FontStyle.Bold))
                {
                    TextRenderer.DrawText(
                        graphics,
                        "?",
                        font,
                        new Rectangle(1, 1, 16, 16),
                        Color.White,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                }

                break;
            case HeaderIconKind.Contact:
                graphics.DrawRoundedRectangle(pen, new RectangleF(2, 3, 14, 12), 2);
                graphics.DrawLines(pen, [new PointF(3, 5), new PointF(9, 10), new PointF(15, 5)]);
                break;
            case HeaderIconKind.Login:
                graphics.DrawEllipse(pen, 6, 2, 6, 6);
                graphics.DrawArc(pen, 3, 9, 12, 8, 180, 180);
                break;
        }

        return image;
    }

    private static ToolStripLabel CreateLogo()
    {
        Bitmap image = new(26, 25);
        using (Graphics graphics = Graphics.FromImage(image))
        {
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using SolidBrush background = new(ScreenCompositionStyle.AccentColor);
            graphics.FillPolygon(background,
            [
                new PointF(13, 0),
                new PointF(25, 8),
                new PointF(21, 23),
                new PointF(5, 23),
                new PointF(1, 8),
            ]);
            using Font font = new((SystemFonts.MessageBoxFont ?? Control.DefaultFont).FontFamily, 10, FontStyle.Bold);
            TextRenderer.DrawText(
                graphics,
                "F",
                font,
                new Rectangle(0, 1, 26, 22),
                Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        return new ToolStripLabel
        {
            AutoSize = false,
            Image = image,
            ImageScaling = ToolStripItemImageScaling.None,
            Name = "HeaderLogo",
            Size = new Size(26, 25),
        };
    }

    private void ApplyLocalization()
    {
        _file.Text = StripAccelerator(_localization["MenuFile"]);
        _new.Text = StripAccelerator(_localization["MenuNew"]);
        _open.Text = StripAccelerator(_localization["MenuOpen"]);
        _save.Text = StripAccelerator(_localization["MenuSave"]);
        _saveAs.Text = StripAccelerator(_localization["MenuSaveAs"]);
        _preset.Text = Localized("Select preset", "プリセットを開く", "选择预设");
        _print.Text = StripAccelerator(_localization["MenuPrint"]);
        _language.Text = StripAccelerator(_localization["MenuLanguage"]);
        _japanese.Text = _localization["LanguageJapanese"];
        _english.Text = _localization["LanguageEnglish"];
        _chinese.Text = _localization["LanguageChinese"];
        _japanese.Checked = _localization.Language == UiLanguage.Japanese;
        _english.Checked = _localization.Language == UiLanguage.English;
        _chinese.Checked = _localization.Language == UiLanguage.Chinese;
        _help.Text = Localized("Manual", "操作マニュアル", "操作手册");
        _contact.Text = Localized("Contact", "お問い合わせ", "询问");
        _login.Text = Localized("Login", "ログイン", "登录");
        ApplyProductTitle();
    }

    private void ApplyProductTitle()
    {
        _title.Text = Localized(
            $"FrameWebForJS ver.{FrameWebProductVersion}",
            $"立体骨組構造解析ソフト ver.{FrameWebProductVersion}",
            $"分析立体构架的软件程式 ver.{FrameWebProductVersion}");
    }

    private string Localized(string english, string japanese, string chinese) =>
        _localization.Language switch
        {
            UiLanguage.Japanese => japanese,
            UiLanguage.Chinese => chinese,
            _ => english,
        };

    private static string StripAccelerator(string value)
    {
        string display = value.Replace("&", string.Empty, StringComparison.Ordinal).TrimEnd('.');
        int marker = display.IndexOf('(');
        while (marker >= 0)
        {
            int end = display.IndexOf(')', marker + 1);
            if (end < 0 || end - marker > 3)
            {
                break;
            }

            display = display.Remove(marker, end - marker + 1);
            marker = display.IndexOf('(', marker);
        }

        return display.Trim();
    }

    private void Request(ScreenCommandKind command) =>
        CommandRequested?.Invoke(this, new ScreenCommandRequestedEventArgs(command));

    private void RequestLanguage(UiLanguage language) =>
        LanguageRequested?.Invoke(this, new UiLanguageRequestedEventArgs(language));

    private void OnNewClick(object? sender, EventArgs eventArgs) => Request(ScreenCommandKind.NewProject);
    private void OnOpenClick(object? sender, EventArgs eventArgs) => Request(ScreenCommandKind.OpenProject);
    private void OnSaveClick(object? sender, EventArgs eventArgs) => Request(ScreenCommandKind.SaveProject);
    private void OnSaveAsClick(object? sender, EventArgs eventArgs) => Request(ScreenCommandKind.SaveProjectAs);
    private void OnPresetClick(object? sender, EventArgs eventArgs) => Request(ScreenCommandKind.ShowPreset);
    private void OnPrintClick(object? sender, EventArgs eventArgs) => Request(ScreenCommandKind.ShowPrint);
    private void OnHelpClick(object? sender, EventArgs eventArgs) => Request(ScreenCommandKind.ShowHelp);
    private void OnContactClick(object? sender, EventArgs eventArgs) => Request(ScreenCommandKind.ShowContact);
    private void OnJapaneseClick(object? sender, EventArgs eventArgs) => RequestLanguage(UiLanguage.Japanese);
    private void OnEnglishClick(object? sender, EventArgs eventArgs) => RequestLanguage(UiLanguage.English);
    private void OnChineseClick(object? sender, EventArgs eventArgs) => RequestLanguage(UiLanguage.Chinese);
    private void OnCultureChanged(object? sender, EventArgs eventArgs) => ApplyLocalization();

    private sealed class HeaderToolStripRenderer : ToolStripProfessionalRenderer
    {
        public HeaderToolStripRenderer()
            : base(new HeaderColorTable())
        {
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs eventArgs)
        {
            if (!eventArgs.Item.Selected || eventArgs.Item is not ToolStripMenuItem)
            {
                eventArgs.TextColor = Color.White;
            }

            base.OnRenderItemText(eventArgs);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs eventArgs)
        {
            // Angular's header is a continuous dark band; ToolStrip's native
            // one-pixel border would create visible seams at the fixed action widths.
        }
    }

    private sealed class HeaderColorTable : ProfessionalColorTable
    {
        public override Color ToolStripGradientBegin => ScreenCompositionStyle.HeaderBackColor;
        public override Color ToolStripGradientMiddle => ScreenCompositionStyle.HeaderBackColor;
        public override Color ToolStripGradientEnd => ScreenCompositionStyle.HeaderBackColor;
        public override Color MenuItemSelected => Color.FromArgb(51, 51, 51);
        public override Color MenuItemBorder => Color.FromArgb(83, 83, 83);
        public override Color ButtonSelectedHighlight => Color.FromArgb(51, 51, 51);
        public override Color ButtonSelectedBorder => Color.FromArgb(83, 83, 83);
    }

    private sealed class CenteredTitleLabel : Label
    {
        protected override void OnPaintBackground(PaintEventArgs eventArgs)
        {
            // The full-width title must not erase ToolStrip siblings when
            // deterministic WinForms capture paints sibling HWNDs in order.
        }
    }

    private enum HeaderIconKind
    {
        Document,
        Print,
        Globe,
        Help,
        Contact,
        Login,
    }
}
