using System.Diagnostics;
using PDF_Manager.Resources;
using PDF_Manager.Shell;

namespace PDF_Manager.UiTests;

public sealed class MainFormSmokeTests
{
    [Fact]
    public void MainForm_OnStaThread_RepeatedlyShowsClosesAndDisposesWithoutLeavingOpenForms()
    {
        StaTestRunner.Run(() =>
        {
            LocalizationService localization = new(UiLanguage.English);
            for (int cycle = 0; cycle < 10; cycle++)
            {
                MainFormServices services = new(
                    projectStore: new FakeProjectStore(),
                    localization: localization,
                    dialogs: new FakeShellDialogs());
                using MainForm form = new(services);
                form.Show();
                Assert.True(form.Visible);
                Assert.Contains(form, Application.OpenForms.Cast<Form>());

                form.Close();
                PumpUntil(() => form.WhenShellTransitionIdleAsync().IsCompleted && form.IsDisposed);

                Assert.False(form.Visible);
                Assert.True(form.IsDisposed);
                Assert.Empty(Application.OpenForms.Cast<Form>());

                UiLanguage nextLanguage = cycle % 2 == 0 ? UiLanguage.Japanese : UiLanguage.English;
                localization.SetLanguage(nextLanguage);
            }
        }, "PDF Manager UI smoke", TimeSpan.FromSeconds(30));
    }

    private static void PumpUntil(Func<bool> completed)
    {
        Stopwatch timeout = Stopwatch.StartNew();
        while (!completed())
        {
            Assert.True(timeout.Elapsed < TimeSpan.FromSeconds(5), "The close transition did not complete.");
            Application.DoEvents();
            Thread.Sleep(1);
        }
    }
}
