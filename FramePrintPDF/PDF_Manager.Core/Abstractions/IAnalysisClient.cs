using PDF_Manager.Core.Analysis;
using PDF_Manager.Core.Documents;

namespace PDF_Manager.Core.Abstractions;

public interface IAnalysisClient
{
    Task<AnalysisResultSet> AnalyzeAsync(
        ProjectDocument document,
        CancellationToken cancellationToken = default);
}
