using PDF_Manager.Core.Abstractions;

namespace PDF_Manager.Core.Documents;

public enum ProjectStoreOperation
{
    Open,
    Save,
}

public sealed class ProjectStoreException : IOException
{
    public ProjectStoreException(ProjectStoreOperation operation, string message, Exception innerException)
        : base(message, innerException)
    {
        Operation = operation;
    }

    public ProjectStoreOperation Operation { get; }
}

public sealed class JsonProjectStore : IProjectStore
{
    public async Task<ProjectDocument> OpenAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        string fullPath = NormalizePath(path, ProjectStoreOperation.Open);
        try
        {
            byte[] bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken).ConfigureAwait(false);
            return ProjectDocumentJson.Deserialize(bytes);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ProjectDocumentFormatException)
        {
            throw;
        }
        catch (ProjectDocumentValidationException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ProjectStoreException(
                ProjectStoreOperation.Open,
                "The project file could not be opened.",
                exception);
        }
    }

    public async Task<ProjectDocument> SaveAsync(
        ProjectDocument document,
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ProjectDocumentValidator.Validate(document);
        byte[] content = ProjectDocumentJson.Serialize(document);
        string fullPath = NormalizePath(path, ProjectStoreOperation.Save);
        string directory = Path.GetDirectoryName(fullPath)
            ?? throw new ProjectStoreException(
                ProjectStoreOperation.Save,
                "The project file location is invalid.",
                new ArgumentException("The path has no directory component.", nameof(path)));
        string temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using (FileStream stream = new(
                temporaryPath,
                new FileStreamOptions
                {
                    Access = FileAccess.Write,
                    Mode = FileMode.CreateNew,
                    Options = FileOptions.Asynchronous | FileOptions.WriteThrough,
                    Share = FileShare.None,
                }))
            {
                await stream.WriteAsync(content, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(fullPath))
            {
                File.Replace(temporaryPath, fullPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temporaryPath, fullPath);
            }

            return document.MarkSaved();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ProjectStoreException(
                ProjectStoreOperation.Save,
                "The project file could not be saved.",
                exception);
        }
        finally
        {
            TryDeleteTemporaryFile(temporaryPath);
        }
    }

    private static string NormalizePath(string path, ProjectStoreOperation operation)
    {
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            return Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ProjectStoreException(
                operation,
                operation == ProjectStoreOperation.Open
                    ? "The project file location is invalid."
                    : "The project file location is invalid.",
                exception);
        }
    }

    private static void TryDeleteTemporaryFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Cleanup is best effort; the original save/cancellation outcome must be preserved.
        }
    }
}
