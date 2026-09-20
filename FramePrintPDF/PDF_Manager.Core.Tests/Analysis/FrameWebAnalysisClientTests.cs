using System.Net;
using System.Net.Http.Headers;
using System.Text;
using PDF_Manager.Core.Abstractions;
using PDF_Manager.Core.Analysis;
using PDF_Manager.Core.Documents;

namespace PDF_Manager.Core.Tests.Analysis;

public sealed class FrameWebAnalysisClientTests
{
    [Fact]
    public async Task Success_PostsRootJsonAndReturnsOnlyValidatedResultSet()
    {
        byte[] responseBytes = ReadPositiveBytes("single-static.json");
        HttpRequestMessage? capturedRequest = null;
        byte[]? capturedBody = null;
        StubHandler handler = new(async (request, _) =>
        {
            capturedRequest = request;
            capturedBody = await request.Content!.ReadAsByteArrayAsync();
            return JsonResponse(HttpStatusCode.OK, responseBytes);
        });
        using HttpClient httpClient = CreateHttpClient(handler);
        FrameWebAnalysisClient client = new(httpClient);

        AnalysisResultSet result = await client.AnalyzeAsync(
            ProjectDocumentPresets.CreateRepresentativeFrame());

        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Equal(new Uri("http://localhost:5000/"), capturedRequest.RequestUri);
        Assert.Equal("application/json", capturedRequest.Content!.Headers.ContentType!.MediaType);
        Assert.Equal(
            FrameWebAnalysisRequestJson.Serialize(ProjectDocumentPresets.CreateRepresentativeFrame()),
            capturedBody);
        Assert.Equal(AnalysisResultSet.ContractKind, result.Kind);
        Assert.Single(result.Results);
    }

    [Fact]
    public async Task UserCancellation_IsPropagatedWithoutReclassification()
    {
        StubHandler handler = new(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException("unreachable");
        });
        using HttpClient httpClient = CreateHttpClient(handler);
        FrameWebAnalysisClient client = new(httpClient);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.AnalyzeAsync(
            ProjectDocumentPresets.CreateRepresentativeFrame(),
            cancellation.Token));
    }

    [Fact]
    public async Task TransportTimeout_IsClassifiedAndPreservesCause()
    {
        StubHandler handler = new(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException("unreachable");
        });
        using HttpClient httpClient = CreateHttpClient(handler);
        FrameWebAnalysisClient client = new(httpClient, new FrameWebAnalysisClientOptions
        {
            TransportTimeout = TimeSpan.FromMilliseconds(30),
        });

        AnalysisClientException exception = await Assert.ThrowsAsync<AnalysisClientException>(() =>
            client.AnalyzeAsync(ProjectDocumentPresets.CreateRepresentativeFrame()));

        Assert.Equal(OperationFailureKind.Timeout, exception.FailureKind);
        Assert.IsAssignableFrom<OperationCanceledException>(exception.InnerException);
    }

    [Fact]
    public async Task NetworkFailure_IsUnavailableAndPreservesCause()
    {
        HttpRequestException cause = new("transport detail");
        StubHandler handler = new((_, _) => Task.FromException<HttpResponseMessage>(cause));
        using HttpClient httpClient = CreateHttpClient(handler);
        FrameWebAnalysisClient client = new(httpClient);

        AnalysisClientException exception = await Assert.ThrowsAsync<AnalysisClientException>(() =>
            client.AnalyzeAsync(ProjectDocumentPresets.CreateRepresentativeFrame()));

        Assert.Equal(OperationFailureKind.Unavailable, exception.FailureKind);
        Assert.Same(cause, exception.InnerException);
        Assert.DoesNotContain(cause.Message, exception.UserMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, OperationFailureKind.Validation)]
    [InlineData(HttpStatusCode.Unauthorized, OperationFailureKind.Unauthorized)]
    [InlineData(HttpStatusCode.ServiceUnavailable, OperationFailureKind.Unavailable)]
    public async Task HttpFailure_IsMappedToTypedUserSafeFailure(
        HttpStatusCode statusCode,
        OperationFailureKind expectedKind)
    {
        StubHandler handler = new((_, _) => Task.FromResult(JsonResponse(statusCode, "{}"u8.ToArray())));
        using HttpClient httpClient = CreateHttpClient(handler);
        FrameWebAnalysisClient client = new(httpClient);

        AnalysisClientException exception = await Assert.ThrowsAsync<AnalysisClientException>(() =>
            client.AnalyzeAsync(ProjectDocumentPresets.CreateRepresentativeFrame()));

        Assert.Equal(expectedKind, exception.FailureKind);
        Assert.IsType<HttpRequestException>(exception.InnerException);
        Assert.DoesNotContain("{}", exception.UserMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OversizedRequestAndResponse_AreRejectedBeforeUnboundedWork()
    {
        int calls = 0;
        byte[] response = ReadPositiveBytes("single-static.json");
        StubHandler handler = new((_, _) =>
        {
            calls++;
            return Task.FromResult(JsonResponse(HttpStatusCode.OK, response));
        });
        using HttpClient httpClient = CreateHttpClient(handler);
        FrameWebAnalysisClient requestLimited = new(httpClient, new FrameWebAnalysisClientOptions
        {
            MaxRequestJsonBytes = 32,
        });

        AnalysisClientException requestFailure = await Assert.ThrowsAsync<AnalysisClientException>(() =>
            requestLimited.AnalyzeAsync(ProjectDocumentPresets.CreateRepresentativeFrame()));
        Assert.Equal(OperationFailureKind.Validation, requestFailure.FailureKind);
        Assert.Equal(0, calls);

        FrameWebAnalysisClient entityLimited = new(httpClient, new FrameWebAnalysisClientOptions
        {
            MaxRequestEntityCount = 1,
        });
        AnalysisClientException entityFailure = await Assert.ThrowsAsync<AnalysisClientException>(() =>
            entityLimited.AnalyzeAsync(ProjectDocumentPresets.CreateRepresentativeFrame()));
        Assert.Equal(OperationFailureKind.Validation, entityFailure.FailureKind);
        Assert.Equal(0, calls);

        FrameWebAnalysisClient responseLimited = new(httpClient, new FrameWebAnalysisClientOptions
        {
            MaxResponseJsonBytes = response.Length - 1,
        });
        AnalysisClientException responseFailure = await Assert.ThrowsAsync<AnalysisClientException>(() =>
            responseLimited.AnalyzeAsync(ProjectDocumentPresets.CreateRepresentativeFrame()));
        Assert.Equal(OperationFailureKind.Protocol, responseFailure.FailureKind);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task UnknownLengthOversizedResponse_IsStoppedWhileStreaming()
    {
        byte[] response = ReadPositiveBytes("single-static.json");
        StubHandler handler = new((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new UnknownLengthJsonContent(response).WithJsonContentType(),
        }));
        using HttpClient httpClient = CreateHttpClient(handler);
        FrameWebAnalysisClient client = new(httpClient, new FrameWebAnalysisClientOptions
        {
            MaxResponseJsonBytes = response.Length - 1,
        });

        AnalysisClientException exception = await Assert.ThrowsAsync<AnalysisClientException>(() =>
            client.AnalyzeAsync(ProjectDocumentPresets.CreateRepresentativeFrame()));

        Assert.Equal(OperationFailureKind.Protocol, exception.FailureKind);
    }

    [Fact]
    public async Task SuccessfulNonJsonResponse_IsAProtocolFailure()
    {
        StubHandler handler = new((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(ReadPositiveBytes("single-static.json")),
        }));
        using HttpClient httpClient = CreateHttpClient(handler);
        FrameWebAnalysisClient client = new(httpClient);

        AnalysisClientException exception = await Assert.ThrowsAsync<AnalysisClientException>(() =>
            client.AnalyzeAsync(ProjectDocumentPresets.CreateRepresentativeFrame()));

        Assert.Equal(OperationFailureKind.Protocol, exception.FailureKind);
        Assert.IsType<InvalidDataException>(exception.InnerException);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("utf-8")]
    [InlineData("UTF8")]
    [InlineData("\"utf-8\"")]
    public async Task JsonResponse_WithAbsentOrUtf8Charset_IsAccepted(string? charset)
    {
        StubHandler handler = new((_, _) => Task.FromResult(JsonResponse(
            HttpStatusCode.OK,
            ReadPositiveBytes("single-static.json"),
            charset)));
        using HttpClient httpClient = CreateHttpClient(handler);
        FrameWebAnalysisClient client = new(httpClient);

        AnalysisResultSet result = await client.AnalyzeAsync(
            ProjectDocumentPresets.CreateRepresentativeFrame());

        Assert.Single(result.Results);
    }

    [Theory]
    [InlineData("utf-16")]
    [InlineData("iso-8859-1")]
    [InlineData("x-unknown")]
    public async Task JsonResponse_WithNonUtf8Charset_IsAProtocolFailure(string charset)
    {
        StubHandler handler = new((_, _) => Task.FromResult(JsonResponse(
            HttpStatusCode.OK,
            ReadPositiveBytes("single-static.json"),
            charset)));
        using HttpClient httpClient = CreateHttpClient(handler);
        FrameWebAnalysisClient client = new(httpClient);

        AnalysisClientException exception = await Assert.ThrowsAsync<AnalysisClientException>(() =>
            client.AnalyzeAsync(ProjectDocumentPresets.CreateRepresentativeFrame()));

        Assert.Equal(OperationFailureKind.Protocol, exception.FailureKind);
        Assert.IsType<InvalidDataException>(exception.InnerException);
    }

    [Theory]
    [MemberData(nameof(InvalidSuccessBodies))]
    public async Task InvalidUtf8MalformedPartialAndAlternateSuccess_AreProtocolFailures(byte[] body)
    {
        StubHandler handler = new((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, body)));
        using HttpClient httpClient = CreateHttpClient(handler);
        FrameWebAnalysisClient client = new(httpClient);
        AnalysisResultSet prior = AnalysisContractFixtureTests.ReadPositive("single-static.json");
        AnalysisResultState state = new();
        state.Commit(prior);

        AnalysisClientException exception = await Assert.ThrowsAsync<AnalysisClientException>(() =>
            client.AnalyzeAsync(ProjectDocumentPresets.CreateRepresentativeFrame()));

        Assert.Equal(OperationFailureKind.Protocol, exception.FailureKind);
        Assert.Same(prior, state.Current);
        Assert.NotNull(exception.InnerException);
    }

    [Theory]
    [InlineData(nameof(FrameWebAnalysisClientOptions.MaxJsonDepth), 3)]
    [InlineData(nameof(FrameWebAnalysisClientOptions.MaxCaseCount), 1)]
    [InlineData(nameof(FrameWebAnalysisClientOptions.MaxResultCount), 1)]
    [InlineData(nameof(FrameWebAnalysisClientOptions.MaxResponseEntityCount), 1)]
    public async Task DepthCaseResultAndEntityLimits_AreEnforced(string property, int limit)
    {
        byte[] response = ReadPositiveBytes("multiple-static.json");
        StubHandler handler = new((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, response)));
        using HttpClient httpClient = CreateHttpClient(handler);
        FrameWebAnalysisClientOptions options = new();
        typeof(FrameWebAnalysisClientOptions).GetProperty(property)!.SetValue(options, limit);
        FrameWebAnalysisClient client = new(httpClient, options);

        AnalysisClientException exception = await Assert.ThrowsAsync<AnalysisClientException>(() =>
            client.AnalyzeAsync(ProjectDocumentPresets.CreateRepresentativeFrame()));

        Assert.Equal(OperationFailureKind.Protocol, exception.FailureKind);
    }

    [Fact]
    public async Task ConcurrencyLimit_IsAcquiredBeforeRequestWork()
    {
        byte[] response = ReadPositiveBytes("single-static.json");
        TaskCompletionSource firstEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseFirst = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource secondEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        StubHandler handler = new(async (_, token) =>
        {
            int call = Interlocked.Increment(ref calls);
            if (call == 1)
            {
                firstEntered.SetResult();
                await releaseFirst.Task.WaitAsync(token);
            }
            else
            {
                secondEntered.SetResult();
            }

            return JsonResponse(HttpStatusCode.OK, response);
        });
        using HttpClient httpClient = CreateHttpClient(handler);
        FrameWebAnalysisClient client = new(httpClient, new FrameWebAnalysisClientOptions
        {
            MaxConcurrentRequests = 1,
        });
        ProjectDocument document = ProjectDocumentPresets.CreateRepresentativeFrame();

        Task<AnalysisResultSet> first = client.AnalyzeAsync(document);
        await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Task<AnalysisResultSet> second = client.AnalyzeAsync(document);
        Assert.False(secondEntered.Task.IsCompleted);

        releaseFirst.SetResult();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(secondEntered.Task.IsCompleted);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Dispose_PreventsNewCallsButDoesNotDisposeInjectedHttpClient()
    {
        byte[] response = ReadPositiveBytes("single-static.json");
        StubHandler handler = new((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, response)));
        using HttpClient httpClient = CreateHttpClient(handler);
        FrameWebAnalysisClient client = new(httpClient);

        client.Dispose();
        client.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            client.AnalyzeAsync(ProjectDocumentPresets.CreateRepresentativeFrame()));
        using HttpResponseMessage directResponse = await httpClient.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, directResponse.StatusCode);
    }

    [Fact]
    public async Task Dispose_DuringAdmittedCalls_DefersSemaphoreDisposalUntilTheyComplete()
    {
        byte[] response = ReadPositiveBytes("single-static.json");
        TaskCompletionSource firstEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseFirst = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        StubHandler handler = new(async (_, token) =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                firstEntered.SetResult();
                await releaseFirst.Task.WaitAsync(token);
            }

            return JsonResponse(HttpStatusCode.OK, response);
        });
        using HttpClient httpClient = CreateHttpClient(handler);
        FrameWebAnalysisClient client = new(httpClient, new FrameWebAnalysisClientOptions
        {
            MaxConcurrentRequests = 1,
        });
        ProjectDocument document = ProjectDocumentPresets.CreateRepresentativeFrame();

        Task<AnalysisResultSet> running = client.AnalyzeAsync(document);
        await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Task<AnalysisResultSet> waiting = client.AnalyzeAsync(document);

        client.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.AnalyzeAsync(document));
        releaseFirst.SetResult();

        AnalysisResultSet[] results = await Task.WhenAll(running, waiting).WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(2, results.Length);
        Assert.Equal(2, calls);
    }

    public static IEnumerable<object[]> InvalidSuccessBodies()
    {
        yield return [new byte[] { 0x7b, 0x22, 0xff, 0x22, 0x7d }];
        yield return ["{"u8.ToArray()];
        yield return ["{\"kind\":\"analysis_result_set\",\"schema_version\":\"1.0\"}"u8.ToArray()];
        yield return ["{\"disg\":{},\"reac\":{},\"fsec\":{}}"u8.ToArray()];
    }

    private static HttpClient CreateHttpClient(HttpMessageHandler handler)
        => new(handler) { BaseAddress = new Uri("http://localhost:5000") };

    private static HttpResponseMessage JsonResponse(
        HttpStatusCode statusCode,
        byte[] body,
        string? charset = "utf-8")
    {
        ByteArrayContent content = new(body);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        if (charset is not null)
        {
            content.Headers.ContentType.CharSet = charset;
        }

        return new HttpResponseMessage(statusCode) { Content = content };
    }

    private static byte[] ReadPositiveBytes(string fileName)
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
        {
            current = current.Parent;
        }

        string root = current?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root.");
        return File.ReadAllBytes(Path.Combine(
            root,
            "FrameWeb",
            "tests",
            "data",
            "contracts",
            "positive",
            fileName));
    }

    private sealed class StubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => sendAsync(request, cancellationToken);
    }

    private sealed class UnknownLengthJsonContent(byte[] content) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => stream.WriteAsync(content).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        public UnknownLengthJsonContent WithJsonContentType()
        {
            Headers.ContentType = new MediaTypeHeaderValue("application/json")
            {
                CharSet = "utf-8",
            };
            return this;
        }
    }
}
