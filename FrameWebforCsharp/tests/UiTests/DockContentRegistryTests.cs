using PDF_Manager.Core.Shell;
using PDF_Manager.Shell.Docking;
using WeifenLuo.WinFormsUI.Docking;

namespace PDF_Manager.UiTests;

public sealed class DockContentRegistryTests
{
    [Fact]
    public void SameToolKey_CreatesOnceAndReusesHiddenInstance()
    {
        StaTestRunner.Run(() =>
        {
            using DockTestHost host = new();
            DocumentKey key = DocumentKey.Tool("project.navigator");
            int factoryCalls = 0;
            int created = 0;
            int activated = 0;
            host.Registry.ContentCreated += (_, _) => created++;
            host.Registry.ContentActivated += (_, _) => activated++;
            host.Registry.Register(key, () =>
            {
                factoryCalls++;
                return new TestDockContent(key);
            });

            DockContent first = host.Registry.Open(key);
            DockContent second = host.Registry.Open(key);
            Assert.Same(first, second);
            Assert.Equal(1, factoryCalls);
            Assert.Equal(1, created);
            Assert.Equal(2, activated);

            Assert.True(host.Registry.Close(key));
            Assert.False(first.IsDisposed);
            Assert.Equal(WeifenLuo.WinFormsUI.Docking.DockState.Hidden, first.DockState);
            Assert.True(host.Registry.TryGet(key, out DockContent? hidden));
            Assert.Same(first, hidden);

            DockContent reopened = host.Registry.Open(key);
            Assert.Same(first, reopened);
            Assert.Equal(1, factoryCalls);
            Assert.Equal(3, activated);
        }, "Dock registry tool reuse");
    }

    [Fact]
    public void DifferentDocumentKeys_CoexistAndClosingOneDisposesOnlyThatDocument()
    {
        StaTestRunner.Run(() =>
        {
            using DockTestHost host = new();
            DocumentKey alpha = DocumentKey.Document("project:alpha");
            DocumentKey beta = DocumentKey.Document("project:beta");
            host.Registry.Register(alpha, () => new TestDockContent(alpha));
            host.Registry.Register(beta, () => new TestDockContent(beta));
            int removed = 0;
            host.Registry.ContentRemoved += (_, _) => removed++;

            DockContent alphaContent = host.Registry.Open(alpha);
            DockContent betaContent = host.Registry.Open(beta);

            Assert.NotSame(alphaContent, betaContent);
            Assert.Equal(2, host.Registry.Contents.Count);
            Assert.True(host.Registry.Close(alpha));
            Assert.True(alphaContent.IsDisposed);
            Assert.False(betaContent.IsDisposed);
            Assert.False(host.Registry.TryGet(alpha, out _));
            Assert.True(host.Registry.TryGet(beta, out DockContent? remaining));
            Assert.Same(betaContent, remaining);
            Assert.Equal(1, removed);
        }, "Dock registry document identity");
    }

    [Fact]
    public void InvalidFactories_FailFastWithoutPublishingContent()
    {
        StaTestRunner.Run(() =>
        {
            using DockTestHost host = new();
            DocumentKey nullDelegateKey = DocumentKey.Tool("tool:null-delegate");
            DocumentKey nullKey = DocumentKey.Tool("tool:null");
            DocumentKey unkeyedKey = DocumentKey.Tool("tool:unkeyed");
            DocumentKey mismatchKey = DocumentKey.Document("project:mismatch");
            DocumentKey throwingKey = DocumentKey.Document("project:throwing");
            Assert.Throws<ArgumentNullException>(() => host.Registry.Register(nullDelegateKey, null!));
            Assert.False(host.Registry.IsRegistered(nullDelegateKey));
            host.Registry.Register(nullKey, static () => null!);
            host.Registry.Register(unkeyedKey, static () => new DockContent());
            host.Registry.Register(
                mismatchKey,
                static () => new TestDockContent(DocumentKey.Document("project:other")));
            InvalidOperationException factoryFailure = new("factory failure");
            host.Registry.Register(throwingKey, () => throw factoryFailure);

            Assert.Equal(nullKey, Assert.Throws<DockContentFactoryException>(() => host.Registry.Open(nullKey)).Key);
            Assert.Equal(unkeyedKey, Assert.Throws<DockContentFactoryException>(() => host.Registry.Open(unkeyedKey)).Key);
            Assert.Equal(mismatchKey, Assert.Throws<DockContentFactoryException>(() => host.Registry.Open(mismatchKey)).Key);
            DockContentFactoryException wrapped = Assert.Throws<DockContentFactoryException>(
                () => host.Registry.Open(throwingKey));
            Assert.Equal(throwingKey, wrapped.Key);
            Assert.Same(factoryFailure, wrapped.InnerException);
            Assert.Empty(host.Registry.Contents);
        }, "Dock registry invalid factories");
    }

    [Fact]
    public void RepeatedDocumentOpenClose_DoesNotDuplicateRemovalSubscriptions()
    {
        StaTestRunner.Run(() =>
        {
            using DockTestHost host = new();
            DocumentKey key = DocumentKey.Document("project:stress");
            int factoryCalls = 0;
            int created = 0;
            int activated = 0;
            int removed = 0;
            host.Registry.Register(key, () =>
            {
                factoryCalls++;
                return new TestDockContent(key);
            });
            host.Registry.ContentCreated += (_, _) => created++;
            host.Registry.ContentActivated += (_, _) => activated++;
            host.Registry.ContentRemoved += (_, _) => removed++;

            for (int cycle = 0; cycle < 50; cycle++)
            {
                DockContent content = host.Registry.Open(key);
                Assert.True(host.Registry.Close(key));
                Assert.True(content.IsDisposed);
                Assert.False(host.Registry.TryGet(key, out _));
            }

            Assert.Equal(50, factoryCalls);
            Assert.Equal(50, created);
            Assert.Equal(50, activated);
            Assert.Equal(50, removed);
            Assert.Empty(host.Registry.Contents);
        }, "Dock registry subscription stress");
    }
}
