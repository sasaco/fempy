using System.Globalization;
using FrameWebforCsharp.Core.Documents;
using FrameWebforCsharp.Printing;
using FrameWebforCsharp.Resources;

namespace FrameWebforCsharp.Shell.Printing;

internal static class DesktopInputTableProjection
{
    private const int NumberCharacterBudget = 24;

    public static IReadOnlyList<PrintSection> Create(
        ProjectDocument document,
        LocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(localization);
        TableProjection[] projections = CreateProjections(document);
        return Array.AsReadOnly<PrintSection>(projections
            .Select(projection => new PrintTableSection(new PrintTable(
                localization[projection.CaptionResource],
                projection.Columns.Select(column => new PrintTableColumn(
                    localization[column.HeaderResource],
                    alignment: column.Numeric ? PrintCellAlignment.Right : PrintCellAlignment.Left)),
                projection.CreateRows())))
            .ToArray());
    }

    public static void Preflight(
        ProjectDocument document,
        LocalizationService localization,
        DesktopPrintProjectionBudget budget)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(localization);
        ArgumentNullException.ThrowIfNull(budget);
        Preflight(CreateProjections(document), localization, budget);
    }

    private static TableProjection[] CreateProjections(ProjectDocument document) =>
    [
        Projection(
            "EditorModelSettings",
            [Text("EditorDimension")],
            1,
            TextLength(document.Dimension.ToString()),
            () => [Row(document.Dimension.ToString())]),
        Projection(
            "EditorNodes",
            [Text("EditorId"), Number("EditorX"), Number("EditorY"), Number("EditorZ")],
            document.Nodes.Count,
            Sum(document.Nodes, node => TextLength(node.Id) + (3 * NumberCharacterBudget)),
            () => document.Nodes.Select(node => Row(node.Id, Value(node.X), Value(node.Y), Value(node.Z)))),
        Projection(
            "EditorMembers",
            [Text("EditorId"), Text("EditorNodeI"), Text("EditorNodeJ"), Text("EditorSection"),
                Number("EditorRotation"), Text("EditorShearCorrection")],
            document.Members.Count,
            Sum(document.Members, member => TextLength(member.Id, member.NodeI, member.NodeJ, member.SectionId) +
                NumberCharacterBudget + 1),
            () => document.Members.Select(member => Row(
                member.Id, member.NodeI, member.NodeJ, member.SectionId ?? string.Empty,
                Value(member.RotationDegrees), Boolean(member.ShearCorrection)))),
        Projection(
            "EditorRigidZones",
            [Text("EditorId"), Text("EditorMember"), Number("EditorILength"), Number("EditorJLength"),
                Text("EditorSection")],
            document.RigidZones.Count,
            Sum(document.RigidZones, zone => TextLength(zone.Id, zone.MemberId, zone.SectionId) +
                (2 * NumberCharacterBudget)),
            () => document.RigidZones.Select(zone => Row(
                zone.Id, zone.MemberId, Value(zone.ILength), Value(zone.JLength), zone.SectionId))),
        Projection(
            "EditorElementPropertySets",
            [Text("EditorId"), Text("EditorName")],
            document.ElementPropertySets.Count,
            Sum(document.ElementPropertySets, set => TextLength(set.Id, set.Name)),
            () => document.ElementPropertySets.Select(set => Row(set.Id, set.Name))),
        Projection(
            "EditorSupports",
            [Text("EditorId"), Text("EditorNode"), Number("EditorFixX"), Number("EditorFixY"),
                Number("EditorFixZ"), Number("EditorFixRx"), Number("EditorFixRy"), Number("EditorFixRz"),
                Text("EditorSet")],
            checked(document.Supports.Count + document.SupportSets.Sum(set => set.Rows.Count)),
            EstimateSupports(document),
            () => SupportRows(document)),
        Projection(
            "EditorSupportSets",
            [Text("EditorId"), Text("EditorName")],
            document.SupportSets.Count,
            Sum(document.SupportSets, set => TextLength(set.Id, set.Name)),
            () => document.SupportSets.Select(set => Row(set.Id, set.Name))),
        Projection(
            "EditorSections",
            [Text("EditorId"), Text("EditorName"), Number("EditorYoungsModulus"), Number("EditorPoissonRatio"),
                Number("EditorShearModulus"), Number("EditorArea"), Number("EditorIy"), Number("EditorIz"),
                Number("EditorTorsion"), Number("EditorThermalExpansion"), Number("EditorDensity"),
                Number("EditorThickness"), Text("EditorSet")],
            checked(document.Sections.Count + document.ElementPropertySets.Sum(set => set.Sections.Count)),
            EstimateSections(document),
            () => SectionRows(document)),
        Projection(
            "EditorPanels",
            [Text("EditorId"), Text("EditorSection"), Text("EditorNode1"), Text("EditorNode2"),
                Text("EditorNode3"), Text("EditorNode4")],
            document.Panels.Count,
            Sum(document.Panels, panel => TextLength(panel.Id, panel.SectionId) + TextLength(panel.NodeIds)),
            () => document.Panels.Select(panel => Row(
                panel.Id, panel.SectionId, panel.NodeIds[0], panel.NodeIds[1], panel.NodeIds[2], panel.NodeIds[3]))),
        Projection(
            "EditorJoints",
            [Text("EditorId"), Text("EditorMember"), Text("EditorXi"), Text("EditorYi"), Text("EditorZi"),
                Text("EditorXj"), Text("EditorYj"), Text("EditorZj"), Text("EditorSet")],
            document.JointReleaseSets.Sum(set => set.Rows.Count),
            Sum(document.JointReleaseSets, set => Sum(set.Rows, row =>
                TextLength(row.Id, row.MemberId, set.Id) + 6)),
            () => document.JointReleaseSets.SelectMany(set => set.Rows.Select(row => Row(
                row.Id, row.MemberId, Boolean(row.ConnectXi), Boolean(row.ConnectYi), Boolean(row.ConnectZi),
                Boolean(row.ConnectXj), Boolean(row.ConnectYj), Boolean(row.ConnectZj), set.Id)))),
        Projection(
            "EditorJointReleaseSets",
            [Text("EditorId"), Text("EditorName")],
            document.JointReleaseSets.Count,
            Sum(document.JointReleaseSets, set => TextLength(set.Id, set.Name)),
            () => document.JointReleaseSets.Select(set => Row(set.Id, set.Name))),
        Projection(
            "EditorNoticePoints",
            [Text("EditorId"), Text("EditorMember"), Number("EditorDistance")],
            document.NoticePoints.Count,
            Sum(document.NoticePoints, point => TextLength(point.Id, point.MemberId) + NumberCharacterBudget),
            () => document.NoticePoints.Select(point => Row(point.Id, point.MemberId, Value(point.Distance)))),
        Projection(
            "EditorMemberSprings",
            [Text("EditorId"), Text("EditorMember"), Number("EditorTx"), Number("EditorTy"), Number("EditorTz"),
                Number("EditorTr"), Text("EditorSet")],
            document.MemberSpringSets.Sum(set => set.Rows.Count),
            Sum(document.MemberSpringSets, set => Sum(set.Rows, row =>
                TextLength(row.Id, row.MemberId, set.Id) + (4 * NumberCharacterBudget))),
            () => document.MemberSpringSets.SelectMany(set => set.Rows.Select(row => Row(
                row.Id, row.MemberId, Value(row.Tx), Value(row.Ty), Value(row.Tz), Value(row.Tr), set.Id)))),
        Projection(
            "EditorMemberSpringSets",
            [Text("EditorId"), Text("EditorName")],
            document.MemberSpringSets.Count,
            Sum(document.MemberSpringSets, set => TextLength(set.Id, set.Name)),
            () => document.MemberSpringSets.Select(set => Row(set.Id, set.Name))),
        Projection(
            "EditorLoadCases",
            [Text("EditorId"), Text("EditorName"), Text("EditorSymbol"), Text("EditorElementSet"),
                Text("EditorSupportSet"), Text("EditorMemberSpringSet"), Text("EditorJointSet"),
                Number("EditorMovingPitch")],
            document.LoadCases.Count,
            Sum(document.LoadCases, loadCase => TextLength(
                loadCase.Id, loadCase.Name, loadCase.Symbol, loadCase.ElementSetId, loadCase.SupportSetId,
                loadCase.MemberSpringSetId, loadCase.JointSetId) + NumberCharacterBudget),
            () => document.LoadCases.Select(loadCase => Row(
                loadCase.Id, loadCase.Name, loadCase.Symbol, loadCase.ElementSetId, loadCase.SupportSetId,
                loadCase.MemberSpringSetId, loadCase.JointSetId, Value(loadCase.MovingLoadPitch)))),
        Projection(
            "EditorLoads",
            [Text("EditorId"), Text("EditorCase"), Text("EditorNode"), Number("EditorFx"), Number("EditorFy"),
                Number("EditorFz"), Number("EditorMx"), Number("EditorMy"), Number("EditorMz")],
            document.NodalLoads.Count,
            Sum(document.NodalLoads, load => TextLength(load.Id, load.CaseId, load.NodeId) +
                (6 * NumberCharacterBudget)),
            () => document.NodalLoads.Select(load => Row(
                load.Id, load.CaseId, load.NodeId, Value(load.Fx), Value(load.Fy), Value(load.Fz),
                Value(load.Mx), Value(load.My), Value(load.Mz)))),
        Projection(
            "EditorPrescribedDisplacements",
            [Text("EditorId"), Text("EditorCase"), Text("EditorNode"), Number("EditorDx"), Number("EditorDy"),
                Number("EditorDz"), Number("EditorAx"), Number("EditorAy"), Number("EditorAz")],
            document.PrescribedDisplacements.Count,
            Sum(document.PrescribedDisplacements, load => TextLength(load.Id, load.CaseId, load.NodeId) +
                (6 * NumberCharacterBudget)),
            () => document.PrescribedDisplacements.Select(load => Row(
                load.Id, load.CaseId, load.NodeId, Value(load.Dx), Value(load.Dy), Value(load.Dz),
                Value(load.Rx), Value(load.Ry), Value(load.Rz)))),
        Projection(
            "EditorMemberLoads",
            [Text("EditorId"), Text("EditorCase"), Text("EditorMember"), Text("EditorLoadKind"),
                Text("EditorDirection"), Number("EditorL1"), Number("EditorL2"), Number("EditorP1"),
                Number("EditorP2")],
            document.MemberLoads.Count,
            Sum(document.MemberLoads, load => TextLength(
                load.Id, load.CaseId, load.MemberId, load.Kind.ToString(), load.Direction.ToString()) +
                (4 * NumberCharacterBudget)),
            () => document.MemberLoads.Select(load => Row(
                load.Id, load.CaseId, load.MemberId, load.Kind.ToString(), load.Direction.ToString(),
                Value(load.L1), Value(load.L2), Value(load.P1), Value(load.P2)))),
        DerivedProjection(document, DerivedResultKind.Define, "EditorDefine"),
        DerivedProjection(document, DerivedResultKind.Combine, "EditorCombine"),
        DerivedProjection(document, DerivedResultKind.Pickup, "EditorPickup"),
        Projection(
            "EditorMovingLoads",
            [Text("EditorId"), Text("EditorName"), Text("EditorCases")],
            document.MovingLoads.Count,
            Sum(document.MovingLoads, moving => TextLength(moving.Id, moving.Name) +
                JoinedLength(moving.CaseIds)),
            () => document.MovingLoads.Select(moving => Row(
                moving.Id, moving.Name, string.Join(';', moving.CaseIds)))),
    ];

    private static TableProjection DerivedProjection(
        ProjectDocument document,
        DerivedResultKind kind,
        string captionResource)
    {
        DerivedResultDefinition[] rows = document.DerivedResults.Where(result => result.Kind == kind).ToArray();
        return Projection(
            captionResource,
            [Text("EditorId"), Text("EditorName"), Text("EditorSources"), Text("EditorFactors")],
            rows.Length,
            Sum(rows, result => TextLength(result.Id, result.Name) +
                JoinedLength(result.Terms.Select(term => term.SourceId)) +
                JoinedNumericLength(result.Terms.Count)),
            () => rows.Select(result => Row(
                result.Id,
                result.Name,
                string.Join(';', result.Terms.Select(term => term.SourceId)),
                string.Join(';', result.Terms.Select(term => term.Factor.ToString("R", CultureInfo.InvariantCulture))))));
    }

    private static IEnumerable<PrintTableRow> SupportRows(ProjectDocument document)
    {
        foreach (ProjectSupport support in document.Supports)
        {
            yield return Row(
                support.Id, support.NodeId, Boolean(support.FixX), Boolean(support.FixY),
                Boolean(support.FixZ), Boolean(support.FixRx), Boolean(support.FixRy),
                Boolean(support.FixRz), "1");
        }

        foreach (SupportSetDefinition set in document.SupportSets)
        {
            foreach (SupportConditionDefinition support in set.Rows)
            {
                yield return Row(
                    support.Id, support.NodeId, Value(support.Tx), Value(support.Ty), Value(support.Tz),
                    Value(support.Rx), Value(support.Ry), Value(support.Rz), set.Id);
            }
        }
    }

    private static IEnumerable<PrintTableRow> SectionRows(ProjectDocument document)
    {
        foreach (FrameSectionDefinition section in document.Sections)
        {
            yield return SectionRow(section, "1");
        }

        foreach (ElementPropertySetDefinition set in document.ElementPropertySets)
        {
            foreach (FrameSectionDefinition section in set.Sections)
            {
                yield return SectionRow(section, set.Id);
            }
        }
    }

    private static PrintTableRow SectionRow(FrameSectionDefinition section, string setId) => Row(
        section.Id,
        section.Name,
        Value(section.YoungsModulus),
        Value(section.PoissonRatio),
        Value(section.ShearModulus),
        Value(section.Area),
        Value(section.MomentOfInertiaY),
        Value(section.MomentOfInertiaZ),
        Value(section.TorsionConstant),
        Value(section.ThermalExpansionCoefficient),
        Value(section.Density),
        Value(section.PanelThickness),
        setId);

    private static long EstimateSupports(ProjectDocument document) => checked(
        Sum(document.Supports, support => TextLength(support.Id, support.NodeId, "1") + 6) +
        Sum(document.SupportSets, set => Sum(set.Rows, row =>
            TextLength(row.Id, row.NodeId, set.Id) + (6 * NumberCharacterBudget))));

    private static long EstimateSections(ProjectDocument document) => checked(
        Sum(document.Sections, section => EstimateSection(section, "1")) +
        Sum(document.ElementPropertySets, set => Sum(set.Sections, section => EstimateSection(section, set.Id))));

    private static long EstimateSection(FrameSectionDefinition section, string setId) =>
        checked(TextLength(section.Id, section.Name, setId) + (10 * NumberCharacterBudget));

    private static void Preflight(
        IReadOnlyList<TableProjection> projections,
        LocalizationService localization,
        DesktopPrintProjectionBudget budget)
    {
        foreach (TableProjection projection in projections)
        {
            if (projection.RowCount < 0 || projection.RowCount > PrintEngineLimits.MaximumRows ||
                projection.Columns.Count == 0 ||
                projection.Columns.Count > PrintEngineLimits.MaximumColumnsPerTable)
            {
                throw new ArgumentException("A projected input table exceeds its shape limit.", nameof(projections));
            }

            long textCharacters = checked(
                projection.RowTextCharacters + TextLength(localization[projection.CaptionResource]));
            foreach (ColumnProjection column in projection.Columns)
            {
                textCharacters = checked(textCharacters + TextLength(localization[column.HeaderResource]));
            }

            budget.ChargeTable(projection.RowCount, projection.Columns.Count, textCharacters);
        }
    }

    private static TableProjection Projection(
        string captionResource,
        IReadOnlyList<ColumnProjection> columns,
        int rowCount,
        long rowTextCharacters,
        Func<IEnumerable<PrintTableRow>> createRows) =>
        new(captionResource, columns, rowCount, rowTextCharacters, createRows);

    private static ColumnProjection Text(string headerResource) => new(headerResource, Numeric: false);

    private static ColumnProjection Number(string headerResource) => new(headerResource, Numeric: true);

    private static PrintTableRow Row(params string[] values) => new(values);

    private static string Value(double value) => value.ToString("G9", CultureInfo.InvariantCulture);

    private static string Boolean(bool value) => value ? "1" : "0";

    private static long JoinedLength(IEnumerable<string> values)
    {
        string[] materialized = values as string[] ?? values.ToArray();
        long length = checked(TextLength(materialized) + Math.Max(0, materialized.Length - 1));
        if (length > PrintEngineLimits.MaximumTextLength)
        {
            throw new ArgumentException("A projected joined input cell exceeds the text limit.", nameof(values));
        }

        return length;
    }

    private static long JoinedNumericLength(int valueCount)
    {
        long length = checked(((long)valueCount * NumberCharacterBudget) + Math.Max(0, valueCount - 1));
        if (length > PrintEngineLimits.MaximumTextLength)
        {
            throw new ArgumentException("A projected joined numeric cell exceeds the text limit.", nameof(valueCount));
        }

        return length;
    }

    private static long TextLength(params string?[] values) => TextLength((IEnumerable<string?>)values);

    private static long TextLength(IEnumerable<string?> values)
    {
        long length = 0;
        foreach (string? value in values)
        {
            if (value?.Length > PrintEngineLimits.MaximumTextLength)
            {
                throw new ArgumentException("A projected input cell exceeds the text limit.", nameof(values));
            }

            length = checked(length + (value?.Length ?? 0));
        }

        return length;
    }

    private static long Sum<T>(IEnumerable<T> values, Func<T, long> selector)
    {
        long total = 0;
        foreach (T value in values)
        {
            total = checked(total + selector(value));
        }

        return total;
    }

    private sealed record TableProjection(
        string CaptionResource,
        IReadOnlyList<ColumnProjection> Columns,
        int RowCount,
        long RowTextCharacters,
        Func<IEnumerable<PrintTableRow>> CreateRows);

    private sealed record ColumnProjection(string HeaderResource, bool Numeric);
}

internal sealed class DesktopPrintProjectionBudget
{
    private int _tables;
    private long _rows;
    private long _cells;
    private long _textCharacters;

    public void ChargeTable(long rows, int columns, long textCharacters)
    {
        if (rows < 0 || rows > PrintEngineLimits.MaximumRows ||
            columns <= 0 || columns > PrintEngineLimits.MaximumColumnsPerTable ||
            textCharacters < 0)
        {
            throw new ArgumentException("A projected print table exceeds its shape or text limit.");
        }

        _tables = checked(_tables + 1);
        _rows = checked(_rows + rows);
        _cells = checked(_cells + checked(rows * columns));
        _textCharacters = checked(_textCharacters + textCharacters);
        if (_tables > PrintEngineLimits.MaximumTables ||
            _rows > PrintEngineLimits.MaximumRows ||
            _cells > PrintEngineLimits.MaximumCells ||
            _textCharacters > PrintEngineLimits.MaximumTextCharacters)
        {
            throw new ArgumentException("The projected print job exceeds the shared table work limit.");
        }
    }
}
