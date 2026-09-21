using FrameWebforCS.Core.Analysis;
using FrameWebforCS.Core.Documents;

namespace FrameWebforCS.Core.Abstractions;

public interface IAnalysisClient
{
    Task<AnalysisResultSet> AnalyzeAsync(
        ProjectDocument document,
        CancellationToken cancellationToken = default);
}
