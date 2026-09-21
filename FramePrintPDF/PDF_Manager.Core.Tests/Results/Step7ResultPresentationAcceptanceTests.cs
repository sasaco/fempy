using PDF_Manager.Core.Analysis;
using PDF_Manager.Core.Documents;
using PDF_Manager.Core.Results;

namespace PDF_Manager.Core.Tests.Results;

public sealed class Step7ResultPresentationAcceptanceTests
{
    public static TheoryData<string, ResultCoordinate[]> CanonicalFixtureCoordinates => new()
    {
        {
            "multiple-static.json",
            [
                new ResultCoordinate("10", ResultStateKind.Static, 0),
                new ResultCoordinate("2", ResultStateKind.Static, 0),
            ]
        },
        {
            "nonlinear-steps.json",
            [
                new ResultCoordinate("NL", ResultStateKind.LoadStep, 0),
                new ResultCoordinate("NL", ResultStateKind.LoadStep, 1),
            ]
        },
        {
            "multiple-nonlinear.json",
            [
                new ResultCoordinate("A", ResultStateKind.LoadStep, 0),
                new ResultCoordinate("B", ResultStateKind.LoadStep, 0),
                new ResultCoordinate("B", ResultStateKind.LoadStep, 1),
            ]
        },
        {
            "modal.json",
            [
                new ResultCoordinate("1", ResultStateKind.Mode, 0),
                new ResultCoordinate("1", ResultStateKind.Mode, 1),
            ]
        },
    };

    [Theory]
    [MemberData(nameof(CanonicalFixtureCoordinates))]
    public void CanonicalPages_PreserveEveryCaseStateCoordinateWithoutFlattening(
        string fixture,
        ResultCoordinate[] expectedCoordinates)
    {
        AnalysisResultSet resultSet = Analysis.AnalysisContractFixtureTests.ReadPositive(fixture);
        ResultPresentationService service = new();

        IReadOnlyList<ResultPresentationPage> pages = service.BuildPages(resultSet, []);

        Assert.Equal(expectedCoordinates, resultSet.Results.Select(result => result.Coordinate));
        Assert.Equal(expectedCoordinates, pages.Select(page => page.PrimaryResult.Coordinate));
        Assert.Equal(
            expectedCoordinates.Select(coordinate => coordinate.CaseId),
            pages.Select(page => page.ResultCase.CaseId));
        Assert.All(pages, page =>
        {
            Assert.False(page.IsMovingLoad);
            Assert.Single(page.SourceResults);
            Assert.Same(page.PrimaryResult, page.SourceResults[0]);
            ResultPresentationSourcePage sourcePage = Assert.Single(page.SourcePages);
            Assert.True(sourcePage.IsParent);
            Assert.Equal(page.PrimaryResult.Coordinate, sourcePage.Result.Coordinate);
            Assert.Empty(page.ChildPages);
        });
    }

    [Fact]
    public void MovingLoadPages_ExposeOneParentAndOrderedChildPagesWithoutLosingSources()
    {
        AnalysisResultSet resultSet = CreateContinuumStaticSet();
        MovingLoadDefinition definition = new("MOVING", "Moving", ["A", "B"]);

        ResultPresentationPage page = Assert.Single(
            new ResultPresentationService().BuildPages(resultSet, [definition]));

        Assert.True(page.IsMovingLoad);
        Assert.Equal("MOVING", page.PageId);
        Assert.Equal(["A", "B"], page.SourceResults.Select(result => result.CaseId));
        Assert.Equal([0, 1], page.SourcePages.Select(source => source.CaseIndex));
        Assert.Equal(["A", "B"], page.SourcePages.Select(source => source.ResultCase.CaseId));
        Assert.Equal([true, false], page.SourcePages.Select(source => source.IsParent));
        ResultPresentationSourcePage child = Assert.Single(page.ChildPages);
        Assert.Equal("B", child.Result.CaseId);
        Assert.False(child.IsParent);
    }

    [Fact]
    public void AngularMovingFixture_GroupsLaterChildrenWithoutMovingTheOrdinarySecondPage()
    {
        AnalysisResultSet resultSet = CreateAngularMovingSet();
        MovingLoadDefinition definition = new("MOVING", "Moving load", ["1", "1.1", "1.2"]);

        IReadOnlyList<ResultPresentationPage> pages =
            new ResultPresentationService().BuildPages(resultSet, [definition]);

        Assert.Equal(["MOVING", "2:Static:0"], pages.Select(page => page.PageId));
        ResultPresentationPage moving = pages[0];
        Assert.Equal(["1", "1.1", "1.2"], moving.SourcePages.Select(page => page.ResultCase.CaseId));
        Assert.Equal([0, 2, 3], moving.SourcePages.Select(page => page.CaseIndex));
        Assert.Equal([true, false, false], moving.SourcePages.Select(page => page.IsParent));
        Assert.Equal(["1.1", "1.2"], moving.ChildPages.Select(page => page.ResultCase.CaseId));
        Assert.False(pages[1].IsMovingLoad);
        Assert.Equal("2", pages[1].ResultCase.CaseId);
    }

    [Fact]
    public void CtV1Fixture_PreservesAllElevenAssetCasesAndExactBoundaryPages()
    {
        AnalysisResultSet resultSet = ReadCtFixture();

        Assert.Equal(11, resultSet.Cases.Count);
        Assert.Equal(
            [
                ("1", "D1", "固定死荷重"),
                ("2", "D2", "付加死荷重"),
                ("3", "L1", "列車荷重１【保守線１＃】"),
                ("4", "L2", "列車荷重２【保守線２＃】"),
                ("5", "L3", "列車荷重３【保守線３＃】"),
                ("6", "L3'", "列車荷重４【分岐線】"),
                ("7", "LF1", "車両横荷重１【保守線１＃】"),
                ("8", "LF2", "車両横荷重２【保守線２＃】"),
                ("9", "LF3", "車両横荷重３【保守線３＃】"),
                ("10", "LF3'", "車両横荷重４【分岐線】"),
                ("11", "W", "風荷重"),
            ],
            resultSet.Cases.Select(value => (value.CaseId, value.Symbol, value.Name)));

        IReadOnlyList<ResultPresentationPage> pages =
            new ResultPresentationService().BuildPages(resultSet, []);

        Assert.Equal(11, pages.Count);
        Assert.Equal(new ResultCoordinate("1", ResultStateKind.Static, 0), pages[0].PrimaryResult.Coordinate);
        Assert.Equal("D1", pages[0].ResultCase.Symbol);
        Assert.Equal("固定死荷重", pages[0].ResultCase.Name);
        Assert.Equal(new ResultCoordinate("11", ResultStateKind.Static, 0), pages[^1].PrimaryResult.Coordinate);
        Assert.Equal("W", pages[^1].ResultCase.Symbol);
        Assert.Equal("風荷重", pages[^1].ResultCase.Name);
    }

    [Fact]
    public void BasicTables_PresentStaticNonlinearModalAndContinuumVariantsByExactCoordinate()
    {
        ResultPresentationService service = new();
        AnalysisResultSet staticSet = Analysis.AnalysisContractFixtureTests.ReadPositive("single-static.json");
        ResultTableSet staticTables = service.BuildTables(
            staticSet,
            new ResultCoordinate("D", ResultStateKind.Static, 0));
        Assert.False(staticTables.IsModal);
        Assert.Equal(0.001, staticTables.NodeDisplacements[1].Components.Dx, 12);
        Assert.Equal(-10, staticTables.SupportReactions[0].Components.Fx, 12);
        Assert.Equal(10, staticTables.MemberSectionForces[0].Segments[0].IEnd.Fx, 12);

        AnalysisResultSet nonlinear = Analysis.AnalysisContractFixtureTests.ReadPositive("nonlinear-steps.json");
        ResultTableSet nonlinearTables = service.BuildTables(
            nonlinear,
            new ResultCoordinate("NL", ResultStateKind.LoadStep, 1));
        Assert.False(nonlinearTables.IsModal);
        Assert.Equal(0.22, Assert.Single(nonlinearTables.NodeDisplacements).Components.Dx, 12);

        AnalysisResultSet modal = Analysis.AnalysisContractFixtureTests.ReadPositive("modal.json");
        ResultTableSet modalTables = service.BuildTables(
            modal,
            new ResultCoordinate("1", ResultStateKind.Mode, 1));
        Assert.True(modalTables.IsModal);
        Assert.Equal(1, Assert.Single(modalTables.NodeDisplacements).Components.Dy, 12);
        Assert.Empty(modalTables.SupportReactions);
        Assert.Empty(modalTables.MemberSectionForces);

        AnalysisResultSet continuum = CreateContinuumStaticSet();
        ResultTableSet continuumTables = service.BuildTables(
            continuum,
            new ResultCoordinate("A", ResultStateKind.Static, 0));
        Assert.Equal("SH1", Assert.Single(continuumTables.ShellResults).ElementId);
        Assert.Equal("SO1", Assert.Single(continuumTables.SolidResults).ElementId);
    }

    [Theory]
    [InlineData("nonlinear-steps.json", "NL", AnalysisType.MaterialNonlinear)]
    [InlineData("modal.json", "1", AnalysisType.Modal)]
    public void DerivedResults_RejectNonStaticOperandsWithTypedLocalizedDomainError(
        string fixture,
        string operandId,
        AnalysisType expectedAnalysisType)
    {
        AnalysisResultSet resultSet = Analysis.AnalysisContractFixtureTests.ReadPositive(fixture);
        DerivedResultDefinition definition = new(
            "DERIVED",
            "Derived",
            DerivedResultKind.Define,
            [new DerivedResultTerm(operandId, 1)]);

        NonStaticDerivedOperandException exception = Assert.Throws<NonStaticDerivedOperandException>(
            () => new ResultPresentationService().BuildDerivedResults(resultSet, [definition]));

        Assert.Equal("DERIVED", exception.DerivedResultId);
        Assert.Equal(operandId, exception.OperandId);
        Assert.Equal(expectedAnalysisType, exception.OperandAnalysisType);
        Assert.Equal("ResultDerivedStaticOnly", exception.ResourceKey);
    }

    [Fact]
    public void DerivedResults_UseStableFirstTieKeepBaseImmutableAndRetainShellAndSolidRows()
    {
        AnalysisResultSet resultSet = CreateContinuumStaticSet();
        StaticAnalysisResult originalA = Assert.IsType<StaticAnalysisResult>(resultSet.Results[0]);
        StaticAnalysisResult originalB = Assert.IsType<StaticAnalysisResult>(resultSet.Results[1]);
        double originalNodeValue = originalA.NodeDisplacements[1].Components.Dx;
        double originalShellValue = originalA.ShellResults[0].Locations[0].MembraneForce.Nx;
        double originalSolidValue = originalA.SolidResults[0].Locations[0].Stress.Sx;
        DerivedResultDefinition[] definitions =
        [
            new(
                "DEFINE",
                "Define",
                DerivedResultKind.Define,
                [new DerivedResultTerm("A", 2), new DerivedResultTerm("B", 0.5)]),
            new(
                "COMBINE",
                "Combine",
                DerivedResultKind.Combine,
                [new DerivedResultTerm("DEFINE", 2)]),
            new(
                "PICKUP",
                "Pickup",
                DerivedResultKind.Pickup,
                [new DerivedResultTerm("A", 1), new DerivedResultTerm("B", 1)]),
        ];

        IReadOnlyList<PresentedStaticResult> derived =
            new ResultPresentationService().BuildDerivedResults(resultSet, definitions);

        Assert.Equal(["DEFINE", "COMBINE", "PICKUP"], derived.Select(result => result.Id));
        Assert.Equal(7.5, derived[0].NodeDisplacements[1].Components.Dx, 12);
        Assert.Equal(15, derived[1].NodeDisplacements[1].Components.Dx, 12);
        Assert.Equal(5, derived[2].NodeDisplacements[1].Components.Dx, 12);
        Assert.Equal(3, derived[2].ShellResults[0].Locations[0].MembraneForce.Nx, 12);
        Assert.Equal(7, derived[2].SolidResults[0].Locations[0].Stress.Sx, 12);
        Assert.Equal(["A", "B"], derived[2].SourceIds);

        Assert.Equal(originalNodeValue, originalA.NodeDisplacements[1].Components.Dx);
        Assert.Equal(originalShellValue, originalA.ShellResults[0].Locations[0].MembraneForce.Nx);
        Assert.Equal(originalSolidValue, originalA.SolidResults[0].Locations[0].Stress.Sx);
        Assert.Equal(-5, originalB.NodeDisplacements[1].Components.Dx);
    }

    [Fact]
    public void MovingLoadEnvelope_PreservesSignedAndAbsoluteExtremaProvenanceWithStableTies()
    {
        AnalysisResultSet resultSet = CreateContinuumStaticSet();
        MovingLoadDefinition definition = new("MOVING", "Moving", ["A", "B"]);

        MovingLoadEnvelope envelope =
            new ResultPresentationService().BuildMovingLoadEnvelope(resultSet, definition);

        ScalarEnvelope reactionFx = envelope.SupportReactions[0].Components.Fx;
        Assert.Equal(new EnvelopeExtreme(10, "A"), reactionFx.Maximum);
        Assert.Equal(new EnvelopeExtreme(-10, "B"), reactionFx.Minimum);
        Assert.Equal(new EnvelopeExtreme(-10, "B"), reactionFx.AbsoluteMaximum);

        ScalarEnvelope memberFx = envelope.MemberSectionForces[0].Segments[0].IEnd.Fx;
        Assert.Equal(new EnvelopeExtreme(5, "A"), memberFx.Maximum);
        Assert.Equal(new EnvelopeExtreme(-5, "B"), memberFx.Minimum);
        Assert.Equal(new EnvelopeExtreme(5, "A"), memberFx.AbsoluteMaximum);

        MemberForceScalarExtrema globalFx = envelope.MemberForceExtrema.Fx;
        Assert.Equal(
            new MemberForceExtreme(5, "A", "M1", "S0-S1", MemberForceEnd.I),
            globalFx.Maximum);
        Assert.Equal(
            new MemberForceExtreme(-5, "A", "M1", "S0-S1", MemberForceEnd.J),
            globalFx.Minimum);
        Assert.Equal(
            new MemberForceExtreme(5, "A", "M1", "S0-S1", MemberForceEnd.I),
            globalFx.AbsoluteMaximum);
    }

    [Fact]
    public void AngularMovingFixture_ReactionSignedEnvelopeUsesAllSourcesButAbsoluteUsesChildrenOnly()
    {
        AnalysisResultSet resultSet = CreateAngularMovingSet();
        ResultPresentationService service = new();

        MovingLoadEnvelope envelope = service.BuildMovingLoadEnvelope(
            resultSet,
            new MovingLoadDefinition("MOVING", "Moving load", ["1", "1.1", "1.2"]));

        ForceEnvelopeComponents reaction = Assert.Single(envelope.SupportReactions).Components;
        ScalarEnvelope[] components = [reaction.Fx, reaction.Fy, reaction.Fz, reaction.Mx, reaction.My, reaction.Mz];
        double[] firstChild = [10, -20, 30, -40, 50, -60];
        double[] secondChild = [-10, 20, -30, 40, -50, 60];
        for (int index = 0; index < components.Length; index++)
        {
            Assert.Equal(new EnvelopeExtreme(100, "1"), components[index].Maximum);
            Assert.Equal(
                new EnvelopeExtreme(Math.Min(firstChild[index], secondChild[index]),
                    firstChild[index] < secondChild[index] ? "1.1" : "1.2"),
                components[index].Minimum);
            Assert.Equal(new EnvelopeExtreme(firstChild[index], "1.1"), components[index].AbsoluteMaximum);
        }

        SupportReactionAbsoluteMaximum absolute = Assert.Single(envelope.AbsoluteSupportReactions);
        Assert.Equal(new ForceComponents(10, -20, 30, -40, 50, -60), absolute.Components);
        Assert.Equal(new ForceSourceCaseIds("1.1", "1.1", "1.1", "1.1", "1.1", "1.1"), absolute.SourceCaseIds);
        SupportReaction projected = Assert.Single(envelope.AbsoluteReactionProjection);
        Assert.Equal(absolute.NodeId, projected.NodeId);
        Assert.Equal(absolute.Components, projected.Components);

        MovingLoadEnvelope parentOnly = service.BuildMovingLoadEnvelope(
            resultSet,
            new MovingLoadDefinition("PARENT", "Parent only", ["1"]));
        SupportReactionAbsoluteMaximum fallback = Assert.Single(parentOnly.AbsoluteSupportReactions);
        Assert.Equal(new ForceComponents(100, 100, 100, 100, 100, 100), fallback.Components);
        Assert.Equal(new ForceSourceCaseIds("1", "1", "1", "1", "1", "1"), fallback.SourceCaseIds);
    }

    private static AnalysisResultSet CreateContinuumStaticSet()
    {
        CoordinateFrame frame = new(
            new Vector3Value(0, 0, 0),
            new Vector3Value(1, 0, 0),
            new Vector3Value(0, 1, 0),
            new Vector3Value(0, 0, 1));
        TopologyNode[] nodes =
        [
            new("N1", new Vector3Value(0, 0, 0), "N1", false),
            new("N2", new Vector3Value(1, 0, 0), "N2", false),
            new("N3", new Vector3Value(0, 1, 0), "N3", false),
            new("N4", new Vector3Value(0, 0, 1), "N4", false),
        ];
        AnalysisTopology topology = new(
            nodes,
            [
                new TopologyMember(
                    "M1",
                    "N1",
                    "N2",
                    frame,
                    [new MemberStation("S0", 0), new MemberStation("S1", 1)]),
            ],
            [
                new TopologyShellElement(
                    "SH1",
                    ShellElementType.Triangle3,
                    ["N1", "N2", "N3"],
                    frame,
                    [new ShellResultLocationDefinition("element_average", "element_average")]),
            ],
            [
                new TopologySolidElement(
                    "SO1",
                    SolidElementType.Tetra4,
                    ["N1", "N2", "N3", "N4"],
                    "global",
                    [new SolidResultLocationDefinition("GP0", new NaturalCoordinates(0.25, 0.25, 0.25))]),
            ]);
        AnalysisResultSet resultSet = new(
            AnalysisResultSet.ContractKind,
            AnalysisResultSet.ContractVersion,
            new AnalysisUnits("SI", "m", "N", "kg", "s"),
            new CoordinateSystem("global_cartesian", "right", ["x", "y", "z"]),
            [
                new AnalysisCase("A", "A", "A", AnalysisType.Static, ["N1"]),
                new AnalysisCase("B", "B", "B", AnalysisType.Static, ["N1"]),
            ],
            topology,
            [
                CreateContinuumResult("A", 5, 10, 5, 3, 7),
                CreateContinuumResult("B", -5, -10, -5, -3, -7),
            ]);
        AnalysisResultSetValidator.Validate(resultSet);
        return resultSet;
    }

    private static StaticAnalysisResult CreateContinuumResult(
        string caseId,
        double displacement,
        double reaction,
        double memberForce,
        double shellForce,
        double solidStress) => new(
        caseId,
        [
            new NodeDisplacement("N1", new DisplacementComponents(0, 0, 0, 0, 0, 0)),
            new NodeDisplacement("N2", new DisplacementComponents(displacement, 0, 0, 0, 0, 0)),
            new NodeDisplacement("N3", new DisplacementComponents(0, 0, 0, 0, 0, 0)),
            new NodeDisplacement("N4", new DisplacementComponents(0, 0, 0, 0, 0, 0)),
        ],
        [new SupportReaction("N1", new ForceComponents(reaction, 0, 0, 0, 0, 0))],
        [
            new MemberSectionForces(
                "M1",
                [
                    new MemberSegmentResult(
                        "S0-S1",
                        "S0",
                        "S1",
                        1,
                        new ForceComponents(memberForce, 0, 0, 0, 0, 0),
                        new ForceComponents(-memberForce, 0, 0, 0, 0, 0)),
                ]),
        ],
        [
            new ShellResult(
                "SH1",
                [
                    new ShellResultLocation(
                        "element_average",
                        new MembraneForce(shellForce, 0, 0),
                        new BendingMoment(0, 0, 0),
                        new TransverseShear(0, 0),
                        new PlaneStress(0, 0, 0),
                        new PlaneStress(0, 0, 0)),
                ]),
        ],
        [
            new SolidResult(
                "SO1",
                [
                    new SolidResultLocation(
                        "GP0",
                        new Stress3D(solidStress, 0, 0, 0, 0, 0),
                        new Strain3D(0, 0, 0, 0, 0, 0)),
                ]),
        ],
        new WarningDiagnostics([]));

    internal static AnalysisResultSet CreateAngularMovingSet()
    {
        AnalysisResultSet source = Analysis.AnalysisContractFixtureTests.ReadPositive("single-static.json");
        StaticAnalysisResult template = Assert.IsType<StaticAnalysisResult>(source.Results[0]);
        AnalysisCase[] cases =
        [
            new("1", "Moving load", "LL", AnalysisType.Static, ["1"]),
            new("2", "Following dead load", "D", AnalysisType.Static, ["1"]),
            new("1.1", "Moving load position 1", "LL", AnalysisType.Static, ["1"]),
            new("1.2", "Moving load position 2", "LL", AnalysisType.Static, ["1"]),
        ];
        AnalysisResultSet resultSet = new(
            source.Kind,
            source.SchemaVersion,
            source.Units,
            source.CoordinateSystem,
            cases,
            source.Topology,
            [
                WithReaction(template, "1", new ForceComponents(100, 100, 100, 100, 100, 100)),
                WithReaction(template, "2", new ForceComponents(200, 200, 200, 200, 200, 200)),
                WithReaction(template, "1.1", new ForceComponents(10, -20, 30, -40, 50, -60)),
                WithReaction(template, "1.2", new ForceComponents(-10, 20, -30, 40, -50, 60)),
            ]);
        AnalysisResultSetValidator.Validate(resultSet);
        return resultSet;
    }

    internal static AnalysisResultSet ReadCtFixture()
    {
        string path = Path.Combine(
            FindRepositoryRoot(),
            "FramePrintPDF",
            "PDF_Manager.Core.Tests",
            "Results",
            "Fixtures",
            "ct-analysis-result-set-v1.fixture");
        return AnalysisResultSetJson.Deserialize(File.ReadAllBytes(path));
    }

    private static StaticAnalysisResult WithReaction(
        StaticAnalysisResult template,
        string caseId,
        ForceComponents components) => new(
        caseId,
        template.NodeDisplacements,
        [new SupportReaction(template.SupportReactions[0].NodeId, components)],
        template.MemberSectionForces,
        template.ShellResults,
        template.SolidResults,
        template.Diagnostics);

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
