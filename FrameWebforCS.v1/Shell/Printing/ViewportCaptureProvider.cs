using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;
using FrameWebforCS.Printing;
using FrameWebforCS.Rendering.Scene;
using FrameWebforCS.Shell.Contents;
using FrameWebforCS.Shell.Viewport;

namespace FrameWebforCS.Shell.Printing;

public sealed class PrintDiagramCaptureSet
{
    private const int MaximumCaptureCount = 3;
    private readonly IReadOnlyDictionary<PrintDiagramKind, ViewportCapture> _captures;

    public PrintDiagramCaptureSet(
        IEnumerable<KeyValuePair<PrintDiagramKind, ViewportCapture>> captures)
    {
        ArgumentNullException.ThrowIfNull(captures);
        Dictionary<PrintDiagramKind, ViewportCapture> values = [];
        long decodedBytes = 0;
        foreach ((PrintDiagramKind kind, ViewportCapture capture) in captures)
        {
            if (kind is not (PrintDiagramKind.Model or PrintDiagramKind.Load or PrintDiagramKind.Result))
            {
                throw new ArgumentOutOfRangeException(nameof(captures), kind, "Unsupported desktop diagram kind.");
            }

            ArgumentNullException.ThrowIfNull(capture);
            if (values.Count == MaximumCaptureCount || !values.TryAdd(kind, capture))
            {
                throw new ArgumentException("Desktop diagram captures must contain unique semantic kinds.", nameof(captures));
            }

            decodedBytes = checked(decodedBytes + capture.Rgb24.Length);
            if (decodedBytes > PrintEngineLimits.MaximumDecodedImageBytes)
            {
                throw new ArgumentException("Desktop diagram captures exceed the decoded-image limit.", nameof(captures));
            }
        }

        _captures = new ReadOnlyDictionary<PrintDiagramKind, ViewportCapture>(values);
    }

    public static PrintDiagramCaptureSet Empty { get; } = new([]);

    public IReadOnlyDictionary<PrintDiagramKind, ViewportCapture> Captures => _captures;

    public ViewportCapture GetRequired(PrintDiagramKind kind) =>
        _captures.TryGetValue(kind, out ViewportCapture? capture)
            ? capture
            : throw new InvalidOperationException($"A {kind} diagram capture was not supplied.");
}

public interface IViewportCaptureProvider
{
    IReadOnlyCollection<PrintDiagramKind> SupportedDiagramKinds =>
        new[] { PrintDiagramKind.Model };

    ViewportCapture Capture(ProjectDocumentContent documentHost);

    PrintDiagramCaptureSet CaptureSet(
        ProjectDocumentContent documentHost,
        IEnumerable<PrintDiagramKind> diagramKinds)
    {
        ArgumentNullException.ThrowIfNull(diagramKinds);
        PrintDiagramKind[] kinds = diagramKinds.Distinct().ToArray();
        if (kinds.Length == 0)
        {
            return PrintDiagramCaptureSet.Empty;
        }

        if (kinds.Length != 1)
        {
            throw new InvalidOperationException(
                "This viewport capture provider cannot produce multiple semantic diagram presentations.");
        }

        return new PrintDiagramCaptureSet(
            [new KeyValuePair<PrintDiagramKind, ViewportCapture>(kinds[0], Capture(documentHost))]);
    }
}

public sealed class LiveViewportCaptureProvider : IViewportCaptureProvider
{
    private static readonly IReadOnlyCollection<PrintDiagramKind> AllDiagramKinds =
        Array.AsReadOnly(
            new[]
            {
                PrintDiagramKind.Model,
                PrintDiagramKind.Load,
                PrintDiagramKind.Result,
            });

    public IReadOnlyCollection<PrintDiagramKind> SupportedDiagramKinds => AllDiagramKinds;

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

    public PrintDiagramCaptureSet CaptureSet(
        ProjectDocumentContent documentHost,
        IEnumerable<PrintDiagramKind> diagramKinds)
    {
        ArgumentNullException.ThrowIfNull(documentHost);
        ArgumentNullException.ThrowIfNull(diagramKinds);
        if (documentHost.InvokeRequired)
        {
            throw new InvalidOperationException("The live viewport must be captured on its owning UI thread.");
        }

        PrintDiagramKind[] kinds = diagramKinds.Distinct().ToArray();
        if (kinds.Length == 0)
        {
            return PrintDiagramCaptureSet.Empty;
        }

        ViewportDisplayMode originalMode = documentHost.Presentation.DisplayMode;
        SceneEntityKey? originalSelection = documentHost.Selection;
        List<KeyValuePair<PrintDiagramKind, ViewportCapture>> captures = [];
        Exception? captureFailure = null;
        try
        {
            foreach (PrintDiagramKind kind in kinds)
            {
                documentHost.SetDisplayMode(ResolveDisplayMode(documentHost, kind));
                documentHost.FlushPendingUpdates();
                captures.Add(new KeyValuePair<PrintDiagramKind, ViewportCapture>(kind, Capture(documentHost)));
            }
        }
        catch (Exception exception)
        {
            captureFailure = exception;
        }

        try
        {
            documentHost.SetDisplayMode(originalMode);
            documentHost.FlushPendingUpdates();
            documentHost.SetSelection(originalSelection, ViewportSelectionOrigin.Programmatic);
        }
        catch (Exception restoreFailure)
        {
            if (captureFailure is not null)
            {
                throw new AggregateException(
                    "Viewport diagram capture and presentation restoration both failed.",
                    captureFailure,
                    restoreFailure);
            }

            throw;
        }

        if (captureFailure is not null)
        {
            ExceptionDispatchInfo.Capture(captureFailure).Throw();
        }

        return new PrintDiagramCaptureSet(captures);
    }

    private static ViewportDisplayMode ResolveDisplayMode(
        ProjectDocumentContent documentHost,
        PrintDiagramKind kind) => kind switch
        {
            PrintDiagramKind.Model => ViewportDisplayMode.Model,
            PrintDiagramKind.Load => ViewportDisplayMode.Loads,
            PrintDiagramKind.Result when documentHost.CurrentResultTables is null =>
                throw new InvalidOperationException("A result diagram requires a current typed result presentation."),
            PrintDiagramKind.Result => ResolveResultDisplayMode(documentHost),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported desktop diagram kind."),
        };

    private static ViewportDisplayMode ResolveResultDisplayMode(ProjectDocumentContent documentHost)
    {
        ViewportDisplayMode current = documentHost.Presentation.DisplayMode;
        if (current is ViewportDisplayMode.Displacements or
            ViewportDisplayMode.Reactions or
            ViewportDisplayMode.SectionForces)
        {
            return current;
        }

        return documentHost.ResultTableSelector.SelectedIndex switch
        {
            1 => ViewportDisplayMode.Reactions,
            2 => ViewportDisplayMode.SectionForces,
            _ => ViewportDisplayMode.Displacements,
        };
    }
}
