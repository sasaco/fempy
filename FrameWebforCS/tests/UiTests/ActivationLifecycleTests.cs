using PDF_Manager.Core.Shell;
using PDF_Manager.Shell.Lifecycle;

namespace PDF_Manager.UiTests;

public sealed class ActivationLifecycleTests
{
    private static readonly DocumentKey Alpha = DocumentKey.Document("project:alpha");
    private static readonly DocumentKey Beta = DocumentKey.Document("project:beta");
    private static readonly DocumentKey Gamma = DocumentKey.Document("project:gamma");

    [Fact]
    public void ActivationReducer_IsPureAndIgnoresObsoleteCompletions()
    {
        ActivationState initial = ActivationState.Empty;
        ActivationState alphaPending = ActivationReducer.Request(initial, Alpha);
        ActivationState betaPending = ActivationReducer.Request(alphaPending, Beta);
        InvalidOperationException obsoleteFailure = new("obsolete");

        Assert.Equal(ActivationState.Empty, initial);
        Assert.Equal(1, alphaPending.Revision);
        Assert.Equal(Alpha, alphaPending.RequestedKey);
        Assert.Equal(ActivationPhase.Pending, alphaPending.Phase);
        Assert.Equal(alphaPending, ActivationReducer.Complete(alphaPending, 0, Alpha));
        Assert.Equal(betaPending, ActivationReducer.Complete(betaPending, alphaPending.Revision, Alpha));
        Assert.Equal(betaPending, ActivationReducer.Cancel(betaPending, alphaPending.Revision, Alpha));
        Assert.Equal(betaPending, ActivationReducer.Fail(betaPending, alphaPending.Revision, Alpha, obsoleteFailure));

        ActivationState completed = ActivationReducer.Complete(betaPending, betaPending.Revision, Beta);
        Assert.Equal(Beta, completed.ActiveKey);
        Assert.Null(completed.RequestedKey);
        Assert.Equal(ActivationPhase.Active, completed.Phase);
        Assert.Null(completed.LastError);
        Assert.Equal(Beta, betaPending.RequestedKey);
        Assert.Equal(ActivationPhase.Pending, betaPending.Phase);
    }

    [Fact]
    public void ActivationReducer_CurrentCancellationAndFailureAreExplicit()
    {
        ActivationState pending = ActivationReducer.Request(ActivationState.Empty, Alpha);
        ActivationState cancelled = ActivationReducer.Cancel(pending, pending.Revision, Alpha);
        InvalidOperationException failure = new("activation failed");
        ActivationState faultedPending = ActivationReducer.Request(cancelled, Beta);
        ActivationState faulted = ActivationReducer.Fail(
            faultedPending,
            faultedPending.Revision,
            Beta,
            failure);

        Assert.Equal(ActivationPhase.Cancelled, cancelled.Phase);
        Assert.Null(cancelled.RequestedKey);
        Assert.Equal(ActivationPhase.Faulted, faulted.Phase);
        Assert.Same(failure, faulted.LastError);
        Assert.Null(faulted.RequestedKey);
    }

    [Fact]
    public async Task ActivationCoordinator_LatestRequestWinsAndEffectsStaySerialized()
    {
        TaskCompletionSource firstStarted = NewSignal();
        List<DocumentKey> started = [];
        int concurrent = 0;
        int maximumConcurrent = 0;
        bool firstObservedCancellation = false;

        await using ActivationCoordinator coordinator = new(async (key, token) =>
        {
            started.Add(key);
            int nowConcurrent = Interlocked.Increment(ref concurrent);
            maximumConcurrent = Math.Max(maximumConcurrent, nowConcurrent);
            try
            {
                if (key == Alpha)
                {
                    firstStarted.TrySetResult();
                    try
                    {
                        await Task.Delay(Timeout.InfiniteTimeSpan, token);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        firstObservedCancellation = true;
                        throw;
                    }
                }
            }
            finally
            {
                Interlocked.Decrement(ref concurrent);
            }
        });

        Task first = coordinator.ActivateAsync(Alpha);
        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task latest = coordinator.ActivateAsync(Beta);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        await latest.WaitAsync(TimeSpan.FromSeconds(5));
        await coordinator.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(firstObservedCancellation);
        Assert.Equal(1, maximumConcurrent);
        Assert.Equal(new[] { Alpha, Beta }, started);
        Assert.Equal(Beta, coordinator.State.ActiveKey);
        Assert.Equal(ActivationPhase.Active, coordinator.State.Phase);
    }

    [Fact]
    public async Task ActivationCoordinator_CoalescesPendingRequestsBeforeTheyStart()
    {
        TaskCompletionSource firstStarted = NewSignal();
        List<DocumentKey> started = [];
        await using ActivationCoordinator coordinator = new(async (key, token) =>
        {
            started.Add(key);
            if (key == Alpha)
            {
                firstStarted.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
        });

        Task first = coordinator.ActivateAsync(Alpha);
        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task superseded = coordinator.ActivateAsync(Beta);
        Task latest = coordinator.ActivateAsync(Gamma);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => superseded);
        await latest.WaitAsync(TimeSpan.FromSeconds(5));
        await coordinator.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(new[] { Alpha, Gamma }, started);
        Assert.Equal(Gamma, coordinator.State.ActiveKey);
    }

    [Fact]
    public async Task ActivationCoordinator_CancelStopsRunningRequestWithoutPublishingIt()
    {
        TaskCompletionSource started = NewSignal();
        await using ActivationCoordinator coordinator = new(async (_, token) =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        });

        Task activation = coordinator.ActivateAsync(Alpha);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        coordinator.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => activation);
        await coordinator.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Null(coordinator.State.ActiveKey);
        Assert.Equal(ActivationPhase.Cancelled, coordinator.State.Phase);
    }

    [Fact]
    public async Task ActivationCoordinator_SynchronouslyCompletingEffect_DoesNotStrandRepeatedRequests()
    {
        List<DocumentKey> effects = [];
        await using ActivationCoordinator coordinator = new((key, _) =>
        {
            effects.Add(key);
            return Task.CompletedTask;
        });

        List<DocumentKey> expected = [];
        for (int cycle = 0; cycle < 100; cycle++)
        {
            DocumentKey key = cycle % 2 == 0 ? Alpha : Beta;
            expected.Add(key);
            await coordinator.ActivateAsync(key).WaitAsync(TimeSpan.FromSeconds(5));
            await coordinator.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }

        Assert.Equal(expected, effects);
        Assert.Equal(expected[^1], coordinator.State.ActiveKey);
        Assert.Equal(ActivationPhase.Active, coordinator.State.Phase);
        Assert.Equal(100, coordinator.State.Revision);
    }

    private static TaskCompletionSource NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
