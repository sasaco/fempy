using PDF_Manager.Core.Analysis;

namespace PDF_Manager.Core.Tests.Analysis;

public sealed class AnalysisContractFixtureTests
{
    [Fact]
    public void SharedPositiveFixtures_AreAcceptedInPlace()
    {
        string directory = ContractDirectory("positive");
        string[] fixtures = Directory.GetFiles(directory, "*.json").Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(6, fixtures.Length);
        foreach (string fixture in fixtures)
        {
            AnalysisResultSet result = AnalysisResultSetJson.Deserialize(File.ReadAllBytes(fixture));
            Assert.Equal(AnalysisResultSet.ContractKind, result.Kind);
            Assert.Equal(AnalysisResultSet.ContractVersion, result.SchemaVersion);
        }
    }

    [Fact]
    public void SharedNegativeFixtures_AreRejectedInPlace()
    {
        string directory = ContractDirectory("negative");
        string[] fixtures = Directory.GetFiles(directory, "*.json").Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(7, fixtures.Length);
        foreach (string fixture in fixtures)
        {
            Assert.Throws<AnalysisContractException>(
                () => AnalysisResultSetJson.Deserialize(File.ReadAllBytes(fixture)));
        }
    }

    [Fact]
    public void MultipleStaticFixture_PreservesCaseMajorOrderAndCoordinates()
    {
        AnalysisResultSet result = ReadPositive("multiple-static.json");

        Assert.Equal(["10", "2"], result.Cases.Select(item => item.CaseId));
        Assert.Equal(
            [
                new ResultCoordinate("10", ResultStateKind.Static, 0),
                new ResultCoordinate("2", ResultStateKind.Static, 0),
            ],
            result.Results.Select(item => item.Coordinate));
    }

    [Fact]
    public void NonlinearFixture_ModelsEachAcceptedStepAsAnIndependentResult()
    {
        AnalysisResultSet result = ReadPositive("nonlinear-steps.json");

        LoadStepAnalysisResult[] steps = result.Results.Cast<LoadStepAnalysisResult>().ToArray();
        Assert.Equal(2, steps.Length);
        Assert.False(steps[0].State.IsFinal);
        Assert.True(steps[1].State.IsFinal);
        Assert.Equal([0, 1], steps.Select(step => step.State.Index));
    }

    [Fact]
    public void ParsedCollections_AreReadOnly()
    {
        AnalysisResultSet result = ReadPositive("single-static.json");
        IList<AnalysisCase> cases = Assert.IsAssignableFrom<IList<AnalysisCase>>(result.Cases);

        Assert.Throws<NotSupportedException>(() => cases.Add(result.Cases[0]));
    }

    [Fact]
    public void Parser_RejectsDuplicateJsonMemberNames()
    {
        const string json = """
            {
              "kind":"analysis_result_set",
              "kind":"analysis_result_set"
            }
            """;

        AnalysisContractException exception = Assert.Throws<AnalysisContractException>(
            () => AnalysisResultSetJson.Deserialize(json));
        Assert.Contains("Duplicate JSON member", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parser_ReportsNullRequiredObjectsAsContractFailures()
    {
        string json = File.ReadAllText(Path.Combine(ContractDirectory("positive"), "single-static.json"));
        string invalid = json.Replace(
            "\"units\": {\"system\": \"consistent_user_defined\", \"length\": \"m\", \"force\": \"kN\", \"mass\": \"t\", \"time\": \"s\"}",
            "\"units\": null",
            StringComparison.Ordinal);

        Assert.Throws<AnalysisContractException>(() => AnalysisResultSetJson.Deserialize(invalid));
    }

    [Fact]
    public void SemanticValidator_RejectsInvalidFrameStationAndModalFrequency()
    {
        string staticJson = File.ReadAllText(Path.Combine(ContractDirectory("positive"), "single-static.json"));
        string invalidStation = staticJson.Replace(
            "\"station_id\": \"S0\"",
            "\"station_id\": \"S9\"",
            StringComparison.Ordinal);
        Assert.Throws<AnalysisContractException>(() => AnalysisResultSetJson.Deserialize(invalidStation));

        string invalidFrame = staticJson.Replace(
            "\"z_axis\": {\"x\": 0, \"y\": 0, \"z\": 1}",
            "\"z_axis\": {\"x\": 0, \"y\": 1, \"z\": 0}",
            StringComparison.Ordinal);
        Assert.Throws<AnalysisContractException>(() => AnalysisResultSetJson.Deserialize(invalidFrame));

        string modalJson = File.ReadAllText(Path.Combine(ContractDirectory("positive"), "modal.json"));
        string invalidFrequency = modalJson.Replace(
            "0.3183098861837907",
            "0.5",
            StringComparison.Ordinal);
        Assert.Throws<AnalysisContractException>(() => AnalysisResultSetJson.Deserialize(invalidFrequency));
    }

    [Fact]
    public void ContractSchema_IsConsumedFromTheSharedPythonFixtureDirectory()
    {
        string root = FindRepositoryRoot();
        string schema = Path.Combine(root, "FrameWeb", "tests", "data", "contracts", "analysis-result-set-v1.schema.json");
        string localTestDirectory = Path.Combine(root, "FramePrintPDF", "PDF_Manager.Core.Tests");

        Assert.True(File.Exists(schema));
        string[] copiedSourceFixtures = Directory
            .EnumerateFiles(localTestDirectory, "*.json", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(localTestDirectory, path)
                .Split(Path.DirectorySeparatorChar)
                .Any(part => part is "bin" or "obj"))
            .ToArray();
        Assert.Empty(copiedSourceFixtures);
    }

    internal static AnalysisResultSet ReadPositive(string fileName)
    {
        string path = Path.Combine(ContractDirectory("positive"), fileName);
        return AnalysisResultSetJson.Deserialize(File.ReadAllBytes(path));
    }

    private static string ContractDirectory(string kind)
        => Path.Combine(FindRepositoryRoot(), "FrameWeb", "tests", "data", "contracts", kind);

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
        {
            current = current.Parent;
        }

        return current?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root from test output directory.");
    }
}
