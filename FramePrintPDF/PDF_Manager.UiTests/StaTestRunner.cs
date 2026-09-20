using System.Runtime.ExceptionServices;

namespace PDF_Manager.UiTests;

internal static class StaTestRunner
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    public static void Run(Action action, string threadName)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(threadName);

        Exception? failure = null;
        Thread thread = new(() => Execute(action, exception => failure = exception))
        {
            IsBackground = true,
            Name = threadName,
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(
            thread.Join(DefaultTimeout),
            $"The STA test thread '{threadName}' did not terminate within {DefaultTimeout}.");
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private static void Execute(Action action, Action<Exception> captureFailure)
    {
        try
        {
            Assert.Equal(ApartmentState.STA, Thread.CurrentThread.GetApartmentState());
            Assert.Empty(Application.OpenForms.Cast<Form>());

            action();

            Assert.Empty(Application.OpenForms.Cast<Form>());
        }
        catch (Exception exception)
        {
            captureFailure(exception);
        }
        finally
        {
            foreach (Form form in Application.OpenForms.Cast<Form>().ToArray())
            {
                form.Close();
                form.Dispose();
            }
        }
    }
}
