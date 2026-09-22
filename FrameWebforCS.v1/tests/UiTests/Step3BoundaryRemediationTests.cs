using PDF_Manager.Core.Shell;
using PDF_Manager.Shell.Docking;
using PDF_Manager.Shell.Lifecycle;
using WeifenLuo.WinFormsUI.Docking;
using DockPanelState = WeifenLuo.WinFormsUI.Docking.DockState;

namespace PDF_Manager.UiTests;

public sealed class Step3BoundaryRemediationTests
{
    [Fact]
    public void LayoutRestore_RejectsUtf8ByteOversizeBeforeParsingOrCreatingPanes()
    {
        StaTestRunner.Run(() =>
        {
            using DockTestHost host = new();
            using DockLayoutAdapter layout = new(host.DockPanel, host.Registry);
            string oversized = $"{{\"padding\":\"{new string('界', 22_000)}\"}}";
            Assert.True(oversized.Length < DockLayoutAdapter.MaximumJsonCharacters);

            DockLayoutCapacityException failure = Assert.Throws<DockLayoutCapacityException>(
                () => layout.RestoreJson(oversized));

            Assert.Equal(DockLayoutCapacityKind.Utf8Bytes, failure.Kind);
            Assert.True(failure.Actual > DockLayoutAdapter.MaximumJsonUtf8Bytes);
            Assert.Equal(DockLayoutAdapter.MaximumJsonUtf8Bytes, failure.Maximum);
            Assert.Empty(host.Registry.Contents);
        }, "Layout UTF-8 byte bound");
    }

    [Fact]
    public void LayoutRestore_RejectsTooManyEntriesBeforeContractConstructionOrFactoryWork()
    {
        StaTestRunner.Run(() =>
        {
            using DockTestHost host = new();
            using DockLayoutAdapter layout = new(host.DockPanel, host.Registry);
            DocumentKey known = DocumentKey.Tool("layout.capacity");
            host.Registry.Register(known, () => new TestDockContent(known));
            int created = 0;
            host.Registry.ContentCreated += (_, _) => created++;
            const string entry =
                "{\"key\":{\"version\":1,\"kind\":\"tool\",\"identifier\":\"layout.capacity\"},\"dockState\":\"dockLeft\",\"bounds\":null,\"order\":0}";
            string json =
                $"{{\"version\":1,\"contents\":[{string.Join(',', Enumerable.Repeat(entry, DockLayoutAdapter.MaximumContentCount + 1))}],\"activeDocument\":null}}";

            DockLayoutCapacityException failure = Assert.Throws<DockLayoutCapacityException>(
                () => layout.RestoreJson(json));

            Assert.Equal(DockLayoutCapacityKind.Contents, failure.Kind);
            Assert.Equal(DockLayoutAdapter.MaximumContentCount + 1, failure.Actual);
            Assert.Equal(DockLayoutAdapter.MaximumContentCount, failure.Maximum);
            Assert.Equal(0, created);
            Assert.Empty(host.Registry.Contents);
        }, "Layout entry-count bound");
    }

    [Fact]
    public void LayoutRestore_UnknownVersionThrowsExactTypedExceptionWithoutSideEffects()
    {
        StaTestRunner.Run(() =>
        {
            using DockTestHost host = new();
            using DockLayoutAdapter layout = new(host.DockPanel, host.Registry);
            DocumentKey known = DocumentKey.Tool("layout.known");
            host.Registry.Register(known, () => new TestDockContent(known));
            int created = 0;
            host.Registry.ContentCreated += (_, _) => created++;

            UnknownContractVersionException failure = Assert.Throws<UnknownContractVersionException>(
                () => layout.RestoreJson("{\"version\":2,\"contents\":[],\"activeDocument\":null}"));

            Assert.Equal(nameof(LayoutState), failure.ContractName);
            Assert.Equal(2, failure.ActualVersion);
            Assert.Equal(LayoutState.CurrentVersion, failure.SupportedVersion);
            Assert.Equal(0, created);
            Assert.Empty(host.Registry.Contents);
        }, "Layout exact unknown-version failure");
    }

    [Fact]
    public void LayoutRestore_UnknownKeyThrowsExactTypedExceptionWithoutSideEffects()
    {
        StaTestRunner.Run(() =>
        {
            using DockTestHost host = new();
            using DockLayoutAdapter layout = new(host.DockPanel, host.Registry);
            DocumentKey known = DocumentKey.Tool("layout.known");
            DocumentKey unknown = DocumentKey.Tool("layout.unknown");
            host.Registry.Register(known, () => new TestDockContent(known));
            int created = 0;
            host.Registry.ContentCreated += (_, _) => created++;
            const string json = """
                {"version":1,"contents":[{"key":{"version":1,"kind":"tool","identifier":"layout.unknown"},"dockState":"dockLeft","bounds":null,"order":0}],"activeDocument":null}
                """;

            UnknownContentKeyException failure = Assert.Throws<UnknownContentKeyException>(
                () => layout.RestoreJson(json));

            Assert.Equal(unknown, failure.Key);
            Assert.Equal(0, created);
            Assert.Empty(host.Registry.Contents);
        }, "Layout exact unknown-key failure");
    }

    [Fact]
    public void DockHandlerClose_HidesAndReusesToolButDisposesAndRemovesDocumentWithExactEvents()
    {
        StaTestRunner.Run(() =>
        {
            using DockTestHost host = new();
            DocumentKey toolKey = DocumentKey.Tool("close.tool");
            DocumentKey documentKey = DocumentKey.Document("close:document");
            host.Registry.Register(toolKey, () => new TestDockContent(toolKey));
            host.Registry.Register(documentKey, () => new TestDockContent(documentKey));
            List<DocumentKey> created = [];
            List<DocumentKey> activated = [];
            List<DocumentKey> removed = [];
            host.Registry.ContentCreated += (_, args) => created.Add(args.Key);
            host.Registry.ContentActivated += (_, args) => activated.Add(args.Key);
            host.Registry.ContentRemoved += (_, args) => removed.Add(args.Key);

            DockContent tool = host.Registry.Open(toolKey);
            DockContent document = host.Registry.Open(documentKey);
            tool.DockHandler.Close();
            document.DockHandler.Close();
            Application.DoEvents();

            Assert.False(tool.IsDisposed);
            Assert.Equal(DockPanelState.Hidden, tool.DockState);
            Assert.True(host.Registry.TryGet(toolKey, out DockContent? hiddenTool));
            Assert.Same(tool, hiddenTool);
            Assert.True(document.IsDisposed);
            Assert.False(host.Registry.TryGet(documentKey, out _));
            DockContent reopenedTool = host.Registry.Open(toolKey);
            Assert.Same(tool, reopenedTool);
            Assert.Equal(new[] { toolKey, documentKey }, created);
            Assert.Equal(new[] { toolKey, documentKey, toolKey }, activated);
            Assert.Equal(new[] { documentKey }, removed);
        }, "DockHandler close lifetime policy");
    }

    [Fact]
    public async Task ExceptionBoundary_SuppressesOnlyCancellationMatchingACancelledToken()
    {
        List<UserSafeExceptionInfo> notifications = [];
        List<Exception> diagnostics = [];
        UserExceptionBoundary boundary = new(notifications.Add, diagnostics.Add);
        using CancellationTokenSource cancelled = new();
        cancelled.Cancel();

        bool expectedCancellation = await boundary.ExecuteAsync(
            token => Task.FromCanceled(token),
            cancelled.Token);

        Assert.False(expectedCancellation);
        Assert.Empty(notifications);
        Assert.Empty(diagnostics);

        OperationCanceledException unexpected = new("uncancelled operation cancellation");
        bool unexpectedResult = await boundary.ExecuteAsync(
            _ => Task.FromException(unexpected),
            CancellationToken.None);

        Assert.False(unexpectedResult);
        Assert.Same(unexpected, Assert.Single(diagnostics));
        UserSafeExceptionInfo notification = Assert.Single(notifications);
        Assert.Equal(UserSafeFailureKind.Unexpected, notification.Kind);
        Assert.DoesNotContain("uncancelled", notification.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExceptionBoundary_SynchronousCancellationWithoutATokenIsUnexpected()
    {
        List<UserSafeExceptionInfo> notifications = [];
        List<Exception> diagnostics = [];
        UserExceptionBoundary boundary = new(notifications.Add, diagnostics.Add);
        OperationCanceledException unexpected = new("uncancelled synchronous cancellation");

        Assert.False(boundary.Execute(() => throw unexpected));

        Assert.Same(unexpected, Assert.Single(diagnostics));
        Assert.Equal(UserSafeFailureKind.Unexpected, Assert.Single(notifications).Kind);
    }
}
