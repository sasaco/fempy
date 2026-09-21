using FrameWebforCsharp.Core.Analysis;
using FrameWebforCsharp.Core.Documents;

namespace FrameWebforCsharp.Core.Abstractions;

public interface IAnalysisClient
{
    Task<AnalysisResultSet> AnalyzeAsync(
        ProjectDocument document,
        CancellationToken cancellationToken = default);
}
