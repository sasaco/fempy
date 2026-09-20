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
            [new ProjectMember("M1", "N1", "N2", "SEC1")],
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
            isDirty,
            sections:
            [
                new FrameSectionDefinition(
                    "SEC1",
                    "Test section",
                    210_000_000,
                    0.3,
                    80_769_230.76923077,
                    0.01,
                    8.333333333333334e-6,
                    8.333333333333334e-6,
                    1.6666666666666667e-5),
            ]);
    }
}
