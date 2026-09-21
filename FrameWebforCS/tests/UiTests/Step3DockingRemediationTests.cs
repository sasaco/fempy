using PDF_Manager.Core.Shell;
using PDF_Manager.Shell.Docking;
using WeifenLuo.WinFormsUI.Docking;
using CoreDockState = PDF_Manager.Core.Shell.DockState;

namespace PDF_Manager.UiTests;

public sealed class Step3DockingRemediationTests
{
    [Fact]
    public void RegistryAndLayout_PublicSurfaceRejectsEverySecondStaThreadAccess()
    {
        StaTestRunner.Run(() =>
        {
            using DockTestHost host = new();
            using DockLayoutAdapter layout = new(host.DockPanel, host.Registry);
            DocumentKey known = DocumentKey.Tool("thread.known");
            DocumentKey additional = DocumentKey.Tool("thread.additional");
            host.Registry.Register(known, () => new TestDockContent(known));
            DockContent content = host.Registry.Open(known, CoreDockState.DockLeft);
            LayoutState state = layout.Capture();
            string json = layout.CaptureJson();
            EventHandler<PDF_Manager.Shell.Docking.DockContentEventArgs> handler = static (_, _) => { };

            bool? secondThreadCheckAccess = null;
            int observedOwnerThreadId = 0;
            Assert.Null(RunOnSecondSta(
                () =>
                {
                    secondThreadCheckAccess = host.Registry.CheckAccess();
                    observedOwnerThreadId = host.Registry.OwnerThreadId;
                },
                "Registry access introspection",
                expectFailure: false));
            Assert.False(secondThreadCheckAccess);
            Assert.Equal(Environment.CurrentManagedThreadId, observedOwnerThreadId);

            (string Name, Action Operation)[] operations =
            [
                ("Registry.ContentCreated.add", () => host.Registry.ContentCreated += handler),
                ("Registry.ContentCreated.remove", () => host.Registry.ContentCreated -= handler),
                ("Registry.ContentActivated.add", () => host.Registry.ContentActivated += handler),
                ("Registry.ContentActivated.remove", () => host.Registry.ContentActivated -= handler),
                ("Registry.ContentRemoved.add", () => host.Registry.ContentRemoved += handler),
                ("Registry.ContentRemoved.remove", () => host.Registry.ContentRemoved -= handler),
                ("Registry.Contents", () => _ = host.Registry.Contents),
                ("Registry.VerifyAccess", host.Registry.VerifyAccess),
                ("Registry.Register", () => host.Registry.Register(additional, () => new TestDockContent(additional))),
                ("Registry.IsRegistered", () => _ = host.Registry.IsRegistered(known)),
                ("Registry.TryGet", () => _ = host.Registry.TryGet(known, out _)),
                ("Registry.TryGetKey", () => _ = host.Registry.TryGetKey(content, out _)),
                ("Registry.Open(default)", () => _ = host.Registry.Open(known)),
                ("Registry.Open(placement)", () => _ = host.Registry.Open(known, CoreDockState.DockLeft)),
                ("Registry.Activate", () => _ = host.Registry.Activate(known)),
                ("Registry.Close", () => _ = host.Registry.Close(known)),
                ("Registry.MoveBefore", () => _ = host.Registry.MoveBefore(known, known)),
                ("Registry.Dispose", host.Registry.Dispose),
                ("Layout.ActiveDocumentKey.get", () => _ = layout.ActiveDocumentKey),
                ("Layout.ActiveDocumentKey.set", () => layout.ActiveDocumentKey = null),
                ("Layout.Capture", () => _ = layout.Capture()),
                ("Layout.CaptureJson", () => _ = layout.CaptureJson()),
                ("Layout.RestoreJson", () => layout.RestoreJson(json)),
                ("Layout.Restore", () => layout.Restore(state)),
                ("Layout.Dispose", layout.Dispose),
            ];

            foreach ((string name, Action operation) in operations)
            {
                Exception? failure = RunOnSecondSta(operation, name);
                Assert.IsType<InvalidOperationException>(failure);
            }

            Assert.True(host.Registry.TryGet(known, out DockContent? remaining));
            Assert.Same(content, remaining);
            Assert.False(host.Registry.IsRegistered(additional));
        }, "Dock owner-thread public surface");
    }

    [Fact]
    public void Restore_WhenLaterFactoryThrows_RollsBackKeysPlacementOrderBoundsAndActiveDocument()
    {
        StaTestRunner.Run(() =>
        {
            using DockTestHost host = new();
            using DockLayoutAdapter layout = new(host.DockPanel, host.Registry);
            DocumentKey navigation = DocumentKey.Tool("rollback.navigation");
            DocumentKey floating = DocumentKey.Tool("rollback.floating");
            DocumentKey document = DocumentKey.Document("rollback:document");
            DocumentKey staged = DocumentKey.Tool("rollback.staged");
            DocumentKey failing = DocumentKey.Document("rollback:failing");
            foreach (DocumentKey key in new[] { navigation, floating, document, staged })
            {
                host.Registry.Register(key, () => new TestDockContent(key));
            }

            InvalidOperationException factoryFailure = new("later factory failed");
            host.Registry.Register(failing, () => throw factoryFailure);
            DockContent originalNavigation = host.Registry.Open(navigation, CoreDockState.DockLeft);
            DockContent originalFloating = host.Registry.Open(
                floating,
                CoreDockState.Float,
                new WindowBounds(150, 175, 340, 260));
            DockContent originalDocument = host.Registry.Open(document, CoreDockState.Document);
            Assert.True(host.Registry.Activate(document));
            Application.DoEvents();
            string baselineJson = layout.CaptureJson();
            LayoutState candidate = new(
                LayoutState.CurrentVersion,
                [
                    new LayoutContentState(document, CoreDockState.Document, null, 0),
                    new LayoutContentState(navigation, CoreDockState.DockRight, null, 1),
                    new LayoutContentState(floating, CoreDockState.Float, new WindowBounds(25, 35, 510, 320), 2),
                    new LayoutContentState(staged, CoreDockState.DockBottom, null, 3),
                    new LayoutContentState(failing, CoreDockState.Document, null, 4),
                ],
                document);

            DockContentFactoryException failure = Assert.Throws<DockContentFactoryException>(
                () => layout.Restore(candidate));
            Application.DoEvents();

            Assert.Equal(failing, failure.Key);
            Assert.Same(factoryFailure, failure.InnerException);
            Assert.Equal(baselineJson, layout.CaptureJson());
            Assert.True(host.Registry.TryGet(navigation, out DockContent? navigationAfter));
            Assert.True(host.Registry.TryGet(floating, out DockContent? floatingAfter));
            Assert.True(host.Registry.TryGet(document, out DockContent? documentAfter));
            Assert.Same(originalNavigation, navigationAfter);
            Assert.Same(originalFloating, floatingAfter);
            Assert.Same(originalDocument, documentAfter);
            Assert.False(host.Registry.TryGet(staged, out _));
            Assert.False(host.Registry.TryGet(failing, out _));
        }, "Transactional layout restore rollback");
    }

    [Fact]
    public void Restore_WithNullActiveDocumentClearsPreviouslyActiveDocument()
    {
        StaTestRunner.Run(() =>
        {
            using DockTestHost host = new();
            using DockLayoutAdapter layout = new(host.DockPanel, host.Registry);
            DocumentKey document = DocumentKey.Document("null-active:document");
            host.Registry.Register(document, () => new TestDockContent(document));
            host.Registry.Open(document, CoreDockState.Document);
            Assert.True(host.Registry.Activate(document));
            Assert.Equal(document, layout.Capture().ActiveDocument);
            LayoutState noActiveDocument = new(
                LayoutState.CurrentVersion,
                [new LayoutContentState(document, CoreDockState.Document, null, 0)],
                activeDocument: null);

            layout.Restore(noActiveDocument);
            Application.DoEvents();

            Assert.Null(layout.Capture().ActiveDocument);
        }, "Layout clears null active document");
    }

    [Fact]
    public void Restore_WhenApplyAndRollbackFactoriesThrow_AggregatesBothExactFailures()
    {
        StaTestRunner.Run(() =>
        {
            using DockTestHost host = new();
            using DockLayoutAdapter layout = new(host.DockPanel, host.Registry);
            DocumentKey fragile = DocumentKey.Document("transaction:fragile");
            DocumentKey failing = DocumentKey.Document("transaction:apply-failure");
            InvalidOperationException applyFailure = new("apply factory failed");
            InvalidOperationException rollbackFailure = new("rollback factory failed");
            int fragileFactoryCalls = 0;
            host.Registry.Register(fragile, () =>
            {
                fragileFactoryCalls++;
                return fragileFactoryCalls == 1
                    ? new TestDockContent(fragile)
                    : throw rollbackFailure;
            });
            host.Registry.Register(failing, () => throw applyFailure);
            host.Registry.Open(fragile, CoreDockState.Document);
            LayoutState candidate = new(
                LayoutState.CurrentVersion,
                [new LayoutContentState(failing, CoreDockState.Document, null, 0)],
                failing);

            DockLayoutTransactionException transaction = Assert.Throws<DockLayoutTransactionException>(
                () => layout.Restore(candidate));

            DockContentFactoryException apply = Assert.IsType<DockContentFactoryException>(
                transaction.ApplyFailure);
            DockContentFactoryException rollback = Assert.IsType<DockContentFactoryException>(
                transaction.RollbackFailure);
            Assert.Equal(failing, apply.Key);
            Assert.Same(applyFailure, apply.InnerException);
            Assert.Equal(fragile, rollback.Key);
            Assert.Same(rollbackFailure, rollback.InnerException);
            AggregateException aggregate = Assert.IsType<AggregateException>(transaction.InnerException);
            Assert.Equal(new Exception[] { apply, rollback }, aggregate.InnerExceptions);
        }, "Layout apply and rollback aggregate");
    }

    [Fact]
    public void CaptureRestore_PreservesLiveTabOrderRedockingAndSupportedFloatingDimensions()
    {
        StaTestRunner.Run(() =>
        {
            DocumentKey navigation = DocumentKey.Tool("arrangement.navigation");
            DocumentKey editor = DocumentKey.Tool("arrangement.editor");
            DocumentKey alpha = DocumentKey.Document("arrangement:alpha");
            DocumentKey beta = DocumentKey.Document("arrangement:beta");
            DocumentKey[] keys = [navigation, editor, alpha, beta];
            string json;
            LayoutState expected;

            using (DockTestHost source = new())
            using (DockLayoutAdapter sourceLayout = new(source.DockPanel, source.Registry))
            {
                foreach (DocumentKey key in keys)
                {
                    source.Registry.Register(key, () => new TestDockContent(key));
                }

                source.Registry.Open(navigation, CoreDockState.DockLeft);
                source.Registry.Open(editor, CoreDockState.DockRight);
                source.Registry.Open(alpha, CoreDockState.Document);
                source.Registry.Open(beta, CoreDockState.Document);
                source.Registry.Open(navigation, CoreDockState.DockRight);
                source.Registry.Open(
                    editor,
                    CoreDockState.Float,
                    new WindowBounds(210, 190, 480, 330));
                Assert.True(source.Registry.MoveBefore(beta, alpha));
                Assert.True(source.Registry.Activate(beta));
                Application.DoEvents();

                expected = sourceLayout.Capture();
                json = sourceLayout.CaptureJson();
                Assert.False(DockLayoutAdapter.PersistsDockedDimensions);
                Assert.Equal(
                    new[] { navigation, editor, beta, alpha },
                    expected.Contents.Select(static state => state.Key));
                Assert.Equal(CoreDockState.DockRight, StateFor(expected, navigation).DockState);
                LayoutContentState floating = StateFor(expected, editor);
                Assert.Equal(CoreDockState.Float, floating.DockState);
                Assert.NotNull(floating.Bounds);
                Assert.Equal(beta, expected.ActiveDocument);
            }

            using DockTestHost target = new();
            using DockLayoutAdapter targetLayout = new(target.DockPanel, target.Registry);
            foreach (DocumentKey key in keys)
            {
                target.Registry.Register(key, () => new TestDockContent(key));
            }

            targetLayout.RestoreJson(json);
            Application.DoEvents();
            LayoutState actual = targetLayout.Capture();

            Assert.Equal(json, targetLayout.CaptureJson());
            Assert.Equal(
                expected.Contents.Select(static state => state.Key),
                actual.Contents.Select(static state => state.Key));
            Assert.Equal(CoreDockState.DockRight, StateFor(actual, navigation).DockState);
            Assert.Equal(StateFor(expected, editor).Bounds, StateFor(actual, editor).Bounds);
            Assert.Equal(beta, actual.ActiveDocument);
        }, "Supported live layout arrangement round trip");
    }

    private static LayoutContentState StateFor(LayoutState state, DocumentKey key) =>
        Assert.Single(state.Contents, content => content.Key == key);

    private static Exception? RunOnSecondSta(
        Action operation,
        string operationName,
        bool expectFailure = true)
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                operation();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        })
        {
            IsBackground = true,
            Name = $"Non-owner {operationName}",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(5)), $"{operationName} did not terminate.");
        if (expectFailure)
        {
            Assert.NotNull(failure);
        }

        return failure;
    }
}
