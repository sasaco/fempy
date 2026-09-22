namespace FrameWebforCS.Core.Documents;

/// <summary>
/// Mutable, session-scoped draft used to construct one validated document edit.
/// A batch never becomes the current document unless every operation and the final validation succeed.
/// </summary>
public sealed class ProjectDocumentEditBatch
{
    private readonly ProjectDocument _source;
    private readonly List<ProjectNode> _nodes;
    private readonly List<FrameSectionDefinition> _sections;
    private readonly List<ElementPropertySetDefinition> _elementPropertySets;
    private readonly List<ProjectMember> _members;
    private readonly List<RigidZoneDefinition> _rigidZones;
    private readonly List<ProjectSupport> _supports;
    private readonly List<SupportSetDefinition> _supportSets;
    private readonly List<PanelDefinition> _panels;
    private readonly List<JointReleaseSetDefinition> _jointReleaseSets;
    private readonly List<NoticePointDefinition> _noticePoints;
    private readonly List<MemberSpringSetDefinition> _memberSpringSets;
    private readonly List<LoadCaseDefinition> _loadCases;
    private readonly List<NodalLoadDefinition> _nodalLoads;
    private readonly List<PrescribedDisplacementDefinition> _prescribedDisplacements;
    private readonly List<MemberLoadDefinition> _memberLoads;
    private readonly List<DerivedResultDefinition> _derivedResults;
    private readonly List<MovingLoadDefinition> _movingLoads;
    private ModelDimension _dimension;

    internal ProjectDocumentEditBatch(ProjectDocument source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _nodes = source.Nodes.ToList();
        _sections = source.Sections.ToList();
        _elementPropertySets = source.ElementPropertySets.ToList();
        _members = source.Members.ToList();
        _rigidZones = source.RigidZones.ToList();
        _supports = source.Supports.ToList();
        _supportSets = source.SupportSets.ToList();
        _panels = source.Panels.ToList();
        _jointReleaseSets = source.JointReleaseSets.ToList();
        _noticePoints = source.NoticePoints.ToList();
        _memberSpringSets = source.MemberSpringSets.ToList();
        _loadCases = source.LoadCases.ToList();
        _nodalLoads = source.NodalLoads.ToList();
        _prescribedDisplacements = source.PrescribedDisplacements.ToList();
        _memberLoads = source.MemberLoads.ToList();
        _derivedResults = source.DerivedResults.ToList();
        _movingLoads = source.MovingLoads.ToList();
        _dimension = source.Dimension;
    }

    public bool HasChanges { get; private set; }

    public void SetDimension(ModelDimension dimension)
    {
        if (_dimension == dimension)
        {
            return;
        }

        _dimension = dimension;
        HasChanges = true;
    }

    public void UpsertNode(ProjectNode value) => Upsert(_nodes, value, item => item.Id);

    public void UpsertSection(FrameSectionDefinition value) => Upsert(_sections, value, item => item.Id);

    public void UpsertElementPropertySet(ElementPropertySetDefinition value)
        => Upsert(_elementPropertySets, value, item => item.Id);

    public void UpsertMember(ProjectMember value) => Upsert(_members, value, item => item.Id);

    public void UpsertRigidZone(RigidZoneDefinition value) => Upsert(_rigidZones, value, item => item.Id);

    public void UpsertSupport(ProjectSupport value) => Upsert(_supports, value, item => item.Id);

    public void UpsertSupportSet(SupportSetDefinition value) => Upsert(_supportSets, value, item => item.Id);

    public void UpsertPanel(PanelDefinition value) => Upsert(_panels, value, item => item.Id);

    public void UpsertJointReleaseSet(JointReleaseSetDefinition value)
        => Upsert(_jointReleaseSets, value, item => item.Id);

    public void UpsertNoticePoint(NoticePointDefinition value)
        => Upsert(_noticePoints, value, item => item.Id);

    public void UpsertMemberSpringSet(MemberSpringSetDefinition value)
        => Upsert(_memberSpringSets, value, item => item.Id);

    public void UpsertLoadCase(LoadCaseDefinition value) => Upsert(_loadCases, value, item => item.Id);

    public void UpsertNodalLoad(NodalLoadDefinition value) => Upsert(_nodalLoads, value, item => item.Id);

    public void UpsertPrescribedDisplacement(PrescribedDisplacementDefinition value)
        => Upsert(_prescribedDisplacements, value, item => item.Id);

    public void UpsertMemberLoad(MemberLoadDefinition value) => Upsert(_memberLoads, value, item => item.Id);

    public void UpsertDerivedResult(DerivedResultDefinition value)
        => Upsert(_derivedResults, value, item => item.Id);

    public void UpsertMovingLoad(MovingLoadDefinition value) => Upsert(_movingLoads, value, item => item.Id);

    public void RemoveNode(string id) => Remove(_nodes, id, item => item.Id);

    public void RemoveSection(string id) => Remove(_sections, id, item => item.Id);

    public void RemoveElementPropertySet(string id) => Remove(_elementPropertySets, id, item => item.Id);

    public void RemoveMember(string id) => Remove(_members, id, item => item.Id);

    public void RemoveRigidZone(string id) => Remove(_rigidZones, id, item => item.Id);

    public void RemoveSupport(string id) => Remove(_supports, id, item => item.Id);

    public void RemoveSupportSet(string id) => Remove(_supportSets, id, item => item.Id);

    public void RemovePanel(string id) => Remove(_panels, id, item => item.Id);

    public void RemoveJointReleaseSet(string id) => Remove(_jointReleaseSets, id, item => item.Id);

    public void RemoveNoticePoint(string id) => Remove(_noticePoints, id, item => item.Id);

    public void RemoveMemberSpringSet(string id) => Remove(_memberSpringSets, id, item => item.Id);

    public void RemoveLoadCase(string id) => Remove(_loadCases, id, item => item.Id);

    public void RemoveNodalLoad(string id) => Remove(_nodalLoads, id, item => item.Id);

    public void RemovePrescribedDisplacement(string id)
        => Remove(_prescribedDisplacements, id, item => item.Id);

    public void RemoveMemberLoad(string id) => Remove(_memberLoads, id, item => item.Id);

    public void RemoveDerivedResult(string id) => Remove(_derivedResults, id, item => item.Id);

    public void RemoveMovingLoad(string id) => Remove(_movingLoads, id, item => item.Id);

    internal ProjectDocument Build()
        => new(
            _source.Version,
            _source.Metadata,
            _nodes,
            _members,
            _supports,
            _loadCases,
            _nodalLoads,
            _derivedResults,
            _movingLoads,
            _source.Selection,
            isDirty: true,
            sections: _sections,
            dimension: _dimension,
            elementPropertySets: _elementPropertySets,
            rigidZones: _rigidZones,
            supportSets: _supportSets,
            panels: _panels,
            jointReleaseSets: _jointReleaseSets,
            noticePoints: _noticePoints,
            memberSpringSets: _memberSpringSets,
            prescribedDisplacements: _prescribedDisplacements,
            memberLoads: _memberLoads);

    private void Upsert<T>(List<T> values, T value, Func<T, string> idSelector)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(value);
        string id = idSelector(value);
        int index = values.FindIndex(item => StringComparer.Ordinal.Equals(idSelector(item), id));
        if (index >= 0)
        {
            if (EqualityComparer<T>.Default.Equals(values[index], value))
            {
                return;
            }

            values[index] = value;
        }
        else
        {
            values.Add(value);
        }

        HasChanges = true;
    }

    private void Remove<T>(List<T> values, string id, Func<T, string> idSelector)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (values.RemoveAll(item => StringComparer.Ordinal.Equals(idSelector(item), id)) > 0)
        {
            HasChanges = true;
        }
    }
}
