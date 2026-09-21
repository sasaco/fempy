using System.Text;
using PDF_Manager.Core.Analysis;
using PDF_Manager.Core.Documents;

namespace PDF_Manager.Core.Tests.Analysis;

public sealed class FrameWebAnalysisRequestJsonTests
{
    private const string GoldenRequest = "{\"analysis_type\":\"static\",\"model_metadata\":{\"units\":{\"system\":\"consistent_user_defined\",\"length\":\"m\",\"force\":\"kN\",\"mass\":\"unspecified\",\"time\":\"unspecified\"}},\"node\":{\"1\":{\"x\":0,\"y\":0,\"z\":0},\"2\":{\"x\":4,\"y\":0,\"z\":0}},\"member\":{\"1\":{\"ni\":1,\"nj\":2,\"e\":1,\"cg\":0,\"shear_correction\":false}},\"element\":{\"1\":{\"1\":{\"n\":\"Steel rectangular section\",\"E\":210000000,\"nu\":0.3,\"G\":80769230.76923077,\"A\":0.01,\"Iy\":8.333333333333334E-06,\"Iz\":8.333333333333334E-06,\"J\":1.6666666666666667E-05}}},\"fix_node\":{\"1\":[{\"n\":1,\"tx\":1,\"ty\":1,\"tz\":1,\"rx\":1,\"ry\":1,\"rz\":1}]},\"fix_member\":{\"1\":[]},\"joint\":{\"1\":[]},\"load\":{\"1\":{\"name\":\"Service\",\"symbol\":\"S\",\"element\":1,\"fix_node\":1,\"fix_member\":1,\"joint\":1,\"load_node\":[{\"n\":2,\"tx\":0,\"ty\":-10,\"tz\":0,\"rx\":0,\"ry\":0,\"rz\":0}]}}}";

    [Fact]
    public void RepresentativePreset_MatchesCurrentPythonInputMeaningByteForByte()
    {
        byte[] bytes = FrameWebAnalysisRequestJson.Serialize(
            ProjectDocumentPresets.CreateRepresentativeFrame());

        Assert.Equal(GoldenRequest, Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public void IncompleteOrNonNumericModelIdentity_IsRejectedInsteadOfReceivingDefaults()
    {
        ProjectDocument complete = ProjectDocumentPresets.CreateRepresentativeFrame();
        ProjectDocument missingSection = new(
            complete.Version,
            complete.Metadata,
            complete.Nodes,
            [new ProjectMember("1", "1", "2")],
            complete.Supports,
            complete.LoadCases,
            complete.NodalLoads,
            complete.DerivedResults,
            complete.MovingLoads,
            sections: complete.Sections);
        ProjectDocument nonNumeric = new(
            complete.Version,
            complete.Metadata,
            [new ProjectNode("N1", 0, 0, 0), new ProjectNode("N2", 4, 0, 0)],
            [new ProjectMember("M1", "N1", "N2", "1")],
            [new ProjectSupport("S1", "N1", true, true, true, true, true, true)],
            complete.LoadCases,
            [new NodalLoadDefinition("P1", "1", "N2", 0, -10, 0, 0, 0, 0)],
            complete.DerivedResults,
            complete.MovingLoads,
            sections: complete.Sections);

        Assert.Throws<FrameWebAnalysisRequestException>(() =>
            FrameWebAnalysisRequestJson.Serialize(missingSection));
        Assert.Throws<FrameWebAnalysisRequestException>(() =>
            FrameWebAnalysisRequestJson.Serialize(nonNumeric));
    }
}
