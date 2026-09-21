using System.Diagnostics;
using PDF_Manager.Core.Analysis;
using PDF_Manager.Core.Abstractions;
using PDF_Manager.Resources;
using PDF_Manager.Shell;
using PDF_Manager.Shell.Lifecycle;
using PDF_Manager.Shell.ScreenComposition.Core;
using PDF_Manager.Shell.ScreenComposition.Surfaces;

namespace PDF_Manager.UiTests;

public sealed class MainFormIntegrationTests
{
    [Fact]
    public void MainForm_ComposesFrameWebShellInSourceOrderAndDefaultState()
    {
        StaTestRunner.Run(() =>
        {
            using MainForm form = new(CreateServices(new LocalizationService(UiLanguage.English)));
            form.Show();
            Application.DoEvents();

            FrameWebShellControl shell = form.ScreenShell;
            Assert.Same(shell, Assert.Single(form.Controls.Cast<Control>()));
            Assert.Equal(DockStyle.Fill, shell.Dock);
            Assert.Equal(new Size(1200, 800), form.ClientSize);
            Assert.Equal("HeaderMenu", shell.HeaderBar.Name);
            Assert.Equal(DockStyle.Top, shell.HeaderBar.Dock);
            Assert.Equal(44, shell.HeaderBar.Height);
            Assert.Equal("OptionalHeader", shell.OptionalHeader.Name);
            Assert.Equal(DockStyle.Top, shell.OptionalHeader.Dock);
            Assert.Equal(40, shell.OptionalHeader.Height);
            Assert.Equal("PrimaryNavigation", shell.PrimaryNavigation.Name);
            Assert.Equal(DockStyle.Left, shell.PrimaryNavigation.Dock);
            Assert.Equal(124, shell.PrimaryNavigation.Width);
            Assert.True(shell.PrimaryNavigation.IsExpanded);
            Assert.Equal("Workspace", shell.Workspace.Name);
            Assert.Equal(DockStyle.Fill, shell.Workspace.Dock);

            Control body = Assert.Single(shell.Controls.Cast<Control>(), control => control.Name == "FrameWebBody");
            Control workspaceLayer = Assert.Single(
                body.Controls.Cast<Control>(),
                control => control.Name == "WorkspaceLayer");
            Assert.Contains(shell.RoutePanelHost, body.Controls.Cast<Control>());
            Assert.Contains(shell.OverlayHost, shell.Controls.Cast<Control>());
            Assert.Contains(shell.Workspace, workspaceLayer.Controls.Cast<Control>());
            Assert.Contains(shell.PrimaryNavigation, workspaceLayer.Controls.Cast<Control>());
            Assert.Equal(0, shell.Controls.GetChildIndex(shell.OverlayHost));
            Assert.Equal(shell.ClientRectangle, shell.OverlayHost.Bounds);
            Assert.Equal(0, body.Controls.GetChildIndex(shell.RoutePanelHost));
            Assert.Equal(1, body.Controls.GetChildIndex(workspaceLayer));
            Assert.True(shell.OptionalHeader.Visible);
            Assert.False(shell.RoutePanelHost.Visible);
            Assert.Null(shell.RoutePanelHost.ActiveRoute);
            Assert.Equal(ScreenOverlayKind.Start, shell.State.Overlay);
            Assert.IsType<StartOverlayControl>(shell.OverlayHost.ActiveSurface);
            Assert.True(shell.OverlayHost.Visible);
            Assert.Same(form.DocumentHost, shell.Workspace.DocumentHost);
            Assert.Same(form.Workspace, shell.Workspace);
            Assert.DoesNotContain(form.Controls.Cast<Control>(), control => control is TabControl);

            PrimaryNavigationId[] expected = Enum.GetValues<PrimaryNavigationId>();
            Assert.Equal(expected, shell.PrimaryNavigation.Buttons.Keys);
            Assert.All(
                AngularScreenManifest.PrimaryNavigation.Where(item => item.RequiresResults),
                item => Assert.False(shell.PrimaryNavigation.Buttons[item.Id].Enabled));
        }, "MainForm FrameWeb shell composition");
    }

    [Fact]
    public void MainForm_HeaderCommandStateTracksDocumentAndAnalysisAvailability()
    {
        StaTestRunner.Run(() =>
        {
            using MainForm form = new(CreateServices(
                new LocalizationService(UiLanguage.English),
                analysisClient: new SuccessfulAnalysisClient()));
            form.Show();

            HeaderMenuControl header = form.ScreenShell.HeaderBar;
            Assert.True(HeaderItem(header, "HeaderNewItem").Enabled);
            Assert.True(HeaderItem(header, "HeaderOpenItem").Enabled);
            Assert.False(HeaderItem(header, "HeaderSaveItem").Enabled);
            Assert.False(header.PrintButton.Enabled);

            form.SetDocument(ShellCommandStateTests.CreateDocument(isDirty: true));
            Assert.True(HeaderItem(header, "HeaderSaveItem").Enabled);
            Assert.True(header.PrintButton.Enabled);
            Assert.False(form.RouteController.State.ResultsEnabled);

            form.ExecuteAnalysisAsync().GetAwaiter().GetResult();
            Assert.True(form.RouteController.State.ResultsEnabled);
            Assert.True(header.PrintButton.Enabled);
            Assert.All(
                AngularScreenManifest.PrimaryNavigation.Where(item => item.RequiresResults),
                item => Assert.True(form.ScreenShell.PrimaryNavigation.Buttons[item.Id].Enabled));
        }, "MainForm FrameWeb command state");
    }

    [Fact]
    public void MainForm_RuntimeLanguageSwitchUpdatesHeaderAndWindow()
    {
        StaTestRunner.Run(() =>
        {
            LocalizationService localization = new(UiLanguage.English);
            using MainForm form = new(CreateServices(localization));
            form.Show();
            Application.DoEvents();

            string englishFile = form.ScreenShell.HeaderBar.FileMenu.Text ?? string.Empty;
            Assert.Equal(localization["AppTitle"], form.Text);

            form.SetLanguage(UiLanguage.Japanese);
            Application.DoEvents();
            Assert.Equal(localization["AppTitle"], form.Text);
            Assert.NotEqual(englishFile, form.ScreenShell.HeaderBar.FileMenu.Text);

            form.SetLanguage(UiLanguage.Chinese);
            Application.DoEvents();
            Assert.Equal("zh", form.CurrentCulture.Name);
            Assert.Equal("文件", form.ScreenShell.HeaderBar.FileMenu.Text);
        }, "MainForm FrameWeb runtime localization");
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
    public void MainForm_TypedOperationFailureUpdatesStatusWithoutEscapingEventBoundary()
    {
        StaTestRunner.Run(() =>
        {
            AnalysisClientException failure = new(OperationFailureKind.Protocol, "The analysis response is invalid.");
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
            Assert.Empty(dialogs.Errors);
            Assert.Equal(failure.UserMessage, form.CurrentStatusMessage);
            Assert.Equal(ScreenOverlayKind.Alert, form.RouteController.State.Overlay);
            Assert.False(form.IsOperationRunning);
        }, "MainForm FrameWeb exception boundary");
    }

    [Fact]
    public void MainForm_CancelCommandCancelsAnalysisAndRestoresNavigationState()
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
            Assert.Equal(ScreenOverlayKind.Wait, form.RouteController.State.Overlay);

            form.CancelOperation();
            PumpUntil(() => operation.IsCompleted);
            operation.GetAwaiter().GetResult();

            Assert.True(analysis.ObservedToken.IsCancellationRequested);
            Assert.False(form.IsOperationRunning);
            Assert.Equal("The operation was canceled.", form.CurrentStatusMessage);
            Assert.False(form.RouteController.State.ResultsEnabled);
        }, "MainForm FrameWeb operation cancellation");
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

    private static ToolStripItem HeaderItem(HeaderMenuControl header, string name) =>
        Assert.Single(header.FileMenu.DropDownItems.Cast<ToolStripItem>(), item => item.Name == name);

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

    private sealed class SuccessfulAnalysisClient : IAnalysisClient
    {
        public Task<AnalysisResultSet> AnalyzeAsync(
            PDF_Manager.Core.Documents.ProjectDocument document,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AnalysisResultSet result = new(
                AnalysisResultSet.ContractKind,
                AnalysisResultSet.ContractVersion,
                new AnalysisUnits("SI", "m", "N", "kg", "s"),
                new CoordinateSystem("global_cartesian", "right", ["x", "y", "z"]),
                [new AnalysisCase("C1", "Case", "C1", AnalysisType.Static, [])],
                new AnalysisTopology([], [], [], []),
                [new StaticAnalysisResult("C1", [], [], [], [], [], new WarningDiagnostics([]))]);
            return Task.FromResult(result);
        }
    }
}
