using PDF_Manager.Printing;
using PDF_Manager.Shell.Contents;

namespace PDF_Manager.Shell.Printing;

public interface IViewportCaptureProvider
{
    ViewportCapture Capture(ProjectDocumentContent documentHost);
}

public sealed class LiveViewportCaptureProvider : IViewportCaptureProvider
{
    public ViewportCapture Capture(ProjectDocumentContent documentHost)
    {
        ArgumentNullException.ThrowIfNull(documentHost);
        if (documentHost.InvokeRequired)
        {
            throw new InvalidOperationException("The live viewport must be captured on its owning UI thread.");
        }

        using Bitmap bitmap = documentHost.CaptureViewport();
        return DesktopPdfExporter.CaptureBitmap(bitmap);
    }
}
