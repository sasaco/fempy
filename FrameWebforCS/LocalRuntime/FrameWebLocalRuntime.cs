using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace FrameWeb.LocalRuntime;

public sealed class FrameWebLocalRuntime : IDisposable, IAsyncDisposable
{
    private const int MaximumReadinessResponseBytes = 1024;
    private const string ExpectedReadinessService = "FrameWeb";
    private const string ExpectedReadinessProtocol = "analysis-result-set-v1";
    private const string ExpectedReadinessStatus = "ready";
    private readonly FrameWebRuntimeOptions _options;
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly object _stateGate = new();
    private readonly BoundedLineBuffer _standardOutput;
    private readonly BoundedLineBuffer _standardError;
    private WindowsProcessJob? _job;
    private ManagedChildProcess? _process;
    private CancellationTokenSource? _lifetimeCancellation;
    private FrameWebRuntimeState _state = FrameWebRuntimeState.Created;
    private string? _failure;
    private string? _authenticationToken;
    private int? _lastProcessId;
    private int? _lastExitCode;
    private int _disposeStarted;

    public FrameWebLocalRuntime(FrameWebRuntimeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        _options = options;
        _standardOutput = new BoundedLineBuffer(options.MaximumCapturedCharactersPerStream);
        _standardError = new BoundedLineBuffer(options.MaximumCapturedCharactersPerStream);
        AppDomain.CurrentDomain.ProcessExit += OnParentProcessExit;
    }

    public FrameWebRuntimeState State
    {
        get
        {
            lock (_stateGate)
            {
                return _state;
            }
        }
    }

    public int? ProcessId
    {
        get
        {
            lock (_stateGate)
            {
                return _process?.Id ?? _lastProcessId;
            }
        }
    }

    public FrameWebRuntimeDiagnostics Diagnostics
    {
        get
        {
            lock (_stateGate)
            {
                return new FrameWebRuntimeDiagnostics(
                    _state,
                    _process?.Id ?? _lastProcessId,
                    _process?.ExitCode ?? _lastExitCode,
                    _standardOutput.Snapshot(),
                    _standardError.Snapshot(),
                    _standardOutput.Truncated || _standardError.Truncated,
                    _failure);
            }
        }
    }

    public event EventHandler<FrameWebRuntimeOutputEventArgs>? OutputReceived;

    public HttpClient CreateHttpClient()
    {
        ThrowIfDisposed();
        lock (_stateGate)
        {
            if (_state != FrameWebRuntimeState.Ready || _authenticationToken is null)
            {
                throw new InvalidOperationException("FrameWeb must be ready before creating an analysis client.");
            }
        }

        return new HttpClient(new FrameWebAuthenticatedHttpMessageHandler(this), disposeHandler: true)
        {
            BaseAddress = _options.EffectiveReadinessUri,
        };
    }

    internal void AuthorizeRequest(HttpRequestMessage request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ThrowIfDisposed();
        Uri endpoint = request.RequestUri ?? throw new InvalidOperationException("The FrameWeb request URI is required.");
        if (!IsExactRuntimeEndpoint(endpoint))
        {
            throw new FrameWebRuntimeException("FrameWeb requests are restricted to the authenticated local endpoint.");
        }

        lock (_stateGate)
        {
            if (_state != FrameWebRuntimeState.Ready ||
                _authenticationToken is null ||
                _job is null ||
                _process is null ||
                _process.HasExited)
            {
                throw new FrameWebRuntimeException("FrameWeb is not ready for an authenticated request.");
            }

            RequireOwnedListener(_job);
            _ = request.Headers.Remove(FrameWebLocalAuthentication.HeaderName);
            if (!request.Headers.TryAddWithoutValidation(
                    FrameWebLocalAuthentication.HeaderName,
                    _authenticationToken))
            {
                throw new FrameWebRuntimeException("Could not authorize the FrameWeb local request.");
            }
        }
    }

    internal SocketsHttpHandler CreateOwnedHttpHandler() =>
        OwnedLoopbackHttpConnection.CreateHandler(_options.EffectiveReadinessUri, RequireReadyOwnedListener);

    internal int[] SnapshotOwnedProcessIds()
    {
        lock (_stateGate)
        {
            return _job?.SnapshotProcessIds() ?? [];
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (State == FrameWebRuntimeState.Ready)
            {
                return;
            }

            if (_process is not null)
            {
                throw new InvalidOperationException("The runtime has an existing process that must be stopped before restart.");
            }

            SetState(FrameWebRuntimeState.Starting, failure: null);
            _lastExitCode = null;
            string authenticationToken = FrameWebLocalAuthentication.CreateToken();
            FrameWebRuntimeCommand command = FrameWebRuntimeCommandFactory.Create(_options, authenticationToken);
            WindowsProcessJob job = new();
            ManagedChildProcess? process = null;
            CancellationTokenSource lifetime = new();
            try
            {
                process = new ManagedChildProcess(
                    command,
                    job,
                    OnOutput,
                    _options.MaximumForwardedOutputLineCharacters,
                    line => RedactAuthenticationToken(line, authenticationToken));
                lock (_stateGate)
                {
                    _job = job;
                    _process = process;
                    _lifetimeCancellation = lifetime;
                    _lastProcessId = process.Id;
                    _authenticationToken = authenticationToken;
                }

                using CancellationTokenSource timeout = new(_options.StartupTimeout);
                using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    lifetime.Token,
                    timeout.Token);
                try
                {
                    await WaitForReadinessAsync(process, job, authenticationToken, linked.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException exception) when (
                    timeout.IsCancellationRequested &&
                    !cancellationToken.IsCancellationRequested &&
                    !lifetime.IsCancellationRequested)
                {
                    throw new TimeoutException(
                        $"FrameWeb did not become ready within {_options.StartupTimeout}.",
                        exception);
                }

                SetState(FrameWebRuntimeState.Ready, failure: null);
                _ = MonitorUnexpectedExitAsync(process);
            }
            catch (Exception exception)
            {
                SetState(
                    exception is OperationCanceledException
                        ? FrameWebRuntimeState.Stopped
                        : FrameWebRuntimeState.Faulted,
                    exception.Message);
                await CleanupOwnedProcessAsync(CancellationToken.None).ConfigureAwait(false);
                if (process is null)
                {
                    lifetime.Dispose();
                    job.Dispose();
                }

                throw;
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        CancellationTokenSource? startupCancellation;
        lock (_stateGate)
        {
            startupCancellation = _lifetimeCancellation;
        }

        startupCancellation?.Cancel();
        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (State == FrameWebRuntimeState.Disposed)
            {
                return;
            }

            if (_process is null)
            {
                SetState(FrameWebRuntimeState.Stopped, failure: null);
                return;
            }

            SetState(FrameWebRuntimeState.Stopping, failure: null);
            try
            {
                await CleanupOwnedProcessAsync(cancellationToken).ConfigureAwait(false);
                SetState(FrameWebRuntimeState.Stopped, failure: null);
            }
            catch (Exception exception)
            {
                SetState(FrameWebRuntimeState.Faulted, exception.Message);
                throw;
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeStarted, 1) != 0)
        {
            return;
        }

        try
        {
            StopCoreForDisposeAsync().GetAwaiter().GetResult();
        }
        finally
        {
            CompleteDispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeStarted, 1) != 0)
        {
            return;
        }

        try
        {
            await StopCoreForDisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            CompleteDispose();
        }
    }

    private async Task WaitForReadinessAsync(
        ManagedChildProcess process,
        WindowsProcessJob job,
        string authenticationToken,
        CancellationToken cancellationToken)
    {
        using SocketsHttpHandler handler = OwnedLoopbackHttpConnection.CreateHandler(
            _options.EffectiveReadinessUri,
            () => RequireOwnedListener(job));
        using HttpClient client = new(handler) { Timeout = _options.ReadinessRequestTimeout };
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (process.HasExited)
            {
                throw new FrameWebRuntimeException(
                    $"FrameWeb exited before readiness (exit {process.ExitCode?.ToString() ?? "unknown"}).");
            }

            if (!IsListenerOwnedByJob(job))
            {
                await Task.Delay(_options.PollInterval, cancellationToken).ConfigureAwait(false);
                continue;
            }

            try
            {
                using HttpRequestMessage request = new(HttpMethod.Get, _options.EffectiveReadinessUri);
                _ = request.Headers.TryAddWithoutValidation(
                    FrameWebLocalAuthentication.HeaderName,
                    authenticationToken);
                using HttpResponseMessage response = await client.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken).ConfigureAwait(false);
                if (await IsExpectedReadinessResponseAsync(response, cancellationToken).ConfigureAwait(false))
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // The listener may not have bound yet; poll until the bounded startup token expires.
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // An individual request timed out; the overall startup token remains authoritative.
            }
            catch (InvalidDataException)
            {
                // An unrelated or malformed loopback response cannot establish readiness.
            }
            catch (JsonException)
            {
                // An unrelated or malformed loopback response cannot establish readiness.
            }
            catch (IOException)
            {
                // A listener may close a partial response while the child is still starting.
            }

            await Task.Delay(_options.PollInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    private void RequireOwnedListener(WindowsProcessJob job)
    {
        if (!IsListenerOwnedByJob(job))
        {
            throw new FrameWebRuntimeException(
                "The FrameWeb listener is not owned by the managed runtime process tree.");
        }
    }

    private void RequireReadyOwnedListener()
    {
        lock (_stateGate)
        {
            if (_state != FrameWebRuntimeState.Ready ||
                _job is null ||
                _process is null ||
                _process.HasExited)
            {
                throw new FrameWebRuntimeException("FrameWeb is not ready for an authenticated connection.");
            }

            RequireOwnedListener(_job);
        }
    }

    private bool IsListenerOwnedByJob(WindowsProcessJob job)
    {
        int? listenerProcessId = WindowsTcpListenerOwner.GetOwningProcessId(_options.EffectiveReadinessUri);
        return listenerProcessId is not null && job.ContainsProcessId(listenerProcessId.Value);
    }

    private bool IsExactRuntimeEndpoint(Uri endpoint)
    {
        Uri expected = _options.EffectiveReadinessUri;
        return endpoint.IsAbsoluteUri &&
            string.Equals(endpoint.Scheme, expected.Scheme, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(endpoint.Host, expected.Host, StringComparison.OrdinalIgnoreCase) &&
            endpoint.Port == expected.Port &&
            string.Equals(endpoint.AbsolutePath, "/", StringComparison.Ordinal) &&
            endpoint.Query.Length == 0 &&
            endpoint.Fragment.Length == 0 &&
            endpoint.UserInfo.Length == 0;
    }

    private static async Task<bool> IsExpectedReadinessResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode != HttpStatusCode.OK || !HasExpectedJsonContentType(response.Content.Headers.ContentType))
        {
            return false;
        }

        byte[] body = await ReadBoundedReadinessBodyAsync(response.Content, cancellationToken).ConfigureAwait(false);
        using JsonDocument document = JsonDocument.Parse(body, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 4,
        });
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 3)
        {
            return false;
        }

        return HasExactString(root, "service", ExpectedReadinessService) &&
            HasExactString(root, "protocol", ExpectedReadinessProtocol) &&
            HasExactString(root, "status", ExpectedReadinessStatus);
    }

    private static bool HasExpectedJsonContentType(MediaTypeHeaderValue? contentType) =>
        string.Equals(contentType?.MediaType, "application/json", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(contentType?.CharSet?.Trim('"'), "utf-8", StringComparison.OrdinalIgnoreCase);

    private static bool HasExactString(JsonElement root, string propertyName, string expected) =>
        root.TryGetProperty(propertyName, out JsonElement value) &&
        value.ValueKind == JsonValueKind.String &&
        string.Equals(value.GetString(), expected, StringComparison.Ordinal);

    private static async Task<byte[]> ReadBoundedReadinessBodyAsync(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is > MaximumReadinessResponseBytes)
        {
            throw new InvalidDataException("FrameWeb readiness response exceeded its byte limit.");
        }

        await using Stream stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using MemoryStream buffer = new();
        byte[] chunk = new byte[MaximumReadinessResponseBytes + 1];
        while (true)
        {
            int remaining = MaximumReadinessResponseBytes + 1 - checked((int)buffer.Length);
            int read = await stream.ReadAsync(chunk.AsMemory(0, remaining), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return buffer.ToArray();
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            if (buffer.Length > MaximumReadinessResponseBytes)
            {
                throw new InvalidDataException("FrameWeb readiness response exceeded its byte limit.");
            }
        }
    }

    private async Task MonitorUnexpectedExitAsync(ManagedChildProcess process)
    {
        try
        {
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            lock (_stateGate)
            {
                if (ReferenceEquals(_process, process) && _state == FrameWebRuntimeState.Ready)
                {
                    _lastExitCode = process.ExitCode;
                    _failure = $"FrameWeb exited unexpectedly (exit {_lastExitCode?.ToString() ?? "unknown"}).";
                    _state = FrameWebRuntimeState.Faulted;
                }
            }
        }
        catch (ObjectDisposedException)
        {
            // Normal StopAsync/Dispose races with the passive monitor.
        }
        catch (InvalidOperationException)
        {
            // Normal StopAsync/Dispose races with the passive monitor.
        }
    }

    private async Task CleanupOwnedProcessAsync(CancellationToken cancellationToken)
    {
        ManagedChildProcess? process;
        WindowsProcessJob? job;
        CancellationTokenSource? lifetime;
        lock (_stateGate)
        {
            process = _process;
            job = _job;
            lifetime = _lifetimeCancellation;
            _process = null;
            _job = null;
            _lifetimeCancellation = null;
            _authenticationToken = null;
        }

        lifetime?.Cancel();
        try
        {
            if (process is not null)
            {
                try
                {
                    await process.StopAsync(_options.StopTimeout, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    lock (_stateGate)
                    {
                        _lastExitCode = process.ExitCode;
                    }
                }
            }
        }
        finally
        {
            process?.Dispose();
            job?.Dispose();
            lifetime?.Dispose();
        }
    }

    private async Task StopCoreForDisposeAsync()
    {
        CancellationTokenSource? lifetime;
        lock (_stateGate)
        {
            lifetime = _lifetimeCancellation;
        }

        lifetime?.Cancel();
        await _operationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await CleanupOwnedProcessAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private void CompleteDispose()
    {
        AppDomain.CurrentDomain.ProcessExit -= OnParentProcessExit;
        SetState(FrameWebRuntimeState.Disposed, failure: null);
        _operationGate.Dispose();
    }

    private void OnParentProcessExit(object? sender, EventArgs eventArgs)
    {
        CancellationTokenSource? lifetime;
        WindowsProcessJob? job;
        lock (_stateGate)
        {
            lifetime = _lifetimeCancellation;
            job = _job;
        }

        lifetime?.Cancel();
        job?.Dispose();
    }

    private void OnOutput(FrameWebRuntimeOutputStream stream, string line, bool isTruncated)
    {
        if (stream == FrameWebRuntimeOutputStream.StandardOutput)
        {
            _standardOutput.Add(line, isTruncated);
        }
        else
        {
            _standardError.Add(line, isTruncated);
        }

        try
        {
            OutputReceived?.Invoke(this, new FrameWebRuntimeOutputEventArgs(stream, line, isTruncated));
        }
        catch
        {
            // Diagnostic subscribers cannot terminate or destabilize the owned process.
        }
    }

    private static string RedactAuthenticationToken(string line, string authenticationToken) =>
        line.Contains(authenticationToken, StringComparison.Ordinal)
            ? line.Replace(authenticationToken, "[REDACTED]", StringComparison.Ordinal)
            : line;

    private void SetState(FrameWebRuntimeState state, string? failure)
    {
        lock (_stateGate)
        {
            _state = state;
            _failure = failure;
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(
        Volatile.Read(ref _disposeStarted) != 0 || State == FrameWebRuntimeState.Disposed,
        this);
}
