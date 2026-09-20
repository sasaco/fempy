using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace FrameWeb.LocalRuntime.Tests;

internal sealed class TemporaryRepository : IDisposable
{
    public TemporaryRepository()
    {
        Root = Path.Combine(Path.GetTempPath(), "FrameWeb.LocalRuntime.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(Root, "FrameWeb"));
        File.WriteAllText(Path.Combine(Root, "FrameWeb", "main.py"), "# test fixture", new UTF8Encoding(false));
    }

    public string Root { get; }

    public void Dispose()
    {
        string fullRoot = Path.GetFullPath(Root);
        string allowedRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "FrameWeb.LocalRuntime.Tests"));
        if (!fullRoot.StartsWith(allowedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Refusing to remove a temporary directory outside the test root.");
        }

        if (Directory.Exists(fullRoot))
        {
            Directory.Delete(fullRoot, recursive: true);
        }
    }
}

internal sealed class LocalReadyServer : IAsyncDisposable
{
    public const string ReadyMarker = "{\"service\":\"FrameWeb\",\"protocol\":\"analysis-result-set-v1\",\"status\":\"ready\"}";

    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task _serveTask;
    private readonly string _body;
    private readonly string _contentType;
    private readonly string? _requiredToken;
    private readonly Uri? _redirectLocation;
    private readonly ConcurrentQueue<string?> _receivedTokens = new();

    public LocalReadyServer(
        HttpStatusCode statusCode = HttpStatusCode.OK,
        string body = ReadyMarker,
        string contentType = "application/json; charset=utf-8",
        string? requiredToken = null,
        Uri? redirectLocation = null,
        int port = 0)
    {
        StatusCode = statusCode;
        _body = body;
        _contentType = contentType;
        _requiredToken = requiredToken;
        _redirectLocation = redirectLocation;
        _listener = new TcpListener(IPAddress.Loopback, port);
        _listener.Server.ExclusiveAddressUse = true;
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _serveTask = ServeAsync();
    }

    public int Port { get; }

    public HttpStatusCode StatusCode { get; }

    public IReadOnlyCollection<string?> ReceivedTokens => _receivedTokens.ToArray();

    public async ValueTask DisposeAsync()
    {
        _cancellation.Cancel();
        _listener.Stop();
        try
        {
            await _serveTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (SocketException) when (_cancellation.IsCancellationRequested)
        {
        }

        _cancellation.Dispose();
    }

    private async Task ServeAsync()
    {
        while (!_cancellation.IsCancellationRequested)
        {
            using TcpClient client = await _listener.AcceptTcpClientAsync(_cancellation.Token).ConfigureAwait(false);
            await using NetworkStream stream = client.GetStream();
            string? requestHeaders = await ReadHeadersAsync(stream, _cancellation.Token).ConfigureAwait(false);
            if (requestHeaders is null)
            {
                continue;
            }

            string? token = GetHeader(requestHeaders, FrameWebLocalAuthentication.HeaderName);
            _receivedTokens.Enqueue(token);

            HttpStatusCode responseStatus = _requiredToken is not null &&
                !string.Equals(token, _requiredToken, StringComparison.Ordinal)
                    ? HttpStatusCode.Unauthorized
                    : StatusCode;
            string body = responseStatus == HttpStatusCode.Unauthorized ? "{\"error\":\"unauthorized\"}" : _body;
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
            StringBuilder headers = new();
            _ = headers.Append($"HTTP/1.1 {(int)responseStatus} {ReasonPhrase(responseStatus)}\r\n");
            _ = headers.Append($"Content-Length: {bodyBytes.Length}\r\n");
            _ = headers.Append($"Content-Type: {_contentType}\r\n");
            if (_redirectLocation is not null)
            {
                _ = headers.Append($"Location: {_redirectLocation.AbsoluteUri}\r\n");
            }

            _ = headers.Append("Connection: close\r\n\r\n");
            byte[] response = [.. Encoding.ASCII.GetBytes(headers.ToString()), .. bodyBytes];
            await stream.WriteAsync(response, _cancellation.Token).ConfigureAwait(false);
        }
    }

    private static async Task<string?> ReadHeadersAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        const int maximumHeaderBytes = 16 * 1024;
        byte[] buffer = new byte[1024];
        using MemoryStream received = new();
        while (received.Length < maximumHeaderBytes)
        {
            int read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return null;
            }

            await received.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            string text = Encoding.ASCII.GetString(received.GetBuffer(), 0, checked((int)received.Length));
            if (text.Contains("\r\n\r\n", StringComparison.Ordinal))
            {
                return text;
            }
        }

        throw new InvalidDataException("Test readiness request headers exceeded their bound.");
    }

    private static string? GetHeader(string headers, string name)
    {
        foreach (string line in headers.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
        {
            int separator = line.IndexOf(':');
            if (separator > 0 && string.Equals(line[..separator], name, StringComparison.OrdinalIgnoreCase))
            {
                return line[(separator + 1)..].Trim();
            }
        }

        return null;
    }

    private static string ReasonPhrase(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.OK => "OK",
        HttpStatusCode.Redirect => "Found",
        HttpStatusCode.Unauthorized => "Unauthorized",
        HttpStatusCode.ServiceUnavailable => "Service Unavailable",
        _ => "Response",
    };
}

internal static class RuntimeTestCommands
{
    public static FrameWebRuntimeCommand PowerShell(string workingDirectory, string script)
    {
        string executable = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe");
        return new FrameWebRuntimeCommand(
            executable,
            workingDirectory,
            ["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", script]);
    }

    public static FrameWebRuntimeCommand OwnedReadyServer(
        string workingDirectory,
        int port,
        string prefixScript = "",
        string responseBody = LocalReadyServer.ReadyMarker,
        string contentType = "application/json; charset=utf-8",
        int maximumRequests = 0,
        bool acceptRuntimeToken = true,
        Uri? redirectAfterReadiness = null)
    {
        string encodedBody = Convert.ToBase64String(Encoding.UTF8.GetBytes(responseBody));
        string encodedContentType = Convert.ToBase64String(Encoding.UTF8.GetBytes(contentType));
        string encodedRedirect = Convert.ToBase64String(Encoding.UTF8.GetBytes(
            redirectAfterReadiness?.AbsoluteUri ?? string.Empty));
        string script = $$"""
            $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, {{port}})
            $listener.Server.ExclusiveAddressUse = $true
            $listener.Start()
            {{prefixScript}}
            [Console]::Out.WriteLine('LISTENING')
            $body = [Convert]::FromBase64String('{{encodedBody}}')
            $contentType = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('{{encodedContentType}}'))
            $redirect = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('{{encodedRedirect}}'))
            $served = 0
            while ($true) {
                $client = $listener.AcceptTcpClient()
                try {
                    $stream = $client.GetStream()
                    $buffer = [byte[]]::new(1024)
                    $received = [IO.MemoryStream]::new()
                    while ($received.Length -lt 16384) {
                        $read = $stream.Read($buffer, 0, $buffer.Length)
                        if ($read -eq 0) { break }
                        $received.Write($buffer, 0, $read)
                        $headers = [Text.Encoding]::ASCII.GetString($received.ToArray())
                        if ($headers.Contains("`r`n`r`n")) { break }
                    }
                    $token = $null
                    foreach ($line in ($headers -split "`r`n")) {
                        if ($line.StartsWith('X-FrameWeb-Local-Token:', [StringComparison]::OrdinalIgnoreCase)) {
                            $token = $line.Substring($line.IndexOf(':') + 1).Trim()
                        }
                    }
                    $authenticated = [string]::Equals(
                        $token,
                        $env:FRAMEWEB_LOCAL_AUTH_TOKEN,
                        [StringComparison]::Ordinal) -and ${{acceptRuntimeToken.ToString().ToLowerInvariant()}}
                    $extraHeaders = ''
                    if ($authenticated -and $served -gt 0 -and $redirect.Length -gt 0) {
                        $status = '302 Found'
                        $responseBody = [byte[]]::new(0)
                        $extraHeaders = "Location: $redirect`r`n"
                    }
                    elseif ($authenticated) {
                        $status = '200 OK'
                        $responseBody = $body
                    }
                    else {
                        $status = '401 Unauthorized'
                        $responseBody = [Text.Encoding]::UTF8.GetBytes('{"error":"unauthorized"}')
                    }
                    $responseHeaders = "HTTP/1.1 $status`r`nContent-Length: $($responseBody.Length)`r`nContent-Type: $contentType`r`n${extraHeaders}Connection: close`r`n`r`n"
                    $headerBytes = [Text.Encoding]::ASCII.GetBytes($responseHeaders)
                    $stream.Write($headerBytes, 0, $headerBytes.Length)
                    $stream.Write($responseBody, 0, $responseBody.Length)
                }
                finally {
                    $client.Dispose()
                }
                $served++
                if ({{maximumRequests}} -gt 0 -and $served -ge {{maximumRequests}}) {
                    $listener.Stop()
                    [Console]::Out.WriteLine('LISTENER_STOPPED')
                    while ($true) { Start-Sleep -Milliseconds 100 }
                }
            }
            """;
        return PowerShell(workingDirectory, script);
    }

    public static FrameWebRuntimeOptions Options(
        string repositoryRoot,
        FrameWebRuntimeCommand command,
        int port,
        TimeSpan? startupTimeout = null,
        int outputBudget = 16 * 1024,
        int maximumOutputLineCharacters = 4 * 1024) => new(repositoryRoot)
        {
            CommandOverride = command,
            ReadinessUri = new Uri($"http://127.0.0.1:{port}/", UriKind.Absolute),
            StartupTimeout = startupTimeout ?? TimeSpan.FromSeconds(5),
            StopTimeout = TimeSpan.FromSeconds(3),
            PollInterval = TimeSpan.FromMilliseconds(25),
            ReadinessRequestTimeout = TimeSpan.FromMilliseconds(200),
            MaximumCapturedCharactersPerStream = outputBudget,
            MaximumForwardedOutputLineCharacters = maximumOutputLineCharacters,
        };

    public static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        while (!condition())
        {
            if (DateTimeOffset.UtcNow >= deadline)
            {
                throw new TimeoutException("The expected test condition was not reached.");
            }

            await Task.Delay(25).ConfigureAwait(false);
        }
    }

    public static bool IsProcessAlive(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public static int ReserveAndReleasePort()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Server.ExclusiveAddressUse = true;
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public static string FindRepositoryRoot()
    {
        for (DirectoryInfo? current = new(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md")) &&
                File.Exists(Path.Combine(current.FullName, "FrameWeb", "main.py")))
            {
                return current.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the FrameWeb3 repository root.");
    }
}
