using System.Text.Json;
using PDF_Manager.Core.Shell;

namespace PDF_Manager.Core.Tests;

public sealed class LayoutStateTests
{
    private static readonly DocumentKey ToolKey = DocumentKey.Tool("project.navigator");
    private static readonly DocumentKey DocumentKey = DocumentKey.Document("project:alpha");

    [Fact]
    public void ValidLayout_RoundTripsDeterministically()
    {
        ContentFactoryRegistry<object> registry = CreateRegistry();
        LayoutState layout = new(
            LayoutState.CurrentVersion,
            [
                new LayoutContentState(ToolKey, DockState.DockLeft, new WindowBounds(10, 20, 300, 600), 0),
                new LayoutContentState(DocumentKey, DockState.Document, null, 1),
            ],
            DocumentKey);

        string first = LayoutStateJson.Serialize(layout, registry);
        LayoutState restored = LayoutStateJson.Deserialize(first, registry);
        string second = LayoutStateJson.Serialize(restored, registry);

        Assert.Equal(first, second);
        Assert.Equal(DocumentKey, restored.ActiveDocument);
        Assert.Collection(
            restored.Contents,
            content => Assert.Equal(ToolKey, content.Key),
            content => Assert.Equal(DocumentKey, content.Key));
    }

    [Fact]
    public void UnknownWhitelistedKey_IsRejected()
    {
        ContentFactoryRegistry<object> registry = CreateRegistry(includeDocument: false);
        LayoutState layout = new(
            LayoutState.CurrentVersion,
            [new LayoutContentState(DocumentKey, DockState.Document, null, 0)],
            DocumentKey);

        UnknownContentKeyException error = Assert.Throws<UnknownContentKeyException>(
            () => LayoutStateJson.Serialize(layout, registry));

        Assert.Equal(DocumentKey, error.Key);
    }

    [Fact]
    public void UnknownLayoutVersion_IsRejected()
    {
        string json = $$"""
            {"version":{{LayoutState.CurrentVersion + 1}},"contents":[],"activeDocument":null}
            """;

        UnknownContractVersionException error = Assert.Throws<UnknownContractVersionException>(
            () => LayoutStateJson.Deserialize(json, CreateRegistry()));

        Assert.Equal("LayoutState", error.ContractName);
    }

    [Fact]
    public void ActiveDocumentMustBePresent()
    {
        LayoutState layout = new(LayoutState.CurrentVersion, [], DocumentKey);

        ContractValidationException error = Assert.Throws<ContractValidationException>(
            () => LayoutStateValidator.Validate(layout, CreateRegistry()));

        Assert.Equal(ContractError.ActiveDocumentInconsistent, error.Error);
    }

    [Fact]
    public void ActiveDocumentMustHaveDocumentKind()
    {
        LayoutState layout = new(
            LayoutState.CurrentVersion,
            [new LayoutContentState(ToolKey, DockState.DockLeft, null, 0)],
            ToolKey);

        ContractValidationException error = Assert.Throws<ContractValidationException>(
            () => LayoutStateValidator.Validate(layout, CreateRegistry()));

        Assert.Equal(ContractError.ActiveDocumentInconsistent, error.Error);
    }

    [Fact]
    public void DocumentCannotBePersistedAsHidden()
    {
        LayoutState layout = new(
            LayoutState.CurrentVersion,
            [new LayoutContentState(DocumentKey, DockState.Hidden, null, 0)],
            null);

        ContractValidationException error = Assert.Throws<ContractValidationException>(
            () => LayoutStateValidator.Validate(layout, CreateRegistry()));

        Assert.Equal(ContractError.InvalidDockState, error.Error);
    }

    [Fact]
    public void FloatStateRequiresBounds()
    {
        LayoutState layout = new(
            LayoutState.CurrentVersion,
            [new LayoutContentState(ToolKey, DockState.Float, null, 0)],
            null);

        ContractValidationException error = Assert.Throws<ContractValidationException>(
            () => LayoutStateValidator.Validate(layout, CreateRegistry()));

        Assert.Equal(ContractError.InvalidBounds, error.Error);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(-1, 100)]
    [InlineData(100, 0)]
    [InlineData(100, -1)]
    [InlineData(100001, 100)]
    public void MalformedBounds_AreRejected(int width, int height)
    {
        ContractValidationException error = Assert.Throws<ContractValidationException>(
            () => new WindowBounds(0, 0, width, height));

        Assert.Equal(ContractError.InvalidBounds, error.Error);
    }

    [Fact]
    public void NegativeOrder_IsRejected()
    {
        ContractValidationException error = Assert.Throws<ContractValidationException>(
            () => new LayoutContentState(ToolKey, DockState.DockLeft, null, -1));

        Assert.Equal(ContractError.InvalidOrder, error.Error);
    }

    [Fact]
    public void DuplicateOrder_IsRejected()
    {
        LayoutState layout = new(
            LayoutState.CurrentVersion,
            [
                new LayoutContentState(ToolKey, DockState.DockLeft, null, 0),
                new LayoutContentState(DocumentKey, DockState.Document, null, 0),
            ],
            null);

        ContractValidationException error = Assert.Throws<ContractValidationException>(
            () => LayoutStateValidator.Validate(layout, CreateRegistry()));

        Assert.Equal(ContractError.InvalidOrder, error.Error);
    }

    [Fact]
    public void OrderGap_IsRejected()
    {
        LayoutState layout = new(
            LayoutState.CurrentVersion,
            [
                new LayoutContentState(ToolKey, DockState.DockLeft, null, 0),
                new LayoutContentState(DocumentKey, DockState.Document, null, 2),
            ],
            null);

        ContractValidationException error = Assert.Throws<ContractValidationException>(
            () => LayoutStateValidator.Validate(layout, CreateRegistry()));

        Assert.Equal(ContractError.InvalidOrder, error.Error);
    }

    [Fact]
    public void DuplicateContentKey_IsRejected()
    {
        LayoutState layout = new(
            LayoutState.CurrentVersion,
            [
                new LayoutContentState(ToolKey, DockState.DockLeft, null, 0),
                new LayoutContentState(ToolKey, DockState.DockRight, null, 1),
            ],
            null);

        ContractValidationException error = Assert.Throws<ContractValidationException>(
            () => LayoutStateValidator.Validate(layout, CreateRegistry()));

        Assert.Equal(ContractError.DuplicateContentKey, error.Error);
    }

    [Fact]
    public void MalformedBoundsInJson_AreRejected()
    {
        const string json = "{\"version\":1,\"contents\":[{\"key\":{\"version\":1,\"kind\":\"tool\",\"identifier\":\"project.navigator\"},\"dockState\":\"float\",\"bounds\":{\"x\":0,\"y\":0,\"width\":0,\"height\":100},\"order\":0}],\"activeDocument\":null}";

        Assert.ThrowsAny<Exception>(() => LayoutStateJson.Deserialize(json, CreateRegistry()));
    }

    [Fact]
    public void UnknownLayoutMember_IsRejected()
    {
        const string json = "{\"version\":1,\"contents\":[],\"activeDocument\":null,\"contentType\":\"Some.Window\"}";

        Assert.Throws<JsonException>(() => LayoutStateJson.Deserialize(json, CreateRegistry()));
    }

    private static ContentFactoryRegistry<object> CreateRegistry(bool includeDocument = true)
    {
        ContentFactoryRegistry<object> registry = new();
        registry.Register(ToolKey, static () => new object());
        if (includeDocument)
        {
            registry.Register(DocumentKey, static () => new object());
        }

        return registry;
    }
}
