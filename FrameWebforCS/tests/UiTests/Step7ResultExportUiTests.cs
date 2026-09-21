using System.Text;
using PDF_Manager.Core.Analysis;
using PDF_Manager.Core.Documents;
using PDF_Manager.Core.Results;
using PDF_Manager.Core.Shell;
using PDF_Manager.Resources;
using PDF_Manager.Shell.Contents;
using PDF_Manager.Shell.Viewport;

namespace PDF_Manager.UiTests;

public sealed class Step7ResultExportUiTests
{
    [Fact]
    public void ProductionPickupSelectionExportsAndAtomicallySavesExactDimensionSpecificBytes()
    {
        StaTestRunner.Run(() =>
        {
            (ProjectDocument source, AnalysisResultSet unvalidated) =
                Step6ViewportBehaviorTests.CreateSignedCaseFixture();
            AnalysisResultSet results = NormalizeSignedResultSet(unvalidated);
            DerivedResultDefinition definition = new(
                "PICKUP",
                "Pickup",
                DerivedResultKind.Pickup,
                [new DerivedResultTerm("C1", 1), new DerivedResultTerm("C2", 1)]);
            ProjectDocument threeDimensional = WithPresentation(source, [definition]);
            PresentedStaticResult pickup = Assert.Single(
                new ResultPresentationService().BuildDerivedResults(results, [definition]));
            ResultCsvExport expected3D = new ResultCsvExporter().ExportPickup(pickup);
            ResultPickupFixedWidthExport expected2D = new ResultCsvExporter().ExportPickup2D(pickup);

            using ProjectDocumentContent content = new(
                DocumentKey.Document("step7-pickup-export"),
                new LocalizationService(UiLanguage.English));
            _ = content.ResultGrid.Handle;
            content.SetDocument(threeDimensional);
            content.SetResult(results);
            content.ResultDerivedSelector.SelectedIndex = 1;
            Application.DoEvents();

            Assert.Equal("PICKUP", content.SelectedDerivedResultId);
            Assert.True(content.ResultPickupExportButton.Enabled);
            Assert.True(content.CanExportSelectedPickupCsv);
            Assert.False(content.ResultCsvExportButton.Enabled);
            Assert.False(content.CanExportSelectedResultCsv);
            ResultExportArtifact actual3D = content.ExportSelectedPickupCsv();
            AssertArtifact(actual3D, expected3D.SuggestedFileName, expected3D.ContentType,
                expected3D.RowCount, expected3D.Utf8Bytes.ToArray());

            string directory = Path.Combine(Path.GetTempPath(), $"step7-pickup-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            try
            {
                string csvPath = Path.Combine(directory, "result.csv");
                File.WriteAllText(csvPath, "stale", new UTF8Encoding(false));
                content.SaveSelectedPickupCsv(csvPath);
                Assert.Equal(expected3D.Utf8Bytes.ToArray(), File.ReadAllBytes(csvPath));
                Assert.Empty(Directory.GetFiles(directory, "*.tmp"));

                content.SetDocument(WithDimension(threeDimensional, ModelDimension.TwoDimensional));
                content.ResultDerivedSelector.SelectedIndex = 1;
                Application.DoEvents();
                ResultExportArtifact actual2D = content.ExportSelectedPickupCsv();
                AssertArtifact(actual2D, expected2D.SuggestedFileName, expected2D.ContentType,
                    expected2D.RowCount, expected2D.Utf8Bytes.ToArray());
                Assert.EndsWith(".pik", actual2D.SuggestedFileName, StringComparison.Ordinal);

                string pickupPath = Path.Combine(directory, "result.pik");
                content.SaveSelectedPickupCsv(pickupPath);
                Assert.Equal(expected2D.Utf8Bytes.ToArray(), File.ReadAllBytes(pickupPath));
                Assert.Empty(Directory.GetFiles(directory, "*.tmp"));

                byte[] sentinel = Encoding.UTF8.GetBytes("preserve-existing-target");
                File.WriteAllBytes(pickupPath, sentinel);
                using (FileStream lockedTarget = new(
                    pickupPath,
                    FileMode.Open,
                    FileAccess.ReadWrite,
                    FileShare.Read))
                {
                    ResultExportOperationException exception = Assert.Throws<ResultExportOperationException>(
                        () => content.SaveSelectedPickupCsv(pickupPath));
                    Assert.True(exception.InnerException is IOException or UnauthorizedAccessException);
                    Assert.Equal("ResultExportFailed", exception.ResourceKey);
                }

                Assert.Equal(sentinel, File.ReadAllBytes(pickupPath));
                Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
                Assert.Equal([csvPath, pickupPath], Directory.GetFiles(directory).Order(StringComparer.Ordinal));
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }, "Step 7 PICKUP engineering export and atomic persistence");
    }

    private static void AssertArtifact(
        ResultExportArtifact actual,
        string fileName,
        string contentType,
        int rowCount,
        byte[] bytes)
    {
        Assert.Equal(fileName, actual.SuggestedFileName);
        Assert.Equal(contentType, actual.ContentType);
        Assert.Equal(rowCount, actual.RowCount);
        Assert.Equal(bytes.Length, actual.ByteCount);
        Assert.Equal(bytes, actual.Utf8Bytes.ToArray());
        Assert.Equal(Encoding.UTF8.GetString(bytes), actual.Text);
        using MemoryStream stream = new();
        actual.WriteTo(stream);
        Assert.Equal(bytes, stream.ToArray());
    }

    private static AnalysisResultSet NormalizeSignedResultSet(AnalysisResultSet value)
    {
        StaticAnalysisResult firstResult = Assert.IsType<StaticAnalysisResult>(value.Results[0]);
        double memberLength = firstResult.MemberSectionForces[0].Segments[0].Length;
        AnalysisTopology topology = new(
            value.Topology.Nodes,
            value.Topology.Members.Select(member => new TopologyMember(
                member.MemberId,
                member.NodeI,
                member.NodeJ,
                member.LocalFrame,
                [new MemberStation("S0", 0), new MemberStation("S1", memberLength)])),
            value.Topology.ShellElements,
            value.Topology.SolidElements);
        AnalysisResult[] results = value.Results.Select(result =>
        {
            StaticAnalysisResult source = Assert.IsType<StaticAnalysisResult>(result);
            return (AnalysisResult)new StaticAnalysisResult(
                source.CaseId,
                source.NodeDisplacements,
                source.SupportReactions,
                source.MemberSectionForces.Select(member => new MemberSectionForces(
                    member.MemberId,
                    member.Segments.Select(segment => new MemberSegmentResult(
                        "S0-S1", "S0", "S1", segment.Length, segment.IEnd, segment.JEnd)))),
                source.ShellResults,
                source.SolidResults,
                source.Diagnostics);
        }).ToArray();
        AnalysisResultSet normalized = new(
            value.Kind,
            value.SchemaVersion,
            value.Units,
            value.CoordinateSystem,
            value.Cases,
            topology,
            results);
        AnalysisResultSetValidator.Validate(normalized);
        return normalized;
    }

    private static ProjectDocument WithPresentation(
        ProjectDocument value,
        IEnumerable<DerivedResultDefinition> derivedResults) => new(
        value.Version,
        value.Metadata,
        value.Nodes,
        value.Members,
        value.Supports,
        value.LoadCases,
        value.NodalLoads,
        derivedResults,
        [],
        value.Selection,
        value.IsDirty,
        value.Sections,
        ModelDimension.ThreeDimensional,
        value.ElementPropertySets,
        value.RigidZones,
        value.SupportSets,
        value.Panels,
        value.JointReleaseSets,
        value.NoticePoints,
        value.MemberSpringSets,
        value.PrescribedDisplacements,
        value.MemberLoads);

    private static ProjectDocument WithDimension(ProjectDocument value, ModelDimension dimension) => new(
        value.Version,
        value.Metadata,
        value.Nodes.Select(node => node with { Z = 0 }),
        value.Members,
        value.Supports,
        value.LoadCases,
        value.NodalLoads,
        value.DerivedResults,
        value.MovingLoads,
        value.Selection,
        value.IsDirty,
        value.Sections,
        dimension,
        value.ElementPropertySets,
        value.RigidZones,
        value.SupportSets,
        value.Panels,
        value.JointReleaseSets,
        value.NoticePoints,
        value.MemberSpringSets,
        value.PrescribedDisplacements,
        value.MemberLoads);
}
