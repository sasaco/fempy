using PDF_Manager.Core.Shell;

namespace PDF_Manager.Core.Tests;

public sealed class ContentFactoryRegistryTests
{
    [Fact]
    public void DuplicateRegistration_FailsExplicitly()
    {
        DocumentKey key = DocumentKey.Tool("project.navigator");
        ContentFactoryRegistry<object> registry = new();
        registry.Register(key, static () => new object());

        DuplicateContentKeyException error = Assert.Throws<DuplicateContentKeyException>(
            () => registry.Register(key, static () => new object()));

        Assert.Equal(key, error.Key);
    }

    [Fact]
    public void UnknownKey_FailsExplicitlyWithoutFallbackConstruction()
    {
        DocumentKey key = DocumentKey.Document("project:unknown");
        ContentFactoryRegistry<object> registry = new();

        UnknownContentKeyException error = Assert.Throws<UnknownContentKeyException>(
            () => registry.CreateOrGet(key));

        Assert.Equal(key, error.Key);
    }

    [Fact]
    public void ToolFactory_IsCreatedOnceAndReused()
    {
        DocumentKey key = DocumentKey.Tool("project.navigator");
        ContentFactoryRegistry<object> registry = new();
        int calls = 0;
        registry.Register(key, () =>
        {
            calls++;
            return new object();
        });

        object first = registry.CreateOrGet(key);
        object second = registry.CreateOrGet(key);

        Assert.Same(first, second);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void DocumentFactory_CreatesANewInstanceAfterEachRequest()
    {
        DocumentKey key = DocumentKey.Document("project:alpha");
        ContentFactoryRegistry<object> registry = new();
        int calls = 0;
        registry.Register(key, () =>
        {
            calls++;
            return new object();
        });

        object first = registry.CreateOrGet(key);
        object second = registry.CreateOrGet(key);

        Assert.NotSame(first, second);
        Assert.Equal(2, calls);
    }

    [Fact]
    public void NullFactoryResult_FailsExplicitly()
    {
        DocumentKey key = DocumentKey.Tool("project.navigator");
        ContentFactoryRegistry<object> registry = new();
        registry.Register(key, static () => null!);

        ContentFactoryException error = Assert.Throws<ContentFactoryException>(
            () => registry.CreateOrGet(key));

        Assert.Equal(key, error.Key);
    }
}
