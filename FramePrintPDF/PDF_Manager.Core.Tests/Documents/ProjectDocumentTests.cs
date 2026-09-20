using PDF_Manager.Core.Analysis;
using PDF_Manager.Core.Documents;

namespace PDF_Manager.Core.Tests.Documents;

public sealed class ProjectDocumentTests
{
    [Fact]
    public void Aggregate_DefensivelyCopiesCollectionsAndKeepsResultsOutsideDocument()
    {
        List<ProjectNode> nodes =
        [
            new ProjectNode("N1", 0, 0, 0),
            new ProjectNode("N2", 1, 0, 0),
        ];
        ProjectDocument document = new(
            1,
            new ProjectMetadata("Model", "", "", "SI"),
            nodes,
            [new ProjectMember("M1", "N1", "N2")],
            [],
            [],
            [],
            [],
            []);

        nodes.Clear();

        Assert.Equal(2, document.Nodes.Count);
        Assert.DoesNotContain(
            typeof(ProjectDocument).GetProperties(),
            property => property.PropertyType == typeof(AnalysisResultSet) ||
                property.Name.Contains("ResultSet", StringComparison.Ordinal));
    }

    [Fact]
    public void DirtyAndSelection_AreExplicitTransientState()
    {
        ProjectDocument original = ProjectDocumentTestData.Create(isDirty: false);
        ProjectSelection selection = new(["N1"], []);

        ProjectDocument changed = original.WithTransientState(selection, isDirty: true);
        ProjectDocument saved = changed.MarkSaved();

        Assert.False(original.IsDirty);
        Assert.True(changed.IsDirty);
        Assert.Same(selection, changed.Selection);
        Assert.False(saved.IsDirty);
        Assert.Equal(["N1"], saved.Selection.NodeIds);
    }

    [Fact]
    public void Validation_RejectsDuplicateIdsBrokenReferencesAndNonFiniteValues()
    {
        Assert.Throws<ProjectDocumentValidationException>(() => new ProjectDocument(
            1,
            new ProjectMetadata("Model", "", "", "SI"),
            [new ProjectNode("N", 0, 0, 0), new ProjectNode("N", 1, 0, 0)],
            [], [], [], [], [], []));

        Assert.Throws<ProjectDocumentValidationException>(() => new ProjectDocument(
            1,
            new ProjectMetadata("Model", "", "", "SI"),
            [new ProjectNode("N1", 0, 0, 0)],
            [new ProjectMember("M", "N1", "missing")],
            [], [], [], [], []));

        Assert.Throws<ProjectDocumentValidationException>(() => new ProjectDocument(
            1,
            new ProjectMetadata("Model", "", "", "SI"),
            [new ProjectNode("N1", double.NaN, 0, 0)],
            [], [], [], [], [], []));
    }

    [Fact]
    public void Validation_RejectsForwardDerivedReferencesAndUnknownSelection()
    {
        Assert.Throws<ProjectDocumentValidationException>(() => new ProjectDocument(
            1,
            new ProjectMetadata("Model", "", "", "SI"),
            [], [], [],
            [new LoadCaseDefinition("D", "Dead", "D")],
            [],
            [new DerivedResultDefinition("X", "X", DerivedResultKind.Combine,
                [new DerivedResultTerm("Y", 1)])],
            []));

        Assert.Throws<ProjectDocumentValidationException>(() => new ProjectDocument(
            1,
            new ProjectMetadata("Model", "", "", "SI"),
            [], [], [], [], [], [], [],
            new ProjectSelection(["missing"], [])));
    }
}
