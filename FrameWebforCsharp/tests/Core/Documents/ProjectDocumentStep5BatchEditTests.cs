using PDF_Manager.Core.Documents;

namespace PDF_Manager.Core.Tests.Documents;

public sealed class ProjectDocumentStep5BatchEditTests
{
    [Fact]
    public void MultiRowCreateEditDelete_CommitsOnceAndUndoesInOneStep()
    {
        ProjectDocument initial = Step5DocumentFactory.Create();
        ProjectDocumentEditSession session = new(initial);

        Assert.True(session.ApplyBatch(batch =>
        {
            batch.RemoveNoticePoint("NP1");
            batch.UpsertNoticePoint(new NoticePointDefinition("NP2", "1", 2));
            batch.UpsertNoticePoint(new NoticePointDefinition("NP3", "1", 8));
            batch.UpsertRigidZone(new RigidZoneDefinition("1", "1", 0.5, 0.5, "1"));
        }));

        Assert.Equal(["NP2", "NP3"], session.Current.NoticePoints.Select(value => value.Id));
        Assert.Equal(0.5, Assert.Single(session.Current.RigidZones).ILength);
        Assert.True(session.Undo());
        Assert.Same(initial, session.Current);
        Assert.False(session.CanUndo);
        Assert.True(session.CanRedo);
    }

    [Fact]
    public void InvalidBatch_RollsBackDocumentAndPreservesRedo()
    {
        ProjectDocument initial = Step5DocumentFactory.Create();
        ProjectDocumentEditSession session = new(initial);
        Assert.True(session.UpsertNodalLoad(
            new NodalLoadDefinition("N1", "1", "2", 0, -11, 0, 0, 0, 0)));
        Assert.True(session.Undo());
        Assert.True(session.CanRedo);

        Assert.Throws<ProjectDocumentValidationException>(() => session.ApplyBatch(batch =>
        {
            batch.UpsertNoticePoint(new NoticePointDefinition("NP2", "1", 2));
            batch.UpsertRigidZone(new RigidZoneDefinition("1", "missing", 0, 0, "1"));
        }));

        Assert.Same(initial, session.Current);
        Assert.False(session.CanUndo);
        Assert.True(session.CanRedo);
        Assert.DoesNotContain(session.Current.NoticePoints, value => value.Id == "NP2");
    }

    [Fact]
    public void NoOpBatch_DoesNotCreateHistoryOrClearRedo()
    {
        ProjectDocumentEditSession session = new(Step5DocumentFactory.Create());
        Assert.True(session.UpsertNodalLoad(
            new NodalLoadDefinition("N1", "1", "2", 0, -11, 0, 0, 0, 0)));
        Assert.True(session.Undo());

        Assert.False(session.ApplyBatch(_ => { }));
        Assert.False(session.CanUndo);
        Assert.True(session.CanRedo);
    }

    [Fact]
    public void SetDimension_IsAtomicAndCreatesOneUndoEntry()
    {
        ProjectDocument initial = Step5DocumentFactory.Create();
        ProjectDocumentEditSession session = new(initial);

        Assert.True(session.ApplyBatch(batch =>
        {
            batch.SetDimension(ModelDimension.TwoDimensional);
            batch.UpsertNoticePoint(new NoticePointDefinition("NP2", "1", 2));
        }));

        Assert.Equal(ModelDimension.TwoDimensional, session.Current.Dimension);
        Assert.Contains(session.Current.NoticePoints, point => point.Id == "NP2");
        Assert.True(session.Undo());
        Assert.Same(initial, session.Current);
        Assert.False(session.CanUndo);
    }

    [Fact]
    public void SessionPublishesTypedUpsertAndRemoveCoverageForEveryStep5Entity()
    {
        string[] expectedMethods =
        [
            "ElementPropertySet",
            "RigidZone",
            "SupportSet",
            "Panel",
            "JointReleaseSet",
            "NoticePoint",
            "MemberSpringSet",
            "PrescribedDisplacement",
            "MemberLoad",
            "DerivedResult",
            "MovingLoad",
        ];
        HashSet<string> publicMethods = typeof(ProjectDocumentEditSession)
            .GetMethods()
            .Select(method => method.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (string suffix in expectedMethods)
        {
            Assert.Contains($"Upsert{suffix}", publicMethods);
            Assert.Contains($"Remove{suffix}", publicMethods);
        }
    }
}
