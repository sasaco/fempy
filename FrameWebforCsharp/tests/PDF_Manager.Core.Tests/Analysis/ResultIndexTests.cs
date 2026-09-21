using PDF_Manager.Core.Analysis;

namespace PDF_Manager.Core.Tests.Analysis;

public sealed class ResultIndexTests
{
    [Fact]
    public void Index_ProvidesCoordinateLookupAndCaseOrder()
    {
        AnalysisResultSet result = AnalysisContractFixtureTests.ReadPositive("multiple-nonlinear.json");
        ResultIndex index = new(result);

        Assert.Equal(3, index.Count);
        Assert.True(index.TryGet(new ResultCoordinate("B", ResultStateKind.LoadStep, 1), out AnalysisResult? value));
        Assert.True(Assert.IsType<LoadStepAnalysisResult>(value).State.IsFinal);
        Assert.Equal([0, 1], index.ForCase("B").Select(item => item.State.Index));
        Assert.Equal(result.Results, index.Ordered);
    }

    [Fact]
    public void Index_RejectsAnInvalidProgrammaticallyConstructedSet()
    {
        AnalysisResultSet valid = AnalysisContractFixtureTests.ReadPositive("empty-topology.json");
        AnalysisResultSet invalid = new(
            valid.Kind,
            valid.SchemaVersion,
            valid.Units,
            valid.CoordinateSystem,
            valid.Cases,
            valid.Topology,
            [valid.Results[0], valid.Results[0]]);

        Assert.Throws<AnalysisContractException>(() => new ResultIndex(invalid));
    }
}
