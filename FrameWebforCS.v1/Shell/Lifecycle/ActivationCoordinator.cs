using FrameWebforCS.Core.Shell;

namespace FrameWebforCS.Shell.Lifecycle;

/// <summary>
/// Runs at most one activation effect at a time. A newer request cancels the running effect,
/// replaces any request that has not started, and is the only request allowed to publish state.
/// Observer and cancellation-callback failures are isolated and sent to the diagnostic sink.
/// </summary>
public sealed class ActivationCoordinator : IDisposable, IAsyncDisposable
{
    private readonly object syncRoot = new();
    private readonly object observerSyncRoot = new();
    private readonly Func<DocumentKey, CancellationToken, Task> activationEffect;
    private readonly Action<ActivationState>? stateObserver;
    private readonly Action<Exception>? reportDiagnostic;
    private ActivationState state = ActivationState.Empty;
    private ActivationRequest? pending;
    private ActivationRequest? running;
    private CancellationTokenSource? runningCancellation;
    private Task? pumpTask;
    private Exception? lastDiagnosticFailure;
    private long notificationSequence;
    private long lastDeliveredSequence;
    private bool disposed;

    public ActivationCoordinator(
        Func<DocumentKey, CancellationToken, Task> activationEffect,
        Action<ActivationState>? stateObserver = null,
        Action<Exception>? reportDiagnostic = null)
    {
        this.activationEffect = activationEffect ?? throw new ArgumentNullException(nameof(activationEffect));
        this.stateObserver = stateObserver;
        this.reportDiagnostic = reportDiagnostic;
    }

    public ActivationState State
    {
        get
        {
            lock (syncRoot)
            {
                return state;
            }
        }
    }

    /// <summary>Gets the most recent isolated observer, cancellation, or reporting failure.</summary>
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

    public Task ActivateAsync(DocumentKey key, CancellationToken cancellationToken = default)
    {
        key.Validate();
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        ActivationRequest request;
        CancellationTokenSource? cancelRunning;
        StateNotification notification;
        lock (syncRoot)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            state = ActivationReducer.Request(state, key);
            notification = CreateNotificationLocked();
            request = new ActivationRequest(state.Revision, key, cancellationToken);

            pending?.CancelAsSuperseded();
            pending = request;
            cancelRunning = runningCancellation;
            EnsurePumpLocked();
        }

        CancelAndReport(cancelRunning);
        Publish(notification);
        return request.Completion.Task;
    }

    /// <summary>Returns a task representing all activation work that was queued when called.</summary>
    public Task WhenIdleAsync()
    {
        lock (syncRoot)
        {
            return pumpTask ?? Task.CompletedTask;
        }
    }

    public void Cancel()
    {
        ActivationRequest? cancelPending = null;
        CancellationTokenSource? cancelRunning;
        StateNotification? notification = null;
        lock (syncRoot)
        {
            if (pending is not null)
            {
                cancelPending = pending;
                pending = null;
                ActivationState nextState = ActivationReducer.Cancel(
                    state,
                    cancelPending.Revision,
                    cancelPending.Key);
                if (nextState != state)
                {
                    state = nextState;
                    notification = CreateNotificationLocked();
                }
            }

            cancelRunning = runningCancellation;
        }

        cancelPending?.Cancel();
        CancelAndReport(cancelRunning);
        if (notification is not null)
        {
            Publish(notification);
        }
    }

    /// <summary>
    /// Cancels all queued/running activation and invalidates it before clearing the active key.
    /// Any later completion from the cancelled revision is ignored by the reducer.
    /// </summary>
    public void ClearActive()
    {
        ActivationRequest? cancelPending;
        CancellationTokenSource? cancelRunning;
        StateNotification notification;
        lock (syncRoot)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            cancelPending = pending;
            pending = null;
            cancelRunning = runningCancellation;
            state = ActivationReducer.Clear(state);
            notification = CreateNotificationLocked();
        }

        cancelPending?.Cancel();
        CancelAndReport(cancelRunning);
        Publish(notification);
    }

    public void Dispose()
    {
        DisposeCore();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        Task outstanding = DisposeCore();
        await outstanding.ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    private async Task PumpAsync()
    {
        // Prevent a synchronously completing effect from clearing pump ownership before the
        // assignment performed by EnsurePumpLocked completes.
        await Task.Yield();
        try
        {
            while (TryStartNext(out ActivationRequest? request, out CancellationTokenSource? cancellation))
            {
                await RunRequestAsync(request!, cancellation!).ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            ReportDiagnostic(exception);
        }
        finally
        {
            lock (syncRoot)
            {
                pumpTask = null;
                if (!disposed && pending is not null)
                {
                    EnsurePumpLocked();
                }
            }
        }
    }

    private bool TryStartNext(
        out ActivationRequest? request,
        out CancellationTokenSource? cancellation)
    {
        lock (syncRoot)
        {
            if (pending is null)
            {
                request = null;
                cancellation = null;
                return false;
            }

            request = pending;
            pending = null;
            cancellation = CancellationTokenSource.CreateLinkedTokenSource(request.ExternalCancellation);
            running = request;
            runningCancellation = cancellation;
            return true;
        }
    }

    private async Task RunRequestAsync(
        ActivationRequest request,
        CancellationTokenSource cancellation)
    {
        StateNotification? notification = null;
        Exception? failure = null;
        bool cancelled = false;
        try
        {
            cancellation.Token.ThrowIfCancellationRequested();
            await activationEffect(request.Key, cancellation.Token).ConfigureAwait(false);
            cancelled = cancellation.IsCancellationRequested;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            cancelled = true;
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            lock (syncRoot)
            {
                if (ReferenceEquals(running, request))
                {
                    running = null;
                    runningCancellation = null;
                }

                ActivationState nextState;
                if (disposed || cancelled || cancellation.IsCancellationRequested)
                {
                    cancelled = true;
                    nextState = ActivationReducer.Cancel(state, request.Revision, request.Key);
                }
                else if (failure is not null)
                {
                    nextState = ActivationReducer.Fail(state, request.Revision, request.Key, failure);
                }
                else
                {
                    nextState = ActivationReducer.Complete(state, request.Revision, request.Key);
                }

                if (nextState != state)
                {
                    state = nextState;
                    notification = CreateNotificationLocked();
                }
            }

            cancellation.Dispose();
        }

        if (notification is not null && !disposed)
        {
            Publish(notification);
        }

        if (cancelled || disposed)
        {
            request.Cancel();
        }
        else if (failure is not null)
        {
            request.Completion.TrySetException(failure);
        }
        else
        {
            request.Completion.TrySetResult();
        }
    }

    private Task DisposeCore()
    {
        Task outstanding;
        ActivationRequest? cancelPending = null;
        CancellationTokenSource? cancelRunning;
        lock (syncRoot)
        {
            if (!disposed)
            {
                disposed = true;
                cancelPending = pending;
                pending = null;
            }

            cancelRunning = runningCancellation;
            outstanding = pumpTask ?? Task.CompletedTask;
        }

        cancelPending?.Cancel();
        CancelAndReport(cancelRunning);
        return outstanding;
    }

    private StateNotification CreateNotificationLocked() => new(++notificationSequence, state);

    private void Publish(StateNotification notification)
    {
        if (stateObserver is null)
        {
            return;
        }

        lock (observerSyncRoot)
        {
            lock (syncRoot)
            {
                if (disposed ||
                    notification.Sequence <= lastDeliveredSequence ||
                    notification.State.Revision < state.Revision)
                {
                    lastDeliveredSequence = Math.Max(lastDeliveredSequence, notification.Sequence);
                    return;
                }

                lastDeliveredSequence = notification.Sequence;
            }

            try
            {
                stateObserver(notification.State);
            }
            catch (Exception exception)
            {
                ReportDiagnostic(exception);
            }
        }
    }

    private void EnsurePumpLocked() => pumpTask ??= PumpAsync();

    private bool CancelAndReport(CancellationTokenSource? cancellation)
    {
        if (cancellation is null)
        {
            return false;
        }

        try
        {
            cancellation.Cancel(throwOnFirstException: false);
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
                    "Lifecycle work and diagnostic reporting both failed.",
                    exception,
                    reportingFailure);
            }
        }
    }

    private sealed record StateNotification(long Sequence, ActivationState State);

    private sealed class ActivationRequest(
        long revision,
        DocumentKey key,
        CancellationToken externalCancellation)
    {
        internal long Revision { get; } = revision;

        internal DocumentKey Key { get; } = key;

        internal CancellationToken ExternalCancellation { get; } = externalCancellation;

        internal TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal void Cancel() => Completion.TrySetCanceled(
            ExternalCancellation.IsCancellationRequested ? ExternalCancellation : new CancellationToken(canceled: true));

        internal void CancelAsSuperseded() => Completion.TrySetCanceled(new CancellationToken(canceled: true));
    }
}
