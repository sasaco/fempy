namespace FrameWebforCS.Core.Shell;

/// <summary>
/// Explicit whitelist of shell factories. Tool instances are cached for hide/show reuse;
/// document instances are created for each open and are expected to be disposed on close.
/// </summary>
public sealed class ContentFactoryRegistry<TContent> : IContentKeyWhitelist
    where TContent : class
{
    private readonly Dictionary<DocumentKey, Registration> registrations = [];
    private readonly object syncRoot = new();

    public void Register(DocumentKey key, Func<TContent> factory)
    {
        key.Validate();
        ArgumentNullException.ThrowIfNull(factory);
        lock (syncRoot)
        {
            if (!registrations.TryAdd(key, new Registration(factory)))
            {
                throw new DuplicateContentKeyException(key);
            }
        }
    }

    public bool IsRegistered(DocumentKey key)
    {
        key.Validate();
        lock (syncRoot)
        {
            return registrations.ContainsKey(key);
        }
    }

    public TContent CreateOrGet(DocumentKey key)
    {
        key.Validate();
        lock (syncRoot)
        {
            if (!registrations.TryGetValue(key, out Registration? registration))
            {
                throw new UnknownContentKeyException(key);
            }

            if (key.Kind == DocumentKind.Tool && registration.ToolInstance is not null)
            {
                return registration.ToolInstance;
            }

            TContent instance = registration.Factory() ?? throw new ContentFactoryException(key);
            if (key.Kind == DocumentKind.Tool)
            {
                registration.ToolInstance = instance;
            }

            return instance;
        }
    }

    private sealed class Registration(Func<TContent> factory)
    {
        internal Func<TContent> Factory { get; } = factory;

        internal TContent? ToolInstance { get; set; }
    }
}
