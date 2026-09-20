using PDF_Manager.Core.Documents;

namespace PDF_Manager.Core.Tests.Documents;

internal static class ProjectDocumentTestData
{
    internal static ProjectDocument Create(bool isDirty = true)
    {
        return new ProjectDocument(
            ProjectDocument.CurrentVersion,
            new ProjectMetadata("Portal frame", "Representative model", "FrameWeb", "kN-m"),
            [
                new ProjectNode("N2", 4, 0, 0),
                new ProjectNode("N1", 0, 0, 0),
            ],
            [new ProjectMember("M1", "N1", "N2")],
            [new ProjectSupport("S1", "N1", true, true, true, true, true, true)],
            [new LoadCaseDefinition("D", "Dead", "D")],
            [new NodalLoadDefinition("P1", "D", "N2", 0, -10, 0, 0, 0, 0)],
            [
                new DerivedResultDefinition(
                    "DEF1",
                    "Factored dead",
                    DerivedResultKind.Define,
                    [new DerivedResultTerm("D", 1.2)]),
                new DerivedResultDefinition(
                    "PICK1",
                    "Picked result",
                    DerivedResultKind.Pickup,
                    [new DerivedResultTerm("DEF1", 1)]),
            ],
            [new MovingLoadDefinition("MOVE1", "Moving load", ["D"])],
            new ProjectSelection(["N2"], ["M1"]),
            isDirty);
    }
}
