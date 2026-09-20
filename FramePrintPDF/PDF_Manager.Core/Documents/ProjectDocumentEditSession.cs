namespace PDF_Manager.Core.Documents;

/// <summary>
/// Runtime-only, bounded command history over immutable validated project documents.
/// History is deliberately not part of <see cref="ProjectDocument"/> serialization.
/// </summary>
public sealed class ProjectDocumentEditSession
{
    public const int DefaultMaxHistoryEntries = 100;
    public const int MaximumHistoryEntries = 1_000;

    private readonly int _maxHistoryEntries;
    private readonly List<ProjectDocument> _undo = [];
    private readonly List<ProjectDocument> _redo = [];

    public ProjectDocumentEditSession(
        ProjectDocument initial,
        int maxHistoryEntries = DefaultMaxHistoryEntries)
    {
        ArgumentNullException.ThrowIfNull(initial);
        ProjectDocumentValidator.Validate(initial);
        if (maxHistoryEntries is < 1 or > MaximumHistoryEntries)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxHistoryEntries),
                $"History size must be between 1 and {MaximumHistoryEntries}.");
        }

        Current = initial;
        _maxHistoryEntries = maxHistoryEntries;
    }

    public ProjectDocument Current { get; private set; }

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public bool UpsertNode(ProjectNode value)
        => Upsert(Current.Nodes, value, item => item.Id, nodes => Rebuild(nodes: nodes));

    public bool UpsertSection(FrameSectionDefinition value)
        => Upsert(Current.Sections, value, item => item.Id, sections => Rebuild(sections: sections));

    public bool UpsertMember(ProjectMember value)
        => Upsert(Current.Members, value, item => item.Id, members => Rebuild(members: members));

    public bool UpsertSupport(ProjectSupport value)
        => Upsert(Current.Supports, value, item => item.Id, supports => Rebuild(supports: supports));

    public bool UpsertLoadCase(LoadCaseDefinition value)
        => Upsert(Current.LoadCases, value, item => item.Id, loadCases => Rebuild(loadCases: loadCases));

    public bool UpsertNodalLoad(NodalLoadDefinition value)
        => Upsert(Current.NodalLoads, value, item => item.Id, nodalLoads => Rebuild(nodalLoads: nodalLoads));

    public bool RemoveNode(string id)
        => Remove(Current.Nodes, id, item => item.Id, nodes => Rebuild(nodes: nodes));

    public bool RemoveSection(string id)
        => Remove(Current.Sections, id, item => item.Id, sections => Rebuild(sections: sections));

    public bool RemoveMember(string id)
        => Remove(Current.Members, id, item => item.Id, members => Rebuild(members: members));

    public bool RemoveSupport(string id)
        => Remove(Current.Supports, id, item => item.Id, supports => Rebuild(supports: supports));

    public bool RemoveLoadCase(string id)
        => Remove(Current.LoadCases, id, item => item.Id, loadCases => Rebuild(loadCases: loadCases));

    public bool RemoveNodalLoad(string id)
        => Remove(Current.NodalLoads, id, item => item.Id, nodalLoads => Rebuild(nodalLoads: nodalLoads));

    public bool Undo()
    {
        if (_undo.Count == 0)
        {
            return false;
        }

        ProjectDocument previous = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _redo.Add(Current);
        Current = previous;
        return true;
    }

    public bool Redo()
    {
        if (_redo.Count == 0)
        {
            return false;
        }

        ProjectDocument next = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        PushUndo(Current);
        Current = next;
        return true;
    }

    private bool Upsert<T>(
        IReadOnlyList<T> source,
        T value,
        Func<T, string> idSelector,
        Func<IReadOnlyList<T>, ProjectDocument> rebuild)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(value);
        List<T> changed = source.ToList();
        string id = idSelector(value);
        int index = changed.FindIndex(item => StringComparer.Ordinal.Equals(idSelector(item), id));
        if (index >= 0)
        {
            if (EqualityComparer<T>.Default.Equals(changed[index], value))
            {
                return false;
            }

            changed[index] = value;
        }
        else
        {
            changed.Add(value);
        }

        return Commit(rebuild(changed));
    }

    private bool Remove<T>(
        IReadOnlyList<T> source,
        string id,
        Func<T, string> idSelector,
        Func<IReadOnlyList<T>, ProjectDocument> rebuild)
        where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        List<T> changed = source.ToList();
        int removed = changed.RemoveAll(item => StringComparer.Ordinal.Equals(idSelector(item), id));
        return removed > 0 && Commit(rebuild(changed));
    }

    private bool Commit(ProjectDocument candidate)
    {
        ProjectDocumentValidator.Validate(candidate);
        PushUndo(Current);
        Current = candidate;
        _redo.Clear();
        return true;
    }

    private void PushUndo(ProjectDocument document)
    {
        _undo.Add(document);
        if (_undo.Count > _maxHistoryEntries)
        {
            _undo.RemoveAt(0);
        }
    }

    private ProjectDocument Rebuild(
        IReadOnlyList<ProjectNode>? nodes = null,
        IReadOnlyList<FrameSectionDefinition>? sections = null,
        IReadOnlyList<ProjectMember>? members = null,
        IReadOnlyList<ProjectSupport>? supports = null,
        IReadOnlyList<LoadCaseDefinition>? loadCases = null,
        IReadOnlyList<NodalLoadDefinition>? nodalLoads = null)
        => new(
            Current.Version,
            Current.Metadata,
            nodes ?? Current.Nodes,
            members ?? Current.Members,
            supports ?? Current.Supports,
            loadCases ?? Current.LoadCases,
            nodalLoads ?? Current.NodalLoads,
            Current.DerivedResults,
            Current.MovingLoads,
            Current.Selection,
            isDirty: true,
            sections: sections ?? Current.Sections);
}
