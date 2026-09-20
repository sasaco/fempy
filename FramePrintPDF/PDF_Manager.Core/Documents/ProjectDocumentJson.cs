using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PDF_Manager.Core.Documents;

public sealed class ProjectDocumentFormatException : FormatException
{
    public ProjectDocumentFormatException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

public static class ProjectDocumentJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        AllowTrailingCommas = false,
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
    };

    public static byte[] Serialize(ProjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ProjectDocumentValidator.Validate(document);
        return JsonSerializer.SerializeToUtf8Bytes(Map(document), Options);
    }

    public static string SerializeToString(ProjectDocument document)
        => Encoding.UTF8.GetString(Serialize(document));

    public static ProjectDocument Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        return Deserialize(Encoding.UTF8.GetBytes(json));
    }

    public static ProjectDocument Deserialize(ReadOnlySpan<byte> utf8Json)
    {
        if (utf8Json.IsEmpty)
        {
            throw new ProjectDocumentFormatException("Project JSON must not be empty.");
        }

        try
        {
            byte[] bytes = utf8Json.ToArray();
            using JsonDocument parsed = JsonDocument.Parse(bytes, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 128,
            });
            RejectDuplicateProperties(parsed.RootElement, "$", 0);
            ProjectFileWire wire = parsed.RootElement.Deserialize<ProjectFileWire>(Options)
                ?? throw new JsonException("The project document root cannot be null.");
            return Map(wire);
        }
        catch (ProjectDocumentValidationException)
        {
            throw;
        }
        catch (ProjectDocumentFormatException)
        {
            throw;
        }
        catch (Exception exception) when (exception is
            JsonException or
            NotSupportedException or
            OverflowException or
            ArgumentNullException or
            NullReferenceException)
        {
            throw new ProjectDocumentFormatException("The file is not a valid FrameWeb project document.", exception);
        }
    }

    private static ProjectFileWire Map(ProjectDocument document)
    {
        return new ProjectFileWire
        {
            Kind = ProjectDocument.ContractKind,
            SchemaVersion = document.Version,
            Metadata = new MetadataWire
            {
                Name = document.Metadata.Name,
                Description = document.Metadata.Description,
                Author = document.Metadata.Author,
                UnitSystem = document.Metadata.UnitSystem,
            },
            Model = new ModelWire
            {
                Nodes = document.Nodes.OrderBy(node => node.Id, StringComparer.Ordinal).Select(node => new NodeWire
                {
                    Id = node.Id,
                    X = node.X,
                    Y = node.Y,
                    Z = node.Z,
                }).ToList(),
                Sections = document.Sections.OrderBy(section => section.Id, StringComparer.Ordinal).Select(section =>
                    new FrameSectionWire
                    {
                        Id = section.Id,
                        Name = section.Name,
                        YoungsModulus = section.YoungsModulus,
                        PoissonRatio = section.PoissonRatio,
                        ShearModulus = section.ShearModulus,
                        Area = section.Area,
                        MomentOfInertiaY = section.MomentOfInertiaY,
                        MomentOfInertiaZ = section.MomentOfInertiaZ,
                        TorsionConstant = section.TorsionConstant,
                    }).ToList(),
                Members = document.Members.OrderBy(member => member.Id, StringComparer.Ordinal).Select(member => new MemberWire
                {
                    Id = member.Id,
                    NodeI = member.NodeI,
                    NodeJ = member.NodeJ,
                    SectionId = member.SectionId,
                    RotationDegrees = member.RotationDegrees,
                    ShearCorrection = member.ShearCorrection,
                }).ToList(),
                Supports = document.Supports.OrderBy(support => support.Id, StringComparer.Ordinal).Select(support => new SupportWire
                {
                    Id = support.Id,
                    NodeId = support.NodeId,
                    FixX = support.FixX,
                    FixY = support.FixY,
                    FixZ = support.FixZ,
                    FixRx = support.FixRx,
                    FixRy = support.FixRy,
                    FixRz = support.FixRz,
                }).ToList(),
            },
            Loads = new LoadsWire
            {
                Cases = document.LoadCases.OrderBy(loadCase => loadCase.Id, StringComparer.Ordinal).Select(loadCase => new LoadCaseWire
                {
                    Id = loadCase.Id,
                    Name = loadCase.Name,
                    Symbol = loadCase.Symbol,
                }).ToList(),
                NodalLoads = document.NodalLoads.OrderBy(load => load.Id, StringComparer.Ordinal).Select(load => new NodalLoadWire
                {
                    Id = load.Id,
                    CaseId = load.CaseId,
                    NodeId = load.NodeId,
                    Fx = load.Fx,
                    Fy = load.Fy,
                    Fz = load.Fz,
                    Mx = load.Mx,
                    My = load.My,
                    Mz = load.Mz,
                }).ToList(),
            },
            DerivedResults = document.DerivedResults.Select(result => new DerivedResultWire
            {
                Id = result.Id,
                Name = result.Name,
                Kind = Format(result.Kind),
                Terms = result.Terms.Select(term => new DerivedResultTermWire
                {
                    SourceId = term.SourceId,
                    Factor = term.Factor,
                }).ToList(),
            }).ToList(),
            MovingLoads = document.MovingLoads.OrderBy(load => load.Id, StringComparer.Ordinal).Select(load => new MovingLoadWire
            {
                Id = load.Id,
                Name = load.Name,
                CaseIds = load.CaseIds.ToList(),
            }).ToList(),
        };
    }

    private static ProjectDocument Map(ProjectFileWire wire)
    {
        if (wire.Kind != ProjectDocument.ContractKind)
        {
            throw new ProjectDocumentFormatException(
                $"Project kind '{wire.Kind}' is not supported.");
        }

        return new ProjectDocument(
            wire.SchemaVersion,
            new ProjectMetadata(
                wire.Metadata.Name,
                wire.Metadata.Description,
                wire.Metadata.Author,
                wire.Metadata.UnitSystem),
            wire.Model.Nodes.Select(node => new ProjectNode(node.Id, node.X, node.Y, node.Z)),
            wire.Model.Members.Select(member => new ProjectMember(
                member.Id,
                member.NodeI,
                member.NodeJ,
                member.SectionId,
                member.RotationDegrees,
                member.ShearCorrection)),
            wire.Model.Supports.Select(support => new ProjectSupport(
                support.Id,
                support.NodeId,
                support.FixX,
                support.FixY,
                support.FixZ,
                support.FixRx,
                support.FixRy,
                support.FixRz)),
            wire.Loads.Cases.Select(loadCase => new LoadCaseDefinition(
                loadCase.Id,
                loadCase.Name,
                loadCase.Symbol)),
            wire.Loads.NodalLoads.Select(load => new NodalLoadDefinition(
                load.Id,
                load.CaseId,
                load.NodeId,
                load.Fx,
                load.Fy,
                load.Fz,
                load.Mx,
                load.My,
                load.Mz)),
            wire.DerivedResults.Select(result => new DerivedResultDefinition(
                result.Id,
                result.Name,
                Parse(result.Kind),
                result.Terms.Select(term => new DerivedResultTerm(term.SourceId, term.Factor)))),
            wire.MovingLoads.Select(load => new MovingLoadDefinition(load.Id, load.Name, load.CaseIds)),
            ProjectSelection.Empty,
            isDirty: false,
            sections: (wire.Model.Sections ?? []).Select(section => new FrameSectionDefinition(
                section.Id,
                section.Name,
                section.YoungsModulus,
                section.PoissonRatio,
                section.ShearModulus,
                section.Area,
                section.MomentOfInertiaY,
                section.MomentOfInertiaZ,
                section.TorsionConstant)));
    }

    private static string Format(DerivedResultKind kind) => kind switch
    {
        DerivedResultKind.Define => "define",
        DerivedResultKind.Combine => "combine",
        DerivedResultKind.Pickup => "pickup",
        _ => throw new ProjectDocumentValidationException("Unsupported derived-result kind."),
    };

    private static DerivedResultKind Parse(string kind) => kind switch
    {
        "define" => DerivedResultKind.Define,
        "combine" => DerivedResultKind.Combine,
        "pickup" => DerivedResultKind.Pickup,
        _ => throw new ProjectDocumentFormatException($"Derived-result kind '{kind}' is not supported."),
    };

    private static void RejectDuplicateProperties(JsonElement element, string path, int depth)
    {
        if (depth > 128)
        {
            throw new ProjectDocumentFormatException("The JSON nesting depth exceeds 128.");
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new ProjectDocumentFormatException(
                        $"Duplicate JSON member '{property.Name}' at {path} is not allowed.");
                }

                RejectDuplicateProperties(property.Value, $"{path}.{property.Name}", depth + 1);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            int index = 0;
            foreach (JsonElement item in element.EnumerateArray())
            {
                RejectDuplicateProperties(item, $"{path}[{index}]", depth + 1);
                index++;
            }
        }
    }

    private sealed class ProjectFileWire
    {
        [JsonPropertyName("kind")]
        public required string Kind { get; init; }

        [JsonPropertyName("schema_version")]
        public required int SchemaVersion { get; init; }

        [JsonPropertyName("metadata")]
        public required MetadataWire Metadata { get; init; }

        [JsonPropertyName("model")]
        public required ModelWire Model { get; init; }

        [JsonPropertyName("loads")]
        public required LoadsWire Loads { get; init; }

        [JsonPropertyName("derived_results")]
        public required List<DerivedResultWire> DerivedResults { get; init; }

        [JsonPropertyName("moving_loads")]
        public required List<MovingLoadWire> MovingLoads { get; init; }
    }

    private sealed class MetadataWire
    {
        [JsonPropertyName("name")]
        public required string Name { get; init; }

        [JsonPropertyName("description")]
        public required string Description { get; init; }

        [JsonPropertyName("author")]
        public required string Author { get; init; }

        [JsonPropertyName("unit_system")]
        public required string UnitSystem { get; init; }
    }

    private sealed class ModelWire
    {
        [JsonPropertyName("nodes")]
        public required List<NodeWire> Nodes { get; init; }

        [JsonPropertyName("sections")]
        public List<FrameSectionWire>? Sections { get; init; }

        [JsonPropertyName("members")]
        public required List<MemberWire> Members { get; init; }

        [JsonPropertyName("supports")]
        public required List<SupportWire> Supports { get; init; }
    }

    private sealed class NodeWire
    {
        [JsonPropertyName("id")]
        public required string Id { get; init; }

        [JsonPropertyName("x")]
        public required double X { get; init; }

        [JsonPropertyName("y")]
        public required double Y { get; init; }

        [JsonPropertyName("z")]
        public required double Z { get; init; }
    }

    private sealed class MemberWire
    {
        [JsonPropertyName("id")]
        public required string Id { get; init; }

        [JsonPropertyName("node_i")]
        public required string NodeI { get; init; }

        [JsonPropertyName("node_j")]
        public required string NodeJ { get; init; }

        [JsonPropertyName("section_id")]
        public string? SectionId { get; init; }

        [JsonPropertyName("rotation_degrees")]
        public double RotationDegrees { get; init; }

        [JsonPropertyName("shear_correction")]
        public bool ShearCorrection { get; init; }
    }

    private sealed class FrameSectionWire
    {
        [JsonPropertyName("id")]
        public required string Id { get; init; }

        [JsonPropertyName("name")]
        public required string Name { get; init; }

        [JsonPropertyName("youngs_modulus")]
        public required double YoungsModulus { get; init; }

        [JsonPropertyName("poisson_ratio")]
        public required double PoissonRatio { get; init; }

        [JsonPropertyName("shear_modulus")]
        public required double ShearModulus { get; init; }

        [JsonPropertyName("area")]
        public required double Area { get; init; }

        [JsonPropertyName("moment_of_inertia_y")]
        public required double MomentOfInertiaY { get; init; }

        [JsonPropertyName("moment_of_inertia_z")]
        public required double MomentOfInertiaZ { get; init; }

        [JsonPropertyName("torsion_constant")]
        public required double TorsionConstant { get; init; }
    }

    private sealed class SupportWire
    {
        [JsonPropertyName("id")]
        public required string Id { get; init; }

        [JsonPropertyName("node_id")]
        public required string NodeId { get; init; }

        [JsonPropertyName("fix_x")]
        public required bool FixX { get; init; }

        [JsonPropertyName("fix_y")]
        public required bool FixY { get; init; }

        [JsonPropertyName("fix_z")]
        public required bool FixZ { get; init; }

        [JsonPropertyName("fix_rx")]
        public required bool FixRx { get; init; }

        [JsonPropertyName("fix_ry")]
        public required bool FixRy { get; init; }

        [JsonPropertyName("fix_rz")]
        public required bool FixRz { get; init; }
    }

    private sealed class LoadsWire
    {
        [JsonPropertyName("cases")]
        public required List<LoadCaseWire> Cases { get; init; }

        [JsonPropertyName("nodal_loads")]
        public required List<NodalLoadWire> NodalLoads { get; init; }
    }

    private sealed class LoadCaseWire
    {
        [JsonPropertyName("id")]
        public required string Id { get; init; }

        [JsonPropertyName("name")]
        public required string Name { get; init; }

        [JsonPropertyName("symbol")]
        public required string Symbol { get; init; }
    }

    private sealed class NodalLoadWire
    {
        [JsonPropertyName("id")]
        public required string Id { get; init; }

        [JsonPropertyName("case_id")]
        public required string CaseId { get; init; }

        [JsonPropertyName("node_id")]
        public required string NodeId { get; init; }

        [JsonPropertyName("fx")]
        public required double Fx { get; init; }

        [JsonPropertyName("fy")]
        public required double Fy { get; init; }

        [JsonPropertyName("fz")]
        public required double Fz { get; init; }

        [JsonPropertyName("mx")]
        public required double Mx { get; init; }

        [JsonPropertyName("my")]
        public required double My { get; init; }

        [JsonPropertyName("mz")]
        public required double Mz { get; init; }
    }

    private sealed class DerivedResultWire
    {
        [JsonPropertyName("id")]
        public required string Id { get; init; }

        [JsonPropertyName("name")]
        public required string Name { get; init; }

        [JsonPropertyName("kind")]
        public required string Kind { get; init; }

        [JsonPropertyName("terms")]
        public required List<DerivedResultTermWire> Terms { get; init; }
    }

    private sealed class DerivedResultTermWire
    {
        [JsonPropertyName("source_id")]
        public required string SourceId { get; init; }

        [JsonPropertyName("factor")]
        public required double Factor { get; init; }
    }

    private sealed class MovingLoadWire
    {
        [JsonPropertyName("id")]
        public required string Id { get; init; }

        [JsonPropertyName("name")]
        public required string Name { get; init; }

        [JsonPropertyName("case_ids")]
        public required List<string> CaseIds { get; init; }
    }
}
