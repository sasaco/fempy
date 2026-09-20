using PDF_Manager.Shell;

namespace PDF_Manager.UiTests;

public sealed class MainFormSmokeTests
{
    [Fact]
    public void MainForm_OnStaThread_CanShowCloseAndDisposeWithoutLeavingOpenForms()
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                Assert.Equal(ApartmentState.STA, Thread.CurrentThread.GetApartmentState());
                Assert.Empty(Application.OpenForms.Cast<Form>());

                using MainForm form = new();
                form.Show();
                Assert.True(form.Visible);
                Assert.Contains(form, Application.OpenForms.Cast<Form>());

                form.Close();

                Assert.False(form.Visible);
                Assert.Empty(Application.OpenForms.Cast<Form>());
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        })
        {
            IsBackground = true,
            Name = "PDF Manager UI smoke",
        };
        thread.SetApartmentState(ApartmentState.STA);

        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "The STA UI smoke thread did not terminate.");
        Assert.Null(failure);
    }
}
