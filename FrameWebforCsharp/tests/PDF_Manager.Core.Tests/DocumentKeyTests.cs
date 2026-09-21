using PDF_Manager.Core.Shell;

namespace PDF_Manager.Core.Tests;

public sealed class DocumentKeyTests
{
    [Fact]
    public void CreateTool_UsesExplicitVersionKindAndIdentifier()
    {
        DocumentKey key = DocumentKey.Tool("model.viewport");

        Assert.Equal(DocumentKey.CurrentVersion, key.Version);
        Assert.Equal(DocumentKind.Tool, key.Kind);
        Assert.Equal("model.viewport", key.Identifier);
    }

    [Fact]
    public void JsonRoundTrip_IsDeterministicAndPreservesIdentity()
    {
        DocumentKey key = DocumentKey.Document("project:4d6cc2e1");

        string first = DocumentKeyJson.Serialize(key);
        string second = DocumentKeyJson.Serialize(key);
        DocumentKey restored = DocumentKeyJson.Deserialize(first);

        Assert.Equal("{\"version\":1,\"kind\":\"document\",\"identifier\":\"project:4d6cc2e1\"}", first);
        Assert.Equal(first, second);
        Assert.Equal(key, restored);
    }

    [Fact]
    public void SameIdentifierWithDifferentKind_HasDifferentIdentity()
    {
        DocumentKey tool = DocumentKey.Tool("project.summary");
        DocumentKey document = DocumentKey.Document("project.summary");

        Assert.NotEqual(tool, document);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Model.Viewport")]
    [InlineData("model viewport")]
    [InlineData(".model")]
    [InlineData("model..viewport")]
    [InlineData("model.")]
    [InlineData("model/view")]
    public void InvalidStableIdentifier_IsRejected(string identifier)
    {
        ContractValidationException error = Assert.Throws<ContractValidationException>(
            () => DocumentKey.Tool(identifier));

        Assert.Equal(ContractError.InvalidIdentifier, error.Error);
    }

    [Fact]
    public void IdentifierLongerThanContractLimit_IsRejected()
    {
        string identifier = new('a', DocumentKey.MaximumIdentifierLength + 1);

        ContractValidationException error = Assert.Throws<ContractValidationException>(
            () => DocumentKey.Document(identifier));

        Assert.Equal(ContractError.InvalidIdentifier, error.Error);
    }

    [Fact]
    public void UnknownDocumentKeyVersion_IsRejected()
    {
        UnknownContractVersionException error = Assert.Throws<UnknownContractVersionException>(
            () => new DocumentKey(DocumentKey.CurrentVersion + 1, DocumentKind.Tool, "model.viewport"));

        Assert.Equal("DocumentKey", error.ContractName);
        Assert.Equal(DocumentKey.CurrentVersion + 1, error.ActualVersion);
        Assert.Equal(ContractError.UnknownVersion, error.Error);
    }

    [Fact]
    public void UnknownDocumentKindInJson_IsRejected()
    {
        const string json = "{\"version\":1,\"kind\":\"caption\",\"identifier\":\"model.viewport\"}";

        ContractValidationException error = Assert.Throws<ContractValidationException>(
            () => DocumentKeyJson.Deserialize(json));

        Assert.Equal(ContractError.UnknownDocumentKind, error.Error);
    }

    [Fact]
    public void UnknownJsonMember_IsRejected()
    {
        const string json = "{\"version\":1,\"kind\":\"tool\",\"identifier\":\"model.viewport\",\"clrType\":\"Some.Window\"}";

        Assert.Throws<System.Text.Json.JsonException>(() => DocumentKeyJson.Deserialize(json));
    }
}
