using PDF_Manager.Core.Documents;

namespace PDF_Manager.Core.Tests.Documents;

public sealed class JsonProjectStoreTests
{
    [Fact]
    public async Task SaveOpenAndReplace_AreAtomicAndLeaveNoTemporaryFiles()
    {
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "model.frameweb.json");
        JsonProjectStore store = new();
        try
        {
            ProjectDocument first = ProjectDocumentTestData.Create(isDirty: true);
            ProjectDocument saved = await store.SaveAsync(first, path);
            ProjectDocument opened = await store.OpenAsync(path);

            Assert.True(File.Exists(path));
            Assert.False(saved.IsDirty);
            Assert.Equal(first.Metadata.Name, opened.Metadata.Name);

            ProjectDocument replacement = new(
                1,
                first.Metadata with { Name = "Replacement" },
                first.Nodes,
                first.Members,
                first.Supports,
                first.LoadCases,
                first.NodalLoads,
                first.DerivedResults,
                first.MovingLoads,
                isDirty: true);
            await store.SaveAsync(replacement, path);

            Assert.Equal("Replacement", (await store.OpenAsync(path)).Metadata.Name);
            Assert.Empty(Directory.GetFiles(directory, ".*.tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task CancelledSave_DoesNotPublishAFileOrLeaveATemporaryFile()
    {
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "cancelled.frameweb.json");
        JsonProjectStore store = new();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => store.SaveAsync(ProjectDocumentTestData.Create(), path, cancellation.Token));

            Assert.False(File.Exists(path));
            Assert.Empty(Directory.GetFiles(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Open_PreservesContractErrorsAndWrapsFileSystemCauses()
    {
        string directory = CreateTemporaryDirectory();
        string malformedPath = Path.Combine(directory, "bad.frameweb.json");
        await File.WriteAllTextAsync(malformedPath, "{}");
        JsonProjectStore store = new();
        try
        {
            await Assert.ThrowsAsync<ProjectDocumentFormatException>(() => store.OpenAsync(malformedPath));

            ProjectStoreException missing = await Assert.ThrowsAsync<ProjectStoreException>(
                () => store.OpenAsync(Path.Combine(directory, "missing.frameweb.json")));
            Assert.Equal(ProjectStoreOperation.Open, missing.Operation);
            Assert.IsAssignableFrom<IOException>(missing.InnerException);
            Assert.DoesNotContain(directory, missing.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"frameweb-core-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
