using System.Text;
using PDF_Manager.Shell;

namespace PDF_Manager.UiTests;

public sealed class ShellLayoutStoreTests
{
    [Fact]
    public async Task LoadAsync_MissingFileReturnsNullWithoutCreatingStorage()
    {
        using TemporaryLayoutDirectory temporary = new();
        LocalShellLayoutStore store = new(temporary.LayoutPath);

        string? json = await store.LoadAsync();

        Assert.Null(json);
        Assert.False(File.Exists(temporary.LayoutPath));
        Assert.Empty(Directory.EnumerateFiles(temporary.RootPath));
    }

    [Fact]
    public async Task SaveAsync_ExactByteLimitRoundTripsAndOneByteOverPreservesExistingFile()
    {
        using TemporaryLayoutDirectory temporary = new();
        LocalShellLayoutStore store = new(temporary.LayoutPath);
        string exactLimit = new('a', LocalShellLayoutStore.MaximumLayoutBytes);
        string oversized = exactLimit + 'b';
        await store.SaveAsync(exactLimit);

        ShellLayoutStoreException failure = await Assert.ThrowsAsync<ShellLayoutStoreException>(
            () => store.SaveAsync(oversized));

        Assert.Contains(LocalShellLayoutStore.MaximumLayoutBytes.ToString(), failure.Message);
        Assert.Equal(exactLimit, await store.LoadAsync());
        Assert.Single(Directory.EnumerateFiles(temporary.RootPath));
    }

    [Fact]
    public async Task LoadAsync_OneByteOverLimitIsRejectedBeforeTextDecoding()
    {
        using TemporaryLayoutDirectory temporary = new();
        byte[] oversized = Enumerable.Repeat(
            (byte)'a',
            LocalShellLayoutStore.MaximumLayoutBytes + 1).ToArray();
        await File.WriteAllBytesAsync(temporary.LayoutPath, oversized);
        LocalShellLayoutStore store = new(temporary.LayoutPath);

        ShellLayoutStoreException failure = await Assert.ThrowsAsync<ShellLayoutStoreException>(
            () => store.LoadAsync());

        Assert.Contains(LocalShellLayoutStore.MaximumLayoutBytes.ToString(), failure.Message);
    }

    [Fact]
    public async Task LoadAsync_InvalidUtf8IsReportedAsTypedStoreFailure()
    {
        using TemporaryLayoutDirectory temporary = new();
        await File.WriteAllBytesAsync(temporary.LayoutPath, [0xC3, 0x28]);
        LocalShellLayoutStore store = new(temporary.LayoutPath);

        ShellLayoutStoreException failure = await Assert.ThrowsAsync<ShellLayoutStoreException>(
            () => store.LoadAsync());

        Assert.IsType<DecoderFallbackException>(failure.InnerException);
        Assert.DoesNotContain(temporary.LayoutPath, failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoadAsync_InvalidJsonReturnsRawUtf8ForTheLayoutContractBoundaryToReject()
    {
        using TemporaryLayoutDirectory temporary = new();
        const string invalidJson = "{not valid json";
        await File.WriteAllTextAsync(temporary.LayoutPath, invalidJson, new UTF8Encoding(false));
        LocalShellLayoutStore store = new(temporary.LayoutPath);

        string? loaded = await store.LoadAsync();

        Assert.Equal(invalidJson, loaded);
    }

    [Fact]
    public async Task SaveAsync_AtomicallyReplacesFinalFileAndLeavesNoTemporarySibling()
    {
        using TemporaryLayoutDirectory temporary = new();
        LocalShellLayoutStore store = new(temporary.LayoutPath);
        await store.SaveAsync("{\"revision\":1}");

        await store.SaveAsync("{\"revision\":2}");

        Assert.Equal("{\"revision\":2}", await File.ReadAllTextAsync(temporary.LayoutPath));
        string[] files = Directory.GetFiles(temporary.RootPath);
        Assert.Equal(new[] { temporary.LayoutPath }, files);
        Assert.Empty(Directory.EnumerateFiles(temporary.RootPath, ".*.tmp"));
    }

    [Fact]
    public async Task PublicOperations_HonorPreCancelledTokenWithoutFilesystemMutation()
    {
        using TemporaryLayoutDirectory temporary = new();
        LocalShellLayoutStore store = new(temporary.LayoutPath);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.LoadAsync(cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.SaveAsync("{}", cancellation.Token));

        Assert.False(File.Exists(temporary.LayoutPath));
        Assert.Empty(Directory.EnumerateFiles(temporary.RootPath));
    }

    private sealed class TemporaryLayoutDirectory : IDisposable
    {
        internal TemporaryLayoutDirectory()
        {
            RootPath = Path.Combine(
                Path.GetTempPath(),
                "FrameWeb3.PDF_Manager.UiTests",
                Guid.NewGuid().ToString("N"));
            LayoutPath = Path.Combine(RootPath, "layout-v1.json");
            Directory.CreateDirectory(RootPath);
        }

        internal string RootPath { get; }

        internal string LayoutPath { get; }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }
}
