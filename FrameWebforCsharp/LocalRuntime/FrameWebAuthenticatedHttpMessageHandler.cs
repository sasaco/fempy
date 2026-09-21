namespace FrameWeb.LocalRuntime;

internal sealed class FrameWebAuthenticatedHttpMessageHandler : DelegatingHandler
{
    private readonly FrameWebLocalRuntime _runtime;

    public FrameWebAuthenticatedHttpMessageHandler(FrameWebLocalRuntime runtime)
        : base(runtime.CreateOwnedHttpHandler())
    {
        _runtime = runtime;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        HttpRequestMessage authenticatedRequest = CloneRequest(request);
        try
        {
            _runtime.AuthorizeRequest(authenticatedRequest);
            HttpResponseMessage response = await base.SendAsync(authenticatedRequest, cancellationToken)
                .ConfigureAwait(false);
            _ = authenticatedRequest.Headers.Remove(FrameWebLocalAuthentication.HeaderName);
            return response;
        }
        catch
        {
            _ = authenticatedRequest.Headers.Remove(FrameWebLocalAuthentication.HeaderName);
            throw;
        }
        finally
        {
            authenticatedRequest.Content = null;
            authenticatedRequest.Dispose();
        }
    }

    private static HttpRequestMessage CloneRequest(HttpRequestMessage source)
    {
        HttpRequestMessage clone = new(source.Method, source.RequestUri)
        {
            Content = source.Content,
            Version = source.Version,
            VersionPolicy = source.VersionPolicy,
        };
        foreach (KeyValuePair<string, IEnumerable<string>> header in source.Headers)
        {
            if (!string.Equals(header.Key, FrameWebLocalAuthentication.HeaderName, StringComparison.OrdinalIgnoreCase))
            {
                _ = clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        foreach (KeyValuePair<string, object?> option in source.Options)
        {
            clone.Options.Set(new HttpRequestOptionsKey<object?>(option.Key), option.Value);
        }

        return clone;
    }
}
