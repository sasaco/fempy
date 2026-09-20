using PDF_Manager.Core.Shell;
using PDF_Manager.Shell.Docking;
using CoreDockState = PDF_Manager.Core.Shell.DockState;

namespace PDF_Manager.UiTests;

public sealed class DockLayoutAdapterTests
{
    [Fact]
    public void Layout_RoundTripsDockStateOrderBoundsAndActiveDocument()
    {
        StaTestRunner.Run(() =>
        {
            string json;
            using (DockTestHost source = new())
            using (DockLayoutAdapter sourceLayout = new(source.DockPanel, source.Registry))
            {
                RegisterLayoutFactories(source.Registry);
                source.Registry.Open(NavigationKey, CoreDockState.DockLeft);
                source.Registry.Open(EditorKey, CoreDockState.DockRight);
                source.Registry.Open(FloatingKey, CoreDockState.Float, new WindowBounds(120, 140, 360, 260));
                source.Registry.Open(AlphaKey, CoreDockState.Document);
                source.Registry.Open(BetaKey, CoreDockState.Document);
                Assert.True(source.Registry.Activate(AlphaKey));
                Application.DoEvents();

                LayoutState captured = sourceLayout.Capture();
                Assert.Equal(AlphaKey, captured.ActiveDocument);
                Assert.Collection(
                    captured.Contents,
                    state => AssertState(state, NavigationKey, CoreDockState.DockLeft, 0),
                    state => AssertState(state, EditorKey, CoreDockState.DockRight, 1),
                    state =>
                    {
                        AssertState(state, FloatingKey, CoreDockState.Float, 2);
                        Assert.NotNull(state.Bounds);
                    },
                    state => AssertState(state, AlphaKey, CoreDockState.Document, 3),
                    state => AssertState(state, BetaKey, CoreDockState.Document, 4));
                json = sourceLayout.CaptureJson();
            }

            using DockTestHost target = new();
            using DockLayoutAdapter targetLayout = new(target.DockPanel, target.Registry);
            RegisterLayoutFactories(target.Registry);
            targetLayout.RestoreJson(json);
            Application.DoEvents();

            Assert.Equal(json, targetLayout.CaptureJson());
            Assert.Equal(AlphaKey, targetLayout.Capture().ActiveDocument);
        }, "Dock layout round trip");
    }

    [Theory]
    [InlineData(
        "{\"version\":2,\"contents\":[],\"activeDocument\":null}",
        typeof(UnknownContractVersionException))]
    [InlineData(
        "{\"version\":1,\"contents\":[{\"key\":{\"version\":1,\"kind\":\"tool\",\"identifier\":\"unknown.pane\"},\"dockState\":\"dockLeft\",\"bounds\":null,\"order\":0}],\"activeDocument\":null}",
        typeof(UnknownContentKeyException))]
    public void InvalidOrNonWhitelistedLayout_IsRejectedBeforeCreatingAnyPane(
        string json,
        Type expectedExceptionType)
    {
        StaTestRunner.Run(() =>
        {
            using DockTestHost host = new();
            using DockLayoutAdapter layout = new(host.DockPanel, host.Registry);
            RegisterLayoutFactories(host.Registry);
            int created = 0;
            host.Registry.ContentCreated += (_, _) => created++;

            Exception? failure = Record.Exception(() => layout.RestoreJson(json));

            Assert.IsType(expectedExceptionType, failure);
            Assert.Equal(0, created);
            Assert.Empty(host.Registry.Contents);
        }, "Dock layout fail-fast restore");
    }

    private static readonly DocumentKey NavigationKey = DocumentKey.Tool("project.navigation");
    private static readonly DocumentKey EditorKey = DocumentKey.Tool("project.editor");
    private static readonly DocumentKey FloatingKey = DocumentKey.Tool("project.floating");
    private static readonly DocumentKey AlphaKey = DocumentKey.Document("project:alpha");
    private static readonly DocumentKey BetaKey = DocumentKey.Document("project:beta");

    private static void RegisterLayoutFactories(DockContentRegistry registry)
    {
        foreach (DocumentKey key in new[] { NavigationKey, EditorKey, FloatingKey, AlphaKey, BetaKey })
        {
            registry.Register(key, () => new TestDockContent(key));
        }
    }

    private static void AssertState(
        LayoutContentState state,
        DocumentKey key,
        CoreDockState dockState,
        int order)
    {
        Assert.Equal(key, state.Key);
        Assert.Equal(dockState, state.DockState);
        Assert.Equal(order, state.Order);
    }
}
