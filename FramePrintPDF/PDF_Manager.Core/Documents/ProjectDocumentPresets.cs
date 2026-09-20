namespace PDF_Manager.Core.Documents;

/// <summary>Validated built-in documents whose persisted values are sufficient for analysis.</summary>
public static class ProjectDocumentPresets
{
    public static ProjectDocument CreateRepresentativeFrame()
    {
        FrameSectionDefinition section = new(
            "1",
            "Steel rectangular section",
            YoungsModulus: 210_000_000,
            PoissonRatio: 0.3,
            ShearModulus: 80_769_230.76923077,
            Area: 0.01,
            MomentOfInertiaY: 8.333333333333334e-6,
            MomentOfInertiaZ: 8.333333333333334e-6,
            TorsionConstant: 1.6666666666666667e-5);

        return new ProjectDocument(
            ProjectDocument.CurrentVersion,
            new ProjectMetadata(
                "Representative cantilever",
                "Two-node static frame used by the vertical MVP.",
                "FrameWeb",
                "kN-m"),
            [
                new ProjectNode("1", 0, 0, 0),
                new ProjectNode("2", 4, 0, 0),
            ],
            [new ProjectMember("1", "1", "2", section.Id, 0, false)],
            [new ProjectSupport("S1", "1", true, true, true, true, true, true)],
            [new LoadCaseDefinition("1", "Service", "S")],
            [new NodalLoadDefinition("P1", "1", "2", 0, -10, 0, 0, 0, 0)],
            [],
            [],
            ProjectSelection.Empty,
            isDirty: false,
            sections: [section]);
    }
}
