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
        => ApplyBatch(batch => batch.UpsertNode(value));

    public bool UpsertSection(FrameSectionDefinition value)
        => ApplyBatch(batch => batch.UpsertSection(value));

    public bool UpsertElementPropertySet(ElementPropertySetDefinition value)
        => ApplyBatch(batch => batch.UpsertElementPropertySet(value));

    public bool UpsertMember(ProjectMember value)
        => ApplyBatch(batch => batch.UpsertMember(value));

    public bool UpsertRigidZone(RigidZoneDefinition value)
        => ApplyBatch(batch => batch.UpsertRigidZone(value));

    public bool UpsertSupport(ProjectSupport value)
        => ApplyBatch(batch => batch.UpsertSupport(value));

    public bool UpsertSupportSet(SupportSetDefinition value)
        => ApplyBatch(batch => batch.UpsertSupportSet(value));

    public bool UpsertPanel(PanelDefinition value)
        => ApplyBatch(batch => batch.UpsertPanel(value));

    public bool UpsertJointReleaseSet(JointReleaseSetDefinition value)
        => ApplyBatch(batch => batch.UpsertJointReleaseSet(value));

    public bool UpsertNoticePoint(NoticePointDefinition value)
        => ApplyBatch(batch => batch.UpsertNoticePoint(value));

    public bool UpsertMemberSpringSet(MemberSpringSetDefinition value)
        => ApplyBatch(batch => batch.UpsertMemberSpringSet(value));

    public bool UpsertLoadCase(LoadCaseDefinition value)
        => ApplyBatch(batch => batch.UpsertLoadCase(value));

    public bool UpsertNodalLoad(NodalLoadDefinition value)
        => ApplyBatch(batch => batch.UpsertNodalLoad(value));

    public bool UpsertPrescribedDisplacement(PrescribedDisplacementDefinition value)
        => ApplyBatch(batch => batch.UpsertPrescribedDisplacement(value));

    public bool UpsertMemberLoad(MemberLoadDefinition value)
        => ApplyBatch(batch => batch.UpsertMemberLoad(value));

    public bool UpsertDerivedResult(DerivedResultDefinition value)
        => ApplyBatch(batch => batch.UpsertDerivedResult(value));

    public bool UpsertMovingLoad(MovingLoadDefinition value)
        => ApplyBatch(batch => batch.UpsertMovingLoad(value));

    public bool RemoveNode(string id)
        => ApplyBatch(batch => batch.RemoveNode(id));

    public bool RemoveSection(string id)
        => ApplyBatch(batch => batch.RemoveSection(id));

    public bool RemoveElementPropertySet(string id)
        => ApplyBatch(batch => batch.RemoveElementPropertySet(id));

    public bool RemoveMember(string id)
        => ApplyBatch(batch => batch.RemoveMember(id));

    public bool RemoveRigidZone(string id)
        => ApplyBatch(batch => batch.RemoveRigidZone(id));

    public bool RemoveSupport(string id)
        => ApplyBatch(batch => batch.RemoveSupport(id));

    public bool RemoveSupportSet(string id)
        => ApplyBatch(batch => batch.RemoveSupportSet(id));

    public bool RemovePanel(string id)
        => ApplyBatch(batch => batch.RemovePanel(id));

    public bool RemoveJointReleaseSet(string id)
        => ApplyBatch(batch => batch.RemoveJointReleaseSet(id));

    public bool RemoveNoticePoint(string id)
        => ApplyBatch(batch => batch.RemoveNoticePoint(id));

    public bool RemoveMemberSpringSet(string id)
        => ApplyBatch(batch => batch.RemoveMemberSpringSet(id));

    public bool RemoveLoadCase(string id)
        => ApplyBatch(batch => batch.RemoveLoadCase(id));

    public bool RemoveNodalLoad(string id)
        => ApplyBatch(batch => batch.RemoveNodalLoad(id));

    public bool RemovePrescribedDisplacement(string id)
        => ApplyBatch(batch => batch.RemovePrescribedDisplacement(id));

    public bool RemoveMemberLoad(string id)
        => ApplyBatch(batch => batch.RemoveMemberLoad(id));

    public bool RemoveDerivedResult(string id)
        => ApplyBatch(batch => batch.RemoveDerivedResult(id));

    public bool RemoveMovingLoad(string id)
        => ApplyBatch(batch => batch.RemoveMovingLoad(id));

    /// <summary>
    /// Applies all requested changes atomically. The final candidate is validated once and a
    /// successful changed batch creates exactly one undo entry.
    /// </summary>
    public bool ApplyBatch(Action<ProjectDocumentEditBatch> edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        ProjectDocumentEditBatch batch = new(Current);
        edit(batch);
        if (!batch.HasChanges)
        {
            return false;
        }

        return Commit(batch.Build());
    }

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

}
