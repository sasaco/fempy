using System.Diagnostics;
using PDF_Manager.Core.Abstractions;
using PDF_Manager.Resources;
using PDF_Manager.Shell;
using PDF_Manager.Shell.Lifecycle;
using WeifenLuo.WinFormsUI.Docking;

namespace PDF_Manager.UiTests;

public sealed class MainFormIntegrationTests
{
    [Fact]
    public void MainForm_ComposesRequiredPanesAndAppliesCommandState()
    {
        StaTestRunner.Run(() =>
        {
            LocalizationService localization = new(UiLanguage.English);
            MainFormServices services = CreateServices(localization, analysisClient: new ImmediateAnalysisClient());
            using MainForm form = new(services);
            form.Show();
            Application.DoEvents();

            Assert.Equal(4, form.ContentRegistry.Contents.Count);
            Assert.Equal(MainForm.NavigationContentKey, form.NavigationPane.ContentKey);
            Assert.Equal(MainForm.WorkspaceDocumentKey, form.DocumentHost.ContentKey);
            Assert.Same(form.DocumentHost, form.Viewport);
            Assert.Equal(MainForm.EditorContentKey, form.EditorPane.ContentKey);
            Assert.Equal(MainForm.DiagnosticsContentKey, form.DiagnosticsPane.ContentKey);
            Assert.Equal(DockState.DockLeft, form.NavigationPane.DockState);
            Assert.Equal(DockState.Document, form.DocumentHost.DockState);
            Assert.Equal(DockState.DockRight, form.EditorPane.DockState);
            Assert.Equal(DockState.DockBottom, form.DiagnosticsPane.DockState);

            Assert.True(form.OpenMenuItem.Enabled);
            Assert.False(form.SaveMenuItem.Enabled);
            Assert.False(form.AnalyzeMenuItem.Enabled);
            Assert.False(form.CancelMenuItem.Enabled);
            Assert.False(form.ExportPdfMenuItem.Enabled);

            form.SetDocument(ShellCommandStateTests.CreateDocument(isDirty: false));
            Assert.False(form.SaveMenuItem.Enabled);
            Assert.True(form.AnalyzeMenuItem.Enabled);
            Assert.False(form.ExportPdfMenuItem.Enabled);

            form.SetDocument(ShellCommandStateTests.CreateDocument(isDirty: true));
            Assert.True(form.SaveMenuItem.Enabled);
            Assert.True(form.AnalyzeMenuItem.Enabled);
        }, "MainForm composition and commands");
    }

    [Fact]
    public void MainForm_RuntimeLanguageSwitchUpdatesAllMenuPaneAndVisibleShellCaptions()
    {
        StaTestRunner.Run(() =>
        {
            LocalizationService localization = new(UiLanguage.English);
            using MainForm form = new(CreateServices(localization));
            form.Show();
            Application.DoEvents();

            AssertLocalizedShell(form, localization);
            string englishFileCaption = Assert.IsType<string>(form.FileMenu.Text);

            form.SetLanguage(UiLanguage.Japanese);
            Application.DoEvents();
            AssertLocalizedShell(form, localization);
            Assert.NotEqual(englishFileCaption, form.FileMenu.Text);

            form.SetLanguage(UiLanguage.Chinese);
            Application.DoEvents();
            AssertLocalizedShell(form, localization);
            Assert.Equal("zh", form.CurrentCulture.Name);
        }, "MainForm runtime localization");
    }

    [Fact]
    public void MainForm_DirtyCloseCancelKeepsWindowOpenThenDiscardClosesIt()
    {
        StaTestRunner.Run(() =>
        {
            FakeShellDialogs dialogs = new() { CloseDecision = DirtyDocumentCloseDecision.Cancel };
            MainForm form = new(CreateServices(new LocalizationService(UiLanguage.English), dialogs: dialogs));
            form.SetDocument(ShellCommandStateTests.CreateDocument(isDirty: true));
            form.Show();

            form.Close();
            Application.DoEvents();

            Assert.True(form.Visible);
            Assert.False(form.IsDisposed);
            Assert.Equal(1, dialogs.ConfirmCloseCalls);

            dialogs.CloseDecision = DirtyDocumentCloseDecision.Discard;
            form.Close();
            PumpUntil(() => form.IsDisposed);

            Assert.True(form.IsDisposed);
            Assert.Equal(2, dialogs.ConfirmCloseCalls);
        }, "MainForm dirty close cancel and discard");
    }

    [Fact]
    public void MainForm_DirtyCloseSavePersistsBeforeClosing()
    {
        StaTestRunner.Run(() =>
        {
            FakeProjectStore store = new();
            FakeShellDialogs dialogs = new()
            {
                CloseDecision = DirtyDocumentCloseDecision.Save,
                ProjectToSave = Path.Combine(Path.GetTempPath(), "main-form-close.frameweb.json"),
            };
            MainForm form = new(CreateServices(
                new LocalizationService(UiLanguage.English),
                store,
                dialogs: dialogs));
            form.SetDocument(ShellCommandStateTests.CreateDocument(isDirty: true));
            form.Show();

            form.Close();
            PumpUntil(() => form.IsDisposed);

            Assert.True(form.IsDisposed);
            Assert.Equal(1, store.SaveCalls);
            Assert.Equal(1, dialogs.ConfirmCloseCalls);
        }, "MainForm dirty close save");
    }

    [Fact]
    public void MainForm_TypedOperationFailureIsReportedWithoutEscapingEventBoundary()
    {
        StaTestRunner.Run(() =>
        {
            AnalysisClientException failure = new(
                OperationFailureKind.Protocol,
                "The analysis response is invalid.");
            ImmediateAnalysisClient analysis = new(failure);
            FakeShellDialogs dialogs = new();
            List<Exception> diagnostics = [];
            using MainForm form = new(CreateServices(
                new LocalizationService(UiLanguage.English),
                analysisClient: analysis,
                dialogs: dialogs,
                reportDiagnostic: diagnostics.Add));
            form.SetDocument(ShellCommandStateTests.CreateDocument(isDirty: false));
            form.Show();

            form.ExecuteAnalysisAsync().GetAwaiter().GetResult();

            Assert.Equal(1, analysis.Calls);
            Assert.Same(failure, Assert.Single(diagnostics));
            (string _, string message) = Assert.Single(dialogs.Errors);
            Assert.Equal(failure.UserMessage, message);
            Assert.Contains(failure.UserMessage, form.DiagnosticsPane.Messages.Items.Cast<string>());
            Assert.False(form.IsOperationRunning);
        }, "MainForm exception boundary");
    }

    [Fact]
    public void MainForm_CancelCommandCancelsRunningAnalysisAndRestoresCommandState()
    {
        StaTestRunner.Run(() =>
        {
            CancellableAnalysisClient analysis = new();
            using MainForm form = new(CreateServices(
                new LocalizationService(UiLanguage.English),
                analysisClient: analysis));
            form.SetDocument(ShellCommandStateTests.CreateDocument(isDirty: false));
            form.Show();

            Task operation = form.ExecuteAnalysisAsync();
            Assert.True(form.IsOperationRunning);
            Assert.True(form.CancelMenuItem.Enabled);
            Assert.False(form.AnalyzeMenuItem.Enabled);

            form.CancelOperation();
            PumpUntil(() => operation.IsCompleted);
            operation.GetAwaiter().GetResult();

            Assert.True(analysis.ObservedToken.IsCancellationRequested);
            Assert.False(form.IsOperationRunning);
            Assert.False(form.CancelMenuItem.Enabled);
            Assert.True(form.AnalyzeMenuItem.Enabled);
            Assert.Equal("The operation was canceled.", form.DiagnosticsPane.StatusLabel.Text);
        }, "MainForm operation cancellation");
    }

    private static MainFormServices CreateServices(
        LocalizationService localization,
        FakeProjectStore? store = null,
        IAnalysisClient? analysisClient = null,
        FakeShellDialogs? dialogs = null,
        Action<Exception>? reportDiagnostic = null) => new(
            projectStore: store ?? new FakeProjectStore(),
            analysisClient: analysisClient,
            printExporter: new FakePrintExporter(),
            localization: localization,
            dialogs: dialogs ?? new FakeShellDialogs(),
            reportDiagnostic: reportDiagnostic);

    private static void AssertLocalizedShell(MainForm form, LocalizationService localization)
    {
        Dictionary<string, string> menuResources = new(StringComparer.Ordinal)
        {
            ["FileMenu"] = "MenuFile",
            ["NewMenuItem"] = "MenuNew",
            ["OpenMenuItem"] = "MenuOpen",
            ["SaveMenuItem"] = "MenuSave",
            ["SaveAsMenuItem"] = "MenuSaveAs",
            ["ExitMenuItem"] = "MenuExit",
            ["AnalysisMenu"] = "MenuAnalysis",
            ["RunAnalysisMenuItem"] = "MenuRunAnalysis",
            ["CancelMenuItem"] = "MenuCancel",
            ["PrintMenu"] = "MenuPrint",
            ["ExportPdfMenuItem"] = "MenuExportPdf",
            ["ViewMenu"] = "MenuView",
            ["NavigationMenuItem"] = "MenuNavigation",
            ["EditorMenuItem"] = "MenuEditor",
            ["DiagnosticsMenuItem"] = "MenuDiagnostics",
            ["LanguageMenu"] = "MenuLanguage",
            ["JapaneseMenuItem"] = "LanguageJapanese",
            ["EnglishMenuItem"] = "LanguageEnglish",
            ["ChineseMenuItem"] = "LanguageChinese",
        };
        Dictionary<string, ToolStripMenuItem> items = EnumerateMenuItems(form.MainMenuStrip!)
            .ToDictionary(static item => Assert.IsType<string>(item.Name), StringComparer.Ordinal);
        foreach ((string itemName, string resourceKey) in menuResources)
        {
            Assert.Equal(localization[resourceKey], items[itemName].Text);
        }

        Assert.Equal(localization["AppTitle"], form.Text);
        Assert.Equal(localization["PaneNavigation"], form.NavigationPane.Text);
        Assert.Equal(localization["PaneViewport"], form.DocumentHost.Text);
        Assert.Equal(localization["PaneEditor"], form.EditorPane.Text);
        Assert.Equal(localization["PaneDiagnostics"], form.DiagnosticsPane.Text);
        Assert.Equal(localization["NavigationModel"], form.NavigationPane.NavigationTree.Nodes["model"]!.Text);
        Assert.Equal(localization["EditorNoSelection"], form.EditorPane.SelectionLabel.Text);
        Assert.Equal(localization["StatusReady"], form.DiagnosticsPane.StatusLabel.Text);
        Label summary = Assert.IsType<Label>(form.DocumentHost.ViewportHost.Controls["ViewportSummary"]);
        Assert.Equal(localization["ViewportEmpty"], summary.Text);
    }

    private static IEnumerable<ToolStripMenuItem> EnumerateMenuItems(MenuStrip menu)
    {
        foreach (ToolStripMenuItem item in menu.Items.OfType<ToolStripMenuItem>())
        {
            yield return item;
            foreach (ToolStripMenuItem child in EnumerateMenuItems(item.DropDownItems))
            {
                yield return child;
            }
        }
    }

    private static IEnumerable<ToolStripMenuItem> EnumerateMenuItems(ToolStripItemCollection items)
    {
        foreach (ToolStripMenuItem item in items.OfType<ToolStripMenuItem>())
        {
            yield return item;
            foreach (ToolStripMenuItem child in EnumerateMenuItems(item.DropDownItems))
            {
                yield return child;
            }
        }
    }

    private static void PumpUntil(Func<bool> completed)
    {
        Stopwatch timeout = Stopwatch.StartNew();
        while (!completed())
        {
            Assert.True(timeout.Elapsed < TimeSpan.FromSeconds(5), "The UI operation did not complete.");
            Application.DoEvents();
            Thread.Sleep(1);
        }
    }
}
