using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using FrameWebforCS.Core.Shell;
using WeifenLuo.WinFormsUI.Docking;
using CoreDockState = FrameWebforCS.Core.Shell.DockState;

namespace FrameWebforCS.Shell.Docking;

/// <summary>
/// Captures and restores LayoutState v1 using only stable whitelisted keys. The supported
/// arrangement is live pane/tab order, dock state, floating bounds, and logical active document.
/// Docked pane proportions and DockPanelSuite auto-hide states are intentionally not persisted.
/// All stateful operations are restricted to the creating UI thread.
/// </summary>
public sealed class DockLayoutAdapter : IDisposable
{
    public const int MaximumJsonCharacters = 65_536;
    public const int MaximumJsonUtf8Bytes = 65_536;
    public const int MaximumContentCount = 256;
    public const bool PersistsDockedDimensions = false;

    private readonly DockPanel dockPanel;
    private readonly DockContentRegistry registry;
    private readonly int ownerThreadId;
    private DocumentKey? activeDocumentKey;
    private bool applyingLayout;
    private bool disposed;

    public DockLayoutAdapter(DockPanel dockPanel, DockContentRegistry registry)
    {
        this.dockPanel = dockPanel ?? throw new ArgumentNullException(nameof(dockPanel));
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        registry.VerifyAccess();
        ownerThreadId = Environment.CurrentManagedThreadId;
        registry.ContentActivated += OnContentActivated;
        registry.ContentRemoved += OnContentRemoved;
        dockPanel.ActiveDocumentChanged += OnActiveDocumentChanged;
        SynchronizeActiveDocument();
    }

    /// <summary>
    /// Gets or sets the logical active document persisted by LayoutState. Setting null explicitly
    /// means that no document should be published as active, even if DockPanelSuite retains a
    /// visual tab selection until the next user action.
    /// </summary>
    public DocumentKey? ActiveDocumentKey
    {
        get
        {
            VerifyAccess();
            ThrowIfDisposed();
            return activeDocumentKey;
        }
        set
        {
            VerifyAccess();
            ThrowIfDisposed();
            if (value is DocumentKey key)
            {
                key.Validate();
                if (key.Kind != DocumentKind.Document ||
                    !registry.IsRegistered(key) ||
                    !registry.TryGet(key, out _))
                {
                    throw new ArgumentException(
                        "The active document key must identify a live registered document.",
                        nameof(value));
                }
            }

            activeDocumentKey = value;
        }
    }

    public LayoutState Capture()
    {
        VerifyAccess();
        ThrowIfDisposed();
        List<KeyValuePair<DocumentKey, DockContent>> contents = registry.Contents.ToList();
        EnsureContentCount(contents.Count);
        Dictionary<DocumentKey, DockContent> byKey = contents.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value);
        List<DocumentKey> captureOrder = CaptureLiveOrder(byKey);

        List<LayoutContentState> states = new(captureOrder.Count);
        for (int index = 0; index < captureOrder.Count; index++)
        {
            DocumentKey key = captureOrder[index];
            DockContent content = byKey[key];
            CoreDockState dockState = DockStateMapper.ToCoreState(content.DockState);
            WindowBounds? bounds = dockState == CoreDockState.Float
                ? CaptureFloatBounds(content)
                : null;
            states.Add(new LayoutContentState(key, dockState, bounds, index));
        }

        DocumentKey? active = activeDocumentKey;
        if (active is DocumentKey activeKey && !byKey.ContainsKey(activeKey))
        {
            active = null;
            activeDocumentKey = null;
        }

        LayoutState result = new(LayoutState.CurrentVersion, states, active);
        ValidateSupportedState(result);
        return result;
    }

    public string CaptureJson()
    {
        VerifyAccess();
        ThrowIfDisposed();
        string json = LayoutStateJson.Serialize(Capture(), registry);
        EnsureJsonSize(json);
        return json;
    }

    public void RestoreJson(string json)
    {
        VerifyAccess();
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(json);
        EnsureJsonSize(json);
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        EnsureJsonContentCount(json);
        LayoutState state = LayoutStateJson.Deserialize(json, registry);
        RestoreValidated(state);
    }

    public void Restore(LayoutState state)
    {
        VerifyAccess();
        ThrowIfDisposed();
        ValidateSupportedState(state);
        RestoreValidated(state);
    }

    public void Dispose()
    {
        VerifyAccess();
        if (disposed)
        {
            return;
        }

        dockPanel.ActiveDocumentChanged -= OnActiveDocumentChanged;
        registry.ContentActivated -= OnContentActivated;
        registry.ContentRemoved -= OnContentRemoved;
        activeDocumentKey = null;
        disposed = true;
    }

    private void RestoreValidated(LayoutState state)
    {
        ValidateSupportedState(state);
        LayoutState snapshot = Capture();
        try
        {
            Apply(state);
        }
        catch (Exception applyFailure)
        {
            try
            {
                Apply(snapshot);
            }
            catch (Exception rollbackFailure)
            {
                throw new DockLayoutTransactionException(applyFailure, rollbackFailure);
            }

            ExceptionDispatchInfo.Capture(applyFailure).Throw();
            throw;
        }
    }

    private void Apply(LayoutState state)
    {
        applyingLayout = true;
        try
        {
            HashSet<DocumentKey> restoredKeys = state.Contents
                .Select(static content => content.Key)
                .ToHashSet();
            foreach ((DocumentKey key, _) in registry.Contents
                .Where(pair => !restoredKeys.Contains(pair.Key))
                .ToArray())
            {
                if (!registry.RemoveFromLayout(key))
                {
                    throw new InvalidOperationException(
                        $"Dock content '{key}' could not be removed while applying the layout.");
                }
            }

            foreach (LayoutContentState content in state.Contents.OrderBy(static content => content.Order))
            {
                registry.Open(content.Key, content.DockState, content.Bounds);
            }

            RestoreLiveTabOrder(state);
            if (state.ActiveDocument is DocumentKey active)
            {
                if (!registry.Activate(active))
                {
                    throw new InvalidOperationException(
                        $"The restored active document '{active}' is not live.");
                }
            }

            activeDocumentKey = state.ActiveDocument;
        }
        finally
        {
            applyingLayout = false;
        }
    }

    private List<DocumentKey> CaptureLiveOrder(
        IReadOnlyDictionary<DocumentKey, DockContent> byKey)
    {
        List<DocumentKey> result = new(byKey.Count);
        HashSet<DocumentKey> seen = [];
        foreach (DockPane pane in dockPanel.Panes)
        {
            foreach (IDockContent dockContent in pane.Contents)
            {
                if (dockContent is DockContent content &&
                    registry.TryGetKey(content, out DocumentKey key) &&
                    byKey.ContainsKey(key) &&
                    seen.Add(key))
                {
                    result.Add(key);
                }
            }
        }

        result.AddRange(byKey.Keys
            .Where(seen.Add)
            .OrderBy(static key => key.ToString(), StringComparer.Ordinal));
        return result;
    }

    private void RestoreLiveTabOrder(LayoutState state)
    {
        Dictionary<DocumentKey, int> desiredOrder = state.Contents.ToDictionary(
            static content => content.Key,
            static content => content.Order);
        foreach (DockPane pane in dockPanel.Panes)
        {
            List<DocumentKey> paneKeys = [];
            foreach (IDockContent dockContent in pane.Contents)
            {
                if (dockContent is DockContent content &&
                    registry.TryGetKey(content, out DocumentKey key) &&
                    desiredOrder.ContainsKey(key) &&
                    content.DockState != WeifenLuo.WinFormsUI.Docking.DockState.Hidden)
                {
                    paneKeys.Add(key);
                }
            }

            paneKeys.Sort((left, right) => desiredOrder[left].CompareTo(desiredOrder[right]));
            for (int index = paneKeys.Count - 2; index >= 0; index--)
            {
                registry.MoveBefore(paneKeys[index], paneKeys[index + 1]);
            }
        }
    }

    private void ValidateSupportedState(LayoutState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        EnsureContentCount(state.Contents.Count);
        foreach (LayoutContentState content in state.Contents)
        {
            if (content.DockState != CoreDockState.Float && content.Bounds is not null)
            {
                throw new UnsupportedDockLayoutException(
                    "LayoutState v1 supports persisted bounds only for floating content.");
            }
        }

        LayoutStateValidator.Validate(state, registry);
    }

    private static WindowBounds CaptureFloatBounds(DockContent content)
    {
        Rectangle bounds = content.DockHandler.FloatPane?.FloatWindow?.Bounds ?? content.Bounds;
        return new WindowBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }

    private static void EnsureContentCount(int count)
    {
        if (count > MaximumContentCount)
        {
            throw new DockLayoutCapacityException(
                DockLayoutCapacityKind.Contents,
                count,
                MaximumContentCount);
        }
    }

    private static void EnsureJsonSize(string json)
    {
        if (json.Length > MaximumJsonCharacters)
        {
            throw new DockLayoutCapacityException(
                DockLayoutCapacityKind.Characters,
                json.Length,
                MaximumJsonCharacters);
        }

        int utf8Bytes = Encoding.UTF8.GetByteCount(json);
        if (utf8Bytes > MaximumJsonUtf8Bytes)
        {
            throw new DockLayoutCapacityException(
                DockLayoutCapacityKind.Utf8Bytes,
                utf8Bytes,
                MaximumJsonUtf8Bytes);
        }
    }

    private static void EnsureJsonContentCount(string json)
    {
        using JsonDocument document = JsonDocument.Parse(
            json,
            new JsonDocumentOptions
            {
                MaxDepth = 64,
                CommentHandling = JsonCommentHandling.Disallow,
                AllowTrailingCommas = false,
            });
        if (document.RootElement.ValueKind == JsonValueKind.Object &&
            document.RootElement.TryGetProperty("contents", out JsonElement contents) &&
            contents.ValueKind == JsonValueKind.Array)
        {
            EnsureContentCount(contents.GetArrayLength());
        }
    }

    private void OnContentActivated(object? sender, DockContentEventArgs eventArgs)
    {
        VerifyAccess();
        if (eventArgs.Key.Kind == DocumentKind.Document)
        {
            activeDocumentKey = eventArgs.Key;
        }
    }

    private void OnContentRemoved(object? sender, DockContentEventArgs eventArgs)
    {
        VerifyAccess();
        if (activeDocumentKey == eventArgs.Key)
        {
            activeDocumentKey = null;
        }
    }

    private void OnActiveDocumentChanged(object? sender, EventArgs eventArgs)
    {
        VerifyAccess();
        if (!applyingLayout)
        {
            SynchronizeActiveDocument();
        }
    }

    private void SynchronizeActiveDocument()
    {
        if (dockPanel.ActiveDocument is DockContent active &&
            registry.TryGetKey(active, out DocumentKey activeKey) &&
            activeKey.Kind == DocumentKind.Document)
        {
            activeDocumentKey = activeKey;
        }
        else
        {
            activeDocumentKey = null;
        }
    }

    private void VerifyAccess()
    {
        if (Environment.CurrentManagedThreadId != ownerThreadId)
        {
            throw new InvalidOperationException(
                $"Dock layout operations must run on creating UI thread {ownerThreadId}.");
        }

        registry.VerifyAccess();
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);
}
