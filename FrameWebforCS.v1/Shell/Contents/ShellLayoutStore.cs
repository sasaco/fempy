using System.Text;

namespace FrameWebforCS.Shell;

public interface IShellLayoutStore
{
    Task<string?> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(string json, CancellationToken cancellationToken = default);
}

public sealed class ShellLayoutStoreException : Exception
{
    public ShellLayoutStoreException(string message)
        : base(message)
    {
    }

    public ShellLayoutStoreException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Persists the versioned shell layout as bounded UTF-8 JSON in the current user's local
/// application-data directory. Writes use a temporary file in the destination directory so the
/// final replace cannot expose a partially written layout.
/// </summary>
public sealed class LocalShellLayoutStore : IShellLayoutStore
{
    public const int MaximumLayoutBytes = 256 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly string layoutPath;

    public LocalShellLayoutStore(string? layoutPath = null)
    {
        this.layoutPath = ValidatePath(layoutPath ?? CreateDefaultPath());
    }

    public async Task<string?> LoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await using FileStream stream = new(
                layoutPath,
                new FileStreamOptions
                {
                    Access = FileAccess.Read,
                    Mode = FileMode.Open,
                    Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
                    Share = FileShare.Read,
                });
            byte[] bytes = new byte[MaximumLayoutBytes + 1];
            int count = 0;
            while (count < bytes.Length)
            {
                int read = await stream.ReadAsync(bytes.AsMemory(count), cancellationToken);
                if (read == 0)
                {
                    break;
                }

                count += read;
            }

            if (count > MaximumLayoutBytes)
            {
                throw new ShellLayoutStoreException(
                    $"The shell layout exceeds the {MaximumLayoutBytes}-byte limit.");
            }

            return StrictUtf8.GetString(bytes, 0, count);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ShellLayoutStoreException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or DecoderFallbackException)
        {
            throw new ShellLayoutStoreException("The shell layout could not be loaded.", exception);
        }
    }

    public async Task SaveAsync(string json, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        cancellationToken.ThrowIfCancellationRequested();

        byte[] bytes;
        try
        {
            bytes = StrictUtf8.GetBytes(json);
        }
        catch (EncoderFallbackException exception)
        {
            throw new ShellLayoutStoreException("The shell layout is not valid UTF-8 text.", exception);
        }

        if (bytes.Length > MaximumLayoutBytes)
        {
            throw new ShellLayoutStoreException(
                $"The shell layout exceeds the {MaximumLayoutBytes}-byte limit.");
        }

        string directory = Path.GetDirectoryName(layoutPath)!;
        string temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(layoutPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            Directory.CreateDirectory(directory);
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
                await stream.WriteAsync(bytes, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, layoutPath, overwrite: true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ShellLayoutStoreException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ShellLayoutStoreException("The shell layout could not be saved.", exception);
        }
        finally
        {
            TryDeleteTemporaryFile(temporaryPath);
        }
    }

    private static string CreateDefaultPath()
    {
        string localApplicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localApplicationData))
        {
            throw new InvalidOperationException("The local application-data directory is unavailable.");
        }

        return Path.Combine(localApplicationData, "FrameWeb3", "FrameWebforCS", "layout-v1.json");
    }

    private static string ValidatePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);
        if (string.IsNullOrWhiteSpace(Path.GetFileName(fullPath)) ||
            string.IsNullOrWhiteSpace(Path.GetDirectoryName(fullPath)))
        {
            throw new ArgumentException("A layout file path is required.", nameof(path));
        }

        return fullPath;
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
            // A failed best-effort cleanup must not replace the original save outcome.
        }
    }
}
