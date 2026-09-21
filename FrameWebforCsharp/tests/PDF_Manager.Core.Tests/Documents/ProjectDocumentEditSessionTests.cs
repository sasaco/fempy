using PDF_Manager.Core.Documents;

namespace PDF_Manager.Core.Tests.Documents;

public sealed class ProjectDocumentEditSessionTests
{
    [Fact]
    public void ValidatedEdits_AreUndoableAndRedoableWithoutPersistingHistory()
    {
        ProjectDocument initial = ProjectDocumentPresets.CreateRepresentativeFrame();
        ProjectDocumentEditSession session = new(initial, maxHistoryEntries: 8);

        Assert.True(session.UpsertNode(new ProjectNode("2", 6, 0, 0)));
        Assert.True(session.UpsertMember(new ProjectMember("1", "1", "2", "1", 15, true)));
        Assert.True(session.UpsertSupport(new ProjectSupport(
            "S1", "1", true, true, true, true, true, false)));
        Assert.True(session.UpsertLoadCase(new LoadCaseDefinition("1", "Ultimate", "U")));
        Assert.True(session.UpsertNodalLoad(new NodalLoadDefinition(
            "P1", "1", "2", 0, -12, 0, 0, 0, 0)));

        Assert.True(session.Current.IsDirty);
        Assert.Equal(-12, Assert.Single(session.Current.NodalLoads).Fy);
        Assert.True(session.CanUndo);
        Assert.False(session.CanRedo);

        Assert.True(session.Undo());
        Assert.Equal(-10, Assert.Single(session.Current.NodalLoads).Fy);
        Assert.True(session.Redo());
        Assert.Equal(-12, Assert.Single(session.Current.NodalLoads).Fy);

        string json = ProjectDocumentJson.SerializeToString(session.Current);
        Assert.DoesNotContain("undo", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("redo", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("history", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InvalidEdit_DoesNotChangeDocumentOrHistory()
    {
        ProjectDocument initial = ProjectDocumentPresets.CreateRepresentativeFrame();
        ProjectDocumentEditSession session = new(initial);

        Assert.Throws<ProjectDocumentValidationException>(() =>
            session.UpsertMember(new ProjectMember("1", "1", "missing", "1")));

        Assert.Same(initial, session.Current);
        Assert.False(session.CanUndo);
        Assert.False(session.CanRedo);
    }

    [Fact]
    public void History_IsBoundedAndNewEditClearsRedo()
    {
        ProjectDocumentEditSession session = new(
            ProjectDocumentPresets.CreateRepresentativeFrame(),
            maxHistoryEntries: 2);

        Assert.True(session.UpsertNode(new ProjectNode("2", 5, 0, 0)));
        Assert.True(session.UpsertNode(new ProjectNode("2", 6, 0, 0)));
        Assert.True(session.UpsertNode(new ProjectNode("2", 7, 0, 0)));
        Assert.True(session.Undo());
        Assert.True(session.Undo());
        Assert.False(session.Undo());

        Assert.True(session.UpsertNode(new ProjectNode("2", 8, 0, 0)));
        Assert.False(session.CanRedo);
    }
}
