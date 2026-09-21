using PDF_Manager.Core.Abstractions;
using PDF_Manager.Core.Documents;
using PDF_Manager.Shell.Lifecycle;

namespace PDF_Manager.UiTests;

public sealed class CloseCancellationBoundaryTests
{
    [Fact]
    public async Task CleanDocument_ClosesWithoutConfirmationOrSave()
    {
        int confirmations = 0;
        int saves = 0;
        DirtyDocumentCloseGuard guard = new(
            _ =>
            {
                confirmations++;
                return DirtyDocumentCloseDecision.Cancel;
            },
            (_, _) =>
            {
                saves++;
                return Task.CompletedTask;
            });

        bool canClose = await guard.CanCloseAsync(ShellCommandStateTests.CreateDocument(isDirty: false));

        Assert.True(canClose);
        Assert.Equal(0, confirmations);
        Assert.Equal(0, saves);
    }

    [Theory]
    [InlineData(DirtyDocumentCloseDecision.Save, true, 1)]
    [InlineData(DirtyDocumentCloseDecision.Discard, true, 0)]
    [InlineData(DirtyDocumentCloseDecision.Cancel, false, 0)]
    public async Task DirtyDocument_DecisionControlsCloseAndSave(
        DirtyDocumentCloseDecision decision,
        bool expectedCanClose,
        int expectedSaves)
    {
        int confirmations = 0;
        int saves = 0;
        DirtyDocumentCloseGuard guard = new(
            _ =>
            {
                confirmations++;
                return decision;
            },
            (_, _) =>
            {
                saves++;
                return Task.CompletedTask;
            });

        bool canClose = await guard.CanCloseAsync(ShellCommandStateTests.CreateDocument(isDirty: true));

        Assert.Equal(expectedCanClose, canClose);
        Assert.Equal(1, confirmations);
        Assert.Equal(expectedSaves, saves);
    }

    [Fact]
    public void StartingReplacementOperation_CancelsPreviousLeaseWithoutLosingCurrentOwnership()
    {
        using OperationCancellationOwner owner = new();
        using OperationCancellationOwner.OperationCancellationLease first = owner.Begin();
        using OperationCancellationOwner.OperationCancellationLease second = owner.Begin();

        Assert.True(first.Token.IsCancellationRequested);
        Assert.False(second.Token.IsCancellationRequested);
        Assert.True(owner.HasActiveOperation);
        first.Dispose();
        Assert.True(owner.HasActiveOperation);
        Assert.True(owner.CancelCurrent());
        Assert.True(second.Token.IsCancellationRequested);
        second.Dispose();
        Assert.False(owner.HasActiveOperation);
        Assert.False(owner.CancelCurrent());
    }

    [Fact]
    public void DisposingCancellationOwner_CancelsCurrentOperationAndRejectsNewOnes()
    {
        OperationCancellationOwner owner = new();
        OperationCancellationOwner.OperationCancellationLease lease = owner.Begin();

        owner.Dispose();

        Assert.True(lease.Token.IsCancellationRequested);
        Assert.Throws<ObjectDisposedException>(() => owner.Begin());
        lease.Dispose();
    }

    [Fact]
    public void ExceptionBoundary_MapsTypedFailureAndKeepsOriginalForDiagnostics()
    {
        List<UserSafeExceptionInfo> notifications = [];
        List<Exception> diagnostics = [];
        UserExceptionBoundary boundary = new(notifications.Add, diagnostics.Add);
        AnalysisClientException failure = new(OperationFailureKind.Protocol, "The result contract is invalid.");

        bool succeeded = boundary.Execute(() => throw failure);

        Assert.False(succeeded);
        UserSafeExceptionInfo notification = Assert.Single(notifications);
        Assert.Equal(UserSafeFailureKind.Operation, notification.Kind);
        Assert.Equal("The result contract is invalid.", notification.Message);
        Assert.Same(failure, Assert.Single(diagnostics));
    }

    [Fact]
    public void ExceptionBoundary_HidesUnexpectedDetailsFromUser()
    {
        UserSafeExceptionInfo? notification = null;
        InvalidOperationException failure = new("secret implementation detail");
        UserExceptionBoundary boundary = new(info => notification = info);

        Assert.False(boundary.Execute(() => throw failure));

        Assert.NotNull(notification);
        Assert.Equal(UserSafeFailureKind.Unexpected, notification.Kind);
        Assert.DoesNotContain("secret", notification.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExceptionBoundary_TreatsCancellationAsNormalControlFlow()
    {
        int notifications = 0;
        int diagnostics = 0;
        UserExceptionBoundary boundary = new(_ => notifications++, _ => diagnostics++);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        bool sync = boundary.Execute(
            () => throw new OperationCanceledException(cancellation.Token),
            cancellation.Token);
        bool asyncResult = await boundary.ExecuteAsync(
            token => Task.FromCanceled(token),
            cancellation.Token);

        Assert.False(sync);
        Assert.False(asyncResult);
        Assert.Equal(0, notifications);
        Assert.Equal(0, diagnostics);
    }
}
