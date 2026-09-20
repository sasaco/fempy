using PDF_Manager.Core.Documents;

namespace PDF_Manager.Core.Abstractions;

public interface IProjectStore
{
    Task<ProjectDocument> OpenAsync(string path, CancellationToken cancellationToken = default);

    Task<ProjectDocument> SaveAsync(
        ProjectDocument document,
        string path,
        CancellationToken cancellationToken = default);
}
