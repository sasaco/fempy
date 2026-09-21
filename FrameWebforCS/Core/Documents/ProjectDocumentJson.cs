using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FrameWebforCS.Core.Documents;

public sealed class ProjectDocumentFormatException : FormatException
{
    public ProjectDocumentFormatException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

public static class ProjectDocumentJson
{
    public const int DefaultMaxJsonBytes = 16 * 1024 * 1024;
    public const int DefaultMaxEntityCount = 100_000;

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
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(Map(document), Options);
        if (bytes.Length > DefaultMaxJsonBytes)
        {
            throw new ProjectDocumentFormatException(
                $"Project JSON exceeds the {DefaultMaxJsonBytes} byte limit.");
        }

        return bytes;
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

        if (utf8Json.Length > DefaultMaxJsonBytes)
        {
            throw new ProjectDocumentFormatException(
                $"Project JSON exceeds the {DefaultMaxJsonBytes} byte limit.");
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
            ValidateEntityBudget(wire);
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
                Dimension = Format(document.Dimension),
                Nodes = document.Nodes.OrderBy(node => node.Id, StringComparer.Ordinal).Select(node => new NodeWire
                {
                    Id = node.Id,
                    X = node.X,
                    Y = node.Y,
                    Z = node.Z,
                }).ToList(),
                Sections = document.Sections.OrderBy(section => section.Id, StringComparer.Ordinal)
                    .Select(Map).ToList(),
                ElementPropertySets = document.ElementPropertySets
                    .OrderBy(set => set.Id, StringComparer.Ordinal)
                    .Select(set => new ElementPropertySetWire
                    {
                        Id = set.Id,
                        Name = set.Name,
                        Sections = set.Sections.OrderBy(section => section.Id, StringComparer.Ordinal)
                            .Select(Map).ToList(),
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
                RigidZones = document.RigidZones.OrderBy(value => value.Id, StringComparer.Ordinal)
                    .Select(value => new RigidZoneWire
                    {
                        Id = value.Id,
                        MemberId = value.MemberId,
                        ILength = value.ILength,
                        JLength = value.JLength,
                        SectionId = value.SectionId,
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
                SupportSets = document.SupportSets.OrderBy(set => set.Id, StringComparer.Ordinal)
                    .Select(set => new SupportSetWire
                    {
                        Id = set.Id,
                        Name = set.Name,
                        Rows = set.Rows.OrderBy(value => value.Id, StringComparer.Ordinal)
                            .Select(value => new SupportConditionWire
                            {
                                Id = value.Id,
                                NodeId = value.NodeId,
                                Tx = value.Tx,
                                Ty = value.Ty,
                                Tz = value.Tz,
                                Rx = value.Rx,
                                Ry = value.Ry,
                                Rz = value.Rz,
                            }).ToList(),
                    }).ToList(),
                Panels = document.Panels.OrderBy(value => value.Id, StringComparer.Ordinal)
                    .Select(value => new PanelWire
                    {
                        Id = value.Id,
                        SectionId = value.SectionId,
                        NodeIds = value.NodeIds.ToList(),
                    }).ToList(),
                JointReleaseSets = document.JointReleaseSets.OrderBy(set => set.Id, StringComparer.Ordinal)
                    .Select(set => new JointReleaseSetWire
                    {
                        Id = set.Id,
                        Name = set.Name,
                        Rows = set.Rows.OrderBy(value => value.Id, StringComparer.Ordinal)
                            .Select(value => new JointReleaseWire
                            {
                                Id = value.Id,
                                MemberId = value.MemberId,
                                ConnectXi = value.ConnectXi,
                                ConnectYi = value.ConnectYi,
                                ConnectZi = value.ConnectZi,
                                ConnectXj = value.ConnectXj,
                                ConnectYj = value.ConnectYj,
                                ConnectZj = value.ConnectZj,
                            }).ToList(),
                    }).ToList(),
                NoticePoints = document.NoticePoints.OrderBy(value => value.Id, StringComparer.Ordinal)
                    .Select(value => new NoticePointWire
                    {
                        Id = value.Id,
                        MemberId = value.MemberId,
                        Distance = value.Distance,
                    }).ToList(),
                MemberSpringSets = document.MemberSpringSets.OrderBy(set => set.Id, StringComparer.Ordinal)
                    .Select(set => new MemberSpringSetWire
                    {
                        Id = set.Id,
                        Name = set.Name,
                        Rows = set.Rows.OrderBy(value => value.Id, StringComparer.Ordinal)
                            .Select(value => new MemberSpringWire
                            {
                                Id = value.Id,
                                MemberId = value.MemberId,
                                Tx = value.Tx,
                                Ty = value.Ty,
                                Tz = value.Tz,
                                Tr = value.Tr,
                            }).ToList(),
                    }).ToList(),
            },
            Loads = new LoadsWire
            {
                Cases = document.LoadCases.OrderBy(loadCase => loadCase.Id, StringComparer.Ordinal).Select(loadCase => new LoadCaseWire
                {
                    Id = loadCase.Id,
                    Name = loadCase.Name,
                    Symbol = loadCase.Symbol,
                    ElementSetId = loadCase.ElementSetId,
                    SupportSetId = loadCase.SupportSetId,
                    MemberSpringSetId = loadCase.MemberSpringSetId,
                    JointSetId = loadCase.JointSetId,
                    MovingLoadPitch = loadCase.MovingLoadPitch,
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
                PrescribedDisplacements = document.PrescribedDisplacements
                    .OrderBy(value => value.Id, StringComparer.Ordinal)
                    .Select(value => new PrescribedDisplacementWire
                    {
                        Id = value.Id,
                        CaseId = value.CaseId,
                        NodeId = value.NodeId,
                        Dx = value.Dx,
                        Dy = value.Dy,
                        Dz = value.Dz,
                        Rx = value.Rx,
                        Ry = value.Ry,
                        Rz = value.Rz,
                    }).ToList(),
                MemberLoads = document.MemberLoads.OrderBy(value => value.Id, StringComparer.Ordinal)
                    .Select(value => new MemberLoadWire
                    {
                        Id = value.Id,
                        CaseId = value.CaseId,
                        MemberId = value.MemberId,
                        Kind = Format(value.Kind),
                        Direction = Format(value.Direction),
                        L1 = value.L1,
                        L2 = value.L2,
                        P1 = value.P1,
                        P2 = value.P2,
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
                loadCase.Symbol,
                loadCase.ElementSetId
                    ?? throw new ProjectDocumentFormatException("Load-case element_set_id cannot be null."),
                loadCase.SupportSetId
                    ?? throw new ProjectDocumentFormatException("Load-case support_set_id cannot be null."),
                loadCase.MemberSpringSetId
                    ?? throw new ProjectDocumentFormatException("Load-case member_spring_set_id cannot be null."),
                loadCase.JointSetId
                    ?? throw new ProjectDocumentFormatException("Load-case joint_set_id cannot be null."),
                loadCase.MovingLoadPitch)),
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
            sections: RequireCollection(wire.Model.Sections, "model.sections").Select(Map),
            dimension: ParseDimension(wire.Model.Dimension),
            elementPropertySets: RequireCollection(
                wire.Model.ElementPropertySets,
                "model.element_property_sets").Select(set =>
                new ElementPropertySetDefinition(set.Id, set.Name, set.Sections.Select(Map))),
            rigidZones: RequireCollection(wire.Model.RigidZones, "model.rigid_zones").Select(value =>
                new RigidZoneDefinition(
                value.Id,
                value.MemberId,
                value.ILength,
                value.JLength,
                value.SectionId)),
            supportSets: RequireCollection(wire.Model.SupportSets, "model.support_sets").Select(set =>
                new SupportSetDefinition(
                set.Id,
                set.Name,
                set.Rows.Select(value => new SupportConditionDefinition(
                    value.Id,
                    value.NodeId,
                    value.Tx,
                    value.Ty,
                    value.Tz,
                    value.Rx,
                    value.Ry,
                    value.Rz)))),
            panels: RequireCollection(wire.Model.Panels, "model.panels").Select(value =>
                new PanelDefinition(value.Id, value.SectionId, value.NodeIds)),
            jointReleaseSets: RequireCollection(
                wire.Model.JointReleaseSets,
                "model.joint_release_sets").Select(set =>
                new JointReleaseSetDefinition(
                    set.Id,
                    set.Name,
                    set.Rows.Select(value => new JointReleaseDefinition(
                        value.Id,
                        value.MemberId,
                        value.ConnectXi,
                        value.ConnectYi,
                        value.ConnectZi,
                        value.ConnectXj,
                        value.ConnectYj,
                        value.ConnectZj)))),
            noticePoints: RequireCollection(wire.Model.NoticePoints, "model.notice_points").Select(value =>
                new NoticePointDefinition(value.Id, value.MemberId, value.Distance)),
            memberSpringSets: RequireCollection(
                wire.Model.MemberSpringSets,
                "model.member_spring_sets").Select(set =>
                new MemberSpringSetDefinition(
                    set.Id,
                    set.Name,
                    set.Rows.Select(value => new MemberSpringDefinition(
                        value.Id,
                        value.MemberId,
                        value.Tx,
                        value.Ty,
                        value.Tz,
                        value.Tr)))),
            prescribedDisplacements: RequireCollection(
                wire.Loads.PrescribedDisplacements,
                "loads.prescribed_displacements").Select(value =>
                new PrescribedDisplacementDefinition(
                    value.Id,
                    value.CaseId,
                    value.NodeId,
                    value.Dx,
                    value.Dy,
                    value.Dz,
                    value.Rx,
                    value.Ry,
                    value.Rz)),
            memberLoads: RequireCollection(wire.Loads.MemberLoads, "loads.member_loads").Select(value =>
                new MemberLoadDefinition(
                value.Id,
                value.CaseId,
                value.MemberId,
                ParseMemberLoadKind(value.Kind),
                ParseMemberLoadDirection(value.Direction),
                value.L1,
                value.L2,
                value.P1,
                value.P2)));
    }

    private static IReadOnlyList<T> RequireCollection<T>(List<T>? values, string path)
        => values ?? throw new ProjectDocumentFormatException($"Project member '{path}' cannot be null.");

    private static FrameSectionWire Map(FrameSectionDefinition section) => new()
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
        ThermalExpansionCoefficient = section.ThermalExpansionCoefficient,
        Density = section.Density,
        PanelThickness = section.PanelThickness,
    };

    private static FrameSectionDefinition Map(FrameSectionWire section) => new(
        section.Id,
        section.Name,
        section.YoungsModulus,
        section.PoissonRatio,
        section.ShearModulus,
        section.Area,
        section.MomentOfInertiaY,
        section.MomentOfInertiaZ,
        section.TorsionConstant,
        section.ThermalExpansionCoefficient,
        section.Density,
        section.PanelThickness);

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

    private static string Format(ModelDimension value) => value switch
    {
        ModelDimension.TwoDimensional => "2d",
        ModelDimension.ThreeDimensional => "3d",
        _ => throw new ProjectDocumentValidationException("Unsupported model dimension."),
    };

    private static ModelDimension ParseDimension(string value) => value switch
    {
        "2d" => ModelDimension.TwoDimensional,
        "3d" => ModelDimension.ThreeDimensional,
        _ => throw new ProjectDocumentFormatException($"Model dimension '{value}' is not supported."),
    };

    private static string Format(MemberLoadKind value) => value switch
    {
        MemberLoadKind.PointForce => "point_force",
        MemberLoadKind.PointMoment => "point_moment",
        MemberLoadKind.DistributedForce => "distributed_force",
        MemberLoadKind.Thermal => "thermal",
        _ => throw new ProjectDocumentValidationException("Unsupported member-load kind."),
    };

    private static MemberLoadKind ParseMemberLoadKind(string value) => value switch
    {
        "point_force" => MemberLoadKind.PointForce,
        "point_moment" => MemberLoadKind.PointMoment,
        "distributed_force" => MemberLoadKind.DistributedForce,
        "thermal" => MemberLoadKind.Thermal,
        _ => throw new ProjectDocumentFormatException($"Member-load kind '{value}' is not supported."),
    };

    private static string Format(MemberLoadDirection value) => value switch
    {
        MemberLoadDirection.LocalX => "local_x",
        MemberLoadDirection.LocalY => "local_y",
        MemberLoadDirection.LocalZ => "local_z",
        MemberLoadDirection.GlobalX => "global_x",
        MemberLoadDirection.GlobalY => "global_y",
        MemberLoadDirection.GlobalZ => "global_z",
        _ => throw new ProjectDocumentValidationException("Unsupported member-load direction."),
    };

    private static MemberLoadDirection ParseMemberLoadDirection(string value) => value switch
    {
        "local_x" => MemberLoadDirection.LocalX,
        "local_y" => MemberLoadDirection.LocalY,
        "local_z" => MemberLoadDirection.LocalZ,
        "global_x" => MemberLoadDirection.GlobalX,
        "global_y" => MemberLoadDirection.GlobalY,
        "global_z" => MemberLoadDirection.GlobalZ,
        _ => throw new ProjectDocumentFormatException($"Member-load direction '{value}' is not supported."),
    };

    private static void ValidateEntityBudget(ProjectFileWire wire)
    {
        int count = 0;
        void Add(int value) => count = checked(count + value);

        Add(wire.Model.Nodes.Count);
        Add(wire.Model.Sections?.Count ?? 0);
        Add(wire.Model.ElementPropertySets?.Count ?? 0);
        foreach (ElementPropertySetWire set in wire.Model.ElementPropertySets ?? [])
        {
            Add(set.Sections.Count);
        }

        Add(wire.Model.Members.Count);
        Add(wire.Model.RigidZones?.Count ?? 0);
        Add(wire.Model.Supports.Count);
        Add(wire.Model.SupportSets?.Count ?? 0);
        foreach (SupportSetWire set in wire.Model.SupportSets ?? [])
        {
            Add(set.Rows.Count);
        }

        Add(wire.Model.Panels?.Count ?? 0);
        Add(wire.Model.JointReleaseSets?.Count ?? 0);
        foreach (JointReleaseSetWire set in wire.Model.JointReleaseSets ?? [])
        {
            Add(set.Rows.Count);
        }

        Add(wire.Model.NoticePoints?.Count ?? 0);
        Add(wire.Model.MemberSpringSets?.Count ?? 0);
        foreach (MemberSpringSetWire set in wire.Model.MemberSpringSets ?? [])
        {
            Add(set.Rows.Count);
        }

        Add(wire.Loads.Cases.Count);
        Add(wire.Loads.NodalLoads.Count);
        Add(wire.Loads.PrescribedDisplacements?.Count ?? 0);
        Add(wire.Loads.MemberLoads?.Count ?? 0);
        Add(wire.DerivedResults.Count);
        foreach (DerivedResultWire result in wire.DerivedResults)
        {
            Add(result.Terms.Count);
        }

        Add(wire.MovingLoads.Count);
        if (count > DefaultMaxEntityCount)
        {
            throw new ProjectDocumentFormatException(
                $"Project JSON contains {count} entities; the limit is {DefaultMaxEntityCount}.");
        }
    }

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
        [JsonPropertyName("dimension")]
        public string Dimension { get; init; } = "3d";

        [JsonPropertyName("nodes")]
        public required List<NodeWire> Nodes { get; init; }

        [JsonPropertyName("sections")]
        public List<FrameSectionWire>? Sections { get; init; } = [];

        [JsonPropertyName("element_property_sets")]
        public List<ElementPropertySetWire>? ElementPropertySets { get; init; } = [];

        [JsonPropertyName("members")]
        public required List<MemberWire> Members { get; init; }

        [JsonPropertyName("rigid_zones")]
        public List<RigidZoneWire>? RigidZones { get; init; } = [];

        [JsonPropertyName("supports")]
        public required List<SupportWire> Supports { get; init; }

        [JsonPropertyName("support_sets")]
        public List<SupportSetWire>? SupportSets { get; init; } = [];

        [JsonPropertyName("panels")]
        public List<PanelWire>? Panels { get; init; } = [];

        [JsonPropertyName("joint_release_sets")]
        public List<JointReleaseSetWire>? JointReleaseSets { get; init; } = [];

        [JsonPropertyName("notice_points")]
        public List<NoticePointWire>? NoticePoints { get; init; } = [];

        [JsonPropertyName("member_spring_sets")]
        public List<MemberSpringSetWire>? MemberSpringSets { get; init; } = [];
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

        [JsonPropertyName("thermal_expansion_coefficient")]
        public double ThermalExpansionCoefficient { get; init; }

        [JsonPropertyName("density")]
        public double Density { get; init; }

        [JsonPropertyName("panel_thickness")]
        public double PanelThickness { get; init; }
    }

    private sealed class ElementPropertySetWire
    {
        [JsonPropertyName("id")]
        public required string Id { get; init; }

        [JsonPropertyName("name")]
        public required string Name { get; init; }

        [JsonPropertyName("sections")]
        public required List<FrameSectionWire> Sections { get; init; }
    }

    private sealed class RigidZoneWire
    {
        [JsonPropertyName("id")]
        public required string Id { get; init; }

        [JsonPropertyName("member_id")]
        public required string MemberId { get; init; }

        [JsonPropertyName("i_length")]
        public required double ILength { get; init; }

        [JsonPropertyName("j_length")]
        public required double JLength { get; init; }

        [JsonPropertyName("section_id")]
        public required string SectionId { get; init; }
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

    private sealed class SupportSetWire
    {
        [JsonPropertyName("id")]
        public required string Id { get; init; }

        [JsonPropertyName("name")]
        public required string Name { get; init; }

        [JsonPropertyName("rows")]
        public required List<SupportConditionWire> Rows { get; init; }
    }

    private sealed class SupportConditionWire
    {
        [JsonPropertyName("id")]
        public required string Id { get; init; }

        [JsonPropertyName("node_id")]
        public required string NodeId { get; init; }

        [JsonPropertyName("tx")]
        public required double Tx { get; init; }

        [JsonPropertyName("ty")]
        public required double Ty { get; init; }

        [JsonPropertyName("tz")]
        public required double Tz { get; init; }

        [JsonPropertyName("rx")]
        public required double Rx { get; init; }

        [JsonPropertyName("ry")]
        public required double Ry { get; init; }

        [JsonPropertyName("rz")]
        public required double Rz { get; init; }
    }

    private sealed class PanelWire
    {
        [JsonPropertyName("id")]
        public required string Id { get; init; }

        [JsonPropertyName("section_id")]
        public required string SectionId { get; init; }

        [JsonPropertyName("node_ids")]
        public required List<string> NodeIds { get; init; }
    }

    private sealed class JointReleaseSetWire
    {
        [JsonPropertyName("id")]
        public required string Id { get; init; }

        [JsonPropertyName("name")]
        public required string Name { get; init; }

        [JsonPropertyName("rows")]
        public required List<JointReleaseWire> Rows { get; init; }
    }

    private sealed class JointReleaseWire
    {
        [JsonPropertyName("id")]
        public required string Id { get; init; }

        [JsonPropertyName("member_id")]
        public required string MemberId { get; init; }

        [JsonPropertyName("connect_xi")]
        public required bool ConnectXi { get; init; }

        [JsonPropertyName("connect_yi")]
        public required bool ConnectYi { get; init; }

        [JsonPropertyName("connect_zi")]
        public required bool ConnectZi { get; init; }

        [JsonPropertyName("connect_xj")]
        public required bool ConnectXj { get; init; }

        [JsonPropertyName("connect_yj")]
        public required bool ConnectYj { get; init; }

        [JsonPropertyName("connect_zj")]
        public required bool ConnectZj { get; init; }
    }

    private sealed class NoticePointWire
    {
        [JsonPropertyName("id")]
        public required string Id { get; init; }

        [JsonPropertyName("member_id")]
        public required string MemberId { get; init; }

        [JsonPropertyName("distance")]
        public required double Distance { get; init; }
    }

    private sealed class MemberSpringSetWire
    {
        [JsonPropertyName("id")]
        public required string Id { get; init; }

        [JsonPropertyName("name")]
        public required string Name { get; init; }

        [JsonPropertyName("rows")]
        public required List<MemberSpringWire> Rows { get; init; }
    }

    private sealed class MemberSpringWire
    {
        [JsonPropertyName("id")]
        public required string Id { get; init; }

        [JsonPropertyName("member_id")]
        public required string MemberId { get; init; }

        [JsonPropertyName("tx")]
        public required double Tx { get; init; }

        [JsonPropertyName("ty")]
        public required double Ty { get; init; }

        [JsonPropertyName("tz")]
        public required double Tz { get; init; }

        [JsonPropertyName("tr")]
        public required double Tr { get; init; }
    }

    private sealed class LoadsWire
    {
        [JsonPropertyName("cases")]
        public required List<LoadCaseWire> Cases { get; init; }

        [JsonPropertyName("nodal_loads")]
        public required List<NodalLoadWire> NodalLoads { get; init; }

        [JsonPropertyName("prescribed_displacements")]
        public List<PrescribedDisplacementWire>? PrescribedDisplacements { get; init; } = [];

        [JsonPropertyName("member_loads")]
        public List<MemberLoadWire>? MemberLoads { get; init; } = [];
    }

    private sealed class LoadCaseWire
    {
        [JsonPropertyName("id")]
        public required string Id { get; init; }

        [JsonPropertyName("name")]
        public required string Name { get; init; }

        [JsonPropertyName("symbol")]
        public required string Symbol { get; init; }

        [JsonPropertyName("element_set_id")]
        public string? ElementSetId { get; init; } = "1";

        [JsonPropertyName("support_set_id")]
        public string? SupportSetId { get; init; } = "1";

        [JsonPropertyName("member_spring_set_id")]
        public string? MemberSpringSetId { get; init; } = "1";

        [JsonPropertyName("joint_set_id")]
        public string? JointSetId { get; init; } = "1";

        [JsonPropertyName("moving_load_pitch")]
        public double MovingLoadPitch { get; init; } = 0.1;
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

    private sealed class PrescribedDisplacementWire
    {
        [JsonPropertyName("id")]
        public required string Id { get; init; }

        [JsonPropertyName("case_id")]
        public required string CaseId { get; init; }

        [JsonPropertyName("node_id")]
        public required string NodeId { get; init; }

        [JsonPropertyName("dx")]
        public required double Dx { get; init; }

        [JsonPropertyName("dy")]
        public required double Dy { get; init; }

        [JsonPropertyName("dz")]
        public required double Dz { get; init; }

        [JsonPropertyName("rx")]
        public required double Rx { get; init; }

        [JsonPropertyName("ry")]
        public required double Ry { get; init; }

        [JsonPropertyName("rz")]
        public required double Rz { get; init; }
    }

    private sealed class MemberLoadWire
    {
        [JsonPropertyName("id")]
        public required string Id { get; init; }

        [JsonPropertyName("case_id")]
        public required string CaseId { get; init; }

        [JsonPropertyName("member_id")]
        public required string MemberId { get; init; }

        [JsonPropertyName("kind")]
        public required string Kind { get; init; }

        [JsonPropertyName("direction")]
        public required string Direction { get; init; }

        [JsonPropertyName("l1")]
        public required double L1 { get; init; }

        [JsonPropertyName("l2")]
        public required double L2 { get; init; }

        [JsonPropertyName("p1")]
        public required double P1 { get; init; }

        [JsonPropertyName("p2")]
        public required double P2 { get; init; }
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
