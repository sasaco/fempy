using System.Diagnostics;
using System.Security.Cryptography;
using PDF_Manager.Core.Documents;
using PDF_Manager.Printing;
using PDF_Manager.Resources;
using PDF_Manager.Shell;
using PDF_Manager.Shell.Contents;
using PDF_Manager.Shell.Printing;
using WeifenLuo.WinFormsUI.Docking;

namespace PDF_Manager.LiveCaptureProbe;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        ApplicationConfiguration.Initialize();
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
        {
            return 2;
        }

        try
        {
            using VS2015LightTheme theme = new();
            using DockPanel dockPanel = new() { Dock = DockStyle.Fill, Theme = theme };
            using Form host = new() { ClientSize = new Size(800, 600), ShowInTaskbar = false };
            using ProjectDocumentContent documentHost = new(
                MainForm.WorkspaceDocumentKey,
                new LocalizationService(UiLanguage.English));
            host.Controls.Add(dockPanel);
            documentHost.SetDocument(ProjectDocumentPresets.CreateRepresentativeFrame());
            documentHost.Show(dockPanel, WeifenLuo.WinFormsUI.Docking.DockState.Document);
            host.Show();
            Application.DoEvents();
            documentHost.Fit();
            Stopwatch rendered = Stopwatch.StartNew();
            while (rendered.Elapsed < TimeSpan.FromSeconds(1))
            {
                Application.DoEvents();
                Thread.Sleep(1);
            }

            ViewportCapture capture = new LiveViewportCaptureProvider().Capture(documentHost);
            string hash = Convert.ToHexString(SHA256.HashData(capture.Rgb24.Span));
            Console.WriteLine($"LIVE_CAPTURE_OK {capture.Width} {capture.Height} {hash}");
            host.Hide();
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}
