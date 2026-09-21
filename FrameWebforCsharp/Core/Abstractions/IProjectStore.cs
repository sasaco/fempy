using FrameWebforCsharp.Core.Documents;

namespace FrameWebforCsharp.Core.Abstractions;

public interface IProjectStore
{
    Task<ProjectDocument> OpenAsync(string path, CancellationToken cancellationToken = default);

    Task<ProjectDocument> SaveAsync(
        ProjectDocument document,
        string path,
        CancellationToken cancellationToken = default);
}
