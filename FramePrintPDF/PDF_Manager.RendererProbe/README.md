# PDF Manager Renderer Probe

This isolated .NET 8 WinForms executable proves the proposed desktop renderer
route before it is introduced into the product projects. It uses a real
OpenTK `GLControl` inside a DockPanelSuite `DockingWindow` document.

The renderer owns its `GLControl`, GPU objects, and event subscriptions. All GL
operations are restricted to the creating UI thread. `Initialize`, `SetModel`,
`Resize`, `Render`, and `Dispose` are no-ops when repeated with unchanged state;
`Capture` returns the same known scene for unchanged state. Rendering is driven
by WinForms invalidation (`Paint`) only; there is no `Application.Idle` loop.

Run from the repository root:

```powershell
dotnet build FramePrintPDF/PDF_Manager.RendererProbe/PDF_Manager.RendererProbe.csproj
dotnet run --project FramePrintPDF/PDF_Manager.RendererProbe/PDF_Manager.RendererProbe.csproj -- --verify --cycles 100
```

Verification opens, resizes, captures, closes, and recreates the real context.
It exits nonzero when pixels, dimensions, event-driven idle behavior, lifecycle
idempotency, or live context/subscription/window counters do not meet the
contract. Captures remain in memory and are never written to the repository.
