using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PDF_Manager.Core.Documents;

namespace PDF_Manager.Core.Tests.Documents;

public sealed class ProjectDocumentJsonTests
{
    [Fact]
    public void SchemaArtifact_DefinesTheStrictVersionedRoot()
    {
        string path = Path.Combine(
            FindRepositoryRoot(),
            "FramePrintPDF",
            "PDF_Manager.Core",
            "Documents",
            "project-document-v1.schema.json");
        using JsonDocument schema = JsonDocument.Parse(File.ReadAllBytes(path));
        JsonElement root = schema.RootElement;

        Assert.Equal("FrameWeb Project Document v1", root.GetProperty("title").GetString());
        Assert.False(root.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal("frameweb_project",
            root.GetProperty("properties").GetProperty("kind").GetProperty("const").GetString());
        Assert.Equal(1,
            root.GetProperty("properties").GetProperty("schema_version").GetProperty("const").GetInt32());
    }

    [Fact]
    public void CanonicalJson_RoundTripsByteStablyAndSortsEntityIds()
    {
        ProjectDocument original = ProjectDocumentTestData.Create();

        byte[] first = ProjectDocumentJson.Serialize(original);
        ProjectDocument restored = ProjectDocumentJson.Deserialize(first);
        byte[] second = ProjectDocumentJson.Serialize(restored);
        string text = Encoding.UTF8.GetString(first);

        Assert.Equal(first, second);
        Assert.True(text.IndexOf("\"id\": \"N1\"", StringComparison.Ordinal) <
            text.IndexOf("\"id\": \"N2\"", StringComparison.Ordinal));
        Assert.False(restored.IsDirty);
        Assert.Empty(restored.Selection.NodeIds);
        Assert.Equal("SEC1", Assert.Single(restored.Sections).Id);
        Assert.Contains("youngs_modulus", text, StringComparison.Ordinal);
        Assert.DoesNotContain("selection", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dirty", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("analysis_result", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StrictReader_RejectsUnknownDuplicateInvalidVersionAndNonFiniteData()
    {
        string valid = ProjectDocumentJson.SerializeToString(ProjectDocumentTestData.Create());

        string unknown = valid.Replace(
            "\"schema_version\": 1,",
            "\"schema_version\": 1,\n  \"legacy\": {},",
            StringComparison.Ordinal);
        Assert.Throws<ProjectDocumentFormatException>(() => ProjectDocumentJson.Deserialize(unknown));

        string duplicate = valid.Replace(
            "\"kind\": \"frameweb_project\",",
            "\"kind\": \"frameweb_project\",\n  \"kind\": \"frameweb_project\",",
            StringComparison.Ordinal);
        Assert.Throws<ProjectDocumentFormatException>(() => ProjectDocumentJson.Deserialize(duplicate));

        string invalidVersion = valid.Replace("\"schema_version\": 1", "\"schema_version\": 2", StringComparison.Ordinal);
        Assert.Throws<ProjectDocumentValidationException>(() => ProjectDocumentJson.Deserialize(invalidVersion));

        string nonFinite = valid.Replace("\"x\": 0", "\"x\": 1e999", StringComparison.Ordinal);
        Assert.Throws<ProjectDocumentValidationException>(() => ProjectDocumentJson.Deserialize(nonFinite));
    }

    [Fact]
    public void StrictReader_RejectsMalformedUtf8()
    {
        byte[] malformed = [0x7b, 0x22, 0xff, 0x22, 0x7d];

        Assert.Throws<ProjectDocumentFormatException>(() => ProjectDocumentJson.Deserialize(malformed));
    }

    [Fact]
    public void StrictReader_ReportsNullRequiredObjectsAsFormatFailures()
    {
        JsonObject root = JsonNode.Parse(
            ProjectDocumentJson.SerializeToString(ProjectDocumentTestData.Create()))!.AsObject();
        root["metadata"] = null;

        Assert.Throws<ProjectDocumentFormatException>(() =>
            ProjectDocumentJson.Deserialize(root.ToJsonString()));
    }

    [Fact]
    public void LegacyV1WithoutAnalysisSections_RemainsEditableButIncomplete()
    {
        JsonObject root = JsonNode.Parse(
            ProjectDocumentJson.SerializeToString(ProjectDocumentTestData.Create()))!.AsObject();
        JsonObject model = root["model"]!.AsObject();
        model.Remove("sections");
        foreach (JsonNode? member in model["members"]!.AsArray())
        {
            JsonObject memberObject = member!.AsObject();
            memberObject.Remove("section_id");
            memberObject.Remove("rotation_degrees");
            memberObject.Remove("shear_correction");
        }

        ProjectDocument restored = ProjectDocumentJson.Deserialize(root.ToJsonString());

        Assert.Empty(restored.Sections);
        Assert.Null(Assert.Single(restored.Members).SectionId);
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
