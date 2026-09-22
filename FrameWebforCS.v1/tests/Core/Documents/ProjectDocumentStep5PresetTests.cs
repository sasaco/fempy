using System.Security.Cryptography;
using PDF_Manager.Core.Analysis;
using PDF_Manager.Core.Documents;

namespace PDF_Manager.Core.Tests.Documents;

public sealed class ProjectDocumentStep5PresetTests
{
    public static TheoryData<BuiltInProjectPreset> Presets => new()
    {
        BuiltInProjectPreset.RamenViaduct,
        BuiltInProjectPreset.ConcreteTBeamBridge,
        BuiltInProjectPreset.UShapedRetainingWall,
        BuiltInProjectPreset.PortalPier,
    };

    [Fact]
    public void Catalog_IsExactlyTheFourStableOrderedEntries()
    {
        Assert.Collection(
            ProjectDocumentPresets.Catalog,
            value => AssertDescriptor(value, BuiltInProjectPreset.RamenViaduct, "ramen-viaduct", 1),
            value => AssertDescriptor(value, BuiltInProjectPreset.ConcreteTBeamBridge, "concrete-t-beam-bridge", 2),
            value => AssertDescriptor(value, BuiltInProjectPreset.UShapedRetainingWall, "u-shaped-retaining-wall", 3),
            value => AssertDescriptor(value, BuiltInProjectPreset.PortalPier, "portal-pier", 4));
        Assert.Equal(
            ProjectDocumentPresets.Catalog.Count,
            ProjectDocumentPresets.Catalog.Select(value => value.StableId).Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [MemberData(nameof(Presets))]
    public void BuiltInPreset_LoadsStrictlyAndRoundTripsByteStably(BuiltInProjectPreset preset)
    {
        ProjectDocument document = ProjectDocumentPresets.Create(preset);

        ProjectDocumentValidator.Validate(document);
        Assert.False(document.IsDirty);
        Assert.Empty(document.Selection.NodeIds);
        Assert.Empty(document.Selection.MemberIds);

        byte[] first = ProjectDocumentJson.Serialize(document);
        byte[] second = ProjectDocumentJson.Serialize(ProjectDocumentJson.Deserialize(first));

        Assert.Equal(first, second);
        Assert.InRange(first.Length, 1, ProjectDocumentPresets.MaxPresetBytes);
        string json = System.Text.Encoding.UTF8.GetString(first);
        Assert.DoesNotContain("\"result\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"three\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"ver\"", json, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Presets))]
    public void BuiltInPreset_MatchesPinnedTopologyMaterialLoadSelectorsAndRequest(BuiltInProjectPreset preset)
    {
        PresetExpectation expected = GetExpectation(preset);
        ProjectDocument document = ProjectDocumentPresets.Create(preset);
        BuiltInProjectPresetDescriptor descriptor = Assert.Single(
            ProjectDocumentPresets.Catalog,
            value => value.Id == preset);

        Assert.Equal(expected.StableId, descriptor.StableId);
        Assert.Equal(expected.Name, document.Metadata.Name);
        Assert.Equal(ModelDimension.ThreeDimensional, document.Dimension);
        Assert.Equal(
            expected.Nodes,
            document.Nodes.Select(value => (value.Id, value.X, value.Y, value.Z)).ToArray());
        Assert.Equal(
            expected.Members,
            document.Members.Select(value => (value.Id, value.NodeI, value.NodeJ, value.SectionId)).ToArray());
        Assert.Equal(
            expected.Supports,
            document.Supports.Select(value => new SupportExpectation(
                value.Id,
                value.NodeId,
                value.FixX,
                value.FixY,
                value.FixZ,
                value.FixRx,
                value.FixRy,
                value.FixRz)).ToArray());

        FrameSectionDefinition section = Assert.Single(document.Sections);
        Assert.Equal("1", section.Id);
        Assert.Equal(30_000_000, section.YoungsModulus);
        Assert.Equal(0.2, section.PoissonRatio);
        Assert.Equal(12_500_000, section.ShearModulus);
        Assert.Equal(expected.Area, section.Area);
        Assert.Equal(expected.Inertia, section.MomentOfInertiaY);
        Assert.Equal(expected.Inertia, section.MomentOfInertiaZ);
        Assert.Equal(expected.TorsionConstant, section.TorsionConstant);

        LoadCaseDefinition loadCase = Assert.Single(document.LoadCases);
        Assert.Equal(("1", "Service", "S"), (loadCase.Id, loadCase.Name, loadCase.Symbol));
        Assert.Equal(("1", "1", "1", "1"), (
            loadCase.ElementSetId,
            loadCase.SupportSetId,
            loadCase.MemberSpringSetId,
            loadCase.JointSetId));
        Assert.Equal(0.1, loadCase.MovingLoadPitch);

        NodalLoadDefinition load = Assert.Single(document.NodalLoads);
        Assert.Equal(expected.Load, new LoadExpectation(
            load.Id,
            load.CaseId,
            load.NodeId,
            load.Fx,
            load.Fy,
            load.Fz,
            load.Mx,
            load.My,
            load.Mz));
        Assert.Empty(document.ElementPropertySets);
        Assert.Empty(document.RigidZones);
        Assert.Empty(document.SupportSets);
        Assert.Empty(document.Panels);
        Assert.Empty(document.JointReleaseSets);
        Assert.Empty(document.NoticePoints);
        Assert.Empty(document.MemberSpringSets);
        Assert.Empty(document.PrescribedDisplacements);
        Assert.Empty(document.MemberLoads);

        byte[] firstRequest = FrameWebAnalysisRequestJson.Serialize(document);
        byte[] secondRequest = FrameWebAnalysisRequestJson.Serialize(document);
        Assert.Equal(firstRequest, secondRequest);
        string actualRequestSha256 = Convert.ToHexString(SHA256.HashData(firstRequest));
        Assert.True(
            string.Equals(expected.RequestSha256, actualRequestSha256, StringComparison.Ordinal),
            $"Preset '{expected.StableId}' request SHA-256 changed. "
            + $"Expected {expected.RequestSha256}; actual {actualRequestSha256}.");
    }

    [Fact]
    public void BuiltInPresets_StayWithinAggregateBudget()
    {
        int totalProjectBytes = ProjectDocumentPresets.Catalog.Sum(descriptor =>
            ProjectDocumentJson.Serialize(ProjectDocumentPresets.Create(descriptor.Id)).Length);

        Assert.InRange(totalProjectBytes, 1, ProjectDocumentPresets.MaxCatalogBytes);
    }

    [Fact]
    public void Create_RejectsUnknownEnumValue()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ProjectDocumentPresets.Create((BuiltInProjectPreset)int.MaxValue));
    }

    private static void AssertDescriptor(
        BuiltInProjectPresetDescriptor actual,
        BuiltInProjectPreset id,
        string stableId,
        int sortOrder)
    {
        Assert.Equal(id, actual.Id);
        Assert.Equal(stableId, actual.StableId);
        Assert.Equal(sortOrder, actual.SortOrder);
    }

    private static PresetExpectation GetExpectation(BuiltInProjectPreset preset) => preset switch
    {
        BuiltInProjectPreset.RamenViaduct => new(
            "ramen-viaduct",
            "Ramen viaduct",
            [
                ("1", 0, 0, 0),
                ("2", 0, 0, 6),
                ("3", 8, 0, 6),
                ("4", 8, 0, 0),
            ],
            [
                ("1", "1", "2", "1"),
                ("2", "2", "3", "1"),
                ("3", "3", "4", "1"),
            ],
            [
                new("S1", "1", true, true, true, true, true, true),
                new("S2", "4", true, true, true, true, true, true),
            ],
            Area: 1.2,
            Inertia: 0.18,
            TorsionConstant: 0.36,
            new("P1", "1", "2", 25, 0, -120, 0, 0, 0),
            "CDC6E3089DAA8D962E1AE59037368ACB784DB054FFB17CB76CE805EEE510C43F"),
        BuiltInProjectPreset.ConcreteTBeamBridge => new(
            "concrete-t-beam-bridge",
            "Concrete T-beam bridge",
            [
                ("1", 0, 0, 0),
                ("2", 5, 0, 0),
                ("3", 10, 0, 0),
            ],
            [
                ("1", "1", "2", "1"),
                ("2", "2", "3", "1"),
            ],
            [
                new("S1", "1", true, true, true, true, false, false),
                new("S2", "3", false, true, true, false, false, false),
            ],
            Area: 0.85,
            Inertia: 0.095,
            TorsionConstant: 0.19,
            new("P1", "1", "2", 0, 0, -80, 0, 0, 0),
            "6A1BD467C2986DD29319BB4FF52E4C670723D90CFDF08654320CF4D141099194"),
        BuiltInProjectPreset.UShapedRetainingWall => new(
            "u-shaped-retaining-wall",
            "U-shaped retaining wall",
            [
                ("1", 0, 0, 4),
                ("2", 0, 0, 0),
                ("3", 5, 0, 0),
                ("4", 5, 0, 4),
            ],
            [
                ("1", "1", "2", "1"),
                ("2", "2", "3", "1"),
                ("3", "3", "4", "1"),
            ],
            [
                new("S1", "2", true, true, true, true, true, true),
                new("S2", "3", true, true, true, true, true, true),
            ],
            Area: 0.7,
            Inertia: 0.065,
            TorsionConstant: 0.13,
            new("P1", "1", "1", 35, 0, 0, 0, 0, 0),
            "8F735EAB4BB0AA4A4EF6637DED6A96A47675D4B2173D9A355DD965271A103D14"),
        BuiltInProjectPreset.PortalPier => new(
            "portal-pier",
            "Portal pier",
            [
                ("1", 0, 0, 0),
                ("2", 0, 0, 5),
                ("3", 7, 0, 5),
                ("4", 7, 0, 0),
            ],
            [
                ("1", "1", "2", "1"),
                ("2", "2", "3", "1"),
                ("3", "3", "4", "1"),
            ],
            [
                new("S1", "1", true, true, true, true, true, true),
                new("S2", "4", true, true, true, true, true, true),
            ],
            Area: 0.95,
            Inertia: 0.12,
            TorsionConstant: 0.24,
            new("P1", "1", "3", 60, 0, -25, 0, 0, 0),
            "74374D764C752D493F236968A211802652FB6A7B05FD3AD8065E25E06435A9E3"),
        _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, null),
    };

    private sealed record PresetExpectation(
        string StableId,
        string Name,
        (string Id, double X, double Y, double Z)[] Nodes,
        (string Id, string NodeI, string NodeJ, string? SectionId)[] Members,
        SupportExpectation[] Supports,
        double Area,
        double Inertia,
        double TorsionConstant,
        LoadExpectation Load,
        string RequestSha256);

    private sealed record SupportExpectation(
        string Id,
        string NodeId,
        bool FixX,
        bool FixY,
        bool FixZ,
        bool FixRx,
        bool FixRy,
        bool FixRz);

    private sealed record LoadExpectation(
        string Id,
        string CaseId,
        string NodeId,
        double Fx,
        double Fy,
        double Fz,
        double Mx,
        double My,
        double Mz);
}
