using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using PDF_Manager.Core.Abstractions;
using PDF_Manager.Core.Analysis;
using PDF_Manager.Core.Documents;
using PDF_Manager.Core.Results;
using PDF_Manager.Core.Shell;
using PDF_Manager.Printing;
using PDF_Manager.Resources;
using PDF_Manager.Shell.Contents;
using PDF_Manager.Shell.Editing;
using PDF_Manager.Shell.Printing;
using CorePageSettings = PDF_Manager.Core.Abstractions.PrintPageSettings;

namespace PDF_Manager.UiTests;

public sealed class Step8DesktopProjectionRemediationTests
{
    [Fact]
    public void ResultContextMatrix_PreservesStaticNonlinearModalDerivedAndMovingSelectionThroughExport()
    {
        StaTestRunner.Run(() =>
        {
            List<ResultProjectionContext> contexts = CreateResultProjectionContexts();
            LocalizationService localization = new(UiLanguage.English);
            DesktopPrintJobFactory factory = new(localization);
            DesktopPdfExporter exporter = new(jobFactory: factory, localization: localization);
            List<string> planIdentities = [];

            foreach (ResultProjectionContext context in contexts)
            {
                ResultCoordinate[] coordinates = context.Selection.Coordinate is ResultCoordinate coordinate
                    ? [coordinate]
                    : [];
                PrintContentSection[] origins =
                [
                    PrintContentSection.DisplacementResults,
                    PrintContentSection.ReactionResults,
                    PrintContentSection.MemberForceResults,
                ];
                PrintExportRequest request = new(
                    context.Document,
                    context.ResultSet,
                    coordinates,
                    sections: origins,
                    selectedResult: context.Selection);

                DesktopPrintJob projection = factory.CreateProjection(request, PrintDiagramCaptureSet.Empty);
                PrintResult[] printed = projection.Job.Sections
                    .Cast<PrintResultSection>()
                    .Select(section => section.Result)
                    .ToArray();
                ResultTableSet source = context.Selection.Tables;
                Assert.Equal(origins, projection.SectionOrigins);
                Assert.Equal(
                    [PrintResultQuantity.Displacement, PrintResultQuantity.Reaction, PrintResultQuantity.SectionForce],
                    printed.Select(result => result.Quantity));
                Assert.All(printed, result =>
                {
                    Assert.Equal(source.Coordinate?.CaseId ?? source.Id, result.CaseId);
                    Assert.Equal(context.Selection.Provenance, result.Provenance);
                });
                Assert.Equal(source.NodeDisplacements.Count, printed[0].Table.Rows.Count);
                Assert.Equal(source.SupportReactions.Count, printed[1].Table.Rows.Count);
                Assert.Equal(
                    source.MemberSectionForces.Sum(member => member.Segments.Count) * 2,
                    printed[2].Table.Rows.Count);
                if (printed[2].Table.Rows.Count > 0)
                {
                    Assert.Equal(
                        ["I", "J"],
                        printed[2].Table.Rows.Take(2).Select(row => row.Cells[2]));
                }

                using MemoryStream pdf = new();
                PrintPreviewResult preview = exporter.PreviewAsync(request, PrintDiagramCaptureSet.Empty)
                    .GetAwaiter().GetResult();
                PrintExportReceipt receipt = exporter.ExportWithResultAsync(
                        request,
                        PrintDiagramCaptureSet.Empty,
                        pdf)
                    .GetAwaiter().GetResult();
                Assert.Equal(preview.PlanIdentity, receipt.PlanIdentity);
                Assert.All(preview.Pages, page =>
                    Assert.StartsWith(preview.PlanIdentity + ":", page.PageIdentity, StringComparison.Ordinal));
                byte[] pdfBytes = pdf.ToArray();
                AssertPdfMapsEveryCharacter(pdfBytes, "IJ" + (context.Selection.Provenance ?? string.Empty));
                string extractedText = ExtractMappedPdfText(pdfBytes);
                if (printed[2].Table.Rows.Count > 0)
                {
                    Assert.Contains("I", extractedText, StringComparison.Ordinal);
                    Assert.Contains("J", extractedText, StringComparison.Ordinal);
                }

                foreach (string cell in printed
                    .SelectMany(result => result.Table.Rows.Take(2))
                    .SelectMany(row => row.Cells)
                    .Where(cell => cell.Length > 0)
                    .Distinct(StringComparer.Ordinal))
                {
                    Assert.Contains(cell, extractedText, StringComparison.Ordinal);
                }

                planIdentities.Add(preview.PlanIdentity);
            }

            Assert.Equal(contexts.Count, planIdentities.Distinct(StringComparer.Ordinal).Count());
            AssertContext(contexts, "static", ResultStateKind.Static, 0, derivedKind: null, moving: false, "Static");
            AssertContext(contexts, "nonlinear", ResultStateKind.LoadStep, 1, derivedKind: null, moving: false, "LoadStep");
            AssertContext(contexts, "modal", ResultStateKind.Mode, 1, derivedKind: null, moving: false, "Mode");
            AssertContext(contexts, "DEFINE", null, null, DerivedResultKind.Define, moving: false, "DEFINE");
            AssertContext(contexts, "COMBINE", null, null, DerivedResultKind.Combine, moving: false, "COMBINE");
            AssertContext(contexts, "PICKUP", null, null, DerivedResultKind.Pickup, moving: false, "PICKUP");
            AssertContext(contexts, "moving-parent", ResultStateKind.Static, 0, derivedKind: null, moving: true, "role=parent");
            AssertContext(contexts, "moving-child", ResultStateKind.Static, 0, derivedKind: null, moving: true, "role=child");
        }, "Step 8 typed result print matrix", TimeSpan.FromSeconds(45));
    }

    [Fact]
    public void InputProjection_ContainsAllTwentyOneEditorSurfacesInOrderThenMovingLoadsWithinSharedBudget()
    {
        ProjectDocument document = WithMovingLoad(Step5EditorMatrixTests.CreateFullDocument());
        LocalizationService localization = new(UiLanguage.English);
        PrintExportRequest request = new(
            document,
            resultSet: null,
            sections: [PrintContentSection.InputTables]);

        DesktopPrintJob projection = new DesktopPrintJobFactory(localization)
            .CreateProjection(request, PrintDiagramCaptureSet.Empty);
        PrintTableSection[] tables = projection.Job.Sections.Cast<PrintTableSection>().ToArray();

        string[] editorCaptionResources =
        [
            "EditorModelSettings",
            "EditorNodes",
            "EditorMembers",
            "EditorRigidZones",
            "EditorElementPropertySets",
            "EditorSupports",
            "EditorSupportSets",
            "EditorSections",
            "EditorPanels",
            "EditorJoints",
            "EditorJointReleaseSets",
            "EditorNoticePoints",
            "EditorMemberSprings",
            "EditorMemberSpringSets",
            "EditorLoadCases",
            "EditorLoads",
            "EditorPrescribedDisplacements",
            "EditorMemberLoads",
            "EditorDefine",
            "EditorCombine",
            "EditorPickup",
        ];
        Assert.Equal(Enum.GetValues<InputTableKey>().Length, editorCaptionResources.Length);
        Assert.Equal(22, tables.Length);
        Assert.Equal(
            editorCaptionResources.Select(resource => localization[resource]),
            tables.Take(21).Select(section => section.Table.Caption));
        Assert.Equal(localization["EditorMovingLoads"], tables[21].Table.Caption);
        Assert.Equal(
            Enumerable.Repeat(PrintContentSection.InputTables, 22),
            projection.SectionOrigins);

        int[] expectedRowCounts =
        [
            1,
            document.Nodes.Count,
            document.Members.Count,
            document.RigidZones.Count,
            document.ElementPropertySets.Count,
            document.Supports.Count + document.SupportSets.Sum(set => set.Rows.Count),
            document.SupportSets.Count,
            document.Sections.Count + document.ElementPropertySets.Sum(set => set.Sections.Count),
            document.Panels.Count,
            document.JointReleaseSets.Sum(set => set.Rows.Count),
            document.JointReleaseSets.Count,
            document.NoticePoints.Count,
            document.MemberSpringSets.Sum(set => set.Rows.Count),
            document.MemberSpringSets.Count,
            document.LoadCases.Count,
            document.NodalLoads.Count,
            document.PrescribedDisplacements.Count,
            document.MemberLoads.Count,
            document.DerivedResults.Count(result => result.Kind == DerivedResultKind.Define),
            document.DerivedResults.Count(result => result.Kind == DerivedResultKind.Combine),
            document.DerivedResults.Count(result => result.Kind == DerivedResultKind.Pickup),
            document.MovingLoads.Count,
        ];
        Assert.Equal(expectedRowCounts, tables.Select(section => section.Table.Rows.Count));
        Assert.All(tables, section => Assert.NotEmpty(section.Table.Rows));

        string[] stableContentMarkers =
        [
            document.Dimension.ToString(), "5", "5", "R1", "Alternate", "SS1", "Secondary",
            "Alternate section", "PL1", "J1", "Alternate", "N1", "MS1", "Alternate", "1",
            "1", "D1", "ML1", "DF1", "CB1", "PK1", "MOV1",
        ];
        for (int index = 0; index < tables.Length; index++)
        {
            Assert.Contains(
                stableContentMarkers[index],
                tables[index].Table.Rows.SelectMany(row => row.Cells));
        }

        PrintDocumentPlan plan = new TypedPdfDocumentWriter().Plan(projection.Job);
        Assert.True(plan.PageCount > 0);
        Assert.Equal(64, plan.Identity.Value.Length);
    }

    [Fact]
    public async Task SemanticDiagramCaptures_RemainDistinctThroughFactoryPreviewAndPdfImageStreams()
    {
        AnalysisResultSet resultSet = ReadResultFixture("single-static.json");
        ResultCoordinate coordinate = resultSet.Results[0].Coordinate;
        ResultTableSet tables = new ResultPresentationService().BuildTables(resultSet, coordinate);
        PrintResultSelection selection = new(tables, coordinate, coordinate.ToString());
        ViewportCapture model = SolidCapture(3, 2, 230, 20, 30);
        ViewportCapture load = SolidCapture(3, 2, 20, 220, 40);
        ViewportCapture result = SolidCapture(3, 2, 30, 40, 210);
        PrintDiagramCaptureSet captures = new(
        [
            new KeyValuePair<PrintDiagramKind, ViewportCapture>(PrintDiagramKind.Model, model),
            new KeyValuePair<PrintDiagramKind, ViewportCapture>(PrintDiagramKind.Load, load),
            new KeyValuePair<PrintDiagramKind, ViewportCapture>(PrintDiagramKind.Result, result),
        ]);
        PrintExportRequest request = new(
            ProjectDocumentPresets.CreateRepresentativeFrame(),
            resultSet,
            [coordinate],
            pageSettings: new CorePageSettings(
                PDF_Manager.Core.Abstractions.PrintPaperSize.A4,
                PDF_Manager.Core.Abstractions.PrintPageOrientation.Portrait,
                layout: PrintLayoutChoice.SectionPerPage),
            sections:
            [
                PrintContentSection.ModelDiagram,
                PrintContentSection.LoadDiagram,
                PrintContentSection.ResultDiagram,
            ],
            selectedResult: selection);
        LocalizationService localization = new(UiLanguage.English);
        DesktopPrintJobFactory factory = new(localization);

        DesktopPrintJob projection = factory.CreateProjection(request, captures);
        PrintDiagram[] projected = projection.Job.Sections
            .Cast<PrintDiagramSection>()
            .Select(section => section.Diagram)
            .ToArray();
        Assert.Equal(
            [PrintDiagramKind.Model, PrintDiagramKind.Load, PrintDiagramKind.Result],
            projected.Select(diagram => diagram.Kind));
        Assert.Equal(
            captures.Captures.OrderBy(pair => pair.Key).Select(pair => Hash(pair.Value.Rgb24.Span)).Order(),
            projected.Select(diagram => Hash(diagram.Capture.Rgb24.Span)).Order());

        DesktopPdfExporter exporter = new(jobFactory: factory, localization: localization);
        PrintPreviewResult preview = await exporter.PreviewAsync(request, captures);
        using MemoryStream destination = new();
        PrintExportReceipt receipt = await exporter.ExportWithResultAsync(request, captures, destination);

        Assert.Equal(3, preview.PageCount);
        Assert.Equal(preview.PlanIdentity, receipt.PlanIdentity);
        Assert.Equal(3, preview.Pages.Select(page => Hash(page.RenderedPageCapture.Rgb24.Span)).Distinct().Count());
        string[] embeddedImageHashes = ReadDecodedImageStreams(destination.ToArray())
            .Select(bytes => Hash(bytes))
            .Order()
            .ToArray();
        Assert.Equal(
            new[] { model, load, result }.Select(capture => Hash(capture.Rgb24.Span)).Order(),
            embeddedImageHashes);
    }

    private static ProjectDocument WithMovingLoad(ProjectDocument value) => new(
        value.Version,
        value.Metadata,
        value.Nodes,
        value.Members,
        value.Supports,
        value.LoadCases,
        value.NodalLoads,
        value.DerivedResults,
        [new MovingLoadDefinition("MOV1", "Moving 1", [value.LoadCases[0].Id])],
        value.Selection,
        value.IsDirty,
        value.Sections,
        value.Dimension,
        value.ElementPropertySets,
        value.RigidZones,
        value.SupportSets,
        value.Panels,
        value.JointReleaseSets,
        value.NoticePoints,
        value.MemberSpringSets,
        value.PrescribedDisplacements,
        value.MemberLoads);

    private static List<ResultProjectionContext> CreateResultProjectionContexts()
    {
        List<ResultProjectionContext> contexts = [];
        AnalysisResultSet staticResults = ReadResultFixture("single-static.json");
        AddContentSelection(
            contexts,
            "static",
            WithResultCases(ProjectDocumentPresets.CreateRepresentativeFrame(), staticResults),
            staticResults);

        AnalysisResultSet nonlinear = ReadResultFixture("nonlinear-steps.json");
        AddContentSelection(
            contexts,
            "nonlinear",
            WithResultCases(ProjectDocumentPresets.CreateRepresentativeFrame(), nonlinear),
            nonlinear,
            content => content.ResultStateSelector.SelectedIndex = 1);

        AnalysisResultSet modal = ReadResultFixture("modal.json");
        AddContentSelection(
            contexts,
            "modal",
            WithResultCases(ProjectDocumentPresets.CreateRepresentativeFrame(), modal),
            modal,
            content => content.ResultStateSelector.SelectedIndex = 1);

        (ProjectDocument signedDocument, AnalysisResultSet unvalidatedSigned) =
            Step6ViewportBehaviorTests.CreateSignedCaseFixture();
        AnalysisResultSet signed = NormalizeSignedResultSet(unvalidatedSigned);
        DerivedResultDefinition[] derived =
        [
            new DerivedResultDefinition(
                "DEFINE",
                "Define",
                DerivedResultKind.Define,
                [new DerivedResultTerm("C1", 2), new DerivedResultTerm("C2", 0.5)]),
            new DerivedResultDefinition(
                "COMBINE",
                "Combine",
                DerivedResultKind.Combine,
                [new DerivedResultTerm("DEFINE", 2)]),
            new DerivedResultDefinition(
                "PICKUP",
                "Pickup",
                DerivedResultKind.Pickup,
                [new DerivedResultTerm("C1", 1), new DerivedResultTerm("C2", 1)]),
        ];
        ProjectDocument derivedDocument = WithPresentation(signedDocument, derived, []);
        for (int index = 0; index < derived.Length; index++)
        {
            int selectorIndex = index + 1;
            AddContentSelection(
                contexts,
                derived[index].Id,
                derivedDocument,
                signed,
                content => content.ResultDerivedSelector.SelectedIndex = selectorIndex);
        }

        ProjectDocument movingDocument = WithPresentation(
            signedDocument,
            [],
            [new MovingLoadDefinition("MOVING", "Moving", ["C1", "C2"])]);
        AddContentSelection(contexts, "moving-parent", movingDocument, signed);
        AddContentSelection(
            contexts,
            "moving-child",
            movingDocument,
            signed,
            content => content.ResultChildSelector.SelectedIndex = 1);
        return contexts;
    }

    private static void AddContentSelection(
        ICollection<ResultProjectionContext> output,
        string name,
        ProjectDocument document,
        AnalysisResultSet resultSet,
        Action<ProjectDocumentContent>? configure = null)
    {
        using ProjectDocumentContent content = new(
            DocumentKey.Document(("step8-print-" + name).ToLowerInvariant()),
            new LocalizationService(UiLanguage.English));
        _ = content.ResultGrid.Handle;
        content.SetDocument(document);
        content.SetResult(resultSet);
        configure?.Invoke(content);
        Application.DoEvents();
        content.FlushPendingUpdates();
        PrintResultSelection selection = Assert.IsType<PrintResultSelection>(content.CreatePrintResultSelection());
        output.Add(new ResultProjectionContext(name, document, resultSet, selection));
    }

    private static void AssertContext(
        IEnumerable<ResultProjectionContext> contexts,
        string name,
        ResultStateKind? stateKind,
        int? stateIndex,
        DerivedResultKind? derivedKind,
        bool moving,
        string provenanceFragment)
    {
        ResultProjectionContext context = contexts.Single(value => value.Name == name);
        Assert.Equal(derivedKind, context.Selection.Tables.DerivedKind);
        Assert.Equal(moving, context.Selection.IsMovingLoad);
        Assert.Contains(provenanceFragment, context.Selection.Provenance, StringComparison.OrdinalIgnoreCase);
        if (stateKind is ResultStateKind expectedKind)
        {
            ResultCoordinate coordinate = Assert.IsType<ResultCoordinate>(context.Selection.Coordinate);
            Assert.Equal(expectedKind, coordinate.StateKind);
            Assert.Equal(stateIndex, coordinate.StateIndex);
        }
        else
        {
            Assert.Null(context.Selection.Coordinate);
        }
    }

    private static ProjectDocument WithResultCases(ProjectDocument value, AnalysisResultSet results) => new(
        value.Version,
        value.Metadata,
        value.Nodes,
        value.Members,
        value.Supports,
        results.Cases.Select(resultCase => new LoadCaseDefinition(
            resultCase.CaseId,
            resultCase.Name,
            resultCase.Symbol)),
        value.NodalLoads.Select(load => load with { CaseId = results.Cases[0].CaseId }),
        [],
        [],
        value.Selection,
        value.IsDirty,
        value.Sections,
        value.Dimension,
        value.ElementPropertySets,
        value.RigidZones,
        value.SupportSets,
        value.Panels,
        value.JointReleaseSets,
        value.NoticePoints,
        value.MemberSpringSets,
        value.PrescribedDisplacements.Select(load => load with { CaseId = results.Cases[0].CaseId }),
        value.MemberLoads.Select(load => load with { CaseId = results.Cases[0].CaseId }));

    private static ProjectDocument WithPresentation(
        ProjectDocument value,
        IEnumerable<DerivedResultDefinition> derived,
        IEnumerable<MovingLoadDefinition> moving) => new(
        value.Version,
        value.Metadata,
        value.Nodes,
        value.Members,
        value.Supports,
        value.LoadCases,
        value.NodalLoads,
        derived,
        moving,
        value.Selection,
        value.IsDirty,
        value.Sections,
        value.Dimension,
        value.ElementPropertySets,
        value.RigidZones,
        value.SupportSets,
        value.Panels,
        value.JointReleaseSets,
        value.NoticePoints,
        value.MemberSpringSets,
        value.PrescribedDisplacements,
        value.MemberLoads);

    private static AnalysisResultSet NormalizeSignedResultSet(AnalysisResultSet value)
    {
        StaticAnalysisResult first = Assert.IsType<StaticAnalysisResult>(value.Results[0]);
        double length = first.MemberSectionForces[0].Segments[0].Length;
        AnalysisTopology topology = new(
            value.Topology.Nodes,
            value.Topology.Members.Select(member => new TopologyMember(
                member.MemberId,
                member.NodeI,
                member.NodeJ,
                member.LocalFrame,
                [new MemberStation("S0", 0), new MemberStation("S1", length)])),
            value.Topology.ShellElements,
            value.Topology.SolidElements);
        AnalysisResult[] results = value.Results.Select(result =>
        {
            StaticAnalysisResult source = Assert.IsType<StaticAnalysisResult>(result);
            return (AnalysisResult)new StaticAnalysisResult(
                source.CaseId,
                source.NodeDisplacements,
                source.SupportReactions,
                source.MemberSectionForces.Select(member => new MemberSectionForces(
                    member.MemberId,
                    member.Segments.Select(segment => new MemberSegmentResult(
                        "S0-S1",
                        "S0",
                        "S1",
                        segment.Length,
                        segment.IEnd,
                        segment.JEnd)))),
                source.ShellResults,
                source.SolidResults,
                source.Diagnostics);
        }).ToArray();
        AnalysisResultSet normalized = new(
            value.Kind,
            value.SchemaVersion,
            value.Units,
            value.CoordinateSystem,
            value.Cases,
            topology,
            results);
        AnalysisResultSetValidator.Validate(normalized);
        return normalized;
    }

    private static void AssertPdfMapsEveryCharacter(byte[] pdf, string expectedText)
    {
        string decoded = string.Join(
            "\n",
            ReadDecodedStreams(pdf).Select(bytes => Encoding.Latin1.GetString(bytes)));
        Assert.Contains("begincmap", decoded, StringComparison.Ordinal);
        foreach (char character in expectedText.Where(value => !char.IsWhiteSpace(value)).Distinct())
        {
            Assert.Contains(((int)character).ToString("X4"), decoded, StringComparison.OrdinalIgnoreCase);
        }
    }

    internal static string ExtractMappedPdfText(byte[] pdf)
    {
        IReadOnlyList<byte[]> streams = ReadDecodedStreams(pdf);
        Dictionary<ushort, char> map = [];
        foreach (byte[] stream in streams)
        {
            string value = Encoding.Latin1.GetString(stream);
            if (!value.Contains("begincmap", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (string line in value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                Match range = Regex.Match(
                    line,
                    @"^\s*<(?<first>[0-9A-Fa-f]{4})>\s*<(?<last>[0-9A-Fa-f]{4})>\s*<(?<destination>[0-9A-Fa-f]{4})>\s*$",
                    RegexOptions.CultureInvariant);
                if (range.Success)
                {
                    ushort first = Convert.ToUInt16(range.Groups["first"].Value, 16);
                    ushort last = Convert.ToUInt16(range.Groups["last"].Value, 16);
                    ushort destination = Convert.ToUInt16(range.Groups["destination"].Value, 16);
                    for (int code = first; code <= last; code++)
                    {
                        TryAddMapping(map, (ushort)code, (char)(destination + code - first));
                    }

                    continue;
                }

                Match character = Regex.Match(
                    line,
                    @"^\s*<(?<source>[0-9A-Fa-f]{4})>\s*<(?<destination>[0-9A-Fa-f]{4})>\s*$",
                    RegexOptions.CultureInvariant);
                if (character.Success)
                {
                    TryAddMapping(
                        map,
                        Convert.ToUInt16(character.Groups["source"].Value, 16),
                        (char)Convert.ToUInt16(character.Groups["destination"].Value, 16));
                }
            }
        }

        StringBuilder output = new();
        foreach (byte[] stream in streams)
        {
            string program = Encoding.Latin1.GetString(stream);
            if (!program.Contains("BT", StringComparison.Ordinal) ||
                !program.Contains("Tj", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (Match literal in Regex.Matches(
                program,
                @"\((?<value>(?:\\[0-7]{1,3}|\\.|[^\\)])*)\)\s*Tj",
                RegexOptions.CultureInvariant))
            {
                byte[] encoded = DecodePdfLiteral(literal.Groups["value"].Value);
                AppendMappedText(output, map, encoded);
            }

            foreach (Match hexadecimal in Regex.Matches(
                program,
                @"<(?<value>[0-9A-Fa-f]+)>\s*Tj",
                RegexOptions.CultureInvariant))
            {
                string value = hexadecimal.Groups["value"].Value;
                if (value.Length % 2 != 0)
                {
                    throw new InvalidDataException("PDF hexadecimal text has an odd number of digits.");
                }

                AppendMappedText(output, map, Convert.FromHexString(value));
            }

            foreach (Match array in Regex.Matches(
                program,
                @"\[(?<value>[^\]]*)\]\s*TJ",
                RegexOptions.CultureInvariant))
            {
                string items = array.Groups["value"].Value;
                foreach (Match hexadecimal in Regex.Matches(
                    items,
                    @"<(?<value>[0-9A-Fa-f]+)>",
                    RegexOptions.CultureInvariant))
                {
                    string value = hexadecimal.Groups["value"].Value;
                    if (value.Length % 2 != 0)
                    {
                        throw new InvalidDataException("PDF hexadecimal text has an odd number of digits.");
                    }

                    AppendMappedText(output, map, Convert.FromHexString(value));
                }

                foreach (Match literal in Regex.Matches(
                    items,
                    @"\((?<value>(?:\\[0-7]{1,3}|\\.|[^\\)])*)\)",
                    RegexOptions.CultureInvariant))
                {
                    AppendMappedText(output, map, DecodePdfLiteral(literal.Groups["value"].Value));
                }
            }
        }

        return output.ToString();
    }

    private static void AppendMappedText(
        StringBuilder output,
        IReadOnlyDictionary<ushort, char> map,
        byte[] encoded)
    {
        if (encoded.Length % 2 != 0)
        {
            output.Append(Encoding.Latin1.GetString(encoded));
            output.Append('\n');
            return;
        }

        for (int offset = 0; offset < encoded.Length; offset += 2)
        {
            ushort code = (ushort)((encoded[offset] << 8) | encoded[offset + 1]);
            if (map.TryGetValue(code, out char mapped))
            {
                output.Append(mapped);
            }
        }

        output.Append('\n');
    }

    private static void TryAddMapping(IDictionary<ushort, char> map, ushort code, char value)
    {
        if (!map.TryGetValue(code, out char existing) || existing == value)
        {
            map[code] = value;
        }
    }

    private static byte[] DecodePdfLiteral(string value)
    {
        List<byte> bytes = [];
        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];
            if (current != '\\')
            {
                bytes.Add((byte)current);
                continue;
            }

            if (++index >= value.Length)
            {
                throw new InvalidDataException("PDF string ends with an escape character.");
            }

            current = value[index];
            if (current is >= '0' and <= '7')
            {
                int octal = current - '0';
                int count = 1;
                while (count < 3 && index + 1 < value.Length && value[index + 1] is >= '0' and <= '7')
                {
                    octal = (octal * 8) + (value[++index] - '0');
                    count++;
                }

                bytes.Add((byte)octal);
                continue;
            }

            bytes.Add(current switch
            {
                'n' => (byte)'\n',
                'r' => (byte)'\r',
                't' => (byte)'\t',
                'b' => (byte)'\b',
                'f' => (byte)'\f',
                '(' => (byte)'(',
                ')' => (byte)')',
                '\\' => (byte)'\\',
                _ => (byte)current,
            });
        }

        return bytes.ToArray();
    }

    private static AnalysisResultSet ReadResultFixture(string fileName) => AnalysisResultSetJson.Deserialize(
        File.ReadAllBytes(Path.Combine(
            FindRepositoryRoot(),
            "FrameWeb",
            "tests",
            "data",
            "contracts",
            "positive",
            fileName)));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private static ViewportCapture SolidCapture(
        int width,
        int height,
        byte red,
        byte green,
        byte blue)
    {
        byte[] rgb = new byte[ViewportCapture.GetRequiredByteLength(width, height)];
        for (int index = 0; index < rgb.Length; index += 3)
        {
            rgb[index] = red;
            rgb[index + 1] = green;
            rgb[index + 2] = blue;
        }

        return new ViewportCapture(width, height, rgb);
    }

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes));

    internal static IReadOnlyList<byte[]> ReadDecodedImageStreams(byte[] pdf)
        => ReadDecodedStreams(pdf, imageOnly: true);

    private static IReadOnlyList<byte[]> ReadDecodedStreams(byte[] pdf, bool imageOnly = false)
    {
        string text = Encoding.Latin1.GetString(pdf);
        List<byte[]> images = [];
        int search = 0;
        while (true)
        {
            (int marker, int dataStart) = FindNextStream(text, search);
            if (marker < 0)
            {
                break;
            }

            int dictionaryStart = text.LastIndexOf("<<", marker, StringComparison.Ordinal);
            int dictionaryEnd = text.LastIndexOf(">>", marker, StringComparison.Ordinal);
            if (dictionaryStart < 0 || dictionaryEnd < dictionaryStart)
            {
                throw new InvalidDataException("PDF stream dictionary is malformed.");
            }

            string dictionary = text[dictionaryStart..(dictionaryEnd + 2)];
            MatchCollection matches = Regex.Matches(dictionary, @"/Length\s+(\d+)\b", RegexOptions.CultureInvariant);
            if (matches.Count == 0 || !int.TryParse(matches[^1].Groups[1].Value, out int length) ||
                dataStart + length > pdf.Length)
            {
                throw new InvalidDataException("PDF stream length is malformed.");
            }

            if (!imageOnly || dictionary.Contains("/Subtype/Image", StringComparison.Ordinal))
            {
                byte[] encoded = pdf.AsSpan(dataStart, length).ToArray();
                images.Add(dictionary.Contains("/FlateDecode", StringComparison.Ordinal)
                    ? Inflate(encoded)
                    : encoded);
            }

            search = dataStart + length;
        }

        return images;
    }

    private static (int Marker, int DataStart) FindNextStream(string value, int search)
    {
        const string lineFeedMarker = "\nstream\n";
        const string carriageReturnMarker = "\r\nstream\r\n";
        int lineFeed = value.IndexOf(lineFeedMarker, search, StringComparison.Ordinal);
        int carriageReturn = value.IndexOf(carriageReturnMarker, search, StringComparison.Ordinal);
        if (lineFeed < 0 && carriageReturn < 0)
        {
            return (-1, -1);
        }

        return carriageReturn >= 0 && (lineFeed < 0 || carriageReturn < lineFeed)
            ? (carriageReturn + 2, carriageReturn + carriageReturnMarker.Length)
            : (lineFeed + 1, lineFeed + lineFeedMarker.Length);
    }

    private static byte[] Inflate(byte[] encoded)
    {
        try
        {
            return Inflate(encoded, zlibWrapped: true);
        }
        catch (InvalidDataException)
        {
            return Inflate(encoded, zlibWrapped: false);
        }
    }

    private static byte[] Inflate(byte[] encoded, bool zlibWrapped)
    {
        using MemoryStream input = new(encoded, writable: false);
        using Stream inflater = zlibWrapped
            ? new ZLibStream(input, CompressionMode.Decompress)
            : new DeflateStream(input, CompressionMode.Decompress);
        using MemoryStream output = new();
        inflater.CopyTo(output);
        return output.ToArray();
    }

    private sealed record ResultProjectionContext(
        string Name,
        ProjectDocument Document,
        AnalysisResultSet ResultSet,
        PrintResultSelection Selection);
}
