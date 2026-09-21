using System.Collections.Concurrent;
using PDF_Manager.Core.Shell;
using PDF_Manager.Shell.Lifecycle;

namespace PDF_Manager.UiTests;

public sealed class Step3LifecycleRemediationTests
{
    private static readonly DocumentKey Alpha = DocumentKey.Document("lifecycle:alpha");
    private static readonly DocumentKey Beta = DocumentKey.Document("lifecycle:beta");

    [Fact]
    public void CancellationOwner_ReplacementCompletesEveryCallbackAndKeepsNewOwnershipUsable()
    {
        List<Exception> diagnostics = [];
        using OperationCancellationOwner owner = new(diagnostics.Add);
        using OperationCancellationOwner.OperationCancellationLease first = owner.Begin();
        InvalidOperationException callbackFailure = new("replacement callback failed");
        int callbacks = 0;
        using CancellationTokenRegistration success = first.Token.Register(() => callbacks++);
        using CancellationTokenRegistration throwing = first.Token.Register(() =>
        {
            callbacks++;
            throw callbackFailure;
        });

        using OperationCancellationOwner.OperationCancellationLease replacement = owner.Begin();

        Assert.Equal(2, callbacks);
        Assert.True(first.Token.IsCancellationRequested);
        Assert.False(replacement.Token.IsCancellationRequested);
        Assert.True(owner.HasActiveOperation);
        AssertContainsFailure(owner.LastDiagnosticFailure, callbackFailure);
        Assert.Same(owner.LastDiagnosticFailure, Assert.Single(diagnostics));
        first.Dispose();
        Assert.True(owner.HasActiveOperation);
        replacement.Dispose();
        Assert.False(owner.HasActiveOperation);
    }

    [Fact]
    public void CancellationOwner_ExplicitCancelCompletesEveryCallbackAndLeaseCanStillReleaseOwnership()
    {
        List<Exception> diagnostics = [];
        using OperationCancellationOwner owner = new(diagnostics.Add);
        using OperationCancellationOwner.OperationCancellationLease lease = owner.Begin();
        InvalidOperationException callbackFailure = new("explicit cancellation callback failed");
        int callbacks = 0;
        using CancellationTokenRegistration throwing = lease.Token.Register(() =>
        {
            callbacks++;
            throw callbackFailure;
        });
        using CancellationTokenRegistration success = lease.Token.Register(() => callbacks++);

        Assert.True(owner.CancelCurrent());

        Assert.Equal(2, callbacks);
        Assert.True(lease.Token.IsCancellationRequested);
        Assert.True(owner.HasActiveOperation);
        AssertContainsFailure(owner.LastDiagnosticFailure, callbackFailure);
        Assert.Same(owner.LastDiagnosticFailure, Assert.Single(diagnostics));
        lease.Dispose();
        Assert.False(owner.HasActiveOperation);
        Assert.False(owner.CancelCurrent());
    }

    [Fact]
    public void CancellationOwner_DisposeCompletesEveryCallbackReportsFailureAndRejectsNewOwnership()
    {
        List<Exception> diagnostics = [];
        OperationCancellationOwner owner = new(diagnostics.Add);
        OperationCancellationOwner.OperationCancellationLease lease = owner.Begin();
        InvalidOperationException callbackFailure = new("dispose callback failed");
        int callbacks = 0;
        using CancellationTokenRegistration success = lease.Token.Register(() => callbacks++);
        using CancellationTokenRegistration throwing = lease.Token.Register(() =>
        {
            callbacks++;
            throw callbackFailure;
        });

        owner.Dispose();

        Assert.Equal(2, callbacks);
        Assert.True(lease.Token.IsCancellationRequested);
        Assert.False(owner.HasActiveOperation);
        AssertContainsFailure(owner.LastDiagnosticFailure, callbackFailure);
        Assert.Same(owner.LastDiagnosticFailure, Assert.Single(diagnostics));
        Assert.Throws<ObjectDisposedException>(() => owner.Begin());
        lease.Dispose();
        owner.Dispose();
    }

    [Fact]
    public async Task ActivationObserver_ConcurrentDeliveryIsRevisionOrderedAndLatestRequestWins()
    {
        TaskCompletionSource firstObserverEntered = NewSignal();
        TaskCompletionSource releaseFirstObserver = NewSignal();
        ConcurrentQueue<ActivationState> observations = new();
        await using ActivationCoordinator coordinator = new(
            static (_, _) => Task.CompletedTask,
            state =>
            {
                if (state.Revision == 1 && state.Phase == ActivationPhase.Pending)
                {
                    firstObserverEntered.TrySetResult();
                    releaseFirstObserver.Task.GetAwaiter().GetResult();
                }

                observations.Enqueue(state);
            });

        Task<Task> firstCall = Task.Factory.StartNew(
            () => coordinator.ActivateAsync(Alpha),
            CancellationToken.None,
            TaskCreationOptions.DenyChildAttach,
            TaskScheduler.Default);
        await firstObserverEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task<Task> secondCall = Task.Factory.StartNew(
            () => coordinator.ActivateAsync(Beta),
            CancellationToken.None,
            TaskCreationOptions.DenyChildAttach,
            TaskScheduler.Default);
        Assert.True(
            SpinWait.SpinUntil(() => coordinator.State.Revision >= 2, TimeSpan.FromSeconds(5)),
            "The concurrent activation request was not accepted while the first observer was blocked.");
        releaseFirstObserver.TrySetResult();

        Task first = await firstCall.WaitAsync(TimeSpan.FromSeconds(5));
        Task second = await secondCall.WaitAsync(TimeSpan.FromSeconds(5));
        await IgnoreExpectedCancellationAsync(first);
        await second.WaitAsync(TimeSpan.FromSeconds(5));
        await coordinator.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(5));

        ActivationState[] delivered = observations.ToArray();
        Assert.NotEmpty(delivered);
        Assert.Equal(
            delivered.Select(static state => state.Revision).OrderBy(static revision => revision),
            delivered.Select(static state => state.Revision));
        Assert.Equal(Beta, coordinator.State.ActiveKey);
        Assert.Equal(ActivationPhase.Active, coordinator.State.Phase);
    }

    [Fact]
    public async Task ActivationObserver_FailureIsObservableAndDoesNotStrandLaterRequests()
    {
        InvalidOperationException observerFailure = new("observer failed");
        List<DocumentKey> effects = [];
        List<Exception> diagnostics = [];
        int observerCalls = 0;
        await using ActivationCoordinator coordinator = new(
            (key, _) =>
            {
                effects.Add(key);
                return Task.CompletedTask;
            },
            _ =>
            {
                if (Interlocked.Increment(ref observerCalls) == 1)
                {
                    throw observerFailure;
                }
            },
            diagnostics.Add);

        await coordinator.ActivateAsync(Alpha).WaitAsync(TimeSpan.FromSeconds(5));
        await coordinator.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(5));
        await coordinator.ActivateAsync(Beta).WaitAsync(TimeSpan.FromSeconds(5));
        await coordinator.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(new[] { Alpha, Beta }, effects);
        Assert.Equal(Beta, coordinator.State.ActiveKey);
        Assert.Same(observerFailure, coordinator.LastDiagnosticFailure);
        Assert.Same(observerFailure, Assert.Single(diagnostics));
    }

    [Fact]
    public async Task ActivationCoordinator_ClearActivePublishesNullWithoutStrandingFutureActivation()
    {
        await using ActivationCoordinator coordinator = new(static (_, _) => Task.CompletedTask);
        await coordinator.ActivateAsync(Alpha).WaitAsync(TimeSpan.FromSeconds(5));

        coordinator.ClearActive();

        Assert.Null(coordinator.State.ActiveKey);
        await coordinator.ActivateAsync(Beta).WaitAsync(TimeSpan.FromSeconds(5));
        await coordinator.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(Beta, coordinator.State.ActiveKey);
    }

    private static async Task IgnoreExpectedCancellationAsync(Task task)
    {
        try
        {
            await task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static void AssertContainsFailure(Exception? reported, Exception expected)
    {
        AggregateException aggregate = Assert.IsType<AggregateException>(reported);
        Assert.Contains(expected, aggregate.Flatten().InnerExceptions);
    }

    private static TaskCompletionSource NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
