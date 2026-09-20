using System.Drawing.Imaging;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using PDF_Manager.Rendering.Scene;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using GlControl = OpenTK.GLControl.GLControl;
using GlControlSettings = OpenTK.GLControl.GLControlSettings;
using GlPixelFormat = OpenTK.Graphics.OpenGL4.PixelFormat;

namespace PDF_Manager.Rendering;

public sealed class OpenGlViewportLifecycle : IDisposable
{
    private const string VertexShaderSource = """
        #version 330 core
        layout (location = 0) in vec2 aPosition;
        layout (location = 1) in vec3 aColor;
        out vec3 vertexColor;

        void main()
        {
            gl_Position = vec4(aPosition, 0.0, 1.0);
            vertexColor = aColor;
        }
        """;

    private const string FragmentShaderSource = """
        #version 330 core
        in vec3 vertexColor;
        out vec4 fragmentColor;

        void main()
        {
            fragmentColor = vec4(vertexColor, 1.0);
        }
        """;

    private readonly int _uiThreadId = Environment.CurrentManagedThreadId;
    private readonly GlControl _control;
    private bool _disposed;
    private bool _initialized;
    private bool _isDirty = true;
    private bool _subscribed;
    private Size _viewportSize = new(1, 1);
    private RenderSceneModel? _model;
    private ViewportSceneModel? _scene;
    private ViewportSceneCommandBuffer? _sceneCommands;
    private SceneEntityKey? _selection;
    private ViewportCameraState _camera = new(
        ViewportProjection.Orthographic,
        new ScenePoint3(0.0f, 0.0f, 0.0f),
        3.25f,
        2.4f);
    private int _program;
    private int _vertexArray;
    private int _vertexBuffer;
    private int _vertexCount;

    public OpenGlViewportLifecycle()
    {
        GlControlSettings settings = new()
        {
            API = ContextAPI.OpenGL,
            APIVersion = new Version(3, 3, 0, 0),
            Profile = ContextProfile.Core,
            Flags = ContextFlags.ForwardCompatible,
            IsEventDriven = true,
            NumberOfSamples = 0,
        };

        _control = new GlControl(settings)
        {
            BackColor = Color.Black,
            Dock = DockStyle.Fill,
            Name = "FrameWebOpenGlViewport",
            TabStop = false,
        };

        SubscribeControlEvents();
    }

    public Control Control => _control;

    public bool IsInitialized => _initialized;

    public string? OpenGlVersion { get; private set; }

    public int ModelUploadCount { get; private set; }

    public int ResizeCount { get; private set; }

    public int RenderedFrameCount { get; private set; }

    public int CaptureCount { get; private set; }

    public ViewportSceneModel? Scene => _scene;

    public SceneEntityKey? Selection => _selection;

    public ViewportProjection Projection => _camera.Projection;

    public ViewportCameraState Camera => _camera;

    public event EventHandler<ViewportSelectionChangedEventArgs>? SelectionChanged;

    public void Initialize()
    {
        EnsureUiThread();
        ThrowIfDisposed();
        if (_initialized)
        {
            return;
        }

        _ = _control.Handle;
        EnsureContextCurrent();

        try
        {
            OpenGlVersion = GL.GetString(StringName.Version)
                ?? throw new InvalidOperationException("The OpenGL context did not report a version.");
            _program = CreateProgram();
            _vertexArray = GL.GenVertexArray();
            _vertexBuffer = GL.GenBuffer();
            ConfigureVertexInput();
            ConfigureDeterministicState();
            _initialized = true;

            if (_scene is not null)
            {
                UploadScene();
            }
            else if (_model is not null)
            {
                UploadModel(_model);
            }

            ApplyViewport(_control.ClientSize);
            RequestRender();
            RendererDiagnostics.ContextCreated();
        }
        catch
        {
            _initialized = false;
            ReleaseGpuResources();
            throw;
        }
    }

    public void SetModel(RenderSceneModel model)
    {
        EnsureUiThread();
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(model);

        if (_model is not null && _model.HasSameContent(model))
        {
            return;
        }

        _model = model;
        _scene = null;
        _sceneCommands = null;
        _selection = null;
        if (_initialized)
        {
            EnsureContextCurrent();
            UploadModel(model);
        }

        RequestRender();
    }

    public void SetScene(ViewportSceneModel scene)
    {
        EnsureUiThread();
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(scene);

        if (_scene is not null && _scene.HasSameContent(scene))
        {
            return;
        }

        _scene = scene;
        _model = null;
        _selection = _selection is { } selection && scene.Contains(selection) ? selection : null;
        _camera = ViewportSceneCompiler.Home(scene, _camera.Projection);
        RebuildSceneCommands(upload: _initialized);
        RequestRender();
    }

    public void SetSelection(
        SceneEntityKey? selection,
        ViewportSelectionOrigin origin = ViewportSelectionOrigin.Table)
    {
        EnsureUiThread();
        ThrowIfDisposed();
        if (selection is { } selected && (_scene is null || !_scene.Contains(selected)))
        {
            throw new ArgumentException("Selection must reference an entity in the current scene.", nameof(selection));
        }

        if (_selection == selection)
        {
            return;
        }

        _selection = selection;
        RebuildSceneCommands(upload: _initialized);
        RequestRender();
        SelectionChanged?.Invoke(this, new ViewportSelectionChangedEventArgs(selection, origin));
    }

    public bool SelectAt(Point clientPoint)
    {
        EnsureUiThread();
        ThrowIfDisposed();
        if (_sceneCommands is null)
        {
            return false;
        }

        SceneEntityKey? selected = ViewportSceneCompiler.HitTest(_sceneCommands, clientPoint, _viewportSize);
        SetSelection(selected, ViewportSelectionOrigin.Viewport);
        return selected is not null;
    }

    public void SetProjection(ViewportProjection projection)
    {
        EnsureUiThread();
        ThrowIfDisposed();
        if (!Enum.IsDefined(projection))
        {
            throw new ArgumentOutOfRangeException(nameof(projection));
        }

        if (_camera.Projection == projection)
        {
            return;
        }

        _camera = _camera with { Projection = projection };
        RebuildSceneCommands(upload: _initialized);
        RequestRender();
    }

    public void ToggleProjection() => SetProjection(
        Projection == ViewportProjection.Orthographic
            ? ViewportProjection.Perspective
            : ViewportProjection.Orthographic);

    public void Fit()
    {
        EnsureUiThread();
        ThrowIfDisposed();
        if (_scene is null)
        {
            return;
        }

        ViewportCameraState fitted = ViewportSceneCompiler.Fit(_scene, _camera);
        if (fitted == _camera)
        {
            return;
        }

        _camera = fitted;
        RebuildSceneCommands(upload: _initialized);
        RequestRender();
    }

    public void Home()
    {
        EnsureUiThread();
        ThrowIfDisposed();
        if (_scene is null)
        {
            return;
        }

        ViewportCameraState home = ViewportSceneCompiler.Home(_scene, _camera.Projection);
        if (home == _camera)
        {
            return;
        }

        _camera = home;
        RebuildSceneCommands(upload: _initialized);
        RequestRender();
    }

    public void Resize(Size size)
    {
        EnsureUiThread();
        ThrowIfDisposed();
        Size normalized = Normalize(size);
        if (normalized == _viewportSize)
        {
            return;
        }

        ApplyViewport(normalized);
        RebuildSceneCommands(upload: _initialized);
        ResizeCount++;
        RequestRender();
    }

    public void RequestRender()
    {
        EnsureUiThread();
        ThrowIfDisposed();
        _isDirty = true;
        _control.Invalidate();
    }

    public void Render()
    {
        EnsureUiThread();
        ThrowIfDisposed();
        if (!ShouldRenderFrame(_initialized, _isDirty, _model is not null, _scene is not null))
        {
            return;
        }

        DrawKnownFrame();
        _control.SwapBuffers();
        CompleteFrame();
    }

    internal static bool ShouldRenderFrame(
        bool initialized,
        bool isDirty,
        bool hasLegacyModel,
        bool hasTypedScene) =>
        initialized && isDirty && (hasLegacyModel || hasTypedScene);

    public Bitmap Capture()
    {
        EnsureUiThread();
        ThrowIfDisposed();
        EnsureReady();

        EnsureContextCurrent();
        DrawKnownFrame();
        GL.Finish();

        int width = _viewportSize.Width;
        int height = _viewportSize.Height;
        byte[] pixels = new byte[checked(width * height * 4)];
        GL.ReadBuffer(ReadBufferMode.Back);
        GL.PixelStore(PixelStoreParameter.PackAlignment, 4);
        GL.ReadPixels(0, 0, width, height, GlPixelFormat.Bgra, PixelType.UnsignedByte, pixels);

        Bitmap capture = CreateTopDownBitmap(width, height, pixels);
        _control.SwapBuffers();
        CompleteFrame();
        CaptureCount++;
        RendererDiagnostics.CaptureCompleted();
        return capture;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        EnsureUiThread();
        UnsubscribeControlEvents();
        Exception? failure = null;
        bool hadContext = _initialized;

        try
        {
            if (!_control.IsDisposed && _control.HasValidContext)
            {
                EnsureContextCurrent();
                try
                {
                    if (hadContext)
                    {
                        ReleaseGpuResources();
                    }
                }
                finally
                {
                    ReleaseCurrentContext();
                }
            }
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        try
        {
            _control.Dispose();
        }
        catch (Exception exception)
        {
            failure = failure is null ? exception : new AggregateException(failure, exception);
        }
        finally
        {
            if (hadContext)
            {
                RendererDiagnostics.ContextDisposed();
            }

            _initialized = false;
            _disposed = true;
        }

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private void SubscribeControlEvents()
    {
        if (_subscribed)
        {
            return;
        }

        _control.Paint += OnControlPaint;
        _control.Resize += OnControlResize;
        _subscribed = true;
        RendererDiagnostics.SubscriptionsAdded(2);
    }

    private void UnsubscribeControlEvents()
    {
        if (!_subscribed)
        {
            return;
        }

        _control.Paint -= OnControlPaint;
        _control.Resize -= OnControlResize;
        _subscribed = false;
        RendererDiagnostics.SubscriptionsRemoved(2);
    }

    private void OnControlPaint(object? sender, PaintEventArgs eventArgs) => Render();

    private void OnControlResize(object? sender, EventArgs eventArgs) => Resize(_control.ClientSize);

    private void ApplyViewport(Size size)
    {
        _viewportSize = Normalize(size);
        if (!_initialized)
        {
            return;
        }

        EnsureContextCurrent();
        GL.Viewport(0, 0, _viewportSize.Width, _viewportSize.Height);
    }

    private void ConfigureVertexInput()
    {
        GL.BindVertexArray(_vertexArray);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBuffer);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 5 * sizeof(float), 0);
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 5 * sizeof(float), 2 * sizeof(float));
        GL.BindVertexArray(0);
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
    }

    private static void ConfigureDeterministicState()
    {
        GL.Disable(EnableCap.Blend);
        GL.Disable(EnableCap.DepthTest);
        GL.Disable(EnableCap.Dither);
        GL.Disable(EnableCap.Multisample);
        GL.ClearColor(0.05f, 0.10f, 0.20f, 1.0f);
    }

    private void UploadModel(RenderSceneModel model)
    {
        float[] source = model.Positions.ToArray();
        float[] positions = new float[checked(model.VertexCount * 5)];
        for (int index = 0; index < model.VertexCount; index++)
        {
            positions[(index * 5) + 0] = source[(index * 2) + 0];
            positions[(index * 5) + 1] = source[(index * 2) + 1];
            positions[(index * 5) + 2] = 0.95f;
            positions[(index * 5) + 3] = 0.35f;
            positions[(index * 5) + 4] = 0.15f;
        }

        GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBuffer);
        GL.BufferData(BufferTarget.ArrayBuffer, positions.Length * sizeof(float), positions, BufferUsageHint.StaticDraw);
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        _vertexCount = model.VertexCount;
        ModelUploadCount++;
    }

    private void UploadScene()
    {
        if (_scene is null)
        {
            return;
        }

        _sceneCommands ??= ViewportSceneCompiler.Compile(_scene, _camera, _viewportSize, _selection);
        float[] vertices = new float[checked(_sceneCommands.Vertices.Count * 5)];
        for (int index = 0; index < _sceneCommands.Vertices.Count; index++)
        {
            SceneRenderVertex vertex = _sceneCommands.Vertices[index];
            vertices[(index * 5) + 0] = vertex.X;
            vertices[(index * 5) + 1] = vertex.Y;
            vertices[(index * 5) + 2] = vertex.Red;
            vertices[(index * 5) + 3] = vertex.Green;
            vertices[(index * 5) + 4] = vertex.Blue;
        }

        GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBuffer);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.DynamicDraw);
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        _vertexCount = _sceneCommands.Vertices.Count;
        ModelUploadCount++;
    }

    private void RebuildSceneCommands(bool upload)
    {
        if (_scene is null)
        {
            return;
        }

        _sceneCommands = ViewportSceneCompiler.Compile(_scene, _camera, _viewportSize, _selection);
        if (upload)
        {
            EnsureContextCurrent();
            UploadScene();
        }
    }

    private void DrawKnownFrame()
    {
        EnsureContextCurrent();
        GL.Viewport(0, 0, _viewportSize.Width, _viewportSize.Height);
        GL.Clear(ClearBufferMask.ColorBufferBit);
        GL.UseProgram(_program);
        GL.BindVertexArray(_vertexArray);
        if (_sceneCommands is null)
        {
            GL.DrawArrays(PrimitiveType.Triangles, 0, _vertexCount);
        }
        else
        {
            foreach (SceneDrawBatch batch in _sceneCommands.Batches)
            {
                if (batch.Primitive == ScenePrimitive.Points)
                {
                    GL.PointSize(batch.Width);
                }
                else if (batch.Primitive == ScenePrimitive.Lines)
                {
                    GL.LineWidth(batch.Width);
                }

                GL.DrawArrays(ToPrimitiveType(batch.Primitive), batch.FirstVertex, batch.VertexCount);
            }
        }
        GL.BindVertexArray(0);
        GL.UseProgram(0);
    }

    private void EnsureContextCurrent()
    {
        IGraphicsContext? context = _control.Context;
        if (!_control.HasValidContext || context is null)
        {
            throw new InvalidOperationException("GLControl did not create a real OpenGL context.");
        }

        if (ShouldActivateContext(_control.HasValidContext, context.IsCurrent))
        {
            context.MakeCurrent();
        }
    }

    private void ReleaseCurrentContext()
    {
        IGraphicsContext? context = _control.Context;
        if (context?.IsCurrent == true)
        {
            context.MakeNoneCurrent();
        }
    }

    internal static bool ShouldActivateContext(bool hasValidContext, bool isCurrent)
    {
        if (!hasValidContext)
        {
            throw new InvalidOperationException("GLControl did not create a real OpenGL context.");
        }

        return !isCurrent;
    }

    private void CompleteFrame()
    {
        _isDirty = false;
        RenderedFrameCount++;
        RendererDiagnostics.FrameRendered();
    }

    private void EnsureReady()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Initialize must complete before rendering or capture.");
        }

        if ((_model is null && _scene is null) || _vertexCount == 0)
        {
            throw new InvalidOperationException("SetModel or SetScene must provide non-empty geometry before rendering or capture.");
        }
    }

    private static int CreateProgram()
    {
        int vertexShader = 0;
        int fragmentShader = 0;
        int program = 0;

        try
        {
            vertexShader = CompileShader(ShaderType.VertexShader, VertexShaderSource);
            fragmentShader = CompileShader(ShaderType.FragmentShader, FragmentShaderSource);
            program = GL.CreateProgram();
            GL.AttachShader(program, vertexShader);
            GL.AttachShader(program, fragmentShader);
            GL.LinkProgram(program);
            GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int status);
            if (status == 0)
            {
                throw new InvalidOperationException($"OpenGL program link failed: {GL.GetProgramInfoLog(program)}");
            }

            return program;
        }
        catch
        {
            if (program != 0)
            {
                GL.DeleteProgram(program);
                program = 0;
            }

            throw;
        }
        finally
        {
            if (program != 0)
            {
                GL.DetachShader(program, vertexShader);
                GL.DetachShader(program, fragmentShader);
            }

            if (vertexShader != 0)
            {
                GL.DeleteShader(vertexShader);
            }

            if (fragmentShader != 0)
            {
                GL.DeleteShader(fragmentShader);
            }
        }
    }

    private static int CompileShader(ShaderType type, string source)
    {
        int shader = GL.CreateShader(type);
        GL.ShaderSource(shader, source);
        GL.CompileShader(shader);
        GL.GetShader(shader, ShaderParameter.CompileStatus, out int status);
        if (status != 0)
        {
            return shader;
        }

        string log = GL.GetShaderInfoLog(shader);
        GL.DeleteShader(shader);
        throw new InvalidOperationException($"OpenGL {type} compilation failed: {log}");
    }

    private static PrimitiveType ToPrimitiveType(ScenePrimitive primitive) => primitive switch
    {
        ScenePrimitive.Points => PrimitiveType.Points,
        ScenePrimitive.Lines => PrimitiveType.Lines,
        ScenePrimitive.Triangles => PrimitiveType.Triangles,
        _ => throw new ArgumentOutOfRangeException(nameof(primitive)),
    };

    private void ReleaseGpuResources()
    {
        if (_vertexBuffer != 0)
        {
            GL.DeleteBuffer(_vertexBuffer);
            _vertexBuffer = 0;
        }

        if (_vertexArray != 0)
        {
            GL.DeleteVertexArray(_vertexArray);
            _vertexArray = 0;
        }

        if (_program != 0)
        {
            GL.DeleteProgram(_program);
            _program = 0;
        }

        _vertexCount = 0;
    }

    private static Bitmap CreateTopDownBitmap(int width, int height, byte[] bottomUpPixels)
    {
        Bitmap bitmap = new(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        Rectangle bounds = new(0, 0, width, height);
        BitmapData data = bitmap.LockBits(bounds, ImageLockMode.WriteOnly, bitmap.PixelFormat);

        try
        {
            int rowBytes = checked(width * 4);
            for (int destinationRow = 0; destinationRow < height; destinationRow++)
            {
                int sourceOffset = checked((height - destinationRow - 1) * rowBytes);
                IntPtr destination = IntPtr.Add(data.Scan0, checked(destinationRow * data.Stride));
                Marshal.Copy(bottomUpPixels, sourceOffset, destination, rowBytes);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return bitmap;
    }

    private void EnsureUiThread()
    {
        if (Environment.CurrentManagedThreadId != _uiThreadId)
        {
            throw new InvalidOperationException("All GL lifecycle operations must run on the creating UI thread.");
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private static Size Normalize(Size size) => new(Math.Max(1, size.Width), Math.Max(1, size.Height));
}
