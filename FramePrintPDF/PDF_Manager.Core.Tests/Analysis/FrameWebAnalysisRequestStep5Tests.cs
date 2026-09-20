using System.Text.Json;
using PDF_Manager.Core.Analysis;
using PDF_Manager.Core.Documents;
using PDF_Manager.Core.Tests.Documents;

namespace PDF_Manager.Core.Tests.Analysis;

public sealed class FrameWebAnalysisRequestStep5Tests
{
    public static IEnumerable<object[]> MemberLoadProjectionCases()
    {
        foreach (MemberLoadKind kind in Enum.GetValues<MemberLoadKind>())
        {
            foreach (MemberLoadDirection direction in Enum.GetValues<MemberLoadDirection>())
            {
                yield return [kind, direction, ExpectedMark(kind), ExpectedDirection(direction)];
            }
        }
    }

    [Fact]
    public void CompleteInputMatrix_EmitsDeterministicPythonLegacyRequest()
    {
        byte[] first = FrameWebAnalysisRequestJson.Serialize(Step5DocumentFactory.Create());
        byte[] second = FrameWebAnalysisRequestJson.Serialize(Step5DocumentFactory.Create());

        Assert.Equal(first, second);
        using JsonDocument parsed = JsonDocument.Parse(first);
        JsonElement root = parsed.RootElement;

        Assert.Equal(2, root.GetProperty("element").EnumerateObject().Count());
        JsonElement alternate = root.GetProperty("element").GetProperty("2").GetProperty("1");
        Assert.Equal(1.2e-5, alternate.GetProperty("Xp").GetDouble());
        Assert.Equal(7.85, alternate.GetProperty("den").GetDouble());
        Assert.Equal(0.2, alternate.GetProperty("thickness").GetDouble());
        Assert.Equal(1, root.GetProperty("rigid")[0].GetProperty("m").GetInt32());
        Assert.Equal(4, root.GetProperty("shell").GetProperty("1").GetProperty("nodes").GetArrayLength());
        Assert.Equal(5, root.GetProperty("notice_points")[0].GetProperty("Points")[0].GetDouble());
        Assert.Equal(1, root.GetProperty("fix_node").GetProperty("2")[0].GetProperty("tx").GetInt32());
        Assert.Equal(10, root.GetProperty("fix_member").GetProperty("1")[0].GetProperty("tx").GetDouble());
        Assert.Equal(0, root.GetProperty("joint").GetProperty("1")[0].GetProperty("zi").GetInt32());

        JsonElement load = root.GetProperty("load").GetProperty("1");
        Assert.Equal(2, load.GetProperty("element").GetInt32());
        Assert.Equal(2, load.GetProperty("fix_node").GetInt32());
        Assert.Equal(0.25, load.GetProperty("LL_pitch").GetDouble());
        Assert.Equal(2, load.GetProperty("load_node").GetArrayLength());
        Assert.Equal(0.001, load.GetProperty("load_node")[1].GetProperty("dx").GetDouble());
        Assert.Equal(2, load.GetProperty("load_member")[0].GetProperty("mark").GetInt32());
        Assert.Equal("gy", load.GetProperty("load_member")[0].GetProperty("direction").GetString());

        Assert.False(root.TryGetProperty("derived_results", out _));
        Assert.False(root.TryGetProperty("moving_loads", out _));
        Assert.False(root.TryGetProperty("define", out _));
        Assert.False(root.TryGetProperty("combine", out _));
        Assert.False(root.TryGetProperty("pickup", out _));
    }

    [Fact]
    public void TwoDimensionalDocument_EmitsNumericDimension()
    {
        ProjectDocument source = Step5DocumentFactory.Create();
        ProjectDocument twoDimensional = Clone(source, dimension: ModelDimension.TwoDimensional);

        using JsonDocument parsed = JsonDocument.Parse(FrameWebAnalysisRequestJson.Serialize(twoDimensional));

        Assert.Equal(2, parsed.RootElement.GetProperty("dimension").GetInt32());
    }

    [Fact]
    public void CalculationPreconditions_AreCheckedBeforeEmission()
    {
        ProjectDocument source = Step5DocumentFactory.Create();
        ProjectDocument zeroSupport = Clone(
            source,
            supportSets:
            [
                new SupportSetDefinition(
                    "2", "Free", [new SupportConditionDefinition("S2", "1", 0, 0, 0, 0, 0, 0)]),
            ],
            memberSpringSets:
            [
                new MemberSpringSetDefinition(
                    "1", "Free", [new MemberSpringDefinition("MS1", "1", 0, 0, 0, 0)]),
            ]);
        ProjectDocument noLoads = Clone(
            source,
            nodalLoads: [],
            prescribedDisplacements: [],
            memberLoads: []);

        Assert.Throws<FrameWebAnalysisRequestException>(() => FrameWebAnalysisRequestJson.Serialize(zeroSupport));
        Assert.Throws<FrameWebAnalysisRequestException>(() => FrameWebAnalysisRequestJson.Serialize(noLoads));
    }

    [Theory]
    [InlineData("nodal")]
    [InlineData("prescribed")]
    [InlineData("point_force")]
    [InlineData("point_moment")]
    [InlineData("distributed")]
    [InlineData("thermal")]
    public void ZeroValuedRows_DoNotSatisfyCalculationReadiness(string contribution)
    {
        ProjectDocument source = Step5DocumentFactory.Create();
        IEnumerable<NodalLoadDefinition> nodalLoads = contribution == "nodal"
            ? [new NodalLoadDefinition("NL0", "1", "2", 0, 0, 0, 0, 0, 0)]
            : [];
        IEnumerable<PrescribedDisplacementDefinition> prescribedDisplacements = contribution == "prescribed"
            ? [new PrescribedDisplacementDefinition("PD0", "1", "2", 0, 0, 0, 0, 0, 0)]
            : [];
        IEnumerable<MemberLoadDefinition> memberLoads = contribution switch
        {
            "point_force" =>
                [new MemberLoadDefinition("ML0", "1", "1", MemberLoadKind.PointForce,
                    MemberLoadDirection.LocalY, 1, 2, 0, 0)],
            "point_moment" =>
                [new MemberLoadDefinition("ML0", "1", "1", MemberLoadKind.PointMoment,
                    MemberLoadDirection.LocalZ, 1, 2, 0, 0)],
            "distributed" =>
                [new MemberLoadDefinition("ML0", "1", "1", MemberLoadKind.DistributedForce,
                    MemberLoadDirection.GlobalY, 1, 2, 0, 0)],
            "thermal" =>
                [new MemberLoadDefinition("ML0", "1", "1", MemberLoadKind.Thermal,
                    MemberLoadDirection.LocalX, 0, 0, 0, 99)],
            _ => [],
        };
        ProjectDocument document = Clone(
            source,
            nodalLoads: nodalLoads,
            prescribedDisplacements: prescribedDisplacements,
            memberLoads: memberLoads);

        FrameWebAnalysisRequestException exception = Assert.Throws<FrameWebAnalysisRequestException>(() =>
            FrameWebAnalysisRequestJson.Serialize(document));

        Assert.Contains("effective nonzero", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NodalAndPrescribedNonzeroContributions_EachSatisfyReadiness(bool prescribed)
    {
        ProjectDocument source = Step5DocumentFactory.Create();
        ProjectDocument document = Clone(
            source,
            nodalLoads: prescribed
                ? []
                : [new NodalLoadDefinition("NL", "1", "2", 0, 0, -1, 0, 0, 0)],
            prescribedDisplacements: prescribed
                ? [new PrescribedDisplacementDefinition("PD", "1", "2", 0, 0.001, 0, 0, 0, 0)]
                : [],
            memberLoads: []);

        using JsonDocument parsed = JsonDocument.Parse(FrameWebAnalysisRequestJson.Serialize(document));

        Assert.Single(parsed.RootElement.GetProperty("load").GetProperty("1").GetProperty("load_node").EnumerateArray());
    }

    [Theory]
    [MemberData(nameof(MemberLoadProjectionCases))]
    public void EveryMemberLoadKindAndDirection_EmitsExpectedLegacyValues(
        MemberLoadKind kind,
        MemberLoadDirection direction,
        int expectedMark,
        string expectedDirection)
    {
        ProjectDocument source = Step5DocumentFactory.Create();
        ProjectDocument document = Clone(
            source,
            nodalLoads: [],
            prescribedDisplacements: [],
            memberLoads:
            [
                new MemberLoadDefinition("ML", "1", "1", kind, direction, 1, 2, 12.5, -3.5),
            ]);

        using JsonDocument parsed = JsonDocument.Parse(FrameWebAnalysisRequestJson.Serialize(document));
        JsonElement emitted = parsed.RootElement.GetProperty("load").GetProperty("1")
            .GetProperty("load_member")[0];

        Assert.Equal(expectedMark, emitted.GetProperty("mark").GetInt32());
        Assert.Equal(expectedDirection, emitted.GetProperty("direction").GetString());
        Assert.Equal(1, emitted.GetProperty("L1").GetDouble());
        Assert.Equal(2, emitted.GetProperty("L2").GetDouble());
        Assert.Equal(12.5, emitted.GetProperty("P1").GetDouble());
        Assert.Equal(-3.5, emitted.GetProperty("P2").GetDouble());
    }

    [Fact]
    public void ThermalContribution_RequiresSelectedTableExpansionCoefficient()
    {
        ProjectDocument source = Step5DocumentFactory.Create();
        FrameSectionDefinition section = Assert.Single(source.ElementPropertySets[0].Sections) with
        {
            ThermalExpansionCoefficient = 0,
        };
        ProjectDocument document = Clone(
            source,
            elementPropertySets: [new ElementPropertySetDefinition("2", "No thermal", [section])],
            nodalLoads: [],
            prescribedDisplacements: [],
            memberLoads:
            [
                new MemberLoadDefinition("ML", "1", "1", MemberLoadKind.Thermal,
                    MemberLoadDirection.LocalX, 0, 0, 20, 0),
            ]);

        FrameWebAnalysisRequestException exception = Assert.Throws<FrameWebAnalysisRequestException>(() =>
            FrameWebAnalysisRequestJson.Serialize(document));

        Assert.Contains("thermal expansion coefficient", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MultipleCases_EmitTheirSelectedPropertySupportSpringAndJointTables()
    {
        ProjectDocument source = Step5DocumentFactory.Create();
        FrameSectionDefinition section = Assert.Single(source.Sections);
        ProjectDocument document = Clone(
            source,
            loadCases:
            [
                source.LoadCases[0],
                new LoadCaseDefinition("2", "Alternate", "A", "3", "3", "3", "3"),
            ],
            elementPropertySets:
            [
                source.ElementPropertySets[0],
                new ElementPropertySetDefinition("3", "Third", [section with { Name = "Third" }]),
            ],
            supportSets:
            [
                source.SupportSets[0],
                new SupportSetDefinition(
                    "3", "Third", [new SupportConditionDefinition("S3", "1", 1, 1, 1, 0, 0, 0)]),
            ],
            memberSpringSets:
            [
                source.MemberSpringSets[0],
                new MemberSpringSetDefinition(
                    "3", "Third", [new MemberSpringDefinition("MS3", "1", 1, 2, 3, 4)]),
            ],
            jointReleaseSets:
            [
                source.JointReleaseSets[0],
                new JointReleaseSetDefinition(
                    "3", "Third", [new JointReleaseDefinition("J3", "1", true, true, true, true, true, true)]),
            ],
            nodalLoads:
            [
                source.NodalLoads[0],
                new NodalLoadDefinition("N2", "2", "2", 0, -2, 0, 0, 0, 0),
            ],
            prescribedDisplacements: [],
            memberLoads: []);

        using JsonDocument parsed = JsonDocument.Parse(FrameWebAnalysisRequestJson.Serialize(document));
        JsonElement root = parsed.RootElement;
        JsonElement caseTwo = root.GetProperty("load").GetProperty("2");

        Assert.True(root.GetProperty("element").TryGetProperty("3", out _));
        Assert.True(root.GetProperty("fix_node").TryGetProperty("3", out _));
        Assert.True(root.GetProperty("fix_member").TryGetProperty("3", out _));
        Assert.True(root.GetProperty("joint").TryGetProperty("3", out _));
        Assert.Equal(3, caseTwo.GetProperty("element").GetInt32());
        Assert.Equal(3, caseTwo.GetProperty("fix_node").GetInt32());
        Assert.Equal(3, caseTwo.GetProperty("fix_member").GetInt32());
        Assert.Equal(3, caseTwo.GetProperty("joint").GetInt32());
    }

    [Fact]
    public void PersistableAlphanumericIdentity_IsRejectedOnlyAtAnalysisProjection()
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
        FrameWebAnalysisRequestException exception = Assert.Throws<FrameWebAnalysisRequestException>(() =>
            FrameWebAnalysisRequestJson.Serialize(document));
        Assert.Contains("canonical positive integer", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ProjectDocument Clone(
        ProjectDocument source,
        ModelDimension? dimension = null,
        IEnumerable<LoadCaseDefinition>? loadCases = null,
        IEnumerable<ElementPropertySetDefinition>? elementPropertySets = null,
        IEnumerable<SupportSetDefinition>? supportSets = null,
        IEnumerable<MemberSpringSetDefinition>? memberSpringSets = null,
        IEnumerable<JointReleaseSetDefinition>? jointReleaseSets = null,
        IEnumerable<NodalLoadDefinition>? nodalLoads = null,
        IEnumerable<PrescribedDisplacementDefinition>? prescribedDisplacements = null,
        IEnumerable<MemberLoadDefinition>? memberLoads = null)
        => new(
            source.Version,
            source.Metadata,
            source.Nodes,
            source.Members,
            source.Supports,
            loadCases ?? source.LoadCases,
            nodalLoads ?? source.NodalLoads,
            source.DerivedResults,
            source.MovingLoads,
            source.Selection,
            source.IsDirty,
            source.Sections,
            dimension ?? source.Dimension,
            elementPropertySets ?? source.ElementPropertySets,
            source.RigidZones,
            supportSets ?? source.SupportSets,
            source.Panels,
            jointReleaseSets ?? source.JointReleaseSets,
            source.NoticePoints,
            memberSpringSets ?? source.MemberSpringSets,
            prescribedDisplacements ?? source.PrescribedDisplacements,
            memberLoads ?? source.MemberLoads);

    private static int ExpectedMark(MemberLoadKind kind) => kind switch
    {
        MemberLoadKind.PointForce => 1,
        MemberLoadKind.PointMoment => 11,
        MemberLoadKind.DistributedForce => 2,
        MemberLoadKind.Thermal => 9,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static string ExpectedDirection(MemberLoadDirection direction) => direction switch
    {
        MemberLoadDirection.LocalX => "x",
        MemberLoadDirection.LocalY => "y",
        MemberLoadDirection.LocalZ => "z",
        MemberLoadDirection.GlobalX => "gx",
        MemberLoadDirection.GlobalY => "gy",
        MemberLoadDirection.GlobalZ => "gz",
        _ => throw new ArgumentOutOfRangeException(nameof(direction)),
    };
}
