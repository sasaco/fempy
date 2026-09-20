using PDF_Manager.Core.Shell;
using CoreDockState = PDF_Manager.Core.Shell.DockState;
using DockPanelState = WeifenLuo.WinFormsUI.Docking.DockState;
using WeifenLuo.WinFormsUI.Docking;

namespace PDF_Manager.Shell.Docking;

/// <summary>
/// Owns the live DockPanelSuite content associated with explicit, whitelisted factories.
/// Exact keys reuse their live pane. Tool panes hide on close; document panes dispose and can
/// subsequently be recreated by their registered factory. All stateful operations are restricted
/// to the creating STA thread.
/// </summary>
public sealed class DockContentRegistry : IContentKeyWhitelist, IDisposable
{
    private readonly DockPanel dockPanel;
    private readonly int ownerThreadId;
    private readonly ContentFactoryRegistry<DockContent> factories = new();
    private readonly Dictionary<DocumentKey, DockContent> liveContents = [];
    private readonly Dictionary<DockContent, DocumentKey> contentKeys = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<DockContent> detachedToolContents = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<DockContent> announcedContents = new(ReferenceEqualityComparer.Instance);
    private EventHandler<DockContentEventArgs>? contentCreated;
    private EventHandler<DockContentEventArgs>? contentActivated;
    private EventHandler<DockContentEventArgs>? contentRemoved;
    private bool disposed;

    public DockContentRegistry(DockPanel dockPanel)
    {
        EnsureStaThread();
        this.dockPanel = dockPanel ?? throw new ArgumentNullException(nameof(dockPanel));
        ownerThreadId = Environment.CurrentManagedThreadId;
    }

    public int OwnerThreadId => ownerThreadId;

    public event EventHandler<DockContentEventArgs>? ContentCreated
    {
        add
        {
            VerifyAccess();
            ThrowIfDisposed();
            contentCreated += value;
        }
        remove
        {
            VerifyAccess();
            ThrowIfDisposed();
            contentCreated -= value;
        }
    }

    /// <summary>
    /// Raised after every successful visible <see cref="Open"/> or <see cref="Activate"/> call.
    /// Restoring hidden tool content does not activate it.
    /// </summary>
    public event EventHandler<DockContentEventArgs>? ContentActivated
    {
        add
        {
            VerifyAccess();
            ThrowIfDisposed();
            contentActivated += value;
        }
        remove
        {
            VerifyAccess();
            ThrowIfDisposed();
            contentActivated -= value;
        }
    }

    /// <summary>Raised once when a live document is disposed and removed from the registry.</summary>
    public event EventHandler<DockContentEventArgs>? ContentRemoved
    {
        add
        {
            VerifyAccess();
            ThrowIfDisposed();
            contentRemoved += value;
        }
        remove
        {
            VerifyAccess();
            ThrowIfDisposed();
            contentRemoved -= value;
        }
    }

    public IReadOnlyList<KeyValuePair<DocumentKey, DockContent>> Contents
    {
        get
        {
            VerifyAccess();
            ThrowIfDisposed();
            return liveContents.ToArray();
        }
    }

    /// <summary>Returns whether the current caller is the registry's creating UI thread.</summary>
    public bool CheckAccess() => Environment.CurrentManagedThreadId == ownerThreadId;

    /// <summary>Throws unless the current caller is the registry's creating UI thread.</summary>
    public void VerifyAccess()
    {
        if (!CheckAccess())
        {
            throw new InvalidOperationException(
                $"Dock content operations must run on creating UI thread {ownerThreadId}.");
        }
    }

    public void Register(DocumentKey key, Func<DockContent> factory)
    {
        VerifyAccess();
        ThrowIfDisposed();
        key.Validate();
        ArgumentNullException.ThrowIfNull(factory);
        factories.Register(key, () => CreateContent(key, factory));
    }

    public bool IsRegistered(DocumentKey key)
    {
        VerifyAccess();
        ThrowIfDisposed();
        return factories.IsRegistered(key);
    }

    public bool TryGet(DocumentKey key, out DockContent? content)
    {
        VerifyAccess();
        ThrowIfDisposed();
        key.Validate();
        if (liveContents.TryGetValue(key, out DockContent? current) && !current.IsDisposed)
        {
            content = current;
            return true;
        }

        content = null;
        return false;
    }

    public bool TryGetKey(DockContent content, out DocumentKey key)
    {
        VerifyAccess();
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(content);
        return contentKeys.TryGetValue(content, out key);
    }

    public DockContent Open(DocumentKey key)
    {
        CoreDockState defaultState = key.Kind == DocumentKind.Document
            ? CoreDockState.Document
            : CoreDockState.DockLeft;
        return OpenCore(key, defaultState, bounds: null, preserveExistingPlacement: true);
    }

    public DockContent Open(DocumentKey key, CoreDockState dockState, WindowBounds? bounds = null)
        => OpenCore(key, dockState, bounds, preserveExistingPlacement: false);

    public bool Activate(DocumentKey key)
    {
        VerifyAccess();
        ThrowIfDisposed();
        if (!TryGet(key, out DockContent? content))
        {
            return false;
        }

        if (content!.DockState == DockPanelState.Hidden)
        {
            content.Show();
        }

        content.Activate();
        contentActivated?.Invoke(this, new DockContentEventArgs(key, content));
        return true;
    }

    public bool Close(DocumentKey key)
    {
        VerifyAccess();
        ThrowIfDisposed();
        if (!TryGet(key, out DockContent? content))
        {
            return false;
        }

        if (WindowLifetimePolicy.GetCloseAction(key) == WindowCloseAction.Hide)
        {
            content!.Hide();
        }
        else
        {
            content!.Close();
        }

        return true;
    }

    /// <summary>
    /// Moves a live tab immediately before another tab in the same DockPanelSuite pane.
    /// Returns false when either key is not live.
    /// </summary>
    public bool MoveBefore(DocumentKey key, DocumentKey beforeKey)
    {
        VerifyAccess();
        ThrowIfDisposed();
        key.Validate();
        beforeKey.Validate();
        if (!TryGet(key, out DockContent? content) ||
            !TryGet(beforeKey, out DockContent? beforeContent))
        {
            return false;
        }

        if (ReferenceEquals(content, beforeContent))
        {
            return true;
        }

        DockPane? pane = beforeContent!.DockHandler.Pane;
        if (pane is null || !ReferenceEquals(content!.DockHandler.Pane, pane))
        {
            throw new UnsupportedDockLayoutException(
                "Tab reordering is supported only for content in the same live dock pane.");
        }

        content.Show(pane, beforeContent);
        return true;
    }

    public void Dispose()
    {
        VerifyAccess();
        if (disposed)
        {
            return;
        }

        disposed = true;
        foreach ((DocumentKey key, DockContent content) in liveContents.ToArray())
        {
            DetachInstance(content);
            liveContents.Remove(key);
            contentKeys.Remove(content);
            content.Dispose();
        }

        foreach (DockContent content in detachedToolContents)
        {
            content.Dispose();
        }

        detachedToolContents.Clear();
        announcedContents.Clear();
        contentCreated = null;
        contentActivated = null;
        contentRemoved = null;
    }

    private DockContent OpenCore(
        DocumentKey key,
        CoreDockState dockState,
        WindowBounds? bounds,
        bool preserveExistingPlacement)
    {
        VerifyAccess();
        ThrowIfDisposed();
        key.Validate();
        ValidatePlacement(key, dockState, bounds);

        bool created = false;
        if (!TryGet(key, out DockContent? content))
        {
            content = factories.CreateOrGet(key);
            if (content.IsDisposed)
            {
                throw new DockContentFactoryException(
                    key,
                    $"The factory for '{key}' returned a disposed dock content instance.");
            }

            detachedToolContents.Remove(content);
            Attach(key, content);
            liveContents.Add(key, content);
            contentKeys.Add(content, key);
            created = true;
        }

        DockContent resolvedContent = content ?? throw new InvalidOperationException(
            $"The live content registry failed to resolve '{key}'.");
        try
        {
            Show(resolvedContent, dockState, bounds, preserveExistingPlacement && !created);
        }
        catch
        {
            if (created)
            {
                RemoveLiveContent(key, resolvedContent);
                if (key.Kind == DocumentKind.Tool && !resolvedContent.IsDisposed)
                {
                    resolvedContent.Hide();
                    detachedToolContents.Add(resolvedContent);
                }
                else
                {
                    resolvedContent.Dispose();
                }
            }

            throw;
        }

        if (created && announcedContents.Add(resolvedContent))
        {
            contentCreated?.Invoke(this, new DockContentEventArgs(key, resolvedContent));
        }

        if (dockState != CoreDockState.Hidden)
        {
            resolvedContent.Activate();
            contentActivated?.Invoke(this, new DockContentEventArgs(key, resolvedContent));
        }

        return resolvedContent;
    }

    private static DockContent CreateContent(DocumentKey key, Func<DockContent> factory)
    {
        DockContent content;
        try
        {
            content = factory() ?? throw new DockContentFactoryException(
                key,
                $"The factory for '{key}' returned null.");
        }
        catch (DockContentFactoryException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new DockContentFactoryException(
                key,
                $"The factory for '{key}' failed.",
                exception);
        }

        if (content is not IKeyedDockContent keyed)
        {
            content.Dispose();
            throw new DockContentFactoryException(
                key,
                $"The factory for '{key}' must return content implementing {nameof(IKeyedDockContent)}.");
        }

        keyed.ContentKey.Validate();
        if (keyed.ContentKey != key)
        {
            content.Dispose();
            throw new DockContentFactoryException(
                key,
                $"The factory registered for '{key}' returned content keyed as '{keyed.ContentKey}'.");
        }

        content.HideOnClose = WindowLifetimePolicy.GetCloseAction(key) == WindowCloseAction.Hide;
        return content;
    }

    private static void ValidatePlacement(DocumentKey key, CoreDockState dockState, WindowBounds? bounds)
    {
        if (dockState != CoreDockState.Float && bounds is not null)
        {
            throw new UnsupportedDockLayoutException(
                "LayoutState v1 supports persisted bounds only for floating content.");
        }

        LayoutState probe = new(
            LayoutState.CurrentVersion,
            [new LayoutContentState(key, dockState, bounds, 0)],
            activeDocument: null);
        SingleKeyWhitelist whitelist = new(key);
        LayoutStateValidator.Validate(probe, whitelist);
    }

    private void Show(
        DockContent content,
        CoreDockState state,
        WindowBounds? bounds,
        bool preserveExistingPlacement)
    {
        if (preserveExistingPlacement && content.DockPanel is not null)
        {
            if (content.DockState == DockPanelState.Hidden)
            {
                content.Show();
            }

            return;
        }

        if (state == CoreDockState.Hidden)
        {
            if (content.DockPanel is null)
            {
                content.Show(dockPanel, DockPanelState.DockLeft);
            }

            content.Hide();
            return;
        }

        if (state == CoreDockState.Float)
        {
            WindowBounds floatBounds = bounds!.Value;
            content.Show(
                dockPanel,
                new Rectangle(floatBounds.X, floatBounds.Y, floatBounds.Width, floatBounds.Height));
            return;
        }

        content.Show(dockPanel, DockStateMapper.ToDockPanelState(state));
    }

    private void Attach(DocumentKey key, DockContent content)
    {
        content.FormClosing += OnContentClosing;
        content.FormClosed += OnContentClosed;
        content.Disposed += OnContentDisposed;
        content.HideOnClose = WindowLifetimePolicy.GetCloseAction(key) == WindowCloseAction.Hide;
    }

    /// <summary>
    /// Removes content from the persisted live layout. Tool instances remain hidden in a detached
    /// pool so Core's cached tool factory can safely return the same instance on a later open.
    /// </summary>
    internal bool RemoveFromLayout(DocumentKey key)
    {
        VerifyAccess();
        ThrowIfDisposed();
        if (!TryGet(key, out DockContent? content))
        {
            return false;
        }

        if (key.Kind == DocumentKind.Document)
        {
            content!.Close();
            return !liveContents.ContainsKey(key);
        }

        content!.Hide();
        DetachInstance(content);
        liveContents.Remove(key);
        contentKeys.Remove(content);
        detachedToolContents.Add(content);
        return true;
    }

    private void DetachInstance(DockContent content)
    {
        content.FormClosing -= OnContentClosing;
        content.FormClosed -= OnContentClosed;
        content.Disposed -= OnContentDisposed;
    }

    private void OnContentClosing(object? sender, FormClosingEventArgs eventArgs)
    {
        VerifyAccess();
        if (!disposed &&
            eventArgs.CloseReason == CloseReason.UserClosing &&
            sender is DockContent content &&
            contentKeys.TryGetValue(content, out DocumentKey key) &&
            WindowLifetimePolicy.GetCloseAction(key) == WindowCloseAction.Hide)
        {
            eventArgs.Cancel = true;
            content.Hide();
        }
    }

    private void OnContentClosed(object? sender, FormClosedEventArgs eventArgs)
    {
        VerifyAccess();
        if (sender is DockContent content && contentKeys.TryGetValue(content, out DocumentKey key))
        {
            RemoveLiveContent(key, content);
        }
    }

    private void OnContentDisposed(object? sender, EventArgs eventArgs)
    {
        VerifyAccess();
        if (sender is DockContent content && contentKeys.TryGetValue(content, out DocumentKey key))
        {
            RemoveLiveContent(key, content);
        }
    }

    private void RemoveLiveContent(DocumentKey key, DockContent content)
    {
        if (!liveContents.TryGetValue(key, out DockContent? current) || !ReferenceEquals(current, content))
        {
            return;
        }

        DetachInstance(content);
        liveContents.Remove(key);
        contentKeys.Remove(content);
        contentRemoved?.Invoke(this, new DockContentEventArgs(key, content));
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);

    private static void EnsureStaThread()
    {
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
        {
            throw new InvalidOperationException("Dock content registry must be created on an STA UI thread.");
        }
    }

    private sealed class SingleKeyWhitelist(DocumentKey key) : IContentKeyWhitelist
    {
        public bool IsRegistered(DocumentKey candidate) => candidate == key;
    }
}
