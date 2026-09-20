using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PDF_Manager.Core.Documents;

namespace PDF_Manager.Core.Tests.Documents;

public sealed class ProjectDocumentStep5PersistenceTests
{
    public static TheoryData<string, string> WrongTypeCases => new()
    {
        { "model.dimension", "3" },
        { "model.sections.0.density", "\"heavy\"" },
        { "model.element_property_sets", "{}" },
        { "model.rigid_zones.0.i_length", "\"one\"" },
        { "model.support_sets.0.rows", "{}" },
        { "model.panels.0.node_ids", "{}" },
        { "model.joint_release_sets.0.rows.0.connect_xi", "1" },
        { "model.notice_points.0.distance", "\"five\"" },
        { "model.member_spring_sets.0.rows.0.tx", "true" },
        { "loads.cases.0.element_set_id", "2" },
        { "loads.prescribed_displacements.0.dx", "\"zero\"" },
        { "loads.member_loads.0.kind", "5" },
    };

    public static TheoryData<string> NullCases => new()
    {
        "model.dimension",
        "model.element_property_sets",
        "model.support_sets.0.rows",
        "model.panels.0.node_ids",
        "model.member_spring_sets",
        "loads.prescribed_displacements",
        "loads.member_loads.0.kind",
    };

    public static TheoryData<string> MissingRequiredMemberCases => new()
    {
        "model.element_property_sets.0.sections",
        "model.rigid_zones.0.member_id",
        "model.support_sets.0.rows",
        "model.panels.0.node_ids",
        "model.joint_release_sets.0.rows",
        "model.notice_points.0.distance",
        "model.member_spring_sets.0.rows",
        "loads.prescribed_displacements.0.dx",
        "loads.member_loads.0.kind",
    };

    public static TheoryData<string> UnknownMemberCases => new()
    {
        "model.element_property_sets.0",
        "model.rigid_zones.0",
        "model.support_sets.0.rows.0",
        "model.panels.0",
        "model.joint_release_sets.0.rows.0",
        "model.notice_points.0",
        "model.member_spring_sets.0.rows.0",
        "loads.prescribed_displacements.0",
        "loads.member_loads.0",
    };

    public static TheoryData<string> DuplicateMemberCases => new()
    {
        "\"dimension\": \"3d\"",
        "\"i_length\": 1",
        "\"distance\": 5",
        "\"moving_load_pitch\": 0.25",
        "\"dx\": 0.001",
        "\"kind\": \"distributed_force\"",
    };

    public static TheoryData<string, string, string> InvalidReferenceAndRangeCases => new()
    {
        { "model.rigid_zones.0.member_id", "\"404\"", "references unknown member '404'" },
        { "model.rigid_zones.0.i_length", "-1", "lengths cannot be negative" },
        { "model.support_sets.0.rows.0.node_id", "\"404\"", "references unknown node '404'" },
        { "model.panels.0.node_ids.3", "\"404\"", "references an unknown node" },
        { "model.joint_release_sets.0.rows.0.member_id", "\"404\"", "references unknown member '404'" },
        { "model.notice_points.0.distance", "10", "must be inside member '1'" },
        { "model.member_spring_sets.0.rows.0.member_id", "\"404\"", "references unknown member '404'" },
        { "loads.cases.0.element_set_id", "\"404\"", "references unknown element property set '404'" },
        { "loads.cases.0.support_set_id", "\"404\"", "references unknown support set '404'" },
        { "loads.cases.0.member_spring_set_id", "\"404\"", "references unknown member spring set '404'" },
        { "loads.cases.0.joint_set_id", "\"404\"", "references unknown joint set '404'" },
        { "loads.cases.0.moving_load_pitch", "-0.01", "moving_load_pitch cannot be negative" },
        { "loads.prescribed_displacements.0.case_id", "\"404\"", "references an unknown load case" },
        { "loads.prescribed_displacements.0.node_id", "\"404\"", "references an unknown node" },
        { "loads.member_loads.0.case_id", "\"404\"", "references an unknown load case" },
        { "loads.member_loads.0.member_id", "\"404\"", "references unknown member '404'" },
        { "loads.member_loads.0.l1", "10", "distributed extent is invalid" },
    };

    [Fact]
    public void CompleteInputMatrix_RoundTripsByteStably()
    {
        ProjectDocument original = Step5DocumentFactory.Create();

        byte[] first = ProjectDocumentJson.Serialize(original);
        ProjectDocument restored = ProjectDocumentJson.Deserialize(first);
        byte[] second = ProjectDocumentJson.Serialize(restored);

        Assert.Equal(first, second);
        Assert.Equal(ModelDimension.ThreeDimensional, restored.Dimension);
        Assert.Equal(1.2e-5, Assert.Single(restored.Sections).ThermalExpansionCoefficient);
        Assert.Equal("2", Assert.Single(restored.ElementPropertySets).Id);
        Assert.Equal("1", Assert.Single(restored.RigidZones).MemberId);
        Assert.Equal("2", Assert.Single(restored.SupportSets).Id);
        Assert.Equal(4, Assert.Single(restored.Panels).NodeIds.Count);
        Assert.Equal("J1", Assert.Single(Assert.Single(restored.JointReleaseSets).Rows).Id);
        Assert.Equal(5, Assert.Single(restored.NoticePoints).Distance);
        Assert.Equal("MS1", Assert.Single(Assert.Single(restored.MemberSpringSets).Rows).Id);
        Assert.Equal("PD1", Assert.Single(restored.PrescribedDisplacements).Id);
        Assert.Equal(MemberLoadKind.DistributedForce, Assert.Single(restored.MemberLoads).Kind);
        Assert.Equal(MemberLoadDirection.GlobalY, Assert.Single(restored.MemberLoads).Direction);
        Assert.Equal(0.25, Assert.Single(restored.LoadCases).MovingLoadPitch);
    }

    [Fact]
    public void ExistingVersionOneWithoutAdditiveFields_UsesDefaultsAndCanonicalizesThem()
    {
        JsonObject root = JsonNode.Parse(
            ProjectDocumentJson.SerializeToString(Step5DocumentFactory.Create()))!.AsObject();
        JsonObject model = root["model"]!.AsObject();
        model.Remove("dimension");
        model.Remove("element_property_sets");
        model.Remove("rigid_zones");
        model.Remove("support_sets");
        model.Remove("panels");
        model.Remove("joint_release_sets");
        model.Remove("notice_points");
        model.Remove("member_spring_sets");
        foreach (JsonNode? section in model["sections"]!.AsArray())
        {
            JsonObject sectionObject = section!.AsObject();
            sectionObject.Remove("thermal_expansion_coefficient");
            sectionObject.Remove("density");
            sectionObject.Remove("panel_thickness");
        }

        JsonObject loads = root["loads"]!.AsObject();
        loads.Remove("prescribed_displacements");
        loads.Remove("member_loads");
        foreach (JsonNode? loadCase in loads["cases"]!.AsArray())
        {
            JsonObject loadCaseObject = loadCase!.AsObject();
            loadCaseObject.Remove("element_set_id");
            loadCaseObject.Remove("support_set_id");
            loadCaseObject.Remove("member_spring_set_id");
            loadCaseObject.Remove("joint_set_id");
            loadCaseObject.Remove("moving_load_pitch");
        }

        ProjectDocument restored = ProjectDocumentJson.Deserialize(root.ToJsonString());

        Assert.Equal(ModelDimension.ThreeDimensional, restored.Dimension);
        Assert.Empty(restored.ElementPropertySets);
        Assert.Empty(restored.RigidZones);
        Assert.Empty(restored.SupportSets);
        Assert.Empty(restored.Panels);
        Assert.Empty(restored.JointReleaseSets);
        Assert.Empty(restored.NoticePoints);
        Assert.Empty(restored.MemberSpringSets);
        Assert.Empty(restored.PrescribedDisplacements);
        Assert.Empty(restored.MemberLoads);
        Assert.Equal(0, Assert.Single(restored.Sections).ThermalExpansionCoefficient);
        LoadCaseDefinition restoredCase = Assert.Single(restored.LoadCases);
        Assert.Equal("1", restoredCase.ElementSetId);
        Assert.Equal("1", restoredCase.SupportSetId);
        Assert.Equal("1", restoredCase.MemberSpringSetId);
        Assert.Equal("1", restoredCase.JointSetId);
        Assert.Equal(0.1, restoredCase.MovingLoadPitch);

        JsonObject canonical = JsonNode.Parse(ProjectDocumentJson.SerializeToString(restored))!.AsObject();
        JsonObject canonicalModel = canonical["model"]!.AsObject();
        JsonObject canonicalLoads = canonical["loads"]!.AsObject();
        Assert.Equal("3d", canonicalModel["dimension"]!.GetValue<string>());
        Assert.Empty(canonicalModel["element_property_sets"]!.AsArray());
        Assert.Empty(canonicalModel["member_spring_sets"]!.AsArray());
        Assert.Empty(canonicalLoads["prescribed_displacements"]!.AsArray());
        Assert.Empty(canonicalLoads["member_loads"]!.AsArray());
        Assert.Equal("1", canonicalLoads["cases"]![0]!["element_set_id"]!.GetValue<string>());
    }

    [Fact]
    public void StrictReader_AppliesExistingRulesToAdditiveFields()
    {
        string valid = ProjectDocumentJson.SerializeToString(Step5DocumentFactory.Create());
        JsonObject unknownRoot = JsonNode.Parse(valid)!.AsObject();
        unknownRoot["model"]!["panels"]![0]!["legacy"] = true;

        Assert.Throws<ProjectDocumentFormatException>(() =>
            ProjectDocumentJson.Deserialize(unknownRoot.ToJsonString()));

        string duplicate = valid.Replace(
            "\"dimension\": \"3d\",",
            "\"dimension\": \"3d\",\n    \"dimension\": \"3d\",",
            StringComparison.Ordinal);
        Assert.Throws<ProjectDocumentFormatException>(() => ProjectDocumentJson.Deserialize(duplicate));

        string nonFinite = valid.Replace(
            "\"moving_load_pitch\": 0.25",
            "\"moving_load_pitch\": 1e999",
            StringComparison.Ordinal);
        Assert.Throws<ProjectDocumentValidationException>(() => ProjectDocumentJson.Deserialize(nonFinite));
    }

    [Theory]
    [MemberData(nameof(WrongTypeCases))]
    public void StrictReader_RejectsWrongTypesAcrossAdditiveDtos(string path, string replacementJson)
    {
        JsonObject root = CreateValidRoot();
        SetAtPath(root, path, JsonNode.Parse(replacementJson));

        Assert.Throws<ProjectDocumentFormatException>(() =>
            ProjectDocumentJson.Deserialize(root.ToJsonString()));
    }

    [Theory]
    [MemberData(nameof(NullCases))]
    public void StrictReader_RejectsExplicitNullAcrossAdditiveDtos(string path)
    {
        JsonObject root = CreateValidRoot();
        SetAtPath(root, path, null);

        Assert.Throws<ProjectDocumentFormatException>(() =>
            ProjectDocumentJson.Deserialize(root.ToJsonString()));
    }

    [Theory]
    [MemberData(nameof(MissingRequiredMemberCases))]
    public void StrictReader_RejectsMissingRequiredMembersInsideAdditiveDtos(string path)
    {
        JsonObject root = CreateValidRoot();
        RemoveAtPath(root, path);

        Assert.Throws<ProjectDocumentFormatException>(() =>
            ProjectDocumentJson.Deserialize(root.ToJsonString()));
    }

    [Theory]
    [MemberData(nameof(UnknownMemberCases))]
    public void StrictReader_RejectsUnknownMembersAcrossAdditiveDtos(string objectPath)
    {
        JsonObject root = CreateValidRoot();
        GetAtPath(root, objectPath).AsObject()["legacy"] = true;

        Assert.Throws<ProjectDocumentFormatException>(() =>
            ProjectDocumentJson.Deserialize(root.ToJsonString()));
    }

    [Theory]
    [MemberData(nameof(DuplicateMemberCases))]
    public void StrictReader_RejectsDuplicateMembersAcrossAdditiveDtos(string memberText)
    {
        string valid = ProjectDocumentJson.SerializeToString(Step5DocumentFactory.Create());
        Assert.Equal(1, CountOccurrences(valid, memberText));
        string duplicate = valid.Replace(
            memberText,
            $"{memberText},{Environment.NewLine}    {memberText}",
            StringComparison.Ordinal);

        ProjectDocumentFormatException exception = Assert.Throws<ProjectDocumentFormatException>(() =>
            ProjectDocumentJson.Deserialize(duplicate));

        Assert.Contains("Duplicate JSON member", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(InvalidReferenceAndRangeCases))]
    public void Reader_RejectsInvalidAdditiveCrossReferencesAndRanges(
        string path,
        string replacementJson,
        string expectedMessage)
    {
        JsonObject root = CreateValidRoot();
        SetAtPath(root, path, JsonNode.Parse(replacementJson));

        ProjectDocumentValidationException exception = Assert.Throws<ProjectDocumentValidationException>(() =>
            ProjectDocumentJson.Deserialize(root.ToJsonString()));

        Assert.Contains(expectedMessage, exception.Message, StringComparison.Ordinal);
        Assert.NotNull(exception.Issue);
        Assert.False(string.IsNullOrWhiteSpace(exception.Issue.Code));
    }

    [Fact]
    public void Reader_PreservesTypedValidationIssueMetadata()
    {
        JsonObject root = CreateValidRoot();
        SetAtPath(root, "model.dimension", JsonValue.Create("2d"));
        SetAtPath(root, "model.nodes.0.z", JsonValue.Create(1));

        ProjectDocumentValidationException exception = Assert.Throws<ProjectDocumentValidationException>(() =>
            ProjectDocumentJson.Deserialize(root.ToJsonString()));

        Assert.Equal("invalid_dimension", exception.Issue.Code);
        Assert.Equal("nodes", exception.Issue.Collection);
        Assert.Equal("1", exception.Issue.EntityId);
        Assert.Equal("z", exception.Issue.Field);
    }

    [Fact]
    public void Reader_AcceptsExactByteLimitAndRejectsOneByteOver()
    {
        byte[] canonical = ProjectDocumentJson.Serialize(Step5DocumentFactory.Create());
        byte[] exact = GC.AllocateUninitializedArray<byte>(ProjectDocumentJson.DefaultMaxJsonBytes);
        Array.Fill(exact, (byte)' ');
        canonical.CopyTo(exact, 0);

        ProjectDocument restored = ProjectDocumentJson.Deserialize(exact);
        Assert.Equal("Step 5", restored.Metadata.Name);

        byte[] oversized = GC.AllocateUninitializedArray<byte>(ProjectDocumentJson.DefaultMaxJsonBytes + 1);
        ProjectDocumentFormatException bytes = Assert.Throws<ProjectDocumentFormatException>(() =>
            ProjectDocumentJson.Deserialize(oversized));

        Assert.Contains("byte limit", bytes.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Reader_AcceptsExactEntityBudgetAndRejectsOneEntityOver()
    {
        string exactEntityBudget = CreateEntityBudgetJson(ProjectDocumentJson.DefaultMaxEntityCount);
        Assert.True(Encoding.UTF8.GetByteCount(exactEntityBudget) < ProjectDocumentJson.DefaultMaxJsonBytes);
        ProjectDocumentValidationException boundary = Assert.Throws<ProjectDocumentValidationException>(() =>
            ProjectDocumentJson.Deserialize(exactEntityBudget));
        Assert.Contains("Duplicate node ID", boundary.Message, StringComparison.Ordinal);

        string excessiveEntities = CreateEntityBudgetJson(ProjectDocumentJson.DefaultMaxEntityCount + 1);
        Assert.True(Encoding.UTF8.GetByteCount(excessiveEntities) < ProjectDocumentJson.DefaultMaxJsonBytes);
        ProjectDocumentFormatException entities = Assert.Throws<ProjectDocumentFormatException>(() =>
            ProjectDocumentJson.Deserialize(excessiveEntities));

        Assert.Contains("entities", entities.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Schema_DeclaresEveryAdditiveCollectionAndRemainsStrict()
    {
        string path = Path.Combine(
            FindRepositoryRoot(),
            "FramePrintPDF",
            "PDF_Manager.Core",
            "Documents",
            "project-document-v1.schema.json");
        using JsonDocument schema = JsonDocument.Parse(File.ReadAllBytes(path));
        JsonElement definitions = schema.RootElement.GetProperty("$defs");
        JsonElement model = definitions.GetProperty("model");
        JsonElement loads = definitions.GetProperty("loads");

        Assert.False(model.GetProperty("additionalProperties").GetBoolean());
        Assert.False(loads.GetProperty("additionalProperties").GetBoolean());
        foreach (string property in new[]
        {
            "dimension",
            "element_property_sets",
            "rigid_zones",
            "support_sets",
            "panels",
            "joint_release_sets",
            "notice_points",
            "member_spring_sets",
        })
        {
            Assert.True(model.GetProperty("properties").TryGetProperty(property, out _), property);
        }

        Assert.True(loads.GetProperty("properties").TryGetProperty("prescribed_displacements", out _));
        Assert.True(loads.GetProperty("properties").TryGetProperty("member_loads", out _));
    }

    private static JsonObject CreateValidRoot()
        => JsonNode.Parse(ProjectDocumentJson.SerializeToString(Step5DocumentFactory.Create()))!.AsObject();

    private static JsonNode GetAtPath(JsonObject root, string path)
    {
        JsonNode? current = root;
        foreach (string segment in path.Split('.'))
        {
            current = current switch
            {
                JsonObject value => value[segment],
                JsonArray value => value[int.Parse(segment, System.Globalization.CultureInfo.InvariantCulture)],
                _ => null,
            };
            Assert.NotNull(current);
        }

        return current;
    }

    private static void SetAtPath(JsonObject root, string path, JsonNode? replacement)
    {
        string[] segments = path.Split('.');
        JsonNode parent = GetAtPath(root, string.Join('.', segments[..^1]));
        string last = segments[^1];
        if (parent is JsonObject parentObject)
        {
            parentObject[last] = replacement;
        }
        else
        {
            parent.AsArray()[int.Parse(last, System.Globalization.CultureInfo.InvariantCulture)] = replacement;
        }
    }

    private static void RemoveAtPath(JsonObject root, string path)
    {
        string[] segments = path.Split('.');
        JsonNode parent = GetAtPath(root, string.Join('.', segments[..^1]));
        string last = segments[^1];
        if (parent is JsonObject parentObject)
        {
            Assert.True(parentObject.Remove(last));
        }
        else
        {
            parent.AsArray().RemoveAt(int.Parse(last, System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    private static int CountOccurrences(string value, string search)
    {
        int count = 0;
        int startIndex = 0;
        while ((startIndex = value.IndexOf(search, startIndex, StringComparison.Ordinal)) >= 0)
        {
            count++;
            startIndex += search.Length;
        }

        return count;
    }

    private static string CreateEntityBudgetJson(int entityCount)
    {
        StringBuilder builder = new(4_000_000);
        builder.Append("{\"kind\":\"frameweb_project\",\"schema_version\":1,");
        builder.Append("\"metadata\":{\"name\":\"Budget\",\"description\":\"\",\"author\":\"\",\"unit_system\":\"kN-m\"},");
        builder.Append("\"model\":{\"nodes\":[");
        for (int index = 0; index < entityCount; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append("{\"id\":\"1\",\"x\":0,\"y\":0,\"z\":0}");
        }

        builder.Append("],\"members\":[],\"supports\":[]},");
        builder.Append("\"loads\":{\"cases\":[],\"nodal_loads\":[]},\"derived_results\":[],\"moving_loads\":[]}");
        return builder.ToString();
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
        {
            current = current.Parent;
        }

        return current?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root from test output directory.");
    }
}
