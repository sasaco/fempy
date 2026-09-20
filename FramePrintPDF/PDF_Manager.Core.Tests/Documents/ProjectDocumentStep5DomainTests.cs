using PDF_Manager.Core.Documents;

namespace PDF_Manager.Core.Tests.Documents;

public sealed class ProjectDocumentStep5DomainTests
{
    [Fact]
    public void CompleteInputMatrix_IsTypedAndValidated()
    {
        ProjectDocument document = Step5DocumentFactory.Create();

        ProjectDocumentValidator.Validate(document);

        Assert.Equal(ModelDimension.ThreeDimensional, document.Dimension);
        Assert.Single(document.ElementPropertySets);
        Assert.Single(document.RigidZones);
        Assert.Single(document.SupportSets);
        Assert.Single(document.Panels);
        Assert.Single(document.JointReleaseSets);
        Assert.Single(document.NoticePoints);
        Assert.Single(document.MemberSpringSets);
        Assert.Single(document.PrescribedDisplacements);
        Assert.Single(document.MemberLoads);
        Assert.Equal("2", Assert.Single(document.LoadCases).ElementSetId);
    }

    [Fact]
    public void InvalidReferencesDimensionsAndDuplicateTopology_AreRejected()
    {
        ProjectDocument source = Step5DocumentFactory.Create();

        ProjectDocumentValidationException badReference = Assert.Throws<ProjectDocumentValidationException>(() =>
            Step5DocumentFactory.Copy(source, rigidZones:
                [new RigidZoneDefinition("1", "missing", 1, 1, "1")]));
        ProjectDocumentValidationException badLength = Assert.Throws<ProjectDocumentValidationException>(() =>
            Step5DocumentFactory.Copy(source, rigidZones:
                [new RigidZoneDefinition("1", "1", 6, 5, "1")]));
        ProjectDocumentValidationException duplicate = Assert.Throws<ProjectDocumentValidationException>(() =>
            Step5DocumentFactory.Copy(source, members:
            [
                new ProjectMember("1", "1", "2", "1"),
                new ProjectMember("2", "2", "1", "1"),
            ]));
        ProjectDocumentValidationException nonPlanar = Assert.Throws<ProjectDocumentValidationException>(() =>
            Step5DocumentFactory.Copy(source, nodes:
            [
                new ProjectNode("1", 0, 0, 0),
                new ProjectNode("2", 10, 0, 0),
                new ProjectNode("3", 10, 2, 0),
                new ProjectNode("4", 0, 2, 1),
            ]));

        Assert.Contains("unknown member", badReference.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("invalid_dimension", badLength.Issue.Code);
        Assert.Equal("duplicate_reference", duplicate.Issue.Code);
        Assert.Equal("invalid_geometry", nonPlanar.Issue.Code);
        Assert.Equal("panels", nonPlanar.Issue.Collection);
        Assert.Equal("1", nonPlanar.Issue.EntityId);
        Assert.Equal("node_ids", nonPlanar.Issue.Field);
    }

    [Fact]
    public void LoadCaseLimitAndUnknownTableSelectors_AreRejected()
    {
        ProjectDocument source = Step5DocumentFactory.Create();
        LoadCaseDefinition[] tooMany = Enumerable.Range(1, 257)
            .Select(index => new LoadCaseDefinition(index.ToString(), $"Case {index}", $"C{index}"))
            .ToArray();

        ProjectDocumentValidationException limit = Assert.Throws<ProjectDocumentValidationException>(() =>
            Step5DocumentFactory.Copy(source, loadCases: tooMany, nodalLoads: []));
        ProjectDocumentValidationException selector = Assert.Throws<ProjectDocumentValidationException>(() =>
            Step5DocumentFactory.Copy(source, loadCases:
                [new LoadCaseDefinition("1", "Case", "C", ElementSetId: "missing")]));

        Assert.Equal("case_limit", limit.Issue.Code);
        Assert.Contains("unknown element property set", selector.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExactlyMaximumLoadCases_RemainsPersistable()
    {
        ProjectDocument source = Step5DocumentFactory.Create();
        LoadCaseDefinition[] maximum = Enumerable.Range(1, ProjectDocumentValidator.MaximumLoadCases)
            .Select(index => new LoadCaseDefinition(index.ToString(), $"Case {index}", $"C{index}"))
            .ToArray();

        ProjectDocument document = Step5DocumentFactory.Copy(
            source,
            loadCases: maximum,
            nodalLoads: [],
            prescribedDisplacements: [],
            memberLoads: []);

        Assert.Equal(ProjectDocumentValidator.MaximumLoadCases, document.LoadCases.Count);
    }

    [Fact]
    public void NodalAndPrescribedRows_RequireDistinctStableIds()
    {
        ProjectDocument source = Step5DocumentFactory.Create();

        ProjectDocumentValidationException exception = Assert.Throws<ProjectDocumentValidationException>(() =>
            Step5DocumentFactory.Copy(
                source,
                prescribedDisplacements:
                [
                    new PrescribedDisplacementDefinition("N1", "1", "2", 0.001, 0, 0, 0, 0, 0),
                ]));

        Assert.Equal("duplicate_id", exception.Issue.Code);
        Assert.Equal("nodal_loads", exception.Issue.Collection);
        Assert.Equal("N1", exception.Issue.EntityId);
        Assert.Equal("id", exception.Issue.Field);
    }

    [Theory]
    [InlineData(MemberLoadKind.PointForce)]
    [InlineData(MemberLoadKind.PointMoment)]
    public void PointMemberLoads_ValidateBothEmittedPositions(MemberLoadKind kind)
    {
        ProjectDocument source = Step5DocumentFactory.Create();

        ProjectDocumentValidationException exception = Assert.Throws<ProjectDocumentValidationException>(() =>
            Step5DocumentFactory.Copy(
                source,
                memberLoads:
                [
                    new MemberLoadDefinition(
                        "ML2", "1", "1", kind, MemberLoadDirection.LocalY,
                        L1: 1, L2: 10.001, P1: 5, P2: 6),
                ]));

        Assert.Equal("invalid_dimension", exception.Issue.Code);
        Assert.Equal("member_loads", exception.Issue.Collection);
        Assert.Equal("ML2", exception.Issue.EntityId);
        Assert.Equal("l2", exception.Issue.Field);
    }

    [Fact]
    public void PanelGeometry_RejectsDuplicateCoordinatesZeroAreaAndSelfIntersection()
    {
        ProjectDocument source = Step5DocumentFactory.Create();

        ProjectDocumentValidationException duplicateCoordinates = Assert.Throws<ProjectDocumentValidationException>(() =>
            Step5DocumentFactory.Copy(source, nodes:
            [
                new ProjectNode("1", 0, 0, 0),
                new ProjectNode("2", 10, 0, 0),
                new ProjectNode("3", 10, 2, 0),
                new ProjectNode("4", 0, 0, 0),
            ]));
        ProjectDocumentValidationException zeroArea = Assert.Throws<ProjectDocumentValidationException>(() =>
            Step5DocumentFactory.Copy(source, nodes:
            [
                new ProjectNode("1", 0, 0, 0),
                new ProjectNode("2", 10, 0, 0),
                new ProjectNode("3", 8, 0, 0),
                new ProjectNode("4", 2, 0, 0),
            ]));
        ProjectDocumentValidationException selfIntersection = Assert.Throws<ProjectDocumentValidationException>(() =>
            Step5DocumentFactory.Copy(source, panels:
                [new PanelDefinition("1", "1", ["1", "3", "2", "4"])]));

        foreach (ProjectDocumentValidationException exception in
                 new[] { duplicateCoordinates, zeroArea, selfIntersection })
        {
            Assert.Equal("invalid_geometry", exception.Issue.Code);
            Assert.Equal("panels", exception.Issue.Collection);
            Assert.Equal("1", exception.Issue.EntityId);
            Assert.Equal("node_ids", exception.Issue.Field);
        }
    }

    [Fact]
    public void PanelPlanarityTolerance_ScalesWithGeometry()
    {
        ProjectDocument source = Step5DocumentFactory.Create();

        ProjectDocument document = Step5DocumentFactory.Copy(source, nodes:
        [
            new ProjectNode("1", 1_000_000_000, 1_000_000_000, 0),
            new ProjectNode("2", 1_001_000_000, 1_000_000_000, 0),
            new ProjectNode("3", 1_001_000_000, 1_000_002_000, 0),
            new ProjectNode("4", 1_000_000_000, 1_000_002_000, 0.0005),
        ]);

        ProjectDocumentValidator.Validate(document);
    }

    [Fact]
    public void AlphanumericStableIds_RemainValidForPersistence()
    {
        ProjectDocument document = new(
            ProjectDocument.CurrentVersion,
            new ProjectMetadata("Alphanumeric", "", "", "kN-m"),
            [new ProjectNode("N1", 0, 0, 0), new ProjectNode("N2", 1, 0, 0)],
            [new ProjectMember("M1", "N1", "N2", "SEC")],
            [new ProjectSupport("SUP", "N1", true, true, true, true, true, true)],
            [new LoadCaseDefinition("LC", "Case", "C")],
            [new NodalLoadDefinition("NL", "LC", "N2", 0, -1, 0, 0, 0, 0)],
            [],
            [],
            sections:
            [
                new FrameSectionDefinition(
                    "SEC", "Section", 210_000_000, 0.3, 80_000_000, 0.01, 1e-5, 1e-5, 2e-5),
            ]);

        ProjectDocumentValidator.Validate(document);
        Assert.Equal("N1", document.Nodes[0].Id);
    }
}

internal static class Step5DocumentFactory
{
    internal static ProjectDocument Create()
    {
        FrameSectionDefinition section = Section();
        return new ProjectDocument(
            ProjectDocument.CurrentVersion,
            new ProjectMetadata("Step 5", "Complete input matrix", "Tests", "kN-m"),
            [
                new ProjectNode("1", 0, 0, 0),
                new ProjectNode("2", 10, 0, 0),
                new ProjectNode("3", 10, 2, 0),
                new ProjectNode("4", 0, 2, 0),
            ],
            [new ProjectMember("1", "1", "2", "1", 5, true)],
            [new ProjectSupport("S1", "1", true, true, true, true, true, true)],
            [new LoadCaseDefinition("1", "Service", "S", "2", "2", "1", "1", 0.25)],
            [new NodalLoadDefinition("N1", "1", "2", 0, -10, 0, 0, 0, 0)],
            [
                new DerivedResultDefinition(
                    "D1", "Derived", DerivedResultKind.Define, [new DerivedResultTerm("1", 1)]),
            ],
            [new MovingLoadDefinition("MV", "Moving", ["1"])],
            sections: [section],
            elementPropertySets: [new ElementPropertySetDefinition("2", "Alternate", [section with { Name = "Alternate" }])],
            rigidZones: [new RigidZoneDefinition("1", "1", 1, 1, "1")],
            supportSets:
            [
                new SupportSetDefinition(
                    "2", "Pinned", [new SupportConditionDefinition("S2", "1", 1, 1, 1, 0, 0, 0)]),
            ],
            panels: [new PanelDefinition("1", "1", ["1", "2", "3", "4"])],
            jointReleaseSets:
            [
                new JointReleaseSetDefinition(
                    "1", "Default", [new JointReleaseDefinition("J1", "1", true, true, false, true, true, false)]),
            ],
            noticePoints: [new NoticePointDefinition("NP1", "1", 5)],
            memberSpringSets:
            [
                new MemberSpringSetDefinition(
                    "1", "Foundation", [new MemberSpringDefinition("MS1", "1", 10, 20, 0, 0)]),
            ],
            prescribedDisplacements:
            [
                new PrescribedDisplacementDefinition("PD1", "1", "2", 0.001, 0, 0, 0, 0, 0),
            ],
            memberLoads:
            [
                new MemberLoadDefinition(
                    "ML1", "1", "1", MemberLoadKind.DistributedForce,
                    MemberLoadDirection.GlobalY, 1, 1, -2, -3),
            ]);
    }

    internal static ProjectDocument Copy(
        ProjectDocument source,
        IEnumerable<ProjectNode>? nodes = null,
        IEnumerable<ProjectMember>? members = null,
        IEnumerable<RigidZoneDefinition>? rigidZones = null,
        IEnumerable<LoadCaseDefinition>? loadCases = null,
        IEnumerable<NodalLoadDefinition>? nodalLoads = null,
        IEnumerable<PanelDefinition>? panels = null,
        IEnumerable<PrescribedDisplacementDefinition>? prescribedDisplacements = null,
        IEnumerable<MemberLoadDefinition>? memberLoads = null)
        => new(
            source.Version,
            source.Metadata,
            nodes ?? source.Nodes,
            members ?? source.Members,
            source.Supports,
            loadCases ?? source.LoadCases,
            nodalLoads ?? source.NodalLoads,
            source.DerivedResults,
            source.MovingLoads,
            source.Selection,
            source.IsDirty,
            source.Sections,
            source.Dimension,
            source.ElementPropertySets,
            rigidZones ?? source.RigidZones,
            source.SupportSets,
            panels ?? source.Panels,
            source.JointReleaseSets,
            source.NoticePoints,
            source.MemberSpringSets,
            prescribedDisplacements ?? source.PrescribedDisplacements,
            memberLoads ?? source.MemberLoads);

    private static FrameSectionDefinition Section()
        => new(
            "1", "Steel", 210_000_000, 0.3, 80_769_230.76923077,
            0.01, 8.333333333333334e-6, 8.333333333333334e-6, 1.6666666666666667e-5,
            ThermalExpansionCoefficient: 1.2e-5,
            Density: 7.85,
            PanelThickness: 0.2);
}
