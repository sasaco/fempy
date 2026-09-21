using System.Collections.ObjectModel;

namespace FrameWeb.LocalRuntime;

public enum FrameWebRuntimeState
{
    Created,
    Starting,
    Ready,
    Stopping,
    Stopped,
    Faulted,
    Disposed,
}

public enum FrameWebRuntimeOutputStream
{
    StandardOutput,
    StandardError,
}

public sealed class FrameWebRuntimeOutputEventArgs(
    FrameWebRuntimeOutputStream stream,
    string line,
    bool isTruncated = false) : EventArgs
{
    public FrameWebRuntimeOutputStream Stream { get; } = stream;

    public string Line { get; } = line;

    public bool IsTruncated { get; } = isTruncated;
}

public sealed record FrameWebRuntimeDiagnostics(
    FrameWebRuntimeState State,
    int? ProcessId,
    int? ExitCode,
    IReadOnlyList<string> StandardOutput,
    IReadOnlyList<string> StandardError,
    bool OutputTruncated,
    string? Failure);

public sealed class FrameWebRuntimeException : Exception
{
    public FrameWebRuntimeException(string message) : base(message)
    {
    }

    public FrameWebRuntimeException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public sealed class FrameWebRuntimeCommand
{
    private readonly ReadOnlyCollection<string> _arguments;
    private readonly ReadOnlyDictionary<string, string?> _environment;

    public FrameWebRuntimeCommand(
        string fileName,
        string workingDirectory,
        IEnumerable<string> arguments,
        IReadOnlyDictionary<string, string?>? environment = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentNullException.ThrowIfNull(arguments);

        FileName = fileName;
        WorkingDirectory = workingDirectory;
        _arguments = Array.AsReadOnly(arguments.ToArray());
        _environment = new ReadOnlyDictionary<string, string?>(
            new Dictionary<string, string?>(environment ?? new Dictionary<string, string?>(), StringComparer.OrdinalIgnoreCase));
    }

    public string FileName { get; }

    public string WorkingDirectory { get; }

    public IReadOnlyList<string> Arguments => _arguments;

    public IReadOnlyDictionary<string, string?> Environment => _environment;
}

public sealed record FrameWebRuntimeOptions
{
    public FrameWebRuntimeOptions(string repositoryRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        RepositoryRoot = Path.GetFullPath(repositoryRoot);
    }

    public string RepositoryRoot { get; init; }

    public string Host { get; init; } = "127.0.0.1";

    public int Port { get; init; } = 8080;

    public Uri? ReadinessUri { get; init; }

    public TimeSpan StartupTimeout { get; init; } = TimeSpan.FromMinutes(2);

    public TimeSpan StopTimeout { get; init; } = TimeSpan.FromSeconds(5);

    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMilliseconds(250);

    public TimeSpan ReadinessRequestTimeout { get; init; } = TimeSpan.FromSeconds(2);

    public int MaximumCapturedCharactersPerStream { get; init; } = 256 * 1024;

    public int MaximumForwardedOutputLineCharacters { get; init; } = 4 * 1024;

    public string? PythonExecutablePath { get; init; }

    public string UvExecutablePath { get; init; } = "uv";

    public FrameWebRuntimeCommand? CommandOverride { get; init; }

    public Uri EffectiveReadinessUri => ReadinessUri ?? new Uri($"http://{Host}:{Port}/", UriKind.Absolute);

    internal void Validate()
    {
        if (!Directory.Exists(RepositoryRoot))
        {
            throw new DirectoryNotFoundException($"Repository root does not exist: {RepositoryRoot}");
        }

        if (string.IsNullOrWhiteSpace(Host))
        {
            throw new ArgumentException("Host is required.", nameof(Host));
        }

        if (!System.Net.IPAddress.TryParse(Host, out System.Net.IPAddress? address) ||
            !System.Net.IPAddress.IsLoopback(address))
        {
            throw new ArgumentException(
                "FrameWeb must bind to a literal loopback IP address so listener ownership can be verified.",
                nameof(Host));
        }

        if (Port is <= 0 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(Port));
        }

        if (!string.Equals(EffectiveReadinessUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("ReadinessUri must use HTTP.", nameof(ReadinessUri));
        }

        if (!System.Net.IPAddress.TryParse(EffectiveReadinessUri.DnsSafeHost, out System.Net.IPAddress? readinessAddress) ||
            !System.Net.IPAddress.IsLoopback(readinessAddress) ||
            EffectiveReadinessUri.UserInfo.Length != 0 ||
            !string.Equals(EffectiveReadinessUri.AbsolutePath, "/", StringComparison.Ordinal) ||
            EffectiveReadinessUri.Query.Length != 0 ||
            EffectiveReadinessUri.Fragment.Length != 0)
        {
            throw new ArgumentException(
                "ReadinessUri must be an exact loopback HTTP origin root without credentials, query, or fragment.",
                nameof(ReadinessUri));
        }

        RequirePositive(StartupTimeout, nameof(StartupTimeout));
        RequirePositive(StopTimeout, nameof(StopTimeout));
        RequirePositive(PollInterval, nameof(PollInterval));
        RequirePositive(ReadinessRequestTimeout, nameof(ReadinessRequestTimeout));
        if (MaximumCapturedCharactersPerStream < 1024)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumCapturedCharactersPerStream),
                "The capture budget must be at least 1024 characters per stream.");
        }

        if (MaximumForwardedOutputLineCharacters is < 256 or > 64 * 1024)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumForwardedOutputLineCharacters),
                "Forwarded output lines must be bounded between 256 and 65536 characters.");
        }
    }

    private static void RequirePositive(TimeSpan value, string name)
    {
        if (value <= TimeSpan.Zero || value == Timeout.InfiniteTimeSpan)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }
}
