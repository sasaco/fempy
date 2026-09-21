using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Numerics;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using FrameWebforCS.Rendering.Scene;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using GlControl = OpenTK.GLControl.GLControl;
using GlControlSettings = OpenTK.GLControl.GLControlSettings;
using GlPixelFormat = OpenTK.Graphics.OpenGL4.PixelFormat;

namespace FrameWebforCS.Rendering;

public sealed class OpenGlViewportLifecycle : IDisposable, ICameraController, IHitTestService
{
    private const int MaximumGridLineCount = 512;
    private const int MaximumOverlayTextLength = 256;
    private const int OverlayMargin = 12;
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

    private const string OverlayVertexShaderSource = """
        #version 330 core
        layout (location = 0) in vec2 aPosition;
        layout (location = 1) in vec2 aTextureCoordinate;
        out vec2 textureCoordinate;

        void main()
        {
            gl_Position = vec4(aPosition, 0.0, 1.0);
            textureCoordinate = aTextureCoordinate;
        }
        """;

    private const string OverlayFragmentShaderSource = """
        #version 330 core
        in vec2 textureCoordinate;
        uniform sampler2D overlayTexture;
        out vec4 fragmentColor;

        void main()
        {
            fragmentColor = texture(overlayTexture, textureCoordinate);
        }
        """;

    private static readonly Color GridMinorColor = Color.FromArgb(255, 32, 52, 79);
    private static readonly Color GridMajorColor = Color.FromArgb(255, 43, 73, 110);
    private static readonly Color AxisXColor = Color.FromArgb(255, 237, 67, 67);
    private static readonly Color AxisYColor = Color.FromArgb(255, 67, 220, 115);
    private static readonly Color AxisZColor = Color.FromArgb(255, 69, 137, 240);
    private static readonly Color LabelColor = Color.FromArgb(255, 242, 242, 232);
    private static readonly Color ScaleColor = Color.FromArgb(255, 109, 227, 255);
    private static readonly Color LegendBorderColor = Color.FromArgb(255, 255, 175, 78);

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
    private IViewportScene? _scene;
    private ViewportSceneCommandBuffer? _sceneCommands;
    private readonly Dictionary<SceneLayerKind, ViewportSceneLayerCommandBuffer> _layerCommands = [];
    private readonly SceneInvalidationCoalescer _invalidations = new();
    private readonly ViewportHitTestService _hitTests = new();
    private SceneEntityKey? _selection;
    private SceneEntityKey? _hover;
    private SceneLayerMask _visibleLayers = SceneLayerMask.All;
    private ViewportCameraPolicy _cameraPolicy = ViewportCameraPolicy.ThreeDimensional;
    private IReadOnlyList<SceneGridCommand> _gridCommands = [];
    private IReadOnlyList<SceneAxisCommand> _axisCommands = [];
    private IReadOnlyList<SceneLabelCommand> _labelCommands = [];
    private IReadOnlyList<SceneScaleCommand> _scaleCommands = [];
    private IReadOnlyList<SceneColorLegendCommand> _colorLegendCommands = [];
    private ViewportCameraState _camera = new(
        ViewportProjection.Orthographic,
        new ScenePoint3(0.0f, 0.0f, 0.0f),
        3.25f,
        2.4f);
    private int _program;
    private int _vertexArray;
    private int _vertexBuffer;
    private int _vertexCount;
    private int _overlayProgram;
    private int _overlayVertexArray;
    private int _overlayVertexBuffer;
    private int _overlayTexture;
    private int _overlaySamplerLocation;
    private bool _hasDecorationOverlay;
    private bool _decorationOverlayDirty = true;

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

    public ViewportSceneModel? Scene => _scene as ViewportSceneModel;

    public IViewportScene? ViewportScene => _scene;

    public SceneEntityKey? Selection => _selection;

    public SceneEntityKey? Hover => _hover;

    public ViewportProjection Projection => _camera.Projection;

    public ViewportCameraState Camera => _camera;

    public ViewportCameraPolicy CameraPolicy => _cameraPolicy;

    public SceneLayerMask VisibleLayers => _visibleLayers;

    public SceneLayerMask PendingInvalidation => _invalidations.Pending;

    public long InvalidationRequestCount => _invalidations.RequestsReceived;

    public long InvalidationBatchCount => _invalidations.BatchesConsumed;

    public long LayerCompilationCount { get; private set; }

    public event EventHandler<ViewportSelectionChangedEventArgs>? SelectionChanged;

    public event EventHandler<ViewportHoverChangedEventArgs>? HoverChanged;

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
            _overlayProgram = CreateProgram(OverlayVertexShaderSource, OverlayFragmentShaderSource);
            _overlayVertexArray = GL.GenVertexArray();
            _overlayVertexBuffer = GL.GenBuffer();
            _overlayTexture = GL.GenTexture();
            ConfigureOverlayInput();
            ConfigureDeterministicState();
            _initialized = true;

            if (_scene is not null)
            {
                InvalidateLayers(SceneLayerMask.All);
                RebuildSceneCommands(upload: false);
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
        _layerCommands.Clear();
        _hasDecorationOverlay = false;
        _decorationOverlayDirty = true;
        _selection = null;
        _hover = null;
        if (_initialized)
        {
            EnsureContextCurrent();
            UploadModel(model);
        }

        RequestRender();
    }

    public void SetScene(ViewportSceneModel scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        SetSceneCore(scene, scene.GetChangedLayers(_scene), resetCamera: true, rebuildImmediately: true);
    }

    public void SetScene(IViewportScene scene, SceneLayerMask affectedLayers)
    {
        ArgumentNullException.ThrowIfNull(scene);
        if ((affectedLayers & ~SceneLayerMask.All) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(affectedLayers));
        }

        SetSceneCore(scene, affectedLayers, resetCamera: false, rebuildImmediately: false);
    }

    private void SetSceneCore(
        IViewportScene scene,
        SceneLayerMask affectedLayers,
        bool resetCamera,
        bool rebuildImmediately)
    {
        EnsureUiThread();
        ThrowIfDisposed();

        if (_scene is not null && affectedLayers == SceneLayerMask.None &&
            string.Equals(_scene.StableId, scene.StableId, StringComparison.Ordinal))
        {
            return;
        }

        bool identityChanged = _scene is null || !string.Equals(_scene.StableId, scene.StableId, StringComparison.Ordinal);
        _scene = scene;
        _model = null;
        _selection = _selection is { } selection && scene.Contains(selection) ? selection : null;
        _hover = _hover is { } hover && scene.Contains(hover) ? hover : null;
        if (resetCamera || identityChanged)
        {
            _camera = ViewportSceneCompiler.Home(scene, _camera.Projection);
            affectedLayers = SceneLayerMask.All;
        }

        InvalidateLayers(affectedLayers);
        if (rebuildImmediately)
        {
            RebuildSceneCommands(upload: _initialized);
        }

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

        SceneLayerMask affected = (_selection?.Kind.ToLayerMask() ?? SceneLayerMask.None) |
            (selection?.Kind.ToLayerMask() ?? SceneLayerMask.None);
        _selection = selection;
        InvalidateLayers(affected);
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

        SceneEntityKey? selected = HitTest(clientPoint)?.Key;
        SetSelection(selected, ViewportSelectionOrigin.Viewport);
        return selected is not null;
    }

    public bool HoverAt(Point clientPoint)
    {
        EnsureUiThread();
        ThrowIfDisposed();
        SceneEntityKey? hover = HitTest(clientPoint)?.Key;
        SetHover(hover);
        return hover is not null;
    }

    public void SetHover(SceneEntityKey? hover)
    {
        EnsureUiThread();
        ThrowIfDisposed();
        if (hover is { } target && (_scene is null || !_scene.Contains(target)))
        {
            throw new ArgumentException("Hover must reference an entity in the current scene.", nameof(hover));
        }

        if (_hover == hover)
        {
            return;
        }

        SceneLayerMask affected = (_hover?.Kind.ToLayerMask() ?? SceneLayerMask.None) |
            (hover?.Kind.ToLayerMask() ?? SceneLayerMask.None);
        _hover = hover;
        InvalidateLayers(affected);
        RebuildSceneCommands(upload: _initialized);
        RequestRender();
        HoverChanged?.Invoke(this, new ViewportHoverChangedEventArgs(hover));
    }

    public SceneHitTestResult? HitTest(
        ViewportSceneCommandBuffer commandBuffer,
        Point clientPoint,
        Size viewportSize,
        SceneHitTestOptions? options = null) =>
        _hitTests.HitTest(commandBuffer, clientPoint, viewportSize, options);

    public SceneHitTestResult? HitTest(Point clientPoint, SceneHitTestOptions? options = null)
    {
        EnsureUiThread();
        ThrowIfDisposed();
        if (_scene is not null && _invalidations.Pending != SceneLayerMask.None)
        {
            RebuildSceneCommands(upload: _initialized);
        }

        return _sceneCommands is null ? null : HitTest(_sceneCommands, clientPoint, _viewportSize, options);
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
        InvalidateLayers(SceneLayerMask.All);
        RebuildSceneCommands(upload: _initialized);
        RequestRender();
    }

    public void ToggleProjection() => SetProjection(
        Projection == ViewportProjection.Orthographic
            ? ViewportProjection.Perspective
            : ViewportProjection.Orthographic);

    public void SetCameraPolicy(ViewportCameraPolicy policy)
    {
        EnsureUiThread();
        ThrowIfDisposed();
        if (!Enum.IsDefined(policy))
        {
            throw new ArgumentOutOfRangeException(nameof(policy));
        }

        ViewportProjection projection = policy == ViewportCameraPolicy.TwoDimensional
            ? ViewportProjection.Orthographic
            : ViewportProjection.Perspective;
        if (_cameraPolicy == policy && _camera.Projection == projection)
        {
            return;
        }

        _cameraPolicy = policy;
        _camera = _camera with { Projection = projection };
        if (_scene is not null)
        {
            _camera = ViewportSceneCompiler.Home(_scene, policy);
        }

        InvalidateLayers(SceneLayerMask.All);
        RebuildSceneCommands(upload: _initialized);
        RequestRender();
    }

    public void SetVisibleLayers(SceneLayerMask visibleLayers)
    {
        EnsureUiThread();
        ThrowIfDisposed();
        if ((visibleLayers & ~SceneLayerMask.All) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(visibleLayers));
        }

        if (_visibleLayers == visibleLayers)
        {
            return;
        }

        SceneLayerMask affected = _visibleLayers ^ visibleLayers;
        _visibleLayers = visibleLayers;
        InvalidateLayers(affected);
    }

    public void InvalidateLayers(SceneLayerMask layers)
    {
        EnsureUiThread();
        ThrowIfDisposed();
        bool scheduled = _invalidations.Invalidate(layers);
        if (layers != SceneLayerMask.None)
        {
            RendererDiagnostics.InvalidationRequested();
        }

        if (scheduled)
        {
            RequestRender();
        }
    }

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
        InvalidateLayers(SceneLayerMask.All);
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
        InvalidateLayers(SceneLayerMask.All);
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
        InvalidateLayers(SceneLayerMask.All);
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
        if (_scene is not null && _invalidations.Pending != SceneLayerMask.None)
        {
            RebuildSceneCommands(upload: _initialized);
        }

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

        if (_scene is not null && _invalidations.Pending != SceneLayerMask.None)
        {
            RebuildSceneCommands(upload: _initialized);
        }

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

    public byte[] CapturePng(ViewportPngCaptureOptions? options = null)
    {
        EnsureUiThread();
        ThrowIfDisposed();
        options ??= new ViewportPngCaptureOptions();
        if (_viewportSize.Width > ViewportPngCaptureOptions.MaximumDimension ||
            _viewportSize.Height > ViewportPngCaptureOptions.MaximumDimension ||
            checked((long)_viewportSize.Width * _viewportSize.Height) > ViewportPngCaptureOptions.MaximumPixelCount)
        {
            throw new InvalidOperationException("The viewport exceeds the bounded PNG capture dimensions.");
        }

        using Bitmap capture = Capture();
        using MemoryStream output = new();
        capture.Save(output, ImageFormat.Png);
        if (output.Length > options.MaximumBytes)
        {
            throw new InvalidOperationException("The encoded PNG exceeds the configured byte limit.");
        }

        return output.ToArray();
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

    private void ConfigureOverlayInput()
    {
        float[] vertices =
        [
            -1.0f, -1.0f, 0.0f, 1.0f,
             1.0f, -1.0f, 1.0f, 1.0f,
             1.0f,  1.0f, 1.0f, 0.0f,
            -1.0f, -1.0f, 0.0f, 1.0f,
             1.0f,  1.0f, 1.0f, 0.0f,
            -1.0f,  1.0f, 0.0f, 0.0f,
        ];

        GL.BindVertexArray(_overlayVertexArray);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _overlayVertexBuffer);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 2 * sizeof(float));
        GL.BindVertexArray(0);
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

        GL.BindTexture(TextureTarget.Texture2D, _overlayTexture);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        GL.BindTexture(TextureTarget.Texture2D, 0);

        _overlaySamplerLocation = GL.GetUniformLocation(_overlayProgram, "overlayTexture");
        if (_overlaySamplerLocation < 0)
        {
            throw new InvalidOperationException("The decoration overlay sampler was not linked.");
        }
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

        _sceneCommands ??= ViewportSceneCompiler.Compile(
            _scene,
            _camera,
            _viewportSize,
            _selection,
            _hover,
            _visibleLayers,
            _cameraPolicy);
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
        if (_decorationOverlayDirty)
        {
            UploadDecorationOverlay();
        }
        ModelUploadCount++;
    }

    private void UploadDecorationOverlay()
    {
        if (_sceneCommands is null || !HasDecorations(_sceneCommands))
        {
            _hasDecorationOverlay = false;
            _decorationOverlayDirty = false;
            return;
        }

        Size rasterSize = BoundedOverlaySize(_viewportSize);
        using Bitmap overlay = new(
            rasterSize.Width,
            rasterSize.Height,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (Graphics graphics = Graphics.FromImage(overlay))
        {
            DrawDecorations(graphics, rasterSize, _sceneCommands);
        }

        Rectangle bounds = new(Point.Empty, rasterSize);
        BitmapData data = overlay.LockBits(
            bounds,
            ImageLockMode.ReadOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            if (data.Stride != checked(rasterSize.Width * 4))
            {
                throw new InvalidOperationException("The decoration overlay bitmap has an unsupported row stride.");
            }

            GL.BindTexture(TextureTarget.Texture2D, _overlayTexture);
            GL.PixelStore(PixelStoreParameter.UnpackAlignment, 4);
            GL.TexImage2D(
                TextureTarget.Texture2D,
                0,
                PixelInternalFormat.Rgba8,
                rasterSize.Width,
                rasterSize.Height,
                0,
                GlPixelFormat.Bgra,
                PixelType.UnsignedByte,
                data.Scan0);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            _hasDecorationOverlay = true;
            _decorationOverlayDirty = false;
        }
        finally
        {
            overlay.UnlockBits(data);
        }
    }

    private void DrawDecorations(
        Graphics graphics,
        Size rasterSize,
        ViewportSceneCommandBuffer commands)
    {
        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.Clear(Color.Transparent);
        graphics.CompositingMode = CompositingMode.SourceOver;
        graphics.SmoothingMode = SmoothingMode.None;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;

        foreach (SceneGridCommand command in commands.GridCommands)
        {
            DrawGrid(graphics, rasterSize, command);
        }

        using Font font = new(FontFamily.GenericSansSerif, 10.0f, FontStyle.Regular, GraphicsUnit.Pixel);
        DrawAxes(graphics, rasterSize, commands.AxisCommands, font);
        DrawLabels(graphics, rasterSize, commands.LabelCommands, font);
        DrawScales(graphics, rasterSize, commands.ScaleCommands, font);
        DrawColorLegends(graphics, rasterSize, commands.ColorLegendCommands, font);
    }

    private void DrawGrid(Graphics graphics, Size rasterSize, SceneGridCommand command)
    {
        float aspectRatio = rasterSize.Width / (float)Math.Max(1, rasterSize.Height);
        double extent = _camera.Projection == ViewportProjection.Orthographic
            ? Math.Max(0.01, _camera.OrthographicHeight) * Math.Max(1.0, aspectRatio)
            : Math.Max(0.01, _camera.Distance) * 1.5;
        double span = extent * 2.0;
        double minorSpacing = command.MajorSpacing / command.MinorDivisions;
        double minimumStep = span / ((MaximumGridLineCount / 2.0) - 1.0);
        double multiplier = Math.Max(1.0, Math.Ceiling(minimumStep / minorSpacing));
        double step = minorSpacing * multiplier;
        if (!double.IsFinite(step) || step <= 0.0)
        {
            step = minimumStep;
        }

        (double centerU, double centerV) = GridCenter(command.Plane, _camera.Target);
        double minimumU = centerU - extent;
        double maximumU = centerU + extent;
        double minimumV = centerV - extent;
        double maximumV = centerV + extent;
        double startU = Math.Floor(minimumU / step) * step;
        double startV = Math.Floor(minimumV / step) * step;
        Matrix4x4 viewProjection = CreateViewProjection(rasterSize);

        using Pen minorPen = new(GridMinorColor, 1.0f);
        using Pen majorPen = new(GridMajorColor, 1.0f);
        for (int index = 0; index < MaximumGridLineCount / 2; index++)
        {
            double coordinate = startU + (index * step);
            if (coordinate > maximumU + (step * 0.25))
            {
                break;
            }

            DrawProjectedGridLine(
                graphics,
                IsMajorGridLine(coordinate, command.MajorSpacing) ? majorPen : minorPen,
                GridPoint(command.Plane, coordinate, minimumV),
                GridPoint(command.Plane, coordinate, maximumV),
                viewProjection,
                rasterSize);
        }

        for (int index = 0; index < MaximumGridLineCount / 2; index++)
        {
            double coordinate = startV + (index * step);
            if (coordinate > maximumV + (step * 0.25))
            {
                break;
            }

            DrawProjectedGridLine(
                graphics,
                IsMajorGridLine(coordinate, command.MajorSpacing) ? majorPen : minorPen,
                GridPoint(command.Plane, minimumU, coordinate),
                GridPoint(command.Plane, maximumU, coordinate),
                viewProjection,
                rasterSize);
        }
    }

    private static void DrawAxes(
        Graphics graphics,
        Size rasterSize,
        IReadOnlyList<SceneAxisCommand> commands,
        Font font)
    {
        foreach (SceneAxisCommand command in commands)
        {
            Color color = command.Axis switch
            {
                "X" => AxisXColor,
                "Y" => AxisYColor,
                "Z" => AxisZColor,
                _ => LabelColor,
            };
            PointF start = ToClient(command.Start, rasterSize);
            PointF end = ToClient(command.End, rasterSize);
            using Pen pen = new(color, 2.0f);
            using SolidBrush brush = new(color);
            graphics.DrawLine(pen, start, end);
            graphics.FillEllipse(brush, end.X - 2.0f, end.Y - 2.0f, 4.0f, 4.0f);
            graphics.DrawString(command.Axis, font, brush, end.X + 3.0f, end.Y + 1.0f);
        }
    }

    private static void DrawLabels(
        Graphics graphics,
        Size rasterSize,
        IReadOnlyList<SceneLabelCommand> commands,
        Font font)
    {
        using SolidBrush brush = new(LabelColor);
        foreach (SceneLabelCommand command in commands)
        {
            PointF position = ToClient(command.Position, rasterSize);
            graphics.FillRectangle(brush, position.X - 1.0f, position.Y - 1.0f, 3.0f, 3.0f);
            graphics.DrawString(OverlayText(command.Text), font, brush, position.X + 4.0f, position.Y - 5.0f);
        }
    }

    private static void DrawScales(
        Graphics graphics,
        Size rasterSize,
        IReadOnlyList<SceneScaleCommand> commands,
        Font font)
    {
        using Pen pen = new(ScaleColor, 2.0f);
        using SolidBrush brush = new(ScaleColor);
        float y = rasterSize.Height - OverlayMargin - 8.0f;
        foreach (SceneScaleCommand command in commands)
        {
            if (y < OverlayMargin)
            {
                break;
            }

            float endX = Math.Min(rasterSize.Width - OverlayMargin, OverlayMargin + 96.0f);
            graphics.DrawLine(pen, OverlayMargin, y, endX, y);
            graphics.DrawLine(pen, OverlayMargin, y - 4.0f, OverlayMargin, y + 4.0f);
            graphics.DrawLine(pen, endX, y - 4.0f, endX, y + 4.0f);
            string text = $"{OverlayText(command.Caption)} ×{command.Scale.ToString("G4", CultureInfo.InvariantCulture)}";
            graphics.DrawString(text, font, brush, OverlayMargin, y - 17.0f);
            y -= 24.0f;
        }
    }

    private static void DrawColorLegends(
        Graphics graphics,
        Size rasterSize,
        IReadOnlyList<SceneColorLegendCommand> commands,
        Font font)
    {
        const float swatchSize = 12.0f;
        const float rowHeight = 16.0f;
        float x = Math.Max(OverlayMargin, rasterSize.Width - 190.0f);
        float y = OverlayMargin;
        using Pen border = new(LegendBorderColor, 1.0f);
        using SolidBrush textBrush = new(LabelColor);
        foreach (SceneColorLegendCommand command in commands)
        {
            if (y + rowHeight >= rasterSize.Height - OverlayMargin)
            {
                break;
            }

            string caption = OverlayText(command.Caption);
            graphics.DrawString(caption, font, textBrush, x, y);
            SizeF captionSize = graphics.MeasureString(caption, font);
            graphics.DrawLine(border, x, y + captionSize.Height, Math.Min(rasterSize.Width - OverlayMargin, x + 160.0f), y + captionSize.Height);
            y += Math.Max(rowHeight, captionSize.Height + 2.0f);

            foreach (SceneColorLegendEntry entry in command.Entries)
            {
                if (y + rowHeight >= rasterSize.Height - OverlayMargin)
                {
                    break;
                }

                RectangleF swatch = new(x, y + 1.0f, swatchSize, swatchSize);
                using SolidBrush swatchBrush = new(ToColor(entry.Red, entry.Green, entry.Blue));
                graphics.FillRectangle(swatchBrush, swatch);
                graphics.DrawRectangle(border, swatch.X, swatch.Y, swatch.Width, swatch.Height);
                string entryText = $"{OverlayText(entry.Label)}  {entry.Value.ToString("G4", CultureInfo.InvariantCulture)}";
                graphics.DrawString(entryText, font, textBrush, x + swatchSize + 5.0f, y);
                y += rowHeight;
            }

            y += 4.0f;
        }
    }

    private static bool HasDecorations(ViewportSceneCommandBuffer commands) =>
        commands.GridCommands.Count > 0 ||
        commands.AxisCommands.Count > 0 ||
        commands.LabelCommands.Count > 0 ||
        commands.ScaleCommands.Count > 0 ||
        commands.ColorLegendCommands.Count > 0;

    private static Size BoundedOverlaySize(Size viewportSize)
    {
        double scale = Math.Min(
            1.0,
            Math.Min(
                ViewportPngCaptureOptions.MaximumDimension / (double)Math.Max(1, viewportSize.Width),
                ViewportPngCaptureOptions.MaximumDimension / (double)Math.Max(1, viewportSize.Height)));
        long pixelCount = checked((long)Math.Max(1, viewportSize.Width) * Math.Max(1, viewportSize.Height));
        if (pixelCount > ViewportPngCaptureOptions.MaximumPixelCount)
        {
            scale = Math.Min(scale, Math.Sqrt(ViewportPngCaptureOptions.MaximumPixelCount / (double)pixelCount));
        }

        return new Size(
            Math.Max(1, (int)Math.Floor(viewportSize.Width * scale)),
            Math.Max(1, (int)Math.Floor(viewportSize.Height * scale)));
    }

    private Matrix4x4 CreateViewProjection(Size rasterSize)
    {
        float aspectRatio = rasterSize.Width / (float)Math.Max(1, rasterSize.Height);
        Vector3 target = new(_camera.Target.X, _camera.Target.Y, _camera.Target.Z);
        Vector3 direction = _cameraPolicy == ViewportCameraPolicy.TwoDimensional
            ? -Vector3.UnitY
            : Vector3.Normalize(new Vector3(1.0f, -1.0f, 0.75f));
        Vector3 eye = target + (direction * Math.Max(0.01f, _camera.Distance));
        Matrix4x4 view = Matrix4x4.CreateLookAt(eye, target, Vector3.UnitZ);
        Matrix4x4 projection = _camera.Projection == ViewportProjection.Orthographic
            ? Matrix4x4.CreateOrthographic(
                Math.Max(0.01f, _camera.OrthographicHeight) * Math.Max(0.01f, aspectRatio),
                Math.Max(0.01f, _camera.OrthographicHeight),
                0.01f,
                Math.Max(10.0f, _camera.Distance * 10.0f))
            : Matrix4x4.CreatePerspectiveFieldOfView(
                MathF.PI / 4.0f,
                Math.Max(0.01f, aspectRatio),
                0.01f,
                Math.Max(10.0f, _camera.Distance * 10.0f));
        return view * projection;
    }

    private static void DrawProjectedGridLine(
        Graphics graphics,
        Pen pen,
        ScenePoint3 start,
        ScenePoint3 end,
        Matrix4x4 viewProjection,
        Size rasterSize)
    {
        if (TryProject(start, viewProjection, rasterSize, out PointF projectedStart) &&
            TryProject(end, viewProjection, rasterSize, out PointF projectedEnd))
        {
            graphics.DrawLine(pen, projectedStart, projectedEnd);
        }
    }

    private static bool TryProject(
        ScenePoint3 point,
        Matrix4x4 viewProjection,
        Size rasterSize,
        out PointF projected)
    {
        Vector4 clip = Vector4.Transform(new Vector4(point.X, point.Y, point.Z, 1.0f), viewProjection);
        if (!float.IsFinite(clip.X) || !float.IsFinite(clip.Y) || !float.IsFinite(clip.W) || MathF.Abs(clip.W) < 1.0e-6f)
        {
            projected = default;
            return false;
        }

        float normalizedX = clip.X / clip.W;
        float normalizedY = clip.Y / clip.W;
        float horizontalLimit = rasterSize.Width * 4.0f;
        float verticalLimit = rasterSize.Height * 4.0f;
        projected = new PointF(
            Math.Clamp((normalizedX + 1.0f) * 0.5f * rasterSize.Width, -horizontalLimit, horizontalLimit),
            Math.Clamp((1.0f - normalizedY) * 0.5f * rasterSize.Height, -verticalLimit, verticalLimit));
        return true;
    }

    private static (double U, double V) GridCenter(SceneGridPlane plane, ScenePoint3 target) => plane switch
    {
        SceneGridPlane.XY => (target.X, target.Y),
        SceneGridPlane.XZ => (target.X, target.Z),
        SceneGridPlane.YZ => (target.Y, target.Z),
        _ => throw new ArgumentOutOfRangeException(nameof(plane)),
    };

    private static ScenePoint3 GridPoint(SceneGridPlane plane, double u, double v) => plane switch
    {
        SceneGridPlane.XY => new ScenePoint3((float)u, (float)v, 0.0f),
        SceneGridPlane.XZ => new ScenePoint3((float)u, 0.0f, (float)v),
        SceneGridPlane.YZ => new ScenePoint3(0.0f, (float)u, (float)v),
        _ => throw new ArgumentOutOfRangeException(nameof(plane)),
    };

    private static bool IsMajorGridLine(double coordinate, float majorSpacing)
    {
        double ratio = coordinate / majorSpacing;
        return double.IsFinite(ratio) && Math.Abs(ratio - Math.Round(ratio)) <= 1.0e-4;
    }

    private static PointF ToClient(ScenePoint2 point, Size rasterSize) => new(
        (point.X + 1.0f) * 0.5f * rasterSize.Width,
        (1.0f - point.Y) * 0.5f * rasterSize.Height);

    private static string OverlayText(string value) => value.Length <= MaximumOverlayTextLength
        ? value
        : string.Concat(value.AsSpan(0, MaximumOverlayTextLength - 1), "…");

    private static Color ToColor(float red, float green, float blue) => Color.FromArgb(
        255,
        Math.Clamp((int)MathF.Round(red * 255.0f), 0, 255),
        Math.Clamp((int)MathF.Round(green * 255.0f), 0, 255),
        Math.Clamp((int)MathF.Round(blue * 255.0f), 0, 255));

    private void RebuildSceneCommands(bool upload)
    {
        if (_scene is null)
        {
            return;
        }

        SceneLayerMask affected = _invalidations.Consume();
        if (affected != SceneLayerMask.None)
        {
            RendererDiagnostics.InvalidationBatchConsumed();
        }
        if (_sceneCommands is null)
        {
            affected = SceneLayerMask.All;
        }

        SceneLayerMask renderableAffected = affected & (SceneLayerMask.Geometry | SceneLayerMask.Loads | SceneLayerMask.Results);
        SceneLayerMask decorationsAffected = affected & SceneLayerMask.Decorations;
        ViewportSceneCommandBuffer? compiled = null;
        if (renderableAffected != SceneLayerMask.None || decorationsAffected != SceneLayerMask.None)
        {
            compiled = ViewportSceneCompiler.Compile(
                _scene,
                _camera,
                _viewportSize,
                _selection,
                _hover,
                (renderableAffected | decorationsAffected) & _visibleLayers,
                _cameraPolicy);
        }

        bool compiledAnyLayer = false;
        foreach (SceneLayerKind kind in ViewportSceneCompiler.RenderableLayers)
        {
            SceneLayerMask layerMask = kind.ToMask();
            if ((renderableAffected & layerMask) == 0)
            {
                continue;
            }

            _layerCommands[kind] = compiled!.Layers.Single(value => value.Kind == kind);
            compiledAnyLayer = true;
            RendererDiagnostics.LayerCompiled();
        }

        if (compiledAnyLayer)
        {
            LayerCompilationCount++;
        }

        if (decorationsAffected != SceneLayerMask.None || _sceneCommands is null)
        {
            _gridCommands = compiled!.GridCommands;
            _axisCommands = compiled.AxisCommands;
            _labelCommands = compiled.LabelCommands;
            _scaleCommands = compiled.ScaleCommands;
            _colorLegendCommands = compiled.ColorLegendCommands;
            _decorationOverlayDirty = true;
        }

        _sceneCommands = ViewportSceneCompiler.Compose(
            _layerCommands.Values,
            _gridCommands,
            _axisCommands,
            _labelCommands,
            _scaleCommands,
            _colorLegendCommands);
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

        if (_hasDecorationOverlay)
        {
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.UseProgram(_overlayProgram);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _overlayTexture);
            GL.Uniform1(_overlaySamplerLocation, 0);
            GL.BindVertexArray(_overlayVertexArray);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
            GL.BindVertexArray(0);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            GL.UseProgram(0);
            GL.Disable(EnableCap.Blend);
        }
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

        if ((_model is null && _scene is null) || (_model is not null && _vertexCount == 0))
        {
            throw new InvalidOperationException("SetModel or SetScene must provide non-empty geometry before rendering or capture.");
        }
    }

    private static int CreateProgram() => CreateProgram(VertexShaderSource, FragmentShaderSource);

    private static int CreateProgram(string vertexShaderSource, string fragmentShaderSource)
    {
        int vertexShader = 0;
        int fragmentShader = 0;
        int program = 0;

        try
        {
            vertexShader = CompileShader(ShaderType.VertexShader, vertexShaderSource);
            fragmentShader = CompileShader(ShaderType.FragmentShader, fragmentShaderSource);
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
        if (_overlayTexture != 0)
        {
            GL.DeleteTexture(_overlayTexture);
            _overlayTexture = 0;
        }

        if (_overlayVertexBuffer != 0)
        {
            GL.DeleteBuffer(_overlayVertexBuffer);
            _overlayVertexBuffer = 0;
        }

        if (_overlayVertexArray != 0)
        {
            GL.DeleteVertexArray(_overlayVertexArray);
            _overlayVertexArray = 0;
        }

        if (_overlayProgram != 0)
        {
            GL.DeleteProgram(_overlayProgram);
            _overlayProgram = 0;
        }

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
        _overlaySamplerLocation = 0;
        _hasDecorationOverlay = false;
        _decorationOverlayDirty = true;
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
