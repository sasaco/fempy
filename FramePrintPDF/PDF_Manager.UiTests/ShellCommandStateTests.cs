using PDF_Manager.Core.Documents;
using PDF_Manager.Shell.Lifecycle;

namespace PDF_Manager.UiTests;

public sealed class ShellCommandStateTests
{
    [Fact]
    public void NoDocument_Idle_EnablesOnlyCreateAndOpen()
    {
        ShellCommandState state = ShellCommandStateReducer.Reduce(
            ShellCommandContext.Create(document: null, hasAnalysisResults: false, isOperationRunning: false));

        Assert.True(state.CanCreate);
        Assert.True(state.CanOpen);
        Assert.False(state.CanSave);
        Assert.False(state.CanSaveAs);
        Assert.False(state.CanAnalyze);
        Assert.False(state.CanPrint);
        Assert.False(state.CanCloseDocument);
        Assert.False(state.CanCancel);
    }

    [Fact]
    public void DirtyDocumentWithResults_Idle_EnablesDocumentCommands()
    {
        ProjectDocument document = CreateDocument(isDirty: true);
        ShellCommandContext context = ShellCommandContext.Create(
            document,
            hasAnalysisResults: true,
            isOperationRunning: false);

        ShellCommandState state = ShellCommandStateReducer.Reduce(context);

        Assert.True(context.IsDocumentDirty);
        Assert.True(state.CanCreate);
        Assert.True(state.CanOpen);
        Assert.True(state.CanSave);
        Assert.True(state.CanSaveAs);
        Assert.True(state.CanAnalyze);
        Assert.True(state.CanPrint);
        Assert.True(state.CanCloseDocument);
        Assert.False(state.CanCancel);
    }

    [Fact]
    public void RunningOperation_DisablesMutatingCommandsButKeepsCloseAndCancel()
    {
        ShellCommandState state = ShellCommandStateReducer.Reduce(
            ShellCommandContext.Create(
                CreateDocument(isDirty: true),
                hasAnalysisResults: true,
                isOperationRunning: true));

        Assert.False(state.CanCreate);
        Assert.False(state.CanOpen);
        Assert.False(state.CanSave);
        Assert.False(state.CanSaveAs);
        Assert.False(state.CanAnalyze);
        Assert.False(state.CanPrint);
        Assert.True(state.CanCloseDocument);
        Assert.True(state.CanCancel);
    }

    internal static ProjectDocument CreateDocument(bool isDirty) => new(
        ProjectDocument.CurrentVersion,
        new ProjectMetadata("Test Project", string.Empty, string.Empty, "test_units"),
        nodes: [],
        members: [],
        supports: [],
        loadCases: [],
        nodalLoads: [],
        derivedResults: [],
        movingLoads: [],
        isDirty: isDirty);
}
