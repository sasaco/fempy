using FrameWeb.LocalRuntime;
using FrameWebforCS.Core.Analysis;
using FrameWebforCS.Resources;
using FrameWebforCS.Shell.Printing;

namespace FrameWebforCS.Shell.Composition;

public interface IDesktopRuntime : IAsyncDisposable
{
    Uri Endpoint { get; }

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);

    HttpClient CreateHttpClient();
}

public sealed class FrameWebDesktopRuntime : IDesktopRuntime
{
    private readonly FrameWebLocalRuntime _runtime;

    public FrameWebDesktopRuntime(string repositoryRoot)
    {
        FrameWebRuntimeOptions options = new(repositoryRoot);
        Endpoint = options.EffectiveReadinessUri;
        _runtime = new FrameWebLocalRuntime(options);
        _runtime.OutputReceived += OnOutputReceived;
    }

    public Uri Endpoint { get; }

    public Task StartAsync(CancellationToken cancellationToken = default) =>
        _runtime.StartAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken = default) =>
        _runtime.StopAsync(cancellationToken);

    public HttpClient CreateHttpClient() => _runtime.CreateHttpClient();

    public async ValueTask DisposeAsync()
    {
        _runtime.OutputReceived -= OnOutputReceived;
        await _runtime.DisposeAsync().ConfigureAwait(false);
    }

    private static void OnOutputReceived(object? sender, FrameWebRuntimeOutputEventArgs eventArgs) =>
        System.Diagnostics.Trace.WriteLine($"FrameWeb [{eventArgs.Stream}]: {eventArgs.Line}");
}

public static class DesktopApplicationSession
{
    public static MainFormServices CreateServices(IDesktopRuntime runtime, LocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(localization);
        HttpClient client = runtime.CreateHttpClient();
        try
        {
            FrameWebAnalysisClient analysisClient = new(client);
            return new MainFormServices(
                analysisClient: analysisClient,
                printExporter: new DesktopPdfExporter(),
                localization: localization,
                reportDiagnostic: exception => System.Diagnostics.Trace.WriteLine(exception),
                ownedResource: new AnalysisClientOwner(analysisClient, client));
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    public static void Run(
        IDesktopRuntime runtime,
        Func<Uri, MainForm> createForm,
        Action<MainForm> runForm,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(createForm);
        ArgumentNullException.ThrowIfNull(runForm);
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
        {
            throw new InvalidOperationException("The desktop application session must run on an STA thread.");
        }

        try
        {
            runtime.StartAsync(cancellationToken).GetAwaiter().GetResult();
            using MainForm form = createForm(runtime.Endpoint);
            runForm(form);
        }
        finally
        {
            try
            {
                runtime.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
            }
            finally
            {
                runtime.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }
    }

    private sealed class AnalysisClientOwner(
        FrameWebAnalysisClient analysisClient,
        HttpClient httpClient) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            analysisClient.Dispose();
            httpClient.Dispose();
        }
    }
}
