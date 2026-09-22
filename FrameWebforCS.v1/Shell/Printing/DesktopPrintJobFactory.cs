using System.Globalization;
using FrameWebforCS.Core.Abstractions;
using FrameWebforCS.Core.Analysis;
using FrameWebforCS.Core.Documents;
using FrameWebforCS.Core.Results;
using FrameWebforCS.Printing;
using FrameWebforCS.Resources;
using CoreLayoutChoice = FrameWebforCS.Core.Abstractions.PrintLayoutChoice;
using CorePageOrientation = FrameWebforCS.Core.Abstractions.PrintPageOrientation;
using CorePageSettings = FrameWebforCS.Core.Abstractions.PrintPageSettings;
using CorePaperSize = FrameWebforCS.Core.Abstractions.PrintPaperSize;
using EngineLayoutMode = FrameWebforCS.Printing.PrintLayoutMode;
using EnginePageOrientation = FrameWebforCS.Printing.PrintPageOrientation;
using EnginePageSettings = FrameWebforCS.Printing.PrintPageSettings;
using EnginePaperSize = FrameWebforCS.Printing.PrintPaperSize;

namespace FrameWebforCS.Shell.Printing;

public sealed class DesktopPrintJob
{
    public DesktopPrintJob(PrintJob job, IEnumerable<PrintContentSection> sectionOrigins)
    {
        Job = job ?? throw new ArgumentNullException(nameof(job));
        PrintContentSection[] origins = sectionOrigins?.ToArray()
            ?? throw new ArgumentNullException(nameof(sectionOrigins));
        if (origins.Length != job.Sections.Count)
        {
            throw new ArgumentException("Every engine print section requires a desktop section origin.", nameof(sectionOrigins));
        }

        SectionOrigins = Array.AsReadOnly(origins);
    }

    public PrintJob Job { get; }

    public IReadOnlyList<PrintContentSection> SectionOrigins { get; }
}

public sealed class DesktopPrintJobFactory
{
    private readonly LocalizationService _localization;

    public DesktopPrintJobFactory(LocalizationService localization)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
    }

    public PrintJob Create(PrintExportRequest request, PrintDiagramCaptureSet diagramCaptures) =>
        CreateProjection(request, diagramCaptures).Job;

    public DesktopPrintJob CreateProjection(
        PrintExportRequest request,
        PrintDiagramCaptureSet diagramCaptures)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(diagramCaptures);
        PrintResultSelection? resultSelection = ResolveSelection(request);
        ValidateWorkBudget(request, resultSelection);
        List<PrintSection> sections = [];
        List<PrintContentSection> origins = [];
        foreach (PrintContentSection section in request.Sections)
        {
            int start = sections.Count;
            AddSection(sections, section, request, resultSelection, diagramCaptures);
            for (int index = start; index < sections.Count; index++)
            {
                origins.Add(section);
            }
        }

        return new DesktopPrintJob(
            new PrintJob(
                request.Document.Metadata.Name,
                CreatePageSettings(request.PageSettings),
                request.Language switch
                {
                    PrintContentLanguage.English => PrintTextLanguage.English,
                    PrintContentLanguage.Japanese => PrintTextLanguage.Japanese,
                    PrintContentLanguage.SimplifiedChinese => PrintTextLanguage.SimplifiedChinese,
                    _ => throw new ArgumentOutOfRangeException(nameof(request), request.Language, null),
                },
                sections,
                request.PageSettings.Layout == CoreLayoutChoice.SectionPerPage
                    ? EngineLayoutMode.SectionPerPage
                    : EngineLayoutMode.Flow),
            origins);
    }

    private void AddSection(
        List<PrintSection> output,
        PrintContentSection section,
        PrintExportRequest request,
        PrintResultSelection? resultSelection,
        PrintDiagramCaptureSet diagramCaptures)
    {
        switch (section)
        {
            case PrintContentSection.ProjectSummary:
                output.Add(CreateSummary(request.Document));
                break;
            case PrintContentSection.InputTables:
                output.AddRange(DesktopInputTableProjection.Create(request.Document, _localization));
                break;
            case PrintContentSection.ModelDiagram:
                output.Add(CreateDiagram("PrintSectionModelDiagram", diagramCaptures, PrintDiagramKind.Model));
                break;
            case PrintContentSection.LoadDiagram:
                output.Add(CreateDiagram("PrintSectionLoadDiagram", diagramCaptures, PrintDiagramKind.Load));
                break;
            case PrintContentSection.DisplacementResults:
                AddResult(output, resultSelection, PrintResultQuantity.Displacement);
                break;
            case PrintContentSection.ReactionResults:
                AddResult(output, resultSelection, PrintResultQuantity.Reaction);
                break;
            case PrintContentSection.MemberForceResults:
                AddResult(output, resultSelection, PrintResultQuantity.SectionForce);
                break;
            case PrintContentSection.ResultDiagram:
                output.Add(CreateDiagram("PrintSectionResultDiagram", diagramCaptures, PrintDiagramKind.Result));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(section), section, null);
        }
    }

    private PrintTextSection CreateSummary(ProjectDocument document)
    {
        string body = string.Join(
            Environment.NewLine,
            FormatResource("PrintSummaryDescription", document.Metadata.Description),
            FormatResource("PrintSummaryAuthor", document.Metadata.Author),
            FormatResource("PrintSummaryUnits", document.Metadata.UnitSystem),
            FormatResource("PrintSummaryDimension", document.Dimension),
            FormatResource("PrintSummaryNodes", document.Nodes.Count),
            FormatResource("PrintSummaryMembers", document.Members.Count),
            FormatResource("PrintSummarySupports", document.Supports.Count),
            FormatResource("PrintSummaryLoadCases", document.LoadCases.Count));
        return new PrintTextSection(_localization["PrintSectionProjectSummary"], body);
    }

    private void AddResult(
        List<PrintSection> output,
        PrintResultSelection? selection,
        PrintResultQuantity quantity)
    {
        if (selection is null)
        {
            return;
        }

        ResultTableSet tables = selection.Tables;
        PrintTable table = quantity switch
        {
            PrintResultQuantity.Displacement => DisplacementTable(tables),
            PrintResultQuantity.Reaction => ReactionTable(tables),
            PrintResultQuantity.SectionForce => MemberForceTable(tables),
            _ => throw new ArgumentOutOfRangeException(nameof(quantity), quantity, null),
        };
        output.Add(new PrintResultSection(new PrintResult(
            tables.Coordinate?.CaseId ?? tables.Id,
            quantity,
            selection.Provenance,
            table)));
    }

    private PrintResultSelection? ResolveSelection(PrintExportRequest request)
    {
        if (request.SelectedResult is not null)
        {
            return request.SelectedResult;
        }

        if (request.ResultSet is null || request.SelectedResults.Count == 0)
        {
            return null;
        }

        ResultCoordinate coordinate = request.SelectedResults[0];
        ResultTableSet tables = new ResultPresentationService().BuildTables(request.ResultSet, coordinate);
        return new PrintResultSelection(tables, coordinate, coordinate.ToString());
    }

    private void ValidateWorkBudget(
        PrintExportRequest request,
        PrintResultSelection? resultSelection)
    {
        DesktopPrintProjectionBudget budget = new();
        if (request.Sections.Contains(PrintContentSection.InputTables))
        {
            DesktopInputTableProjection.Preflight(request.Document, _localization, budget);
        }

        ResultTableSet? result = resultSelection?.Tables;
        if (result is not null && request.Sections.Contains(PrintContentSection.DisplacementResults))
        {
            budget.ChargeTable(
                result.NodeDisplacements.Count,
                7,
                EstimateResultTableText(
                    "ResultDisplacements",
                    ["EditorNode", "EditorDx", "EditorDy", "EditorDz", "EditorRx", "EditorRy", "EditorRz"],
                    result.NodeDisplacements.Select(value => value.NodeId),
                    numericCellsPerRow: 6));
        }

        if (result is not null && request.Sections.Contains(PrintContentSection.ReactionResults))
        {
            budget.ChargeTable(
                result.SupportReactions.Count,
                7,
                EstimateResultTableText(
                    "ResultReactions",
                    ["EditorNode", "EditorFx", "EditorFy", "EditorFz", "EditorMx", "EditorMy", "EditorMz"],
                    result.SupportReactions.Select(value => value.NodeId),
                    numericCellsPerRow: 6));
        }

        if (result is not null && request.Sections.Contains(PrintContentSection.MemberForceResults))
        {
            long forceRows = checked(result.MemberSectionForces.Sum(member => (long)member.Segments.Count) * 2);
            long textCharacters = _localization["ResultMemberForces"].Length +
                new[]
                {
                    "EditorMember", "ResultStation", "ResultEnd", "EditorFx", "EditorFy", "EditorFz",
                    "EditorMx", "EditorMy", "EditorMz",
                }.Sum(resource => _localization[resource].Length);
            foreach (MemberSectionForces member in result.MemberSectionForces)
            {
                foreach (MemberSegmentResult segment in member.Segments)
                {
                    textCharacters = checked(textCharacters +
                        (2L * (member.MemberId.Length + segment.SegmentId.Length + 1 + (6 * 24))));
                }
            }

            budget.ChargeTable(forceRows, 9, textCharacters);
        }
    }

    private long EstimateResultTableText(
        string captionResource,
        IEnumerable<string> columnResources,
        IEnumerable<string> rowIds,
        int numericCellsPerRow)
    {
        long textCharacters = _localization[captionResource].Length +
            columnResources.Sum(resource => _localization[resource].Length);
        foreach (string rowId in rowIds)
        {
            textCharacters = checked(textCharacters + rowId.Length + ((long)numericCellsPerRow * 24));
        }

        return textCharacters;
    }

    private PrintTable DisplacementTable(ResultTableSet tables) => new(
        _localization["ResultDisplacements"],
        [Column("EditorNode"), Column("EditorDx", numeric: true), Column("EditorDy", numeric: true),
            Column("EditorDz", numeric: true), Column("EditorRx", numeric: true),
            Column("EditorRy", numeric: true), Column("EditorRz", numeric: true)],
        tables.NodeDisplacements.Select(value => Row(
            value.NodeId,
            Number(value.Components.Dx),
            Number(value.Components.Dy),
            Number(value.Components.Dz),
            Number(value.Components.Rx),
            Number(value.Components.Ry),
            Number(value.Components.Rz))));

    private PrintTable ReactionTable(ResultTableSet tables) => new(
        _localization["ResultReactions"],
        [Column("EditorNode"), Column("EditorFx", numeric: true), Column("EditorFy", numeric: true),
            Column("EditorFz", numeric: true), Column("EditorMx", numeric: true),
            Column("EditorMy", numeric: true), Column("EditorMz", numeric: true)],
        tables.SupportReactions.Select(value => Row(
            value.NodeId,
            Number(value.Components.Fx),
            Number(value.Components.Fy),
            Number(value.Components.Fz),
            Number(value.Components.Mx),
            Number(value.Components.My),
            Number(value.Components.Mz))));

    private PrintTable MemberForceTable(ResultTableSet tables) => new(
        _localization["ResultMemberForces"],
        [Column("EditorMember"), Column("ResultStation"), Column("ResultEnd"),
            Column("EditorFx", numeric: true), Column("EditorFy", numeric: true), Column("EditorFz", numeric: true),
            Column("EditorMx", numeric: true), Column("EditorMy", numeric: true), Column("EditorMz", numeric: true)],
        tables.MemberSectionForces.SelectMany(member => member.Segments.SelectMany(segment => new[]
        {
            ForceRow(member.MemberId, segment.SegmentId, "I", segment.IEnd),
            ForceRow(member.MemberId, segment.SegmentId, "J", segment.JEnd),
        })));

    private PrintDiagramSection CreateDiagram(
        string captionResource,
        PrintDiagramCaptureSet captures,
        PrintDiagramKind kind) =>
        new(new PrintDiagram(_localization[captionResource], captures.GetRequired(kind), kind));

    public static IReadOnlyList<PrintDiagramKind> RequiredDiagramKinds(PrintExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        List<PrintDiagramKind> kinds = [];
        foreach (PrintContentSection section in request.Sections)
        {
            switch (section)
            {
                case PrintContentSection.ModelDiagram:
                    kinds.Add(PrintDiagramKind.Model);
                    break;
                case PrintContentSection.LoadDiagram:
                    kinds.Add(PrintDiagramKind.Load);
                    break;
                case PrintContentSection.ResultDiagram:
                    kinds.Add(PrintDiagramKind.Result);
                    break;
            }
        }

        return Array.AsReadOnly(kinds.ToArray());
    }

    private PrintTableColumn Column(string resourceKey, bool numeric = false) =>
        new(
            _localization[resourceKey],
            alignment: numeric ? PrintCellAlignment.Right : PrintCellAlignment.Left);

    private static PrintTableRow ForceRow(
        string memberId,
        string stationId,
        string end,
        ForceComponents components) => Row(
            memberId,
            stationId,
            end,
            Number(components.Fx),
            Number(components.Fy),
            Number(components.Fz),
            Number(components.Mx),
            Number(components.My),
            Number(components.Mz));

    private static PrintTableRow Row(params string[] values) => new(values);

    private string FormatResource(string resourceKey, object value) =>
        string.Format(_localization.Culture, _localization[resourceKey], value);

    private static string Number(double value) => value.ToString("G9", CultureInfo.InvariantCulture);

    private static EnginePageSettings CreatePageSettings(CorePageSettings settings) => new(
        settings.PaperSize == CorePaperSize.A3 ? EnginePaperSize.A3 : EnginePaperSize.A4,
        settings.Orientation == CorePageOrientation.Landscape
            ? EnginePageOrientation.Landscape
            : EnginePageOrientation.Portrait,
        new PrintMargins(
            settings.LeftMarginMillimetres,
            settings.TopMarginMillimetres,
            settings.RightMarginMillimetres,
            settings.BottomMarginMillimetres),
        settings.Scale,
        settings.ShowPageNumbers);
}
