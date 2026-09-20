# PDF Manager Renderer Probe

This .NET 8 WinForms executable verifies the production
`PDF_Manager.Rendering` library with a real OpenTK `GLControl` inside a
DockPanelSuite `DockingWindow` document. The executable owns only the known
scene, docking host, command-line runner, and verification assertions.

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

Verification opens, resizes, floats, redocks, captures, closes, and recreates
the real context. Every cycle renders and captures both a typed orthographic 2D
frame and a typed perspective 3D frame, validates their in-memory PNG signature
and 16 MiB probe limit, and reports projection-specific capture counts. The
typed scene verifies grid, axes, labels, scale, and color-legend pixels through
the shared Paint/capture OpenGL composition path, then disables all decoration
layers and verifies that those pixels disappear. It exits nonzero when pixels,
dimensions, event-driven idle behavior, lifecycle idempotency, or live
context/subscription/window counters do not meet the contract. Captures remain
in memory and are never written to the repository.
