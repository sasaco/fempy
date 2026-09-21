namespace FrameWebforCsharp.Shell.Viewport;

internal static class AtomicResultExportWriter
{
    internal const string CleanupFailureDataKey = "ResultExportCleanupFailure";

    internal static void Write(string path, ResultExportArtifact export)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(export);

        string fullPath = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("The export path must have a parent directory.", nameof(path));
        Directory.CreateDirectory(directory);
        string temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

        Exception? primaryFailure = null;
        try
        {
            using (FileStream stream = new(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 64 * 1024,
                FileOptions.WriteThrough))
            {
                export.WriteTo(stream);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        catch (Exception exception)
        {
            primaryFailure = exception;
            throw;
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch (Exception cleanupFailure) when (primaryFailure is not null)
            {
                primaryFailure.Data[CleanupFailureDataKey] = cleanupFailure;
            }
        }
    }
}
