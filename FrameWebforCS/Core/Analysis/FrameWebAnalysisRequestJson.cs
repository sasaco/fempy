using System.Globalization;
using System.Text.Json;
using FrameWebforCS.Core.Documents;

namespace FrameWebforCS.Core.Analysis;

public sealed class FrameWebAnalysisRequestException : ArgumentException
{
    public FrameWebAnalysisRequestException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Deterministic projection from the persisted desktop input contract to the documented Python
/// <c>POST /</c> JSON meaning. No material or section value is synthesized here.
/// </summary>
public static class FrameWebAnalysisRequestJson
{
    public const int DefaultMaxEntityCount = 100_000;
    public const int DefaultMaxJsonBytes = 4 * 1024 * 1024;
    public const int DefaultMaxCaseCount = 256;

    public static byte[] Serialize(ProjectDocument document)
        => Serialize(
            document,
            DefaultMaxEntityCount,
            DefaultMaxJsonBytes,
            DefaultMaxCaseCount,
            CancellationToken.None);

    internal static byte[] Serialize(
        ProjectDocument document,
        int maxEntityCount,
        int maxJsonBytes,
        int maxCaseCount,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateLimits(document, maxEntityCount, maxCaseCount);
            if (maxJsonBytes < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxJsonBytes));
            }

            ProjectDocumentValidator.Validate(document);
            cancellationToken.ThrowIfCancellationRequested();
            Dictionary<string, int> nodeIds = ParseIds(document.Nodes.Select(value => value.Id), "node");
            Dictionary<string, int> memberIds = ParseIds(document.Members.Select(value => value.Id), "member");
            Dictionary<string, int> sectionIds = ParseIds(document.Sections.Select(value => value.Id), "section");
            ParseIds(document.LoadCases.Select(value => value.Id), "load case");
            ParseIds(document.ElementPropertySets.Select(value => value.Id).Prepend("1"), "element property set");
            ParseIds(document.SupportSets.Select(value => value.Id).Prepend("1"), "support set");
            ParseIds(
                document.MemberSpringSets.Select(value => value.Id).Append("1").Distinct(StringComparer.Ordinal),
                "member spring set");
            ParseIds(
                document.JointReleaseSets.Select(value => value.Id).Append("1").Distinct(StringComparer.Ordinal),
                "joint set");
            ParseIds(document.RigidZones.Select(value => value.Id), "rigid zone");
            ParseIds(document.Panels.Select(value => value.Id), "panel");
            foreach (ElementPropertySetDefinition set in document.ElementPropertySets)
            {
                ParseIds(set.Sections.Select(value => value.Id), $"element property set '{set.Id}' section");
            }

            ValidateAnalysisReadiness(document, nodeIds, memberIds, sectionIds);

            using BoundedMemoryStream buffer = new(maxJsonBytes);
            using (Utf8JsonWriter writer = new(buffer, new JsonWriterOptions
            {
                Indented = false,
                SkipValidation = false,
            }))
            {
                WriteDocument(writer, document, nodeIds, memberIds, sectionIds);
                writer.Flush();
            }

            cancellationToken.ThrowIfCancellationRequested();
            return buffer.ToArray();
        }
        catch (FrameWebAnalysisRequestException)
        {
            throw;
        }
        catch (Exception exception) when (exception is
            ProjectDocumentValidationException or
            OverflowException or
            InvalidOperationException)
        {
            throw new FrameWebAnalysisRequestException(
                "The project is not a valid FrameWeb analysis request.",
                exception);
        }
    }

    private static void ValidateLimits(
        ProjectDocument document,
        int maxEntityCount,
        int maxCaseCount)
    {
        if (maxEntityCount < 1 || maxCaseCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxEntityCount));
        }

        if (document.LoadCases.Count is < 1 || document.LoadCases.Count > maxCaseCount)
        {
            throw new FrameWebAnalysisRequestException(
                $"Analysis requests must contain between 1 and {maxCaseCount} load cases.");
        }

        int entities = checked(
            document.Nodes.Count +
            document.Sections.Count +
            document.ElementPropertySets.Sum(set => 1 + set.Sections.Count) +
            document.Members.Count +
            document.RigidZones.Count +
            document.Supports.Count +
            document.SupportSets.Sum(set => 1 + set.Rows.Count) +
            document.Panels.Count +
            document.JointReleaseSets.Sum(set => 1 + set.Rows.Count) +
            document.NoticePoints.Count +
            document.MemberSpringSets.Sum(set => 1 + set.Rows.Count) +
            document.LoadCases.Count +
            document.NodalLoads.Count +
            document.PrescribedDisplacements.Count +
            document.MemberLoads.Count);
        if (entities > maxEntityCount)
        {
            throw new FrameWebAnalysisRequestException(
                $"Analysis request contains {entities} entities; the limit is {maxEntityCount}.");
        }
    }

    private static void ValidateAnalysisReadiness(
        ProjectDocument document,
        IReadOnlyDictionary<string, int> nodeIds,
        IReadOnlyDictionary<string, int> memberIds,
        IReadOnlyDictionary<string, int> sectionIds)
    {
        if (document.Metadata.UnitSystem != "kN-m")
        {
            throw new FrameWebAnalysisRequestException(
                "The vertical MVP analysis transport currently requires the explicit 'kN-m' unit system.");
        }

        if (nodeIds.Count < 2 || (memberIds.Count < 1 && document.Panels.Count < 1) || sectionIds.Count < 1)
        {
            throw new FrameWebAnalysisRequestException(
                "Analysis requires at least two nodes, one member or panel, and one persisted section.");
        }

        foreach (ProjectMember member in document.Members)
        {
            if (string.IsNullOrWhiteSpace(member.SectionId) || !sectionIds.ContainsKey(member.SectionId))
            {
                throw new FrameWebAnalysisRequestException(
                    $"Member '{member.Id}' must reference a persisted frame section before analysis.");
            }
        }

        foreach (PanelDefinition panel in document.Panels)
        {
            foreach (LoadCaseDefinition loadCase in document.LoadCases)
            {
                FrameSectionDefinition section = SectionsFor(document, loadCase.ElementSetId)
                    .Single(value => value.Id == panel.SectionId);
                if (section.PanelThickness <= 0)
                {
                    throw new FrameWebAnalysisRequestException(
                        $"Panel '{panel.Id}' requires a positive thickness in property set '{loadCase.ElementSetId}'.");
                }
            }
        }

        foreach (LoadCaseDefinition loadCase in document.LoadCases)
        {
            IEnumerable<double> nodeConditions = SupportRowsFor(document, loadCase.SupportSetId)
                .SelectMany(row => new[] { row.Tx, row.Ty, row.Tz, row.Rx, row.Ry, row.Rz });
            IEnumerable<double> memberConditions = MemberSpringRowsFor(document, loadCase.MemberSpringSetId)
                .SelectMany(row => new[] { row.Tx, row.Ty, row.Tz, row.Tr });
            if (!nodeConditions.Concat(memberConditions).Any(value => value != 0))
            {
                throw new FrameWebAnalysisRequestException(
                    $"Load case '{loadCase.Id}' requires a selected support or member-spring table with a nonzero condition.");
            }

            if (!HasEffectiveLoadContribution(document, loadCase.Id))
            {
                throw new FrameWebAnalysisRequestException(
                    $"Load case '{loadCase.Id}' must contain at least one effective nonzero nodal, " +
                    "prescribed-displacement, or member-load contribution.");
            }

            IReadOnlyDictionary<string, ProjectMember> membersById = document.Members.ToDictionary(
                member => member.Id, StringComparer.Ordinal);
            IReadOnlyDictionary<string, FrameSectionDefinition> selectedSections = SectionsFor(
                document, loadCase.ElementSetId).ToDictionary(section => section.Id, StringComparer.Ordinal);
            foreach (MemberLoadDefinition thermal in document.MemberLoads.Where(load =>
                load.CaseId == loadCase.Id && load.Kind == MemberLoadKind.Thermal))
            {
                string? sectionId = membersById[thermal.MemberId].SectionId;
                if (sectionId is null || selectedSections[sectionId].ThermalExpansionCoefficient == 0)
                {
                    throw new FrameWebAnalysisRequestException(
                        $"Thermal member load '{thermal.Id}' requires a nonzero thermal expansion coefficient.");
                }
            }
        }
    }

    private static bool HasEffectiveLoadContribution(ProjectDocument document, string caseId)
        => document.NodalLoads.Any(load =>
                load.CaseId == caseId &&
                (load.Fx != 0 || load.Fy != 0 || load.Fz != 0 ||
                 load.Mx != 0 || load.My != 0 || load.Mz != 0))
            || document.PrescribedDisplacements.Any(load =>
                load.CaseId == caseId &&
                (load.Dx != 0 || load.Dy != 0 || load.Dz != 0 ||
                 load.Rx != 0 || load.Ry != 0 || load.Rz != 0))
            || document.MemberLoads.Any(load =>
                load.CaseId == caseId &&
                (load.Kind == MemberLoadKind.Thermal
                    ? load.P1 != 0
                    : load.P1 != 0 || load.P2 != 0));

    private static IReadOnlyList<FrameSectionDefinition> SectionsFor(ProjectDocument document, string setId)
        => setId == "1"
            ? document.Sections
            : document.ElementPropertySets.Single(set => set.Id == setId).Sections;

    private static IEnumerable<SupportConditionDefinition> SupportRowsFor(ProjectDocument document, string setId)
    {
        if (setId == "1")
        {
            return document.Supports.Select(support => new SupportConditionDefinition(
                support.Id,
                support.NodeId,
                support.FixX ? 1 : 0,
                support.FixY ? 1 : 0,
                support.FixZ ? 1 : 0,
                support.FixRx ? 1 : 0,
                support.FixRy ? 1 : 0,
                support.FixRz ? 1 : 0));
        }

        return document.SupportSets.Single(set => set.Id == setId).Rows;
    }

    private static IEnumerable<MemberSpringDefinition> MemberSpringRowsFor(ProjectDocument document, string setId)
        => document.MemberSpringSets.FirstOrDefault(set => set.Id == setId)?.Rows ?? [];

    private static Dictionary<string, int> ParseIds(IEnumerable<string> values, string description)
    {
        Dictionary<string, int> result = new(StringComparer.Ordinal);
        HashSet<int> numericIds = [];
        foreach (string value in values)
        {
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed) ||
                parsed < 1 ||
                parsed.ToString(CultureInfo.InvariantCulture) != value)
            {
                throw new FrameWebAnalysisRequestException(
                    $"{description} ID '{value}' must be a canonical positive integer string for the current Python input.");
            }

            if (!numericIds.Add(parsed))
            {
                throw new FrameWebAnalysisRequestException(
                    $"{description} IDs must be unique after numeric conversion.");
            }

            result.Add(value, parsed);
        }

        return result;
    }

    private static void WriteDocument(
        Utf8JsonWriter writer,
        ProjectDocument document,
        IReadOnlyDictionary<string, int> nodeIds,
        IReadOnlyDictionary<string, int> memberIds,
        IReadOnlyDictionary<string, int> sectionIds)
    {
        writer.WriteStartObject();
        writer.WriteString("analysis_type", "static");
        WriteUnits(writer);
        if (document.Dimension == ModelDimension.TwoDimensional)
        {
            writer.WriteNumber("dimension", 2);
        }

        WriteNodes(writer, document, nodeIds);
        WriteMembers(writer, document, nodeIds, memberIds, sectionIds);
        WriteRigidZones(writer, document, memberIds, sectionIds);
        WritePanels(writer, document, nodeIds, sectionIds);
        WriteNoticePoints(writer, document, memberIds);
        WriteSections(writer, document, sectionIds);
        WriteSupports(writer, document, nodeIds);
        WriteMemberSprings(writer, document, memberIds);
        WriteJointReleases(writer, document, memberIds);
        WriteLoads(writer, document, nodeIds, memberIds);
        writer.WriteEndObject();
    }

    private static void WriteUnits(Utf8JsonWriter writer)
    {
        writer.WritePropertyName("model_metadata");
        writer.WriteStartObject();
        writer.WritePropertyName("units");
        writer.WriteStartObject();
        writer.WriteString("system", "consistent_user_defined");
        writer.WriteString("length", "m");
        writer.WriteString("force", "kN");
        writer.WriteString("mass", "unspecified");
        writer.WriteString("time", "unspecified");
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WriteNodes(
        Utf8JsonWriter writer,
        ProjectDocument document,
        IReadOnlyDictionary<string, int> nodeIds)
    {
        writer.WritePropertyName("node");
        writer.WriteStartObject();
        foreach (ProjectNode node in document.Nodes.OrderBy(value => nodeIds[value.Id]))
        {
            writer.WritePropertyName(node.Id);
            writer.WriteStartObject();
            writer.WriteNumber("x", node.X);
            writer.WriteNumber("y", node.Y);
            writer.WriteNumber("z", node.Z);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    private static void WriteMembers(
        Utf8JsonWriter writer,
        ProjectDocument document,
        IReadOnlyDictionary<string, int> nodeIds,
        IReadOnlyDictionary<string, int> memberIds,
        IReadOnlyDictionary<string, int> sectionIds)
    {
        writer.WritePropertyName("member");
        writer.WriteStartObject();
        foreach (ProjectMember member in document.Members.OrderBy(value => memberIds[value.Id]))
        {
            writer.WritePropertyName(member.Id);
            writer.WriteStartObject();
            writer.WriteNumber("ni", nodeIds[member.NodeI]);
            writer.WriteNumber("nj", nodeIds[member.NodeJ]);
            writer.WriteNumber("e", sectionIds[member.SectionId!]);
            writer.WriteNumber("cg", member.RotationDegrees);
            writer.WriteBoolean("shear_correction", member.ShearCorrection);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    private static void WriteRigidZones(
        Utf8JsonWriter writer,
        ProjectDocument document,
        IReadOnlyDictionary<string, int> memberIds,
        IReadOnlyDictionary<string, int> sectionIds)
    {
        if (document.RigidZones.Count == 0)
        {
            return;
        }

        writer.WritePropertyName("rigid");
        writer.WriteStartArray();
        foreach (RigidZoneDefinition zone in document.RigidZones
            .OrderBy(value => int.Parse(value.Id, CultureInfo.InvariantCulture)))
        {
            writer.WriteStartObject();
            writer.WriteNumber("m", memberIds[zone.MemberId]);
            writer.WriteNumber("Ilength", zone.ILength);
            writer.WriteNumber("Jlength", zone.JLength);
            writer.WriteNumber("e", sectionIds[zone.SectionId]);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WritePanels(
        Utf8JsonWriter writer,
        ProjectDocument document,
        IReadOnlyDictionary<string, int> nodeIds,
        IReadOnlyDictionary<string, int> sectionIds)
    {
        if (document.Panels.Count == 0)
        {
            return;
        }

        writer.WritePropertyName("shell");
        writer.WriteStartObject();
        foreach (PanelDefinition panel in document.Panels
            .OrderBy(value => int.Parse(value.Id, CultureInfo.InvariantCulture)))
        {
            writer.WritePropertyName(panel.Id);
            writer.WriteStartObject();
            writer.WriteNumber("e", sectionIds[panel.SectionId]);
            writer.WritePropertyName("nodes");
            writer.WriteStartArray();
            foreach (string nodeId in panel.NodeIds)
            {
                writer.WriteNumberValue(nodeIds[nodeId]);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    private static void WriteNoticePoints(
        Utf8JsonWriter writer,
        ProjectDocument document,
        IReadOnlyDictionary<string, int> memberIds)
    {
        if (document.NoticePoints.Count == 0)
        {
            return;
        }

        writer.WritePropertyName("notice_points");
        writer.WriteStartArray();
        foreach (IGrouping<string, NoticePointDefinition> group in document.NoticePoints
            .GroupBy(value => value.MemberId, StringComparer.Ordinal)
            .OrderBy(group => memberIds[group.Key]))
        {
            writer.WriteStartObject();
            writer.WriteNumber("m", memberIds[group.Key]);
            writer.WritePropertyName("Points");
            writer.WriteStartArray();
            foreach (double distance in group.Select(value => value.Distance).Order())
            {
                writer.WriteNumberValue(distance);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteSections(
        Utf8JsonWriter writer,
        ProjectDocument document,
        IReadOnlyDictionary<string, int> sectionIds)
    {
        writer.WritePropertyName("element");
        writer.WriteStartObject();
        WriteSectionTable(writer, "1", document.Sections, sectionIds);
        foreach (ElementPropertySetDefinition set in document.ElementPropertySets
            .OrderBy(value => int.Parse(value.Id, CultureInfo.InvariantCulture)))
        {
            WriteSectionTable(writer, set.Id, set.Sections, ParseIds(set.Sections.Select(value => value.Id), "section"));
        }

        writer.WriteEndObject();
    }

    private static void WriteSectionTable(
        Utf8JsonWriter writer,
        string setId,
        IEnumerable<FrameSectionDefinition> sections,
        IReadOnlyDictionary<string, int> sectionIds)
    {
        writer.WritePropertyName(setId);
        writer.WriteStartObject();
        foreach (FrameSectionDefinition section in sections.OrderBy(value => sectionIds[value.Id]))
        {
            writer.WritePropertyName(section.Id);
            writer.WriteStartObject();
            writer.WriteString("n", section.Name);
            writer.WriteNumber("E", section.YoungsModulus);
            writer.WriteNumber("nu", section.PoissonRatio);
            writer.WriteNumber("G", section.ShearModulus);
            writer.WriteNumber("A", section.Area);
            writer.WriteNumber("Iy", section.MomentOfInertiaY);
            writer.WriteNumber("Iz", section.MomentOfInertiaZ);
            writer.WriteNumber("J", section.TorsionConstant);
            if (section.ThermalExpansionCoefficient != 0)
            {
                writer.WriteNumber("Xp", section.ThermalExpansionCoefficient);
            }

            if (section.Density != 0)
            {
                writer.WriteNumber("den", section.Density);
            }

            if (section.PanelThickness != 0)
            {
                writer.WriteNumber("thickness", section.PanelThickness);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    private static void WriteSupports(
        Utf8JsonWriter writer,
        ProjectDocument document,
        IReadOnlyDictionary<string, int> nodeIds)
    {
        writer.WritePropertyName("fix_node");
        writer.WriteStartObject();
        WriteSupportTable(writer, "1", SupportRowsFor(document, "1"), nodeIds);
        foreach (SupportSetDefinition set in document.SupportSets
            .OrderBy(value => int.Parse(value.Id, CultureInfo.InvariantCulture)))
        {
            WriteSupportTable(writer, set.Id, set.Rows, nodeIds);
        }

        writer.WriteEndObject();
    }

    private static void WriteSupportTable(
        Utf8JsonWriter writer,
        string setId,
        IEnumerable<SupportConditionDefinition> rows,
        IReadOnlyDictionary<string, int> nodeIds)
    {
        writer.WritePropertyName(setId);
        writer.WriteStartArray();
        foreach (SupportConditionDefinition row in rows.OrderBy(value => nodeIds[value.NodeId]))
        {
            writer.WriteStartObject();
            writer.WriteNumber("n", nodeIds[row.NodeId]);
            writer.WriteNumber("tx", Math.Abs(row.Tx));
            writer.WriteNumber("ty", Math.Abs(row.Ty));
            writer.WriteNumber("tz", Math.Abs(row.Tz));
            writer.WriteNumber("rx", Math.Abs(row.Rx));
            writer.WriteNumber("ry", Math.Abs(row.Ry));
            writer.WriteNumber("rz", Math.Abs(row.Rz));
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteMemberSprings(
        Utf8JsonWriter writer,
        ProjectDocument document,
        IReadOnlyDictionary<string, int> memberIds)
    {
        writer.WritePropertyName("fix_member");
        writer.WriteStartObject();
        if (document.MemberSpringSets.All(set => set.Id != "1"))
        {
            WriteMemberSpringTable(writer, "1", [], memberIds);
        }

        foreach (MemberSpringSetDefinition set in document.MemberSpringSets
            .OrderBy(value => int.Parse(value.Id, CultureInfo.InvariantCulture)))
        {
            WriteMemberSpringTable(writer, set.Id, set.Rows, memberIds);
        }

        writer.WriteEndObject();
    }

    private static void WriteMemberSpringTable(
        Utf8JsonWriter writer,
        string setId,
        IEnumerable<MemberSpringDefinition> rows,
        IReadOnlyDictionary<string, int> memberIds)
    {
        writer.WritePropertyName(setId);
        writer.WriteStartArray();
        foreach (MemberSpringDefinition row in rows.OrderBy(value => memberIds[value.MemberId]))
        {
            writer.WriteStartObject();
            writer.WriteNumber("m", memberIds[row.MemberId]);
            writer.WriteNumber("tx", Math.Abs(row.Tx));
            writer.WriteNumber("ty", Math.Abs(row.Ty));
            writer.WriteNumber("tz", Math.Abs(row.Tz));
            writer.WriteNumber("tr", Math.Abs(row.Tr));
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteJointReleases(
        Utf8JsonWriter writer,
        ProjectDocument document,
        IReadOnlyDictionary<string, int> memberIds)
    {
        writer.WritePropertyName("joint");
        writer.WriteStartObject();
        if (document.JointReleaseSets.All(set => set.Id != "1"))
        {
            WriteJointReleaseTable(writer, "1", [], memberIds);
        }

        foreach (JointReleaseSetDefinition set in document.JointReleaseSets
            .OrderBy(value => int.Parse(value.Id, CultureInfo.InvariantCulture)))
        {
            WriteJointReleaseTable(writer, set.Id, set.Rows, memberIds);
        }

        writer.WriteEndObject();
    }

    private static void WriteJointReleaseTable(
        Utf8JsonWriter writer,
        string setId,
        IEnumerable<JointReleaseDefinition> rows,
        IReadOnlyDictionary<string, int> memberIds)
    {
        writer.WritePropertyName(setId);
        writer.WriteStartArray();
        foreach (JointReleaseDefinition row in rows.OrderBy(value => memberIds[value.MemberId]))
        {
            writer.WriteStartObject();
            writer.WriteNumber("m", memberIds[row.MemberId]);
            writer.WriteNumber("xi", row.ConnectXi ? 1 : 0);
            writer.WriteNumber("yi", row.ConnectYi ? 1 : 0);
            writer.WriteNumber("zi", row.ConnectZi ? 1 : 0);
            writer.WriteNumber("xj", row.ConnectXj ? 1 : 0);
            writer.WriteNumber("yj", row.ConnectYj ? 1 : 0);
            writer.WriteNumber("zj", row.ConnectZj ? 1 : 0);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteLoads(
        Utf8JsonWriter writer,
        ProjectDocument document,
        IReadOnlyDictionary<string, int> nodeIds,
        IReadOnlyDictionary<string, int> memberIds)
    {
        writer.WritePropertyName("load");
        writer.WriteStartObject();
        foreach (LoadCaseDefinition loadCase in document.LoadCases
            .OrderBy(value => int.Parse(value.Id, CultureInfo.InvariantCulture)))
        {
            writer.WritePropertyName(loadCase.Id);
            writer.WriteStartObject();
            writer.WriteString("name", loadCase.Name);
            writer.WriteString("symbol", loadCase.Symbol);
            writer.WriteNumber("element", int.Parse(loadCase.ElementSetId, CultureInfo.InvariantCulture));
            writer.WriteNumber("fix_node", int.Parse(loadCase.SupportSetId, CultureInfo.InvariantCulture));
            writer.WriteNumber("fix_member", int.Parse(loadCase.MemberSpringSetId, CultureInfo.InvariantCulture));
            writer.WriteNumber("joint", int.Parse(loadCase.JointSetId, CultureInfo.InvariantCulture));
            if (loadCase.MovingLoadPitch != 0.1)
            {
                writer.WriteNumber("LL_pitch", loadCase.MovingLoadPitch);
            }

            writer.WritePropertyName("load_node");
            writer.WriteStartArray();
            foreach (NodalLoadDefinition load in document.NodalLoads
                .Where(value => value.CaseId == loadCase.Id)
                .OrderBy(value => value.Id, StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteNumber("n", nodeIds[load.NodeId]);
                writer.WriteNumber("tx", load.Fx);
                writer.WriteNumber("ty", load.Fy);
                writer.WriteNumber("tz", load.Fz);
                writer.WriteNumber("rx", load.Mx);
                writer.WriteNumber("ry", load.My);
                writer.WriteNumber("rz", load.Mz);
                writer.WriteEndObject();
            }

            foreach (PrescribedDisplacementDefinition load in document.PrescribedDisplacements
                .Where(value => value.CaseId == loadCase.Id)
                .OrderBy(value => value.Id, StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteNumber("n", nodeIds[load.NodeId]);
                writer.WriteNumber("dx", load.Dx);
                writer.WriteNumber("dy", load.Dy);
                writer.WriteNumber("dz", load.Dz);
                writer.WriteNumber("ax", load.Rx);
                writer.WriteNumber("ay", load.Ry);
                writer.WriteNumber("az", load.Rz);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            MemberLoadDefinition[] memberLoads = document.MemberLoads
                .Where(value => value.CaseId == loadCase.Id)
                .OrderBy(value => value.Id, StringComparer.Ordinal)
                .ToArray();
            if (memberLoads.Length > 0)
            {
                writer.WritePropertyName("load_member");
                writer.WriteStartArray();
                foreach (MemberLoadDefinition load in memberLoads)
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("m", memberIds[load.MemberId]);
                    writer.WriteString("direction", DirectionCode(load.Direction));
                    writer.WriteNumber("mark", Mark(load.Kind));
                    writer.WriteNumber("L1", load.L1);
                    writer.WriteNumber("L2", load.L2);
                    writer.WriteNumber("P1", load.P1);
                    writer.WriteNumber("P2", load.P2);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    private static int Mark(MemberLoadKind kind) => kind switch
    {
        MemberLoadKind.PointForce => 1,
        MemberLoadKind.PointMoment => 11,
        MemberLoadKind.DistributedForce => 2,
        MemberLoadKind.Thermal => 9,
        _ => throw new InvalidOperationException($"Unsupported member-load kind '{kind}'."),
    };

    private static string DirectionCode(MemberLoadDirection direction) => direction switch
    {
        MemberLoadDirection.LocalX => "x",
        MemberLoadDirection.LocalY => "y",
        MemberLoadDirection.LocalZ => "z",
        MemberLoadDirection.GlobalX => "gx",
        MemberLoadDirection.GlobalY => "gy",
        MemberLoadDirection.GlobalZ => "gz",
        _ => throw new InvalidOperationException($"Unsupported member-load direction '{direction}'."),
    };

    private sealed class BoundedMemoryStream(int maxBytes) : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count)
        {
            EnsureCapacity(count);
            base.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            EnsureCapacity(buffer.Length);
            base.Write(buffer);
        }

        private void EnsureCapacity(int count)
        {
            if (count > maxBytes - Length)
            {
                throw new FrameWebAnalysisRequestException(
                    $"Analysis request JSON exceeds the {maxBytes} byte limit.");
            }
        }
    }
}
