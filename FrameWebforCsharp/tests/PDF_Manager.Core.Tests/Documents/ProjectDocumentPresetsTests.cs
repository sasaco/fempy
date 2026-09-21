using PDF_Manager.Core.Documents;

namespace PDF_Manager.Core.Tests.Documents;

public sealed class ProjectDocumentPresetsTests
{
    [Fact]
    public void RepresentativeFrame_IsAValidatedCompletePersistedAnalysisInput()
    {
        ProjectDocument document = ProjectDocumentPresets.CreateRepresentativeFrame();

        ProjectDocumentValidator.Validate(document);
        Assert.Equal(ProjectDocument.CurrentVersion, document.Version);
        Assert.Equal(2, document.Nodes.Count);
        ProjectMember member = Assert.Single(document.Members);
        FrameSectionDefinition section = Assert.Single(document.Sections);
        Assert.Equal(section.Id, member.SectionId);
        Assert.True(section.YoungsModulus > 0);
        Assert.True(section.ShearModulus > 0);
        Assert.True(section.Area > 0);
        Assert.True(section.MomentOfInertiaY > 0);
        Assert.True(section.MomentOfInertiaZ > 0);
        Assert.True(section.TorsionConstant > 0);
        Assert.Single(document.Supports);
        Assert.Single(document.LoadCases);
        Assert.Single(document.NodalLoads);
        Assert.False(document.IsDirty);

        ProjectDocument restored = ProjectDocumentJson.Deserialize(ProjectDocumentJson.Serialize(document));
        Assert.Equal(section, Assert.Single(restored.Sections));
        Assert.Equal(member, Assert.Single(restored.Members));
    }
}
