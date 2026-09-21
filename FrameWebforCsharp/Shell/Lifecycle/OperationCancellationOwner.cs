namespace PDF_Manager.Shell.Lifecycle;

/// <summary>
/// Owns cancellation sources for replaceable shell commands. Exceptions thrown by cancellation
/// callbacks are isolated from ownership cleanup and forwarded to the optional diagnostic sink.
/// </summary>
public sealed class OperationCancellationOwner : IDisposable
{
    private readonly object syncRoot = new();
    private readonly Action<Exception>? reportDiagnostic;
    private CancellationTokenSource? current;
    private Exception? lastDiagnosticFailure;
    private bool disposed;

    public OperationCancellationOwner(Action<Exception>? reportDiagnostic = null)
    {
        this.reportDiagnostic = reportDiagnostic;
    }

    public bool HasActiveOperation
    {
        get
        {
            lock (syncRoot)
            {
                return current is not null;
            }
        }
    }

    /// <summary>
    /// Gets the most recent cancellation or diagnostic-sink failure. This remains available after
    /// disposal so shutdown diagnostics do not depend on a live cancellation source.
    /// </summary>
    public Exception? LastDiagnosticFailure
    {
        get
        {
            lock (syncRoot)
            {
                return lastDiagnosticFailure;
            }
        }
    }

    public OperationCancellationLease Begin(CancellationToken cancellationToken = default)
    {
        CancellationTokenSource source;
        CancellationTokenSource? previous;
        lock (syncRoot)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            previous = current;
            current = source;
        }

        CancelAndReport(previous);
        return new OperationCancellationLease(this, source);
    }

    public bool CancelCurrent()
    {
        CancellationTokenSource? source;
        lock (syncRoot)
        {
            source = current;
        }

        return source is not null && CancelAndReport(source);
    }

    public void Dispose()
    {
        CancellationTokenSource? source;
        lock (syncRoot)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            source = current;
            current = null;
        }

        try
        {
            CancelAndReport(source);
        }
        finally
        {
            source?.Dispose();
        }
    }

    private void Release(CancellationTokenSource source)
    {
        lock (syncRoot)
        {
            if (ReferenceEquals(current, source))
            {
                current = null;
            }
        }

        source.Dispose();
    }

    private bool CancelAndReport(CancellationTokenSource? source)
    {
        if (source is null)
        {
            return false;
        }

        try
        {
            source.Cancel(throwOnFirstException: false);
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
        catch (Exception exception)
        {
            ReportDiagnostic(exception);
            return true;
        }
    }

    private void ReportDiagnostic(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        lock (syncRoot)
        {
            lastDiagnosticFailure = exception;
        }

        try
        {
            reportDiagnostic?.Invoke(exception);
        }
        catch (Exception reportingFailure)
        {
            lock (syncRoot)
            {
                lastDiagnosticFailure = new AggregateException(
                    "Cancellation and diagnostic reporting both failed.",
                    exception,
                    reportingFailure);
            }
        }
    }

    public sealed class OperationCancellationLease : IDisposable
    {
        private OperationCancellationOwner? owner;
        private CancellationTokenSource? source;
        private readonly CancellationToken token;

        internal OperationCancellationLease(OperationCancellationOwner owner, CancellationTokenSource source)
        {
            this.owner = owner;
            this.source = source;
            token = source.Token;
        }

        /// <summary>
        /// Remains observable after the lease or owner is disposed so callers can verify that an
        /// in-flight operation received cancellation without dereferencing a disposed source.
        /// </summary>
        public CancellationToken Token => token;

        public void Dispose()
        {
            OperationCancellationOwner? capturedOwner = Interlocked.Exchange(ref owner, null);
            CancellationTokenSource? capturedSource = Interlocked.Exchange(ref source, null);
            if (capturedOwner is not null && capturedSource is not null)
            {
                capturedOwner.Release(capturedSource);
            }
        }
    }
}
