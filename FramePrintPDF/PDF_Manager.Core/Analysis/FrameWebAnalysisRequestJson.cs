using System.Globalization;
using System.Text.Json;
using PDF_Manager.Core.Documents;

namespace PDF_Manager.Core.Analysis;

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
            document.Members.Count +
            document.Supports.Count +
            document.LoadCases.Count +
            document.NodalLoads.Count);
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

        if (nodeIds.Count < 2 || memberIds.Count < 1 || sectionIds.Count < 1)
        {
            throw new FrameWebAnalysisRequestException(
                "Analysis requires at least two nodes, one member, and one persisted frame section.");
        }

        foreach (ProjectMember member in document.Members)
        {
            if (string.IsNullOrWhiteSpace(member.SectionId) || !sectionIds.ContainsKey(member.SectionId))
            {
                throw new FrameWebAnalysisRequestException(
                    $"Member '{member.Id}' must reference a persisted frame section before analysis.");
            }
        }

        if (document.Supports.Count < 1 || document.Supports.All(value =>
            !value.FixX && !value.FixY && !value.FixZ && !value.FixRx && !value.FixRy && !value.FixRz))
        {
            throw new FrameWebAnalysisRequestException(
                "Analysis requires at least one support with a restrained degree of freedom.");
        }

        string duplicateSupportNode = document.Supports
            .GroupBy(value => value.NodeId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)?.Key ?? string.Empty;
        if (duplicateSupportNode.Length > 0)
        {
            throw new FrameWebAnalysisRequestException(
                $"Multiple support rows for node '{duplicateSupportNode}' are not supported by the MVP request contract.");
        }
    }

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
        WriteNodes(writer, document, nodeIds);
        WriteMembers(writer, document, nodeIds, memberIds, sectionIds);
        WriteSections(writer, document, sectionIds);
        WriteSupports(writer, document, nodeIds);
        WriteEmptyMemberBoundaryTables(writer);
        WriteLoads(writer, document, nodeIds);
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

    private static void WriteSections(
        Utf8JsonWriter writer,
        ProjectDocument document,
        IReadOnlyDictionary<string, int> sectionIds)
    {
        writer.WritePropertyName("element");
        writer.WriteStartObject();
        writer.WritePropertyName("1");
        writer.WriteStartObject();
        foreach (FrameSectionDefinition section in document.Sections.OrderBy(value => sectionIds[value.Id]))
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
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WriteSupports(
        Utf8JsonWriter writer,
        ProjectDocument document,
        IReadOnlyDictionary<string, int> nodeIds)
    {
        writer.WritePropertyName("fix_node");
        writer.WriteStartObject();
        writer.WritePropertyName("1");
        writer.WriteStartArray();
        foreach (ProjectSupport support in document.Supports.OrderBy(value => nodeIds[value.NodeId]))
        {
            writer.WriteStartObject();
            writer.WriteNumber("n", nodeIds[support.NodeId]);
            writer.WriteNumber("tx", support.FixX ? 1 : 0);
            writer.WriteNumber("ty", support.FixY ? 1 : 0);
            writer.WriteNumber("tz", support.FixZ ? 1 : 0);
            writer.WriteNumber("rx", support.FixRx ? 1 : 0);
            writer.WriteNumber("ry", support.FixRy ? 1 : 0);
            writer.WriteNumber("rz", support.FixRz ? 1 : 0);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteEmptyMemberBoundaryTables(Utf8JsonWriter writer)
    {
        foreach (string name in new[] { "fix_member", "joint" })
        {
            writer.WritePropertyName(name);
            writer.WriteStartObject();
            writer.WritePropertyName("1");
            writer.WriteStartArray();
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
    }

    private static void WriteLoads(
        Utf8JsonWriter writer,
        ProjectDocument document,
        IReadOnlyDictionary<string, int> nodeIds)
    {
        writer.WritePropertyName("load");
        writer.WriteStartObject();
        foreach (LoadCaseDefinition loadCase in document.LoadCases.OrderBy(value => value.Id, StringComparer.Ordinal))
        {
            writer.WritePropertyName(loadCase.Id);
            writer.WriteStartObject();
            writer.WriteString("name", loadCase.Name);
            writer.WriteString("symbol", loadCase.Symbol);
            writer.WriteNumber("element", 1);
            writer.WriteNumber("fix_node", 1);
            writer.WriteNumber("fix_member", 1);
            writer.WriteNumber("joint", 1);
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

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

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
