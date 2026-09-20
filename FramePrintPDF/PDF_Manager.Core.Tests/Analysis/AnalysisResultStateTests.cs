using PDF_Manager.Core.Analysis;

namespace PDF_Manager.Core.Tests.Analysis;

public sealed class AnalysisResultStateTests
{
    [Fact]
    public void FailedCandidate_DoesNotReplacePreviousValidatedResult()
    {
        AnalysisResultSet first = AnalysisContractFixtureTests.ReadPositive("single-static.json");
        AnalysisResultState state = new();
        state.Commit(first);

        Assert.Throws<AnalysisContractException>(() => state.CommitJson("{}"u8));

        Assert.Same(first, state.Current);
        Assert.NotNull(state.Index);
        Assert.Equal(1, state.Index.Count);
    }

    [Fact]
    public void ValidCandidate_ReplacesCurrentAndIndexTogether()
    {
        AnalysisResultSet first = AnalysisContractFixtureTests.ReadPositive("single-static.json");
        AnalysisResultSet second = AnalysisContractFixtureTests.ReadPositive("multiple-static.json");
        AnalysisResultState state = new();
        state.Commit(first);

        state.Commit(second);

        Assert.Same(second, state.Current);
        Assert.Equal(2, state.Index?.Count);
    }
}
