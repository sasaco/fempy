using FrameWebforCsharp.Resources;
using FrameWebforCsharp.Shell;
using FrameWebforCsharp.Shell.Composition;

namespace FrameWebforCsharp;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        LocalizationService localization = new();
        try
        {
            string repositoryRoot = FindRepositoryRoot();
            FrameWebDesktopRuntime runtime = new(repositoryRoot);
            DesktopApplicationSession.Run(
                runtime,
                _ => new MainForm(DesktopApplicationSession.CreateServices(runtime, localization)),
                Application.Run);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.WriteLine(exception);
            MessageBox.Show(
                localization["RuntimeStartError"],
                localization["ErrorTitle"],
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "FrameWeb", "pyproject.toml")))
        {
            current = current.Parent;
        }

        return current?.FullName
            ?? throw new DirectoryNotFoundException("The FrameWeb repository root could not be located.");
    }
}
