using PDF_Manager.Shell;

namespace PDF_Manager.UiTests;

public sealed class MainFormSmokeTests
{
    [Fact]
    public void MainForm_OnStaThread_RepeatedlyShowsClosesAndDisposesWithoutLeavingOpenForms()
    {
        StaTestRunner.Run(() =>
        {
            for (int cycle = 0; cycle < 25; cycle++)
            {
                using MainForm form = new();
                form.Show();
                Assert.True(form.Visible);
                Assert.Contains(form, Application.OpenForms.Cast<Form>());

                form.Close();

                Assert.False(form.Visible);
                Assert.True(form.IsDisposed);
                Assert.Empty(Application.OpenForms.Cast<Form>());
            }
        }, "PDF Manager UI smoke");
    }
}
