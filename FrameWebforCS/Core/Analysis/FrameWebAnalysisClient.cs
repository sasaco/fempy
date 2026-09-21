using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FrameWebforCS.Core.Abstractions;
using FrameWebforCS.Core.Documents;

namespace FrameWebforCS.Core.Analysis;

public sealed record FrameWebAnalysisClientOptions
{
    public Uri Endpoint { get; init; } = new("/", UriKind.Relative);

    public TimeSpan TransportTimeout { get; init; } = TimeSpan.FromSeconds(30);

    public int MaxConcurrentRequests { get; init; } = 2;

    public int MaxRequestJsonBytes { get; init; } = FrameWebAnalysisRequestJson.DefaultMaxJsonBytes;

    public int MaxResponseJsonBytes { get; init; } = 64 * 1024 * 1024;

    public int MaxJsonDepth { get; init; } = 64;

    public int MaxRequestEntityCount { get; init; } = FrameWebAnalysisRequestJson.DefaultMaxEntityCount;

    public int MaxResponseEntityCount { get; init; } = 2_000_000;

    public int MaxCaseCount { get; init; } = FrameWebAnalysisRequestJson.DefaultMaxCaseCount;

    public int MaxResultCount { get; init; } = 10_000;

    internal FrameWebAnalysisClientOptions ValidateAndCopy()
    {
        FrameWebAnalysisClientOptions copy = this with { };
        if (!IsValidEndpoint(copy.Endpoint))
        {
            throw new ArgumentException(
                "Analysis endpoint must be an origin-relative path or an absolute HTTP/HTTPS URI.",
                nameof(Endpoint));
        }

        RequireRange(copy.TransportTimeout, TimeSpan.FromMilliseconds(1), TimeSpan.FromMinutes(10),
            nameof(TransportTimeout));
        RequireRange(copy.MaxConcurrentRequests, 1, 16, nameof(MaxConcurrentRequests));
        RequireRange(copy.MaxRequestJsonBytes, 1, 64 * 1024 * 1024, nameof(MaxRequestJsonBytes));
        RequireRange(copy.MaxResponseJsonBytes, 1, 256 * 1024 * 1024, nameof(MaxResponseJsonBytes));
        RequireRange(copy.MaxJsonDepth, 1, 256, nameof(MaxJsonDepth));
        RequireRange(copy.MaxRequestEntityCount, 1, 1_000_000, nameof(MaxRequestEntityCount));
        RequireRange(copy.MaxResponseEntityCount, 1, 10_000_000, nameof(MaxResponseEntityCount));
        RequireRange(copy.MaxCaseCount, 1, 256, nameof(MaxCaseCount));
        RequireRange(copy.MaxResultCount, 1, 10_000, nameof(MaxResultCount));
        return copy;
    }

    private static bool IsValidEndpoint(Uri? endpoint)
    {
        if (endpoint is null)
        {
            return false;
        }

        if (endpoint.IsAbsoluteUri)
        {
            return (string.Equals(endpoint.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) &&
                endpoint.UserInfo.Length == 0 &&
                endpoint.Fragment.Length == 0;
        }

        string value = endpoint.OriginalString;
        return value.StartsWith("/", StringComparison.Ordinal) &&
            !value.StartsWith("//", StringComparison.Ordinal) &&
            !value.Contains('\\') &&
            !value.Contains('#');
    }

    private static void RequireRange(int value, int minimum, int maximum, string paramName)
    {
        if (value < minimum || value > maximum)
        {
            throw new ArgumentOutOfRangeException(paramName, $"Value must be between {minimum} and {maximum}.");
        }
    }

    private static void RequireRange(TimeSpan value, TimeSpan minimum, TimeSpan maximum, string paramName)
    {
        if (value < minimum || value > maximum)
        {
            throw new ArgumentOutOfRangeException(paramName, $"Value must be between {minimum} and {maximum}.");
        }
    }
}

/// <summary>
/// Bounded HTTP implementation of the sole AnalysisResultSet v1 calculation contract.
/// The supplied <see cref="HttpClient"/> is not owned or disposed by this instance.
/// Disposing rejects new calls; already admitted calls are allowed to finish before owned
/// concurrency resources are released.
/// </summary>
public sealed class FrameWebAnalysisClient : IAnalysisClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly FrameWebAnalysisClientOptions _options;
    private readonly SemaphoreSlim _concurrency;
    private readonly object _lifecycleGate = new();
    private int _activeCalls;
    private bool _disposed;
    private bool _concurrencyDisposed;

    public FrameWebAnalysisClient(
        HttpClient httpClient,
        FrameWebAnalysisClientOptions? options = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = (options ?? new FrameWebAnalysisClientOptions()).ValidateAndCopy();
        _concurrency = new SemaphoreSlim(
            _options.MaxConcurrentRequests,
            _options.MaxConcurrentRequests);
    }

    public async Task<AnalysisResultSet> AnalyzeAsync(
        ProjectDocument document,
        CancellationToken cancellationToken = default)
    {
        BeginCall();
        bool concurrencyAcquired = false;
        try
        {
            ArgumentNullException.ThrowIfNull(document);
            await _concurrency.WaitAsync(cancellationToken).ConfigureAwait(false);
            concurrencyAcquired = true;
            byte[] requestBytes = SerializeRequest(document, cancellationToken);
            return await SendAsync(requestBytes, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                if (concurrencyAcquired)
                {
                    _concurrency.Release();
                }
            }
            finally
            {
                EndCall();
            }
        }
    }

    public void Dispose()
    {
        lock (_lifecycleGate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            DisposeConcurrencyWhenIdle();
        }
    }

    private void BeginCall()
    {
        lock (_lifecycleGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _activeCalls = checked(_activeCalls + 1);
        }
    }

    private void EndCall()
    {
        lock (_lifecycleGate)
        {
            _activeCalls--;
            DisposeConcurrencyWhenIdle();
        }
    }

    private void DisposeConcurrencyWhenIdle()
    {
        if (_disposed && _activeCalls == 0 && !_concurrencyDisposed)
        {
            _concurrencyDisposed = true;
            _concurrency.Dispose();
        }
    }

    private byte[] SerializeRequest(
        ProjectDocument document,
        CancellationToken cancellationToken)
    {
        try
        {
            return FrameWebAnalysisRequestJson.Serialize(
                document,
                _options.MaxRequestEntityCount,
                _options.MaxRequestJsonBytes,
                _options.MaxCaseCount,
                cancellationToken);
        }
        catch (Exception exception) when (exception is
            FrameWebAnalysisRequestException or
            ProjectDocumentValidationException or
            OverflowException)
        {
            throw new AnalysisClientException(
                OperationFailureKind.Validation,
                "解析入力を確認してください。",
                exception);
        }
    }

    private async Task<AnalysisResultSet> SendAsync(
        byte[] requestBytes,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, _options.Endpoint)
        {
            Content = new ByteArrayContent(requestBytes),
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json")
        {
            CharSet = "utf-8",
        };

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.TransportTimeout);
        try
        {
            using HttpResponseMessage response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw CreateHttpFailure(response.StatusCode);
            }

            ValidateContentType(response.Content.Headers.ContentType);
            byte[] responseBytes = await ReadBoundedAsync(
                response.Content,
                _options.MaxResponseJsonBytes,
                timeout.Token).ConfigureAwait(false);
            return ParseResponse(responseBytes);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception)
        {
            throw new AnalysisClientException(
                OperationFailureKind.Timeout,
                "解析サービスの応答がタイムアウトしました。",
                exception);
        }
        catch (AnalysisClientException)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidOperationException)
        {
            throw new AnalysisClientException(
                OperationFailureKind.Unavailable,
                "解析サービスを利用できません。",
                exception);
        }
    }

    private AnalysisResultSet ParseResponse(byte[] responseBytes)
    {
        if (responseBytes.Length == 0)
        {
            InvalidDataException cause = new("The analysis response body is empty.");
            throw new AnalysisClientException(
                OperationFailureKind.Protocol,
                "解析サービスから無効な応答を受信しました。",
                cause);
        }

        try
        {
            FrameWebResponsePreflight.Validate(responseBytes, _options);
            return AnalysisResultSetJson.Deserialize(responseBytes);
        }
        catch (Exception exception) when (exception is
            JsonException or
            AnalysisContractException or
            FrameWebResponseLimitException or
            InvalidDataException or
            OverflowException)
        {
            throw new AnalysisClientException(
                OperationFailureKind.Protocol,
                "解析サービスから無効な応答を受信しました。",
                exception);
        }
    }

    private static void ValidateContentType(MediaTypeHeaderValue? contentType)
    {
        if (contentType is null ||
            !string.Equals(contentType.MediaType, "application/json", StringComparison.OrdinalIgnoreCase))
        {
            InvalidDataException cause = new("Successful analysis response must use application/json.");
            throw new AnalysisClientException(
                OperationFailureKind.Protocol,
                "解析サービスから無効な応答を受信しました。",
                cause);
        }

        if (contentType.CharSet is string charset && !IsUtf8Charset(charset))
        {
            InvalidDataException cause = new(
                "Successful analysis response must omit charset or declare UTF-8.");
            throw new AnalysisClientException(
                OperationFailureKind.Protocol,
                "解析サービスから無効な応答を受信しました。",
                cause);
        }
    }

    private static bool IsUtf8Charset(string charset)
    {
        string name = charset.Trim();
        if (name.Length >= 2 && name[0] == '"' && name[^1] == '"')
        {
            name = name[1..^1].Trim();
        }

        if (name.Length == 0)
        {
            return false;
        }

        if (string.Equals(name, "utf8", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        try
        {
            return Encoding.GetEncoding(name).CodePage == Encoding.UTF8.CodePage;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static async Task<byte[]> ReadBoundedAsync(
        HttpContent content,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is long contentLength && contentLength > maxBytes)
        {
            throw ProtocolLimit($"Analysis response exceeds the {maxBytes} byte limit.");
        }

        await using Stream stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using MemoryStream buffer = new(capacity: content.Headers.ContentLength is > 0 and <= int.MaxValue
            ? Math.Min((int)content.Headers.ContentLength.Value, maxBytes)
            : 0);
        byte[] chunk = new byte[Math.Min(81_920, maxBytes + 1)];
        while (true)
        {
            int read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return buffer.ToArray();
            }

            if (buffer.Length + read > maxBytes)
            {
                throw ProtocolLimit($"Analysis response exceeds the {maxBytes} byte limit.");
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
    }

    private static AnalysisClientException CreateHttpFailure(HttpStatusCode statusCode)
    {
        OperationFailureKind kind = statusCode switch
        {
            HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity => OperationFailureKind.Validation,
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => OperationFailureKind.Unauthorized,
            HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout => OperationFailureKind.Timeout,
            >= HttpStatusCode.InternalServerError => OperationFailureKind.Unavailable,
            _ => OperationFailureKind.Protocol,
        };
        string userMessage = kind switch
        {
            OperationFailureKind.Validation => "解析入力がサービスに拒否されました。",
            OperationFailureKind.Unauthorized => "解析サービスの認証に失敗しました。",
            OperationFailureKind.Timeout => "解析サービスの応答がタイムアウトしました。",
            OperationFailureKind.Unavailable => "解析サービスを利用できません。",
            _ => "解析サービスから無効な応答を受信しました。",
        };
        HttpRequestException cause = new(
            $"FrameWeb returned HTTP {(int)statusCode}.",
            inner: null,
            statusCode);
        return new AnalysisClientException(kind, userMessage, cause);
    }

    private static AnalysisClientException ProtocolLimit(string message)
        => new(
            OperationFailureKind.Protocol,
            "解析サービスから大きすぎる応答を受信しました。",
            new FrameWebResponseLimitException(message));
}

internal sealed class FrameWebResponseLimitException : Exception
{
    internal FrameWebResponseLimitException(string message)
        : base(message)
    {
    }
}

internal static class FrameWebResponsePreflight
{
    internal static void Validate(
        byte[] utf8Json,
        FrameWebAnalysisClientOptions options)
    {
        using JsonDocument document = JsonDocument.Parse(utf8Json, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = options.MaxJsonDepth,
        });
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Analysis response root must be an object.");
        }

        int cases = ArrayLength(root, "cases");
        int results = ArrayLength(root, "results");
        if (cases > options.MaxCaseCount)
        {
            throw new FrameWebResponseLimitException(
                $"Analysis response contains {cases} cases; the limit is {options.MaxCaseCount}.");
        }

        if (results > options.MaxResultCount)
        {
            throw new FrameWebResponseLimitException(
                $"Analysis response contains {results} results; the limit is {options.MaxResultCount}.");
        }

        EntityCounter counter = new(options.MaxResponseEntityCount);
        counter.Add(cases);
        counter.Add(results);
        if (root.TryGetProperty("cases", out JsonElement caseArray) &&
            caseArray.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement analysisCase in caseArray.EnumerateArray())
            {
                if (analysisCase.ValueKind == JsonValueKind.Object)
                {
                    counter.Add(ArrayLength(analysisCase, "support_node_ids"));
                }
            }
        }

        if (root.TryGetProperty("topology", out JsonElement topology) &&
            topology.ValueKind == JsonValueKind.Object)
        {
            foreach (string property in new[] { "nodes", "members", "shell_elements", "solid_elements" })
            {
                counter.Add(ArrayLength(topology, property));
            }

            CountNestedArrays(topology, "members", "stations", counter);
            CountNestedArrays(topology, "shell_elements", "node_ids", counter);
            CountNestedArrays(topology, "shell_elements", "result_locations", counter);
            CountNestedArrays(topology, "solid_elements", "node_ids", counter);
            CountNestedArrays(topology, "solid_elements", "result_locations", counter);
        }

        if (root.TryGetProperty("results", out JsonElement resultArray) &&
            resultArray.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement result in resultArray.EnumerateArray())
            {
                CountResultEntities(result, counter);
            }
        }
    }

    private static void CountResultEntities(JsonElement result, EntityCounter counter)
    {
        if (result.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (string property in new[]
        {
            "node_displacements",
            "support_reactions",
            "member_section_forces",
            "shell_results",
            "solid_results",
            "node_mode_shapes",
        })
        {
            counter.Add(ArrayLength(result, property));
        }

        CountNestedArrays(result, "member_section_forces", "segments", counter);
        CountNestedArrays(result, "shell_results", "locations", counter);
        CountNestedArrays(result, "solid_results", "locations", counter);
        if (result.TryGetProperty("diagnostics", out JsonElement diagnostics) &&
            diagnostics.ValueKind == JsonValueKind.Object)
        {
            counter.Add(ArrayLength(diagnostics, "warnings"));
            counter.Add(ArrayLength(diagnostics, "iterations"));
        }
    }

    private static void CountNestedArrays(
        JsonElement owner,
        string collectionName,
        string nestedName,
        EntityCounter counter)
    {
        if (!owner.TryGetProperty(collectionName, out JsonElement collection) ||
            collection.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (JsonElement item in collection.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object)
            {
                counter.Add(ArrayLength(item, nestedName));
            }
        }
    }

    private static int ArrayLength(JsonElement owner, string propertyName)
        => owner.TryGetProperty(propertyName, out JsonElement value) &&
            value.ValueKind == JsonValueKind.Array
                ? value.GetArrayLength()
                : 0;

    private sealed class EntityCounter(int limit)
    {
        private int _count;

        internal void Add(int value)
        {
            _count = checked(_count + value);
            if (_count > limit)
            {
                throw new FrameWebResponseLimitException(
                    $"Analysis response contains more than {limit} bounded entities.");
            }
        }
    }
}
