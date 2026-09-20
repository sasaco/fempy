using System.Reflection;
using PDF_Manager.Core.Abstractions;
using PDF_Manager.Core.Analysis;
using PDF_Manager.Core.Documents;
using PDF_Manager.Core.Tests.Analysis;
using PDF_Manager.Core.Tests.Documents;

namespace PDF_Manager.Core.Tests.Abstractions;

public sealed class CoreOperationBoundaryTests
{
    [Fact]
    public void AnalysisAndPrintInterfaces_AreTypedAsyncAndCancellationAware()
    {
        MethodInfo analysis = Assert.Single(typeof(IAnalysisClient).GetMethods());
        MethodInfo print = Assert.Single(typeof(IPrintExporter).GetMethods());

        Assert.Equal(typeof(Task<AnalysisResultSet>), analysis.ReturnType);
        Assert.Equal([typeof(ProjectDocument), typeof(CancellationToken)],
            analysis.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.Equal(typeof(Task), print.ReturnType);
        Assert.Equal([typeof(PrintExportRequest), typeof(Stream), typeof(CancellationToken)],
            print.GetParameters().Select(parameter => parameter.ParameterType));
    }

    [Fact]
    public void CoreBoundaries_DoNotExposeTransportUiRenderPdfOrLegacyTypes()
    {
        Type[] boundaryTypes =
        [
            typeof(IAnalysisClient),
            typeof(IPrintExporter),
            typeof(IProjectStore),
            typeof(PrintExportRequest),
        ];
        string publicSurface = string.Join(
            Environment.NewLine,
            boundaryTypes.SelectMany(type => type.GetMethods().Cast<MemberInfo>().Append(type))
                .Select(member => member.ToString()));

        string[] forbidden = ["Http", "WinForms", "OpenTK", "PdfSharp", "Dictionary", "PrintInput", "PrintData"];
        foreach (string term in forbidden)
        {
            Assert.DoesNotContain(term, publicSurface, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void OperationExceptions_ExposeSafeMessageAndPreserveCause()
    {
        IOException cause = new("low-level details");
        AnalysisClientException exception = new(
            OperationFailureKind.Unavailable,
            "解析サービスを利用できません。",
            cause);

        Assert.Equal(OperationFailureKind.Unavailable, exception.FailureKind);
        Assert.Equal("解析サービスを利用できません。", exception.UserMessage);
        Assert.Same(cause, exception.InnerException);
    }

    [Fact]
    public void PrintRequest_ValidatesSelectedCoordinatesAndDefensivelyCopiesThem()
    {
        ProjectDocument document = ProjectDocumentTestData.Create(isDirty: false);
        AnalysisResultSet resultSet = AnalysisContractFixtureTests.ReadPositive("single-static.json");
        List<ResultCoordinate> selection = [resultSet.Results[0].Coordinate];

        PrintExportRequest request = new(document, resultSet, selection);
        selection.Clear();

        Assert.Single(request.SelectedResults);
        Assert.Throws<ArgumentException>(() => new PrintExportRequest(
            document,
            resultSet,
            [new ResultCoordinate("missing", ResultStateKind.Static, 0)]));
    }

    [Fact]
    public void PrintRequest_StopsOversizedSelectionAtTheConfiguredBound()
    {
        ProjectDocument document = ProjectDocumentTestData.Create(isDirty: false);
        AnalysisResultSet resultSet = AnalysisContractFixtureTests.ReadPositive("single-static.json");
        ResultCoordinate coordinate = resultSet.Results[0].Coordinate;
        int enumerated = 0;

        IEnumerable<ResultCoordinate> UnboundedSelection()
        {
            while (true)
            {
                enumerated++;
                yield return coordinate;
            }
        }

        Assert.Throws<ArgumentException>(() => new PrintExportRequest(
            document,
            resultSet,
            UnboundedSelection()));
        Assert.Equal(PrintExportRequest.MaximumSelectedResultCount + 1, enumerated);
    }
}
